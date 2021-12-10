using System;
using System.Threading;

namespace CSharp.Parallelx.Channels
{
    class Chan<T>
    {
        readonly int size;

        T[] buffer;
        long head = -1;
        long tail = -1;
        long closed = 0;

        public Chan() { this.size = 0; }
        public Chan(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException();

            this.size = size;
            this.buffer = new T[this.size];
        }

        object headLock = new object();
        public bool To(T t)
        {
            lock (headLock)
            {
                long localClosed = 0L;

                if (tail - head == buffer.Length) SpinWait.SpinUntil(() => (localClosed = Interlocked.Read(ref closed)) > 0 || tail - head < buffer.Length);
                if (localClosed > 0) return false;

                var newTail = Interlocked.Increment(ref tail);
                buffer[newTail % buffer.Length] = t;

                return true;
            }
        }

        object tailLock = new object();
        public bool From(out T val)
        {
            lock (tailLock)
            {
                long localClosed = 0L;

                if (tail - head == 0) SpinWait.SpinUntil(() => (localClosed = Interlocked.Read(ref closed)) > 0 || tail - head > 0);
                if (localClosed > 0)
                {
                    val = default(T);
                    return false;
                }

                var newHead = Interlocked.Increment(ref head);
                val = buffer[newHead % buffer.Length];

                return true;
            }
        }

        public void Close()
        {
            Interlocked.Increment(ref closed);
        }
    }

}