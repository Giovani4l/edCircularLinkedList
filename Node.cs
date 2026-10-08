namespace edCircularLinkedList
{
    internal class Node
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Genero { get; set; }
        public string Plataforma { get; set; }
        public Node? Next { get; set; }

        public Node()
        {
            Id = 0;
            Nombre = "";
            Genero = "";
            Plataforma = "";
            Next = null;
        }

        public Node(int id, string nombre,
            string genero, string plataforma, Node? next)
        {
            Id = id;
            Nombre = nombre;
            Genero = genero;
            Plataforma = plataforma;
            Next = next;
        }

        public override string ToString()
        {
            return $"Id: {Id}, Videojuego: {Nombre}" +
                $", Género: {Genero}, Plataforma: {Plataforma}";
        }
    }
}
