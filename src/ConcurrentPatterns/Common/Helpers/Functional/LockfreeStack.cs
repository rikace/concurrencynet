using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Parallelism
{
    public class LockFreeStack<T>
    {
        private class Node
        {
            public T Value;
            public Node Next;
        }

        private Node head;
        private volatile int count;

        public int Count => count;
        public bool IsEmpty => count > 0;

        public void Push(T value)
        {
            var newNode = new Node() {Value = value};
            while (true)
            {
                newNode.Next = this.head;
                if (Interlocked.CompareExchange(ref this.head, newNode, newNode.Next) == newNode.Next)
                {
                    Interlocked.Increment(ref this.count);
                    return;
                }
            }
        }

        public T Pop()
        {
            while (true)
            {
                Node node = this.head;
                if (node == null)
                    return default(T);
                if (Interlocked.CompareExchange(ref this.head, node.Next, node) == node)
                {
                    Interlocked.Decrement(ref this.count);
                    return node.Value;
                }
            }
        }
    }

    //var stack = new LockFreeStack<int>();
    //Parallel.For(0, 5000, stack.Push);

}
