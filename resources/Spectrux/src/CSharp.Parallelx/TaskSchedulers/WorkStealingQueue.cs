namespace CSharp.Parallelx.TaskSchedulers
{

    using System;
    using System.Threading;

    public class WorkStealingQueue<T>
    {
        private const int INITIAL_SIZE = 32;
        private T[] m_array = new T[INITIAL_SIZE];
        private int m_mask = INITIAL_SIZE - 1;
        private volatile int m_headIndex = 0;
        private volatile int m_tailIndex = 0;
        private object m_foreignLock = new object();

        public bool IsEmpty
        {
            get { return m_headIndex >= m_tailIndex; }
        }

        public int Count
        {
            get { return m_tailIndex - m_headIndex; }
        }

        public void LocalPush(T obj)
        {
            int tail = m_tailIndex;

            // When there is space, we can take the fast path.
            if (tail < (m_headIndex + m_mask))
            {
                m_array[tail & m_mask] = obj;
                m_tailIndex = tail + 1;
            }
            else
            {
                // We need to contend with foreign pops, so we lock.
                lock (m_foreignLock)
                {
                    int head = m_headIndex;

                    // If there is still space (one left), add the element.
                    if (tail < (head + m_mask))
                    {
                        m_array[tail & m_mask] = obj;
                        m_tailIndex = tail + 1;
                    }
                    else
                    {
                        // Otherwise, we're full; expand the queue by
                        // doubling its size (ignoring overflow).
                        T[] newArray = new T[m_array.Length << 1];
                        for (int i = 0; i < m_array.Length; i++)
                            newArray[i] = m_array[(i + head) & m_mask];

                        // Reset the field values, incl. the mask.
                        m_array = newArray;
                        m_headIndex = 0;
                        m_tailIndex = tail - m_mask;
                        m_mask = (m_mask << 1) | 1;

                        // Now place the new value.
                        m_array[tail & m_mask] = obj;
                        m_tailIndex = tail + 1;
                    }
                }
            }
        }

        public bool LocalPop(out T obj)
        {
            // Decrement the tail using a fence to ensure the subsequent
            // read doesn't come before.
            int tail = m_tailIndex - 1;
            Interlocked.Exchange(ref m_tailIndex, tail);

            // If there's no interaction with a take, do the fast path.
            if (m_headIndex <= tail)
            {
                obj = m_array[tail & m_mask];
                return true;
            }
            else
            {
                // Interaction with takes: 0 or 1 elements left.
                lock (m_foreignLock)
                {
                    if (m_headIndex <= tail)
                    {
                        // Element still available. Take it.
                        obj = m_array[tail & m_mask];
                        return true;
                    }
                    else
                    {
                        // We lost the race, element was stolen, restore.
                        m_tailIndex = tail + 1;
                        obj = default(T);
                        return false;
                    }
                }
            }
        }

        public bool TrySteal(out T obj)
        {
            return TrySteal(out obj, 0); // No blocking by default.
        }

        private bool TrySteal(out T obj, int millisecondsTimeout)
        {
            if (Monitor.TryEnter(m_foreignLock, millisecondsTimeout))
            {
                try
                {
                    // Increment head, and ensure read of tail doesn't
                    // move before it (fence).
                    int head = m_headIndex;
                    Interlocked.Exchange(ref m_headIndex, head + 1);

                    if (head < m_tailIndex)
                    {
                        obj = m_array[head & m_mask];
                        return true;
                    }
                    else
                    {
                        // Failed, restore head.
                        m_headIndex = head;
                    }
                }
                finally
                {
                    Monitor.Exit(m_foreignLock);
                }
            }

            obj = default(T);
            return false;
        }
    }
}
