using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace edCircularLinkedList
{
    internal sealed class TiempoInsercion
    {
        public string Estructura { get; }
        public int Inserciones { get; private set; }
        public long UltimosTicks { get; private set; }
        public long TotalTicks { get; private set; }
        public double UltimosMicrosegundos => UltimosTicks * 1_000_000.0 / Stopwatch.Frequency;
        public double TotalMilisegundos => TotalTicks * 1_000.0 / Stopwatch.Frequency;

        public TiempoInsercion(string estructura)
        {
            Estructura = estructura;
        }

        public void Registrar(long ticks)
        {
            Inserciones++;
            UltimosTicks = ticks;
            TotalTicks += ticks;
        }
    }

    // Las tres estructuras guardan los mismos campos, ordenan por ID y rechazan
    // duplicados. Cada una tiene su propia copia de los registros en RAM.
    internal sealed class ComparadorInserciones
    {
        public CircularListLinked Circular { get; } = new CircularListLinked();
        private readonly LinkedList<Node> linked = new LinkedList<Node>();
        private readonly List<Node> arreglo = new List<Node>();
        private readonly TiempoInsercion[] tiempos =
        {
            new TiempoInsercion("Lista circular"),
            new TiempoInsercion("LinkedList<Node>"),
            new TiempoInsercion("List<Node>")
        };
        private int turno;

        public IReadOnlyList<TiempoInsercion> Tiempos => tiempos;

        public bool Agregar(Node datos)
        {
            // La validación del formulario queda fuera de la medición. Cada
            // método de inserción también comprueba duplicados al buscar su lugar.
            if (Circular.Exist(datos.Id))
                return false;

            var reloj = new Stopwatch();
            // Rotar cuál se mide primero evita favorecer siempre a la misma.
            for (int paso = 0; paso < 3; paso++)
            {
                int indice = (turno + paso) % 3;
                reloj.Restart();
                bool agregado = indice switch
                {
                    0 => InsercionOrdenada.EnCircular(Circular, datos),
                    1 => InsercionOrdenada.EnLinkedList(linked, datos),
                    _ => InsercionOrdenada.EnList(arreglo, datos)
                };
                reloj.Stop();

                if (!agregado)
                    throw new InvalidOperationException("Las estructuras no contienen los mismos IDs.");

                tiempos[indice].Registrar(reloj.ElapsedTicks);
            }

            turno = (turno + 1) % 3;
            return true;
        }

        public bool Eliminar(int id)
        {
            if (!Circular.Exist(id))
                return false;

            Circular.Remove(id);
            LinkedListNode<Node>? actual = linked.First;
            while (actual != null && actual.Value.Id != id)
                actual = actual.Next;
            if (actual != null)
                linked.Remove(actual);
            arreglo.RemoveAll(n => n.Id == id);
            return true;
        }

        public IReadOnlyList<Node> ObtenerElementos(int indice)
        {
            return indice switch
            {
                0 => InsercionOrdenada.RecorrerCircular(Circular).ToArray(),
                1 => linked.ToArray(),
                2 => arreglo.ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(indice))
            };
        }
    }

    internal static class InsercionOrdenada
    {
        // Copiar dentro de cada inserción incluye crear el registro/nodo en RAM.
        // Los textos ya están preparados: no se generan cadenas dentro del reloj.
        private static Node Copiar(Node datos) =>
            new Node(datos.Id, datos.Nombre, datos.Genero, datos.Plataforma, null);

        public static bool EnCircular(CircularListLinked lista, Node datos) =>
            lista.Add(Copiar(datos));

        public static bool EnLinkedList(LinkedList<Node> lista, Node datos)
        {
            Node nuevo = Copiar(datos);
            LinkedListNode<Node>? actual = lista.First;
            while (actual != null && actual.Value.Id < nuevo.Id)
                actual = actual.Next;

            if (actual != null && actual.Value.Id == nuevo.Id)
                return false;

            if (actual == null)
                lista.AddLast(nuevo);
            else
                lista.AddBefore(actual, nuevo);
            return true;
        }

        public static bool EnList(List<Node> lista, Node datos)
        {
            Node nuevo = Copiar(datos);
            int posicion = 0;
            while (posicion < lista.Count && lista[posicion].Id < nuevo.Id)
                posicion++;

            if (posicion < lista.Count && lista[posicion].Id == nuevo.Id)
                return false;

            // La capacidad crece normalmente: no se reserva espacio por adelantado.
            lista.Insert(posicion, nuevo);
            return true;
        }

        public static IEnumerable<Node> RecorrerCircular(CircularListLinked lista)
        {
            if (lista.Head == null)
                yield break;

            Node actual = lista.Head;
            do
            {
                yield return actual;
                actual = actual.Next!;
            } while (actual != lista.Head);
        }
    }
}
