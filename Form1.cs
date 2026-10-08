using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace edCircularLinkedList
{
    public partial class Form1 : Form
    {
        private readonly ComparadorInserciones comparador = new ComparadorInserciones();
        private CircularListLinked lista => comparador.Circular;
        private CancellationTokenSource? cancelacionLote;
        private CancellationTokenSource? cancelacionCsv;

        // Datos del último CSV cargado. Se usan con VirtualMode para
        // poder mostrar archivos grandes sin crear 100,000 filas de golpe.
        private IReadOnlyList<Node> datosCsv = Array.Empty<Node>();

        public Form1()
        {
            InitializeComponent();
            ConfigurarTabla();
            ConfigurarComparacion();
            ConfigurarTablaCsv();
            MedidorLotes.Calentar();
            MostrarLista();
            MostrarTiempos();
            MostrarMensaje("Listo. Captura los datos de un videojuego para comenzar.");
        }

        private void ConfigurarComparacion()
        {
            dgvTiempos.Columns.Add("Estructura", "Estructura");
            dgvTiempos.Columns.Add("Inserciones", "Altas");
            dgvTiempos.Columns.Add("Ultima", "Última\n(µs)");
            dgvTiempos.Columns.Add("Total", "Acumulado\n(ms)");
            dgvTiempos.Columns.Add("Diferencia", "Diferencia\n(ms)");
            dgvTiempos.Columns[0].FillWeight = 145;
            dgvTiempos.Columns[1].FillWeight = 55;

            dgvLote.Columns.Add("Estructura", "Estructura");
            dgvLote.Columns.Add("Total", "Mediana\n(ms)");
            dgvLote.Columns.Add("Promedio", "Por dato\n(µs)");
            dgvLote.Columns.Add("Diferencia", "Diferencia\n(ms)");
            dgvLote.Columns[0].FillWeight = 145;

            foreach (DataGridView tabla in new[] { dgvTiempos, dgvLote })
            {
                tabla.ColumnHeadersHeight = 42;
                tabla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
                foreach (DataGridViewColumn columna in tabla.Columns)
                {
                    if (columna.Index > 0)
                        columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    columna.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
            }

            foreach (TiempoInsercion tiempo in comparador.Tiempos)
                dgvLote.Rows.Add(tiempo.Estructura, "—", "—", "—");

            cboOrden.SelectedIndex = 0;
            lblReloj.Text = $"Stopwatch: {Stopwatch.Frequency:N0} ticks/s | " +
                $"{1_000_000_000.0 / Stopwatch.Frequency:N2} ns por tick";
        }

        private void MostrarTiempos()
        {
            dgvTiempos.Rows.Clear();
            long menor = comparador.Tiempos.Min(t => t.TotalTicks);
            foreach (TiempoInsercion tiempo in comparador.Tiempos)
            {
                bool medido = tiempo.Inserciones > 0;
                double diferencia = (tiempo.TotalTicks - menor) * 1_000.0 / Stopwatch.Frequency;
                dgvTiempos.Rows.Add(tiempo.Estructura, tiempo.Inserciones,
                    medido ? tiempo.UltimosMicrosegundos.ToString("F3") : "—",
                    medido ? tiempo.TotalMilisegundos.ToString("F6") : "—",
                    medido ? diferencia.ToString("F6") : "—");
            }
            dgvTiempos.ClearSelection();
        }

        private async void btnComparar_Click(object? sender, EventArgs e)
        {
            if (cancelacionLote != null)
                return;

            int cantidad = (int)nudCantidad.Value;
            OrdenDatos orden = (OrdenDatos)cboOrden.SelectedIndex;
            string nombreOrden = cboOrden.Text;
            using var cancelacion = new CancellationTokenSource();
            cancelacionLote = cancelacion;
            btnComparar.Enabled = false;
            btnCancelar.Enabled = true;
            nudCantidad.Enabled = false;
            cboOrden.Enabled = false;
            dgvLote.Rows.Clear();
            foreach (TiempoInsercion tiempo in comparador.Tiempos)
                dgvLote.Rows.Add(tiempo.Estructura, "—", "—", "—");
            lblResumenLote.Text = $"Preparando {cantidad:N0} registros…";

            var progreso = new Progress<string>(mensaje =>
            {
                if (!IsDisposed && !Disposing && cancelacionLote == cancelacion &&
                    !cancelacion.IsCancellationRequested)
                    lblResumenLote.Text = mensaje;
            });

            try
            {
                IReadOnlyList<ResultadoLote> resultados = await Task.Run(
                    () => MedidorLotes.Comparar(cantidad, orden, cancelacion.Token, progreso),
                    cancelacion.Token);
                if (IsDisposed || Disposing)
                    return;

                dgvLote.Rows.Clear();
                long menor = resultados.Min(r => r.MedianaTicks);
                foreach (ResultadoLote resultado in resultados)
                {
                    double diferencia = (resultado.MedianaTicks - menor) * 1_000.0 / Stopwatch.Frequency;
                    dgvLote.Rows.Add(resultado.Estructura,
                        resultado.MedianaMilisegundos.ToString("F6"),
                        resultado.MicrosegundosPorDato.ToString("F3"),
                        diferencia.ToString("F6"));
                }
                dgvLote.ClearSelection();
                string mejores = string.Join(" y ", resultados
                    .Where(r => r.MedianaTicks == menor).Select(r => r.Estructura));
                lblResumenLote.Text = $"{cantidad:N0} datos · {nombreOrden} · 3 repeticiones. " +
                    $"Menor mediana: {mejores}.";
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing)
                    lblResumenLote.Text = "Comparación cancelada. Puedes iniciar otro lote.";
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing)
                    lblResumenLote.Text = $"No se pudo completar la comparación: {error.Message}";
            }
            finally
            {
                cancelacionLote = null;
                if (!IsDisposed && !Disposing)
                {
                    btnComparar.Enabled = true;
                    btnCancelar.Enabled = false;
                    nudCantidad.Enabled = true;
                    cboOrden.Enabled = true;
                }
            }
        }

        private void btnCancelar_Click(object? sender, EventArgs e)
        {
            cancelacionLote?.Cancel();
            lblResumenLote.Text = "Cancelando comparación…";
        }

        private void ConfigurarTablaCsv()
        {
            dgvCsvDatos.AutoGenerateColumns = false;
            dgvCsvDatos.Columns.Clear();

            dgvCsvDatos.Columns.Add("Id", "ID");
            dgvCsvDatos.Columns.Add("Nombre", "Videojuego");
            dgvCsvDatos.Columns.Add("Genero", "Género");
            dgvCsvDatos.Columns.Add("Plataforma", "Plataforma");

            dgvCsvDatos.Columns[0].FillWeight = 12;
            dgvCsvDatos.Columns[1].FillWeight = 38;
            dgvCsvDatos.Columns[2].FillWeight = 25;
            dgvCsvDatos.Columns[3].FillWeight = 25;

            dgvCsvDatos.VirtualMode = true;
            dgvCsvDatos.CellValueNeeded += dgvCsvDatos_CellValueNeeded;

            dgvCsvTiempos.AutoGenerateColumns = false;
            dgvCsvTiempos.Columns.Clear();
            dgvCsvTiempos.Columns.Add("Estructura", "Estructura");
            dgvCsvTiempos.Columns.Add("Tiempo", "Tiempo de inserción (ms)");
            dgvCsvTiempos.Columns.Add("Diferencia", "Diferencia (ms)");

            dgvCsvTiempos.Columns[0].FillWeight = 40;
            dgvCsvTiempos.Columns[1].FillWeight = 30;
            dgvCsvTiempos.Columns[2].FillWeight = 30;

            foreach (DataGridView tabla in new[] { dgvCsvDatos, dgvCsvTiempos })
            {
                tabla.ColumnHeadersHeight = 32;
                tabla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
                foreach (DataGridViewColumn columna in tabla.Columns)
                    columna.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }

        private void dgvCsvDatos_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= datosCsv.Count)
                return;

            Node dato = datosCsv[e.RowIndex];

            e.Value = e.ColumnIndex switch
            {
                0 => dato.Id,
                1 => dato.Nombre,
                2 => dato.Genero,
                3 => dato.Plataforma,
                _ => null
            };
        }

        private async void btnSeleccionarCsv_Click(object? sender, EventArgs e)
        {
            if (cancelacionCsv != null)
                return;

            using var dialogo = new OpenFileDialog
            {
                Title = "Seleccionar archivo CSV",
                Filter = "Archivos CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

            cancelacionCsv = new CancellationTokenSource();
            btnSeleccionarCsv.Enabled = false;
            txtResultadoCsv.Text = "Leyendo CSV y preparando la prueba...";
            lblResumenCsv.Text = "Procesando...";
            datosCsv = Array.Empty<Node>();
            dgvCsvDatos.RowCount = 0;
            dgvCsvDatos.Invalidate();
            dgvCsvTiempos.Rows.Clear();

            try
            {
                string ruta = dialogo.FileName;
                CancellationToken token = cancelacionCsv.Token;

                ResultadoComparacionCsv resultado = await Task.Run(
                    () => ComparadorCsv.Ejecutar(ruta, token),
                    token);

                if (IsDisposed || Disposing)
                    return;

                // Guardamos los datos para mostrarlos en el grid.
                datosCsv = resultado.Datos;
                dgvCsvDatos.RowCount = datosCsv.Count;
                dgvCsvDatos.ClearSelection();
                dgvCsvDatos.Invalidate();

                MostrarTiemposCsv(resultado);

                txtResultadoCsv.Text =
                    $"Archivo: {Path.GetFileName(ruta)} | " +
                    $"Registros leídos: {resultado.Cantidad:N0}";

            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing)
                    txtResultadoCsv.Text = "Prueba cancelada.";
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing)
                    txtResultadoCsv.Text = "No se pudo procesar el CSV:" + Environment.NewLine + error.Message;
            }
            finally
            {
                cancelacionCsv?.Dispose();
                cancelacionCsv = null;
                if (!IsDisposed && !Disposing)
                    btnSeleccionarCsv.Enabled = true;
            }
        }

        private void MostrarTiemposCsv(ResultadoComparacionCsv resultado)
        {
            dgvCsvTiempos.Rows.Clear();

            double menor = Math.Min(
                resultado.TiempoCircularMs,
                Math.Min(resultado.TiempoLinkedListMs, resultado.TiempoListMs));

            dgvCsvTiempos.Rows.Add(
                "Lista circular",
                resultado.TiempoCircularMs.ToString("F6"),
                (resultado.TiempoCircularMs - menor).ToString("F6"));

            dgvCsvTiempos.Rows.Add(
                "LinkedList<Node>",
                resultado.TiempoLinkedListMs.ToString("F6"),
                (resultado.TiempoLinkedListMs - menor).ToString("F6"));

            dgvCsvTiempos.Rows.Add(
                "List<Node>",
                resultado.TiempoListMs.ToString("F6"),
                (resultado.TiempoListMs - menor).ToString("F6"));

            dgvCsvTiempos.ClearSelection();

            string mejor = resultado.TiempoCircularMs == menor
                ? "Lista circular"
                : resultado.TiempoLinkedListMs == menor
                    ? "LinkedList<Node>"
                    : "List<Node>";

            lblResumenCsv.Text =
                $"{resultado.Cantidad:N0} registros insertados en RAM · " +
                $"Mejor tiempo: {mejor} ({menor:F6} ms).";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cancelacionLote?.Cancel();
            cancelacionCsv?.Cancel();
            base.OnFormClosing(e);
        }

        private void ConfigurarTabla()
        {
            dgvVideojuegos.AutoGenerateColumns = false;
            dgvVideojuegos.Columns.Clear();
            dgvVideojuegos.Columns.Add("Id", "ID");
            dgvVideojuegos.Columns.Add("Nombre", "Videojuego");
            dgvVideojuegos.Columns.Add("Genero", "Género");
            dgvVideojuegos.Columns.Add("Plataforma", "Plataforma");
        }

        private void MostrarLista()
        {
            dgvVideojuegos.Rows.Clear();

            if (lista.Head != null)
            {
                Node actual = lista.Head;
                do
                {
                    dgvVideojuegos.Rows.Add(
                        actual.Id,
                        actual.Nombre,
                        actual.Genero,
                        actual.Plataforma);

                    actual = actual.Next!;
                } while (actual != lista.Head);
            }

            lblTotal.Text = $"Total de videojuegos: {lista.Count()}";
        }

        private bool ObtenerDatos(out int id, out string nombre, out string genero, out string plataforma)
        {
            id = 0;
            nombre = txtNombre.Text.Trim();
            genero = txtGenero.Text.Trim();
            plataforma = txtPlataforma.Text.Trim();

            if (!int.TryParse(txtId.Text.Trim(), out id))
            {
                MostrarMensaje("El ID debe ser un número entero.");
                txtId.Focus();
                return false;
            }

            if (nombre == "")
            {
                MostrarMensaje("Escribe el nombre del videojuego.");
                txtNombre.Focus();
                return false;
            }

            if (genero == "")
            {
                MostrarMensaje("Escribe el género del videojuego.");
                txtGenero.Focus();
                return false;
            }

            if (plataforma == "")
            {
                MostrarMensaje("Escribe la plataforma del videojuego.");
                txtPlataforma.Focus();
                return false;
            }

            return true;
        }

        private void btnAgregar_Click(object? sender, EventArgs e)
        {
            if (!ObtenerDatos(out int id, out string nombre, out string genero, out string plataforma))
                return;

            Node nuevo = new Node(id, nombre, genero, plataforma, null);
            if (!comparador.Agregar(nuevo))
            {
                MostrarMensaje("Ya existe un videojuego con ese ID.");
                txtId.Focus();
                return;
            }

            MostrarLista();
            MostrarTiempos();
            LimpiarCampos();
            MostrarMensaje("Videojuego agregado a las tres estructuras. Tiempos actualizados.");
        }

        private void btnBuscar_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtId.Text.Trim(), out int id))
            {
                MostrarMensaje("Escribe un ID válido para buscar.");
                txtId.Focus();
                return;
            }

            Node? encontrado = lista.Search(id);

            if (encontrado == null)
            {
                MostrarMensaje("Videojuego no encontrado.");
                return;
            }

            txtNombre.Text = encontrado.Nombre;
            txtGenero.Text = encontrado.Genero;
            txtPlataforma.Text = encontrado.Plataforma;

            dgvVideojuegos.ClearSelection();
            foreach (DataGridViewRow fila in dgvVideojuegos.Rows)
            {
                if (fila.Cells[0].Value != null && Convert.ToInt32(fila.Cells[0].Value) == id)
                {
                    fila.Selected = true;
                    dgvVideojuegos.CurrentCell = fila.Cells[0];
                    break;
                }
            }

            MostrarMensaje("Videojuego encontrado.");
        }

        private void btnEliminar_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtId.Text.Trim(), out int id))
            {
                MostrarMensaje("Escribe un ID válido para eliminar.");
                txtId.Focus();
                return;
            }

            if (!lista.Exist(id))
            {
                MostrarMensaje("No existe un videojuego con ese ID.");
                return;
            }

            comparador.Eliminar(id);
            MostrarLista();
            LimpiarCampos();
            MostrarMensaje("Videojuego eliminado correctamente.");
        }

        private void btnContar_Click(object? sender, EventArgs e)
        {
            int total = lista.Count();
            lblTotal.Text = $"Total de videojuegos: {total}";

            if (total == 1)
                MostrarMensaje("Hay 1 videojuego en la lista circular.");
            else
                MostrarMensaje($"Hay {total} videojuegos en la lista circular.");
        }

        private void btnLimpiar_Click(object? sender, EventArgs e)
        {
            LimpiarCampos();
            dgvVideojuegos.ClearSelection();
            MostrarMensaje("Campos limpiados.");
        }

        private void dgvVideojuegos_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            DataGridViewRow fila = dgvVideojuegos.Rows[e.RowIndex];

            txtId.Text = fila.Cells[0].Value?.ToString() ?? "";
            txtNombre.Text = fila.Cells[1].Value?.ToString() ?? "";
            txtGenero.Text = fila.Cells[2].Value?.ToString() ?? "";
            txtPlataforma.Text = fila.Cells[3].Value?.ToString() ?? "";

            MostrarMensaje("Videojuego seleccionado.");
        }

        private void LimpiarCampos()
        {
            txtId.Clear();
            txtNombre.Clear();
            txtGenero.Clear();
            txtPlataforma.Clear();
            txtId.Focus();
        }

        private void MostrarMensaje(string mensaje)
        {
            lblMensaje.Text = mensaje;
        }
    }
}
