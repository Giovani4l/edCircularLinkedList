using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace edCircularLinkedList
{
    internal enum OrdenDatos
    {
        Aleatorio,
        Ascendente,
        Descendente
    }

    internal sealed class ResultadoLote
    {
        public string Estructura { get; }
        public int Cantidad { get; }
        public IReadOnlyList<long> TicksPorRepeticion { get; }
        public long MedianaTicks { get; }
        public double MedianaMilisegundos => MedianaTicks * 1_000.0 / Stopwatch.Frequency;
        public double MicrosegundosPorDato => MedianaTicks * 1_000_000.0 / Stopwatch.Frequency / Cantidad;

        public ResultadoLote(string estructura, int cantidad, long[] ticks)
        {
            Estructura = estructura;
            Cantidad = cantidad;
            TicksPorRepeticion = Array.AsReadOnly((long[])ticks.Clone());
            long[] ordenados = (long[])ticks.Clone();
            Array.Sort(ordenados);
            MedianaTicks = ordenados[ordenados.Length / 2];
        }
    }

    internal static class MedidorLotes
    {
        public const int Repeticiones = 3;
        public const int MaximoDatos = 50_000;
        private static readonly string[] nombres = { "Lista circular", "LinkedList<Node>", "List<Node>" };
        private static readonly Lazy<bool> calentamiento = new Lazy<bool>(() =>
        {
            Node[] datos = PrepararDatos(512, OrdenDatos.Aleatorio);
            for (int indice = 0; indice < 3; indice++)
                MedirUnaEstructura(indice, datos, CancellationToken.None);
            return true;
        });

        public static void Calentar() => _ = calentamiento.Value;

        public static IReadOnlyList<ResultadoLote> Comparar(
            int cantidad, OrdenDatos orden, CancellationToken cancelacion,
            IProgress<string>? progreso = null)
        {
            if (cantidad < 1 || cantidad > MaximoDatos)
                throw new ArgumentOutOfRangeException(nameof(cantidad));
            if (!Enum.IsDefined(typeof(OrdenDatos), orden))
                throw new ArgumentOutOfRangeException(nameof(orden));

            Calentar();
            cancelacion.ThrowIfCancellationRequested();
            // Generación y mezcla antes del Stopwatch. Se reutiliza exactamente
            // la misma entrada en las tres estructuras y en las tres repeticiones.
            Node[] datos = PrepararDatos(cantidad, orden);
            long[][] mediciones = { new long[Repeticiones], new long[Repeticiones], new long[Repeticiones] };

            for (int repeticion = 0; repeticion < Repeticiones; repeticion++)
            {
                for (int paso = 0; paso < 3; paso++)
                {
                    cancelacion.ThrowIfCancellationRequested();
                    int indice = (repeticion + paso) % 3;
                    progreso?.Report($"Repetición {repeticion + 1}/{Repeticiones}: {nombres[indice]}…");
                    mediciones[indice][repeticion] = MedirUnaEstructura(indice, datos, cancelacion);
                }
            }

            cancelacion.ThrowIfCancellationRequested();
            return Enumerable.Range(0, 3)
                .Select(i => new ResultadoLote(nombres[i], cantidad, mediciones[i]))
                .ToArray();
        }

        private static Node[] PrepararDatos(int cantidad, OrdenDatos orden)
        {
            var datos = new Node[cantidad];
            for (int i = 0; i < cantidad; i++)
                datos[i] = new Node(i + 1, $"Videojuego {i + 1}", "Prueba", "PC", null);

            if (orden == OrdenDatos.Descendente)
                Array.Reverse(datos);
            else if (orden == OrdenDatos.Aleatorio)
            {
                var aleatorio = new Random(12345);
                for (int i = datos.Length - 1; i > 0; i--)
                {
                    int j = aleatorio.Next(i + 1);
                    (datos[i], datos[j]) = (datos[j], datos[i]);
                }
            }
            return datos;
        }

        private static long MedirUnaEstructura(int indice, Node[] datos, CancellationToken cancelacion)
        {
            // Estructuras vacías, reloj y delegados preparados antes del tiempo.
            Func<Node, bool> insertar;
            Func<IEnumerable<Node>> recorrer;
            switch (indice)
            {
                case 0:
                    var circular = new CircularListLinked();
                    insertar = dato => InsercionOrdenada.EnCircular(circular, dato);
                    recorrer = () => InsercionOrdenada.RecorrerCircular(circular);
                    break;
                case 1:
                    var linked = new LinkedList<Node>();
                    insertar = dato => InsercionOrdenada.EnLinkedList(linked, dato);
                    recorrer = () => linked;
                    break;
                default:
                    var lista = new List<Node>();
                    insertar = dato => InsercionOrdenada.EnList(lista, dato);
                    recorrer = () => lista;
                    break;
            }

            var reloj = new Stopwatch();
            bool todosAgregados = true;
            reloj.Start();
            for (int i = 0; i < datos.Length; i++)
            {
                // La misma comprobación de cancelación en las tres estructuras.
                if ((i & 255) == 0)
                    cancelacion.ThrowIfCancellationRequested();
                todosAgregados &= insertar(datos[i]);
            }
            reloj.Stop();

            // Comprobar que realmente se insertaron todos, fuera del cronómetro.
            if (!todosAgregados)
                throw new InvalidOperationException("Una estructura rechazó datos del lote.");
            int esperados = 1;
            foreach (Node nodo in recorrer())
            {
                if (esperados > datos.Length || nodo.Id != esperados)
                    throw new InvalidOperationException("Una estructura perdió el orden o la circularidad.");
                esperados++;
            }
            if (esperados != datos.Length + 1)
                throw new InvalidOperationException("Una estructura no insertó todos los datos.");

            return reloj.ElapsedTicks;
        }
    }
}
