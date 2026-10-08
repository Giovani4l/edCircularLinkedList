using System.Diagnostics;
using System.Globalization;

namespace edCircularLinkedList
{
    internal sealed class ResultadoComparacionCsv
    {
        public int Cantidad { get; }
        public double TiempoCircularMs { get; }
        public double TiempoLinkedListMs { get; }
        public double TiempoListMs { get; }

        // Registros que se leyeron del CSV para mostrarlos en el DataGridView.
        public IReadOnlyList<Node> Datos { get; }

        public ResultadoComparacionCsv(
            int cantidad,
            double tiempoCircularMs,
            double tiempoLinkedListMs,
            double tiempoListMs,
            IReadOnlyList<Node> datos)
        {
            Cantidad = cantidad;
            TiempoCircularMs = tiempoCircularMs;
            TiempoLinkedListMs = tiempoLinkedListMs;
            TiempoListMs = tiempoListMs;
            Datos = datos;
        }
    }

    internal static class ComparadorCsv
    {
        public static ResultadoComparacionCsv Ejecutar(string ruta, CancellationToken cancelacion)
        {
            // La lectura y toda la prueba se ejecutan fuera del hilo de la interfaz.
            string[] lineas = File.ReadAllLines(ruta);
            List<Node> datos = LeerDatos(lineas, cancelacion);

            if (datos.Count == 0)
                throw new InvalidOperationException("El archivo CSV no contiene registros válidos.");

            double circularMs = MedirCircular(datos, cancelacion);
            double linkedMs = MedirLinkedList(datos, cancelacion);
            double listMs = MedirList(datos, cancelacion);

            return new ResultadoComparacionCsv(
                datos.Count,
                circularMs,
                linkedMs,
                listMs,
                datos);
        }

        private static List<Node> LeerDatos(string[] lineas, CancellationToken cancelacion)
        {
            var datos = new List<Node>();
            var ids = new HashSet<int>();
            if (lineas.Length == 0)
                return datos;

            char separador = DetectarSeparador(lineas[0]);
            bool primeraLineaEsEncabezado = !int.TryParse(
                ObtenerCampo(ParsearLinea(lineas[0], separador), 0),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _);

            int inicio = primeraLineaEsEncabezado ? 1 : 0;

            for (int i = inicio; i < lineas.Length; i++)
            {
                cancelacion.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(lineas[i]))
                    continue;

                List<string> campos = ParsearLinea(lineas[i], separador);
                if (campos.Count < 4)
                    continue;

                if (!int.TryParse(campos[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                    continue;

                if (!ids.Add(id))
                    throw new InvalidOperationException($"El CSV contiene el ID duplicado {id}.");

                datos.Add(new Node(
                    id,
                    campos[1].Trim(),
                    campos[2].Trim(),
                    campos[3].Trim(),
                    null));
            }

            return datos;
        }

        private static double MedirCircular(IReadOnlyList<Node> datos, CancellationToken cancelacion)
        {
            PrepararGarbageCollection();
            var estructura = new CircularListLinked();
            var reloj = Stopwatch.StartNew();

            for (int i = 0; i < datos.Count; i++)
            {
                if ((i & 255) == 0)
                    cancelacion.ThrowIfCancellationRequested();

                Node dato = datos[i];
                estructura.Add(new Node(dato.Id, dato.Nombre, dato.Genero, dato.Plataforma, null));
            }

            reloj.Stop();
            return reloj.Elapsed.TotalMilliseconds;
        }

        private static double MedirLinkedList(IReadOnlyList<Node> datos, CancellationToken cancelacion)
        {
            PrepararGarbageCollection();
            var estructura = new LinkedList<Node>();
            var reloj = Stopwatch.StartNew();

            for (int i = 0; i < datos.Count; i++)
            {
                if ((i & 255) == 0)
                    cancelacion.ThrowIfCancellationRequested();

                Node dato = datos[i];
                Node nuevo = new Node(dato.Id, dato.Nombre, dato.Genero, dato.Plataforma, null);

                LinkedListNode<Node>? actual = estructura.First;
                while (actual != null && actual.Value.Id < nuevo.Id)
                    actual = actual.Next;

                if (actual == null)
                    estructura.AddLast(nuevo);
                else if (actual.Value.Id != nuevo.Id)
                    estructura.AddBefore(actual, nuevo);
            }

            reloj.Stop();
            return reloj.Elapsed.TotalMilliseconds;
        }

        private static double MedirList(IReadOnlyList<Node> datos, CancellationToken cancelacion)
        {
            PrepararGarbageCollection();
            var estructura = new List<Node>();
            var reloj = Stopwatch.StartNew();

            for (int i = 0; i < datos.Count; i++)
            {
                if ((i & 255) == 0)
                    cancelacion.ThrowIfCancellationRequested();

                Node dato = datos[i];
                Node nuevo = new Node(dato.Id, dato.Nombre, dato.Genero, dato.Plataforma, null);

                int posicion = 0;
                while (posicion < estructura.Count && estructura[posicion].Id < nuevo.Id)
                    posicion++;

                if (posicion >= estructura.Count || estructura[posicion].Id != nuevo.Id)
                    estructura.Insert(posicion, nuevo);
            }

            reloj.Stop();
            return reloj.Elapsed.TotalMilliseconds;
        }

        private static void PrepararGarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static char DetectarSeparador(string linea)
        {
            int comas = ContarCaracter(linea, ',');
            int puntosComa = ContarCaracter(linea, ';');
            int tabulaciones = ContarCaracter(linea, '\t');

            if (puntosComa >= comas && puntosComa >= tabulaciones)
                return ';';
            if (tabulaciones > comas)
                return '\t';
            return ',';
        }

        private static int ContarCaracter(string texto, char caracter)
        {
            int contador = 0;
            bool entreComillas = false;

            foreach (char actual in texto)
            {
                if (actual == '\"')
                    entreComillas = !entreComillas;
                else if (actual == caracter && !entreComillas)
                    contador++;
            }

            return contador;
        }

        private static string ObtenerCampo(IReadOnlyList<string> campos, int indice) =>
            indice < campos.Count ? campos[indice].Trim() : string.Empty;

        private static List<string> ParsearLinea(string linea, char separador)
        {
            var campos = new List<string>();
            var actual = new System.Text.StringBuilder();
            bool entreComillas = false;

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];

                if (caracter == '\"')
                {
                    if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '\"')
                    {
                        actual.Append('\"');
                        i++;
                    }
                    else
                    {
                        entreComillas = !entreComillas;
                    }
                }
                else if (caracter == separador && !entreComillas)
                {
                    campos.Add(actual.ToString());
                    actual.Clear();
                }
                else
                {
                    actual.Append(caracter);
                }
            }

            campos.Add(actual.ToString());
            return campos;
        }
    }
}
