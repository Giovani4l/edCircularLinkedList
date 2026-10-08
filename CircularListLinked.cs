namespace edCircularLinkedList
{
    internal class CircularListLinked
    {
        public Node? Head { get; set; }

        public CircularListLinked()
        {
            Head = null;
        }

        // Agrega el nodo manteniendo la lista ordenada por ID.
        // La lista es circular: el último nodo siempre apunta al primero.
        public bool Add(Node newNode)
        {
            if (Head == null)
            {
                Head = newNode;
                newNode.Next = Head;
                return true;
            }

            // Comprobar el ID durante el mismo recorrido que busca la posición.
            // Así no se recorre toda la lista dos veces para cada inserción.
            if (newNode.Id == Head.Id)
                return false;

            if (newNode.Id < Head.Id)
            {
                Node Current = Head;
                while (Current.Next != Head)
                    Current = Current.Next!;

                newNode.Next = Head;
                Head = newNode;
                Current.Next = Head;
                return true;
            }

            Node current = Head;
            while (current.Next != Head && current.Next!.Id < newNode.Id)
                current = current.Next!;

            if (current.Next != Head && current.Next!.Id == newNode.Id)
                return false;

            newNode.Next = current.Next;
            current.Next = newNode;
            return true;
        }

        public void Remove(int id)
        {
            if (Head == null || !Exist(id))
                return;

            // Si solo existe un nodo.
            if (Head.Next == Head && Head.Id == id)
            {
                Head = null;
                return;
            }

            // Eliminar la cabeza.
            if (Head.Id == id)
            {
                Node current = Head;
                while (current.Next != Head)
                    current = current.Next!;

                Head = Head.Next;
                current.Next = Head;
                return;
            }

            Node Current = Head;
            while (Current.Next != Head && Current.Next!.Id != id)
                Current = Current.Next!;

            if (Current.Next != Head && Current.Next!.Id == id)
                Current.Next = Current.Next.Next;
        }

        public bool Exist(int id)
        {
            if (Head == null)
                return false;

            Node current = Head;
            do
            {
                if (current.Id == id)
                    return true;

                current = current.Next!;
            } while (current != Head);

            return false;
        }

        public Node? Search(int id)
        {
            if (Head == null)
                return null;

            Node current = Head;
            do
            {
                if (current.Id == id)
                    return current;

                current = current.Next!;
            } while (current != Head);

            return null;
        }

        public int Count()
        {
            if (Head == null)
                return 0;

            int count = 0;
            Node current = Head;

            do
            {
                count++;
                current = current.Next!;
            } while (current != Head);

            return count;
        }

        public override string ToString()
        {
            if (Head == null)
                return "";

            string result = "";
            Node current     = Head;

            do
            {
                result += current.ToString() + Environment.NewLine;
                current = current.Next!;
            } while (current != Head);

            return result;
        }
    }
}
