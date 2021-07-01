namespace CSharp.Parallelx.TaskSchedulers.WorkQueue
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Security;
    using System.Threading;
    
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Runtime.ConstrainedExecution;
    using System.Security;
    using System.Threading;
    
    /// <summary>
    /// The type of threads to use - either foreground or background threads.
    /// </summary>
    public enum ThreadType
    {
        Foreground,
        Background
    }

    /// <summary>
    /// Provides settings for a dedicated thread pool
    /// </summary>
    internal class DedicatedThreadPoolSettings
    {
        /// <summary>
        /// Background threads are the default thread type
        /// </summary>
        public const ThreadType DefaultThreadType = ThreadType.Background;

        public DedicatedThreadPoolSettings(int numThreads) : this(numThreads, DefaultThreadType) { }

        public DedicatedThreadPoolSettings(int numThreads, ThreadType threadType)
        {
            ThreadType = threadType;
            NumThreads = numThreads;
            if(numThreads <= 0) 
                throw new ArgumentOutOfRangeException("numThreads", string.Format("numThreads must be at least 1. Was {0}", numThreads));
        }

        /// <summary>
        /// The total number of threads to run in this thread pool.
        /// </summary>
        public int NumThreads { get; private set; }

        /// <summary>
        /// The type of threads to run in this thread pool.
        /// </summary>
        public ThreadType ThreadType { get; private set; }
    }
    
        internal class DedicatedThreadPool : IDisposable
    {
        public DedicatedThreadPool(DedicatedThreadPoolSettings settings)
        {
            Settings = settings;
            WorkQueue = new ThreadPoolWorkQueue();
        }

        public DedicatedThreadPoolSettings Settings { get; private set; }

        public int ThreadCount { get { return Settings.NumThreads; } }

        public ThreadType ThreadType { get { return Settings.ThreadType; } }

        /// <summary>
        /// The global work queue, shared by all threads.
        /// 
        /// Each local thread has its own work-stealing local queue.
        /// </summary>
        public ThreadPoolWorkQueue WorkQueue { get; private set; }

        public bool WasDisposed { get; private set; }

        private volatile bool _shutdownRequested;

        private void Shutdown()
        {
            _shutdownRequested = true;
        }

        private volatile int numOutstandingThreadRequests = 0;

        public bool EnqueueWorkItem(Action callback)
        {
            bool success = true;
            if (callback != null)
            {
                //
                // If we are able to create the workitem, we need to get it in the queue without being interrupted
                // by a ThreadAbortException.
                //
                try
                {
                }
                finally
                {
                    var heliosActionCallback = new ActionWorkItem(callback);
                    WorkQueue.Enqueue(heliosActionCallback, false);
                    EnsureThreadRequested();
                    success = true;
                }
            }
            else
            {
                throw new ArgumentNullException("callback");
            }
            return success;
        }

        /// <summary>
        /// Method run internally by each worker thread
        /// </summary>
        private bool Dispatch()
        {
            var workQueue = WorkQueue;

            //
            // Update our records to indicate that an outstanding request for a thread has now been fulfilled.
            // From this point on, we are responsible for requesting another thread if we stop working for any
            // reason, and we believe there might still be work in the queue.
            MarkThreadRequestSatisfied();

            bool needAnotherThread = true;
            IHeliosWorkItem workItem = null;
            try
            {
                //Set up thread-local data
                ThreadPoolWorkQueueThreadLocals tl = workQueue.EnsureCurrentThreadHasQueue();
                while (!_shutdownRequested && tl.ConsecutiveQueueMissCount < workQueue.QueueMissUpperLimit) //look for work until explicitly shut down or too many queue misses
                {
                    bool missedSteal = false;
                    workQueue.Dequeue(tl, out workItem, out missedSteal);

                    try
                    {
                    }
                    finally
                    {
                        if (workItem == null)
                        {
                            //
                            // No work.  We're going to return to the VM once we leave this protected region.
                            // If we missed a steal, though, there may be more work in the queue.
                            // Instead of looping around and trying again, we'll just request another thread.  This way
                            // we won't starve other AppDomains while we spin trying to get locks, and hopefully the thread
                            // that owns the contended work-stealing queue will pick up its own workitems in the meantime, 
                            // which will be more efficient than this thread doing it anyway.
                            //
                            needAnotherThread = missedSteal;
                        }
                        else
                        {
                            //
                            // If we found work, there may be more work.  Ask for another thread so that the other work can be processed
                            // in parallel.  Note that this will only ask for a max of #procs threads, so it's safe to call it for every dequeue.
                            //
                            EnsureThreadRequested();
                        }
                    }

                    if (workItem == null)
                    {
                        tl.IncrementQueueMiss();
                    }
                    else //execute our work
                    {
                        tl.ResetQueueMiss();
                        workItem.ExecuteWorkItem();
                        workItem = null;
                    }
                }
                return true;
            }
            finally
            {
                //had an exception in the course of executing some work, and this thread is going to die.
                if(needAnotherThread)
                    EnsureThreadRequested();
            }

            //should never hit this code, unless something catastrophically bad happened (like an aborted thread)
            Contract.Assert(false);
            return true;
        }

        internal void RequestWorkerThread()
        {
            //don't acknowledge thread create requests when disposing or stopping
            if (!_shutdownRequested)
            {
                var thread = new Thread(_ => Dispatch()) { IsBackground = ThreadType == ThreadType.Background };
                thread.Start();
            }
        }

        [SecurityCritical]
        internal void EnsureThreadRequested()
        {
            //
            // If we have not yet requested #procs threads from the VM, then request a new thread.
            // Note that there is a separate count in the VM which will also be incremented in this case, 
            // which is handled by RequestWorkerThread.
            //
            int count = numOutstandingThreadRequests;
            while (count < ThreadCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    RequestWorkerThread();
                    break;
                }
                count = prev;
            }
        }

        [SecurityCritical]
        internal void MarkThreadRequestSatisfied()
        {
            //
            // The VM has called us, so one of our outstanding thread requests has been satisfied.
            // Decrement the count so that future calls to EnsureThreadRequested will succeed.
            // Note that there is a separate count in the VM which has already been decremented by the VM
            // by the time we reach this point.
            //
            int count = numOutstandingThreadRequests;
            while (count > 0)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }

        #region IDisposable members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool isDisposing)
        {
            if (!WasDisposed)
            {
                if (isDisposing)
                {
                    Shutdown();
                }
            }

            WasDisposed = true;
        }

        #endregion
    }
        
            internal interface IHeliosWorkItem
    {
        [SecurityCritical]
        void ExecuteWorkItem();
    }

    /// <summary>
    /// Simple work item for executing <see cref="Action"/> delegates
    /// </summary>
    internal sealed class ActionWorkItem : IHeliosWorkItem
    {
        [System.Security.SecuritySafeCritical]
        static ActionWorkItem() {}

        private Action _callback;

        internal ActionWorkItem(Action callback)
        {
            _callback = callback;
        }

        void IHeliosWorkItem.ExecuteWorkItem()
        {
            var action = _callback;
            _callback = null;
            action();
        }
    }

    internal sealed class ThreadPoolWorkQueue
    {
        //10 misses in a row == release thread
        public const int DefaultQueueMissUpperLimit = 10;

        public readonly int QueueMissUpperLimit;

        // Simple sparsely populated array to allow lock-free reading.
        internal class SparseArray<T> where T : class
        {
            private volatile T[] m_array;

            internal SparseArray(int initialSize)
            {
                m_array = new T[initialSize];
            }

            internal T[] Current
            {
                get { return m_array; }
            }

            internal int Add(T e)
            {
                while (true)
                {
                    T[] array = m_array;
                    lock (array)
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            if (array[i] == null)
                            {
                                Volatile.Write(ref array[i], e);
                                return i;
                            }
                            else if (i == array.Length - 1)
                            {
                                // Must resize. If we ----d and lost, we start over again.
                                if (array != m_array)
                                    continue;

                                T[] newArray = new T[array.Length*2];
                                Array.Copy(array, newArray, i + 1);
                                newArray[i + 1] = e;
                                m_array = newArray;
                                return i + 1;
                            }
                        }
                    }
                }
            }

            internal void Remove(T e)
            {
                T[] array = m_array;
                lock (array)
                {
                    for (int i = 0; i < m_array.Length; i++)
                    {
                        if (m_array[i] == e)
                        {
                            Volatile.Write(ref m_array[i], null);
                            break;
                        }
                    }
                }
            }
        }

        internal class WorkStealingQueue
        {
            private const int INITIAL_SIZE = 32;
            internal volatile IHeliosWorkItem[] m_array = new IHeliosWorkItem[INITIAL_SIZE];
            private volatile int m_mask = INITIAL_SIZE - 1;

#if DEBUG
            // in debug builds, start at the end so we exercise the index reset logic.
            private const int START_INDEX = int.MaxValue;
#else
            private const int START_INDEX = 0;
#endif

            private volatile int m_headIndex = START_INDEX;
            private volatile int m_tailIndex = START_INDEX;

            private SpinLock m_foreignLock = new SpinLock(false);

            public void LocalPush(IHeliosWorkItem obj)
            {
                int tail = m_tailIndex;

                // We're going to increment the tail; if we'll overflow, then we need to reset our counts
                if (tail == int.MaxValue)
                {
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        if (m_tailIndex == int.MaxValue)
                        {
                            //
                            // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                            // This way we don't need to rearrange the items already in the queue; they'll be found
                            // correctly exactly where they are.  One subtlety here is that we need to make sure that
                            // if head is currently < tail, it remains that way.  This happens to just fall out from
                            // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all
                            // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible
                            // for the head to end up > than the tail, since you can't set any more bits than all of 
                            // them.
                            //
                            m_headIndex = m_headIndex & m_mask;
                            m_tailIndex = tail = m_tailIndex & m_mask;
                            Contract.Assert(m_headIndex <= m_tailIndex);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(true);
                    }
                }

                // When there are at least 2 elements' worth of space, we can take the fast path.
                if (tail < m_headIndex + m_mask)
                {
                    Volatile.Write(ref m_array[tail & m_mask], obj);
                    m_tailIndex = tail + 1;
                }
                else
                {
                    // We need to contend with foreign pops, so we lock.
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        int head = m_headIndex;
                        int count = m_tailIndex - m_headIndex;

                        // If there is still space (one left), just add the element.
                        if (count >= m_mask)
                        {
                            // We're full; expand the queue by doubling its size.
                            IHeliosWorkItem[] newArray = new IHeliosWorkItem[m_array.Length << 1];
                            for (int i = 0; i < m_array.Length; i++)
                                newArray[i] = m_array[(i + head) & m_mask];

                            // Reset the field values, incl. the mask.
                            m_array = newArray;
                            m_headIndex = 0;
                            m_tailIndex = tail = count;
                            m_mask = (m_mask << 1) | 1;
                        }

                        Volatile.Write(ref m_array[tail & m_mask], obj);
                        m_tailIndex = tail + 1;
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(false);
                    }
                }
            }

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            public bool LocalFindAndPop(IHeliosWorkItem obj)
            {
                // Fast path: check the tail. If equal, we can skip the lock.
                if (m_array[(m_tailIndex - 1) & m_mask] == obj)
                {
                    IHeliosWorkItem unused;
                    if (LocalPop(out unused))
                    {
                        Contract.Assert(unused == obj);
                        return true;
                    }
                    return false;
                }

                // Else, do an O(N) search for the work item. The theory of work stealing and our
                // inlining logic is that most waits will happen on recently queued work.  And
                // since recently queued work will be close to the tail end (which is where we
                // begin our search), we will likely find it quickly.  In the worst case, we
                // will traverse the whole local queue; this is typically not going to be a
                // problem (although degenerate cases are clearly an issue) because local work
                // queues tend to be somewhat shallow in length, and because if we fail to find
                // the work item, we are about to block anyway (which is very expensive).
                for (int i = m_tailIndex - 2; i >= m_headIndex; i--)
                {
                    if (m_array[i & m_mask] == obj)
                    {
                        // If we found the element, block out steals to avoid interference.
                        // @
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            // If we lost the ----, bail.
                            if (m_array[i & m_mask] == null)
                                return false;

                            // Otherwise, null out the element.
                            Volatile.Write(ref m_array[i & m_mask], null);

                            // And then check to see if we can fix up the indexes (if we're at
                            // the edge).  If we can't, we just leave nulls in the array and they'll
                            // get filtered out eventually (but may lead to superflous resizing).
                            if (i == m_tailIndex)
                                m_tailIndex -= 1;
                            else if (i == m_headIndex)
                                m_headIndex += 1;

                            return true;
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(false);
                        }
                    }
                }

                return false;
            }

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            public bool LocalPop(out IHeliosWorkItem obj)
            {
                while (true)
                {
                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    {
                        obj = null;
                        return false;
                    }

                    tail -= 1;
                    Interlocked.Exchange(ref m_tailIndex, tail);

                    // If there is no interaction with a take, we can head down the fast path.
                    if (m_headIndex <= tail)
                    {
                        int idx = tail & m_mask;
                        obj = Volatile.Read(ref m_array[idx]);

                        // Check for nulls in the array.
                        if (obj == null) continue;

                        m_array[idx] = null;
                        return true;
                    }
                    else
                    {
                        // Interaction with takes: 0 or 1 elements left.
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            if (m_headIndex <= tail)
                            {
                                // Element still available. Take it.
                                int idx = tail & m_mask;
                                obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return true;
                            }
                            else
                            {
                                // We lost the ----, element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                obj = null;
                                return false;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(false);
                        }
                    }
                }
            }

            public bool TrySteal(out IHeliosWorkItem obj, ref bool missedSteal)
            {
                return TrySteal(out obj, ref missedSteal, 0); // no blocking by default.
            }

            private bool TrySteal(out IHeliosWorkItem obj, ref bool missedSteal, int millisecondsTimeout)
            {
                obj = null;

                while (true)
                {
                    if (m_headIndex >= m_tailIndex)
                        return false;

                    bool taken = false;
                    try
                    {
                        m_foreignLock.TryEnter(millisecondsTimeout, ref taken);
                        if (taken)
                        {
                            // Increment head, and ensure read of tail doesn't move before it (fence).
                            int head = m_headIndex;
                            Interlocked.Exchange(ref m_headIndex, head + 1);

                            if (head < m_tailIndex)
                            {
                                int idx = head & m_mask;
                                obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return true;
                            }
                            else
                            {
                                // Failed, restore head.
                                m_headIndex = head;
                                obj = null;
                                missedSteal = true;
                            }
                        }
                        else
                        {
                            missedSteal = true;
                        }
                    }
                    finally
                    {
                        if (taken)
                            m_foreignLock.Exit(false);
                    }

                    return false;
                }
            }
        }

        internal class QueueSegment
        {
            // Holds a segment of the queue.  Enqueues/Dequeues start at element 0, and work their way up.
            internal readonly IHeliosWorkItem[] nodes;
            private const int QueueSegmentLength = 256;

            // Holds the indexes of the lowest and highest valid elements of the nodes array.
            // The low index is in the lower 16 bits, high index is in the upper 16 bits.
            // Use GetIndexes and CompareExchangeIndexes to manipulate this.
            private volatile int indexes;

            // The next segment in the queue.
            public volatile QueueSegment Next;


            const int SixteenBits = 0xffff;

            void GetIndexes(out int upper, out int lower)
            {
                int i = indexes;
                upper = (i >> 16) & SixteenBits;
                lower = i & SixteenBits;

                Contract.Assert(upper >= lower);
                Contract.Assert(upper <= nodes.Length);
                Contract.Assert(lower <= nodes.Length);
                Contract.Assert(upper >= 0);
                Contract.Assert(lower >= 0);
            }

            bool CompareExchangeIndexes(ref int prevUpper, int newUpper, ref int prevLower, int newLower)
            {
                Contract.Assert(newUpper >= newLower);
                Contract.Assert(newUpper <= nodes.Length);
                Contract.Assert(newLower <= nodes.Length);
                Contract.Assert(newUpper >= 0);
                Contract.Assert(newLower >= 0);
                Contract.Assert(newUpper >= prevUpper);
                Contract.Assert(newLower >= prevLower);
                Contract.Assert(newUpper == prevUpper ^ newLower == prevLower);

                int oldIndexes = (prevUpper << 16) | (prevLower & SixteenBits);
                int newIndexes = (newUpper << 16) | (newLower & SixteenBits);
                int prevIndexes = Interlocked.CompareExchange(ref indexes, newIndexes, oldIndexes);
                prevUpper = (prevIndexes >> 16) & SixteenBits;
                prevLower = prevIndexes & SixteenBits;
                return prevIndexes == oldIndexes;
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            public QueueSegment()
            {
                Contract.Assert(QueueSegmentLength <= SixteenBits);
                nodes = new IHeliosWorkItem[QueueSegmentLength];
            }


            public bool IsUsedUp()
            {
                int upper, lower;
                GetIndexes(out upper, out lower);
                return (upper == nodes.Length) &&
                       (lower == nodes.Length);
            }

            public bool TryEnqueue(IHeliosWorkItem node)
            {
                //
                // If there's room in this segment, atomically increment the upper count (to reserve
                // space for this node), then store the node.
                // Note that this leaves a window where it will look like there is data in that
                // array slot, but it hasn't been written yet.  This is taken care of in TryDequeue
                // with a busy-wait loop, waiting for the element to become non-null.  This implies
                // that we can never store null nodes in this data structure.
                //
                Contract.Assert(null != node);

                int upper, lower;
                GetIndexes(out upper, out lower);

                while (true)
                {
                    if (upper == nodes.Length)
                        return false;

                    if (CompareExchangeIndexes(ref upper, upper + 1, ref lower, lower))
                    {
                        Contract.Assert(Volatile.Read(ref nodes[upper]) == null);
                        Volatile.Write(ref nodes[upper], node);
                        return true;
                    }
                }
            }

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            public bool TryDequeue(out IHeliosWorkItem node)
            {
                //
                // If there are nodes in this segment, increment the lower count, then take the
                // element we find there.
                //
                int upper, lower;
                GetIndexes(out upper, out lower);

                while (true)
                {
                    if (lower == upper)
                    {
                        node = null;
                        return false;
                    }

                    if (CompareExchangeIndexes(ref upper, upper, ref lower, lower + 1))
                    {
                        // It's possible that a concurrent call to Enqueue hasn't yet
                        // written the node reference to the array.  We need to spin until
                        // it shows up.
                        SpinWait spinner = new SpinWait();
                        while ((node = Volatile.Read(ref nodes[lower])) == null)
                            spinner.SpinOnce();

                        // Null-out the reference so the object can be GC'd earlier.
                        nodes[lower] = null;

                        return true;
                    }
                }
            }
        }

        // The head and tail of the queue.  We enqueue to the head, and dequeue from the tail.
        internal volatile QueueSegment queueHead;
        internal volatile QueueSegment queueTail;

        internal SparseArray<WorkStealingQueue> allThreadQueues = new SparseArray<WorkStealingQueue>(16);

        public ThreadPoolWorkQueue() : this(DefaultQueueMissUpperLimit) { }

        public ThreadPoolWorkQueue(int queueMissUpperLimit)
        {
            QueueMissUpperLimit = queueMissUpperLimit;
            queueTail = queueHead = new QueueSegment();
        }

        [SecurityCritical]
        public ThreadPoolWorkQueueThreadLocals EnsureCurrentThreadHasQueue()
        {
            if (null == ThreadPoolWorkQueueThreadLocals.threadLocals)
                ThreadPoolWorkQueueThreadLocals.threadLocals = new ThreadPoolWorkQueueThreadLocals(this);
            return ThreadPoolWorkQueueThreadLocals.threadLocals;
        }

        [SecurityCritical]
        public void Enqueue(IHeliosWorkItem callback, bool forceGlobal)
        {
            ThreadPoolWorkQueueThreadLocals tl = null;
            if (!forceGlobal)
                tl = ThreadPoolWorkQueueThreadLocals.threadLocals;

            if (null != tl)
            {
                tl.workStealingQueue.LocalPush(callback);
            }
            else
            {
                QueueSegment head = queueHead;

                while (!head.TryEnqueue(callback))
                {
                    Interlocked.CompareExchange(ref head.Next, new QueueSegment(), null);

                    while (head.Next != null)
                    {
                        Interlocked.CompareExchange(ref queueHead, head.Next, head);
                        head = queueHead;
                    }
                }
            }
        }

        [SecurityCritical]
        internal bool LocalFindAndPop(IHeliosWorkItem callback)
        {
            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
            if (null == tl)
                return false;

            return tl.workStealingQueue.LocalFindAndPop(callback);
        }

        [SecurityCritical]
        public void Dequeue(ThreadPoolWorkQueueThreadLocals tl, out IHeliosWorkItem callback, out bool missedSteal)
        {
            callback = null;
            missedSteal = false;
            WorkStealingQueue wsq = tl.workStealingQueue;

            if (wsq.LocalPop(out callback))
                Contract.Assert(null != callback);

            if (null == callback)
            {
                QueueSegment tail = queueTail;
                while (true)
                {
                    if (tail.TryDequeue(out callback))
                    {
                        Contract.Assert(null != callback);
                        break;
                    }

                    if (null == tail.Next || !tail.IsUsedUp())
                    {
                        break;
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref queueTail, tail.Next, tail);
                        tail = queueTail;
                    }
                }
            }

            if (null == callback)
            {
                WorkStealingQueue[] otherQueues = allThreadQueues.Current;
                int i = tl.random.Next(otherQueues.Length);
                int c = otherQueues.Length;
                while (c > 0)
                {
                    WorkStealingQueue otherQueue = Volatile.Read(ref otherQueues[i % otherQueues.Length]);
                    if (otherQueue != null &&
                        otherQueue != wsq &&
                        otherQueue.TrySteal(out callback, ref missedSteal))
                    {
                        Contract.Assert(null != callback);
                        break;
                    }
                    i++;
                    c--;
                }
            }
        }
    }

    internal sealed class ThreadPoolWorkQueueThreadLocals
    {
        [ThreadStatic]
        [SecurityCritical]
        public static ThreadPoolWorkQueueThreadLocals threadLocals;

        /// <summary>
        /// Consecutive number of misses against the queue
        /// </summary>
        public int ConsecutiveQueueMissCount { get; private set; }

        public readonly ThreadPoolWorkQueue workQueue;
        public readonly ThreadPoolWorkQueue.WorkStealingQueue workStealingQueue;
        public readonly Random random = new Random(Thread.CurrentThread.ManagedThreadId);

        public ThreadPoolWorkQueueThreadLocals(ThreadPoolWorkQueue tpq)
        {
            Contract.Assert(tpq != null, "ThreadPoolWorkQueue cannot be null");
            workQueue = tpq;
            workStealingQueue = new ThreadPoolWorkQueue.WorkStealingQueue();
            workQueue.allThreadQueues.Add(workStealingQueue);
        }

        public void IncrementQueueMiss()
        {
            ConsecutiveQueueMissCount = ConsecutiveQueueMissCount + 1;
        }

        public void ResetQueueMiss()
        {
            ConsecutiveQueueMissCount = 0;
        }

        [SecurityCritical]
        private void CleanUp()
        {
            if (null != workStealingQueue)
            {
                if (null != workQueue)
                {
                    bool done = false;
                    while (!done)
                    {
                        // Ensure that we won't be aborted between LocalPop and Enqueue.
                        try { }
                        finally
                        {
                            IHeliosWorkItem cb = null;
                            if (workStealingQueue.LocalPop(out cb))
                            {
                                Contract.Assert(null != cb);
                                workQueue.Enqueue(cb, true);
                            }
                            else
                            {
                                done = true;
                            }
                        }
                    }
                }

                workQueue.allThreadQueues.Remove(workStealingQueue);
            }
        }

        [SecuritySafeCritical]
        ~ThreadPoolWorkQueueThreadLocals()
        {
            // Since the purpose of calling CleanUp is to transfer any pending workitems into the global
            // queue so that they will be executed by another thread, there's no point in doing this cleanup
            // if we're in the process of shutting down or unloading the AD.  In those cases, the work won't
            // execute anyway.  And there are subtle ----s involved there that would lead us to do the wrong
            // thing anyway.  So we'll only clean up if this is a "normal" finalization.
            if (!(Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload()))
                CleanUp();
        }
    }
}

