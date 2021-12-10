using System;
using System.Threading;

namespace CSharp.Parallelx.TaskSchedulers
{
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using  System.Threading.Tasks;
    
        /// <summary>Provides a work-stealing scheduler.</summary>
    public class WorkStealingTaskSchedulerExtensible : TaskScheduler, IDisposable
    {
        #region Fields

        private readonly int m_concurrencyLevel;

        /// <summary>
        /// Global task queue for current scheduler.
        /// All access should be protected by mutual-exclusion lock on itself.
        /// </summary>
        private readonly Queue<Task> m_queue = new Queue<Task>();

        /// <summary>
        /// Collection of local work-stealing task queues for threads active in current scheduler.
        /// </summary>
        private WorkStealingQueueExtensable<Task>[] m_wsQueues = new WorkStealingQueueExtensable<Task>[Environment.ProcessorCount];

        /// <summary>
        /// The lazily-initialized collection of local threads started in the current scheduler.
        /// </summary>
        private Lazy<Thread[]> m_threads;
        
        /// <summary>
        /// The number of threads active in the current scheduler that are blocked, waiting for new tasks.
        /// </summary>
        private int m_threadsWaiting;

        /// <summary>
        /// Whether the current scheduler is being disposed.
        /// </summary>
        private bool m_shutdown;

        /// <summary>
        /// Local work-stealing task queue for current thread.
        /// </summary>
        [ThreadStatic]
        private static WorkStealingQueueExtensable<Task> m_wsq;

        private bool createDedicatedThreadsForLongRunningTasks = true;
        private bool transferLocalTasksFromDepartingThreads = false;
        private bool useVolatileReadAfterLocalPush = false;
       
        private const TaskCreationOptions TaskCreationOptions_PreferFairnessOrLongRunning =
            TaskCreationOptions.PreferFairness | 
            TaskCreationOptions.LongRunning;
        
        #endregion

        #region Properties

        /// <summary>Gets the maximum concurrency level supported by this scheduler.</summary>
        public override int MaximumConcurrencyLevel
        {
            get { return m_concurrencyLevel; }
        }

        public int ConcurrencyLevel
        {
            get { return m_concurrencyLevel; }
        }

        public bool CreateDedicatedThreadsForLongRunningTasks
        {
            get { return createDedicatedThreadsForLongRunningTasks; }
            set { createDedicatedThreadsForLongRunningTasks = value; }
        }

        public bool TransferLocalTasksFromDepartingThreads
        {
            get { return transferLocalTasksFromDepartingThreads; }
            set { transferLocalTasksFromDepartingThreads = value; }
        }

        // http://igoro.com/archive/volatile-keyword-in-c-memory-model-explained/
        public bool UseVolatileReadAfterLocalPush
        {
            get { return useVolatileReadAfterLocalPush; }
            set { useVolatileReadAfterLocalPush = value; }
        }

        protected Thread[] WorkerThreads
        {
            get { return m_threads.Value; }
        }
        
        #endregion

        #region Constructors

        /// <summary>Initializes a new instance of the WorkStealingTaskScheduler class.</summary>
        /// <remarks>This constructors defaults to using twice as many threads as there are processors.</remarks>
        public WorkStealingTaskSchedulerExtensible() : this(Environment.ProcessorCount * 2) { }

        /// <summary>Initializes a new instance of the WorkStealingTaskScheduler class.</summary>
        /// <param name="concurrencyLevel">The number of threads to use in the scheduler.</param>
        public WorkStealingTaskSchedulerExtensible(int concurrencyLevel)
        {
            // Store the concurrency level
            if (concurrencyLevel < 0) throw new ArgumentOutOfRangeException("concurrencyLevel");
            m_concurrencyLevel = concurrencyLevel;

            // Set up threads
            m_threads = new Lazy<Thread[]>(CreateThreads);
        }

        #endregion

        #region Methods

        protected virtual Thread[] CreateThreads()
        {
            var threads = new Thread[m_concurrencyLevel];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = this.CreateThread();
                threads[i].Start();
            }
            return threads;
        }

        protected virtual Thread CreateThread()
        {
            return new Thread(DispatchLoop) { IsBackground = false };
        }

        public virtual void RunThreads()
        {
            // To get proper timings, 
            // we want our pool of threads to be created at the beginning,
            // not on first task.
            m_threads.Force();
        }

        /// <summary>Queues a task to the scheduler.</summary>
        /// <param name="task">The task to be scheduled.</param>
        protected override void QueueTask(Task task)
        {
            // Make sure the pool is started, e.g. that all threads have been created.
            m_threads.Force();

            // If the task is marked as long-running, give it its own dedicated thread,
            // rather than queueing it, provided that dedicated threads are allowed.
            if ((task.CreationOptions & TaskCreationOptions.LongRunning) != 0 && this.createDedicatedThreadsForLongRunningTasks)
            {
                new Thread(state => base.TryExecuteTask((Task)state)) { IsBackground = true }.Start(task);
            }
            else
            {
                // Otherwise, insert the work item into a queue, possibly waking a thread.
                // If there's a local queue and the task does not prefer to be in the global queue,
                // add it to the local queue.
                WorkStealingQueueExtensable<Task> wsq = m_wsq;
                if (wsq != null && ((task.CreationOptions & TaskCreationOptions_PreferFairnessOrLongRunning) == 0))
                {
                    // Add to the local queue and notify any waiting threads that work is available.
                    wsq.LocalPush(task);

                    // Races may occur which result in missed event notifications, but they're benign in that
                    // this thread will eventually pick up the work item anyway, as will other threads when another
                    // work item notification is received.
                    int threadsWaiting =
                        this.UseVolatileReadAfterLocalPush ?
                        Thread.VolatileRead(ref m_threadsWaiting) :
                        m_threadsWaiting;   // OK to read lock-free.
                    
                    if (threadsWaiting > 0)
                    {
                        lock (m_queue) { Monitor.Pulse(m_queue); }
                    }
                }
                // Otherwise, add the work item to the global queue.
                // This is also done if the task is marked as long-running,
                // but dedicated threads are not allowed.
                else
                {
                    lock (m_queue)
                    {
                        m_queue.Enqueue(task);
                        if (m_threadsWaiting > 0) Monitor.Pulse(m_queue);
                    }
                }
            }
        }

        /// <summary>Executes a task on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued">Ignored.</param>
        /// <returns>Whether the task could be executed.</returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryExecuteTask(task);

            // // Optional replacement: Instead of always trying to execute the task (which could
            // // benignly leave a task in the queue that's already been executed), we
            // // can search the current work-stealing queue and remove the task,
            // // executing it inline only if it's found.
            // WorkStealingQueue<Task> wsq = m_wsq;
            // return wsq != null && wsq.TryFindAndPop(task) && TryExecuteTask(task);
        }

        /// <summary>Gets all of the tasks currently scheduled to this scheduler.</summary>
        /// <returns>An enumerable containing all of the scheduled tasks.</returns>
        /// <remarks>For debugger support only.</remarks>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // Keep track of all of the tasks we find
            List<Task> tasks = new List<Task>();

            // Get all of the global tasks.  We use TryEnter so as not to hang
            // a debugger if the lock is held by a frozen thread.
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(m_queue, ref lockTaken);
                if (lockTaken) tasks.AddRange(m_queue.ToArray());
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(m_queue);
            }

            // Now get all of the tasks from the work-stealing queues
            WorkStealingQueueExtensable<Task>[] queues = m_wsQueues;
            for (int i = 0; i < queues.Length; i++)
            {
                WorkStealingQueueExtensable<Task> wsq = queues[i];
                if (wsq != null) tasks.AddRange(wsq.ToArray());
            }

            // Return to the debugger all of the collected task instances
            return tasks;
        }

        /// <summary>Adds a work-stealing queue to the set of queues.</summary>
        /// <param name="wsq">The queue to be added.</param>
        private void AddWsq(WorkStealingQueueExtensable<Task> wsq)
        {
            lock (m_wsQueues)
            {
                // Find the next open slot in the array. If we find one,
                // store the queue and we're done.
                int i;
                for (i = 0; i < m_wsQueues.Length; i++)
                {
                    if (m_wsQueues[i] == null)
                    {
                        m_wsQueues[i] = wsq;
                        return;
                    }
                }

                // We couldn't find an open slot, so double the length 
                // of the array by creating a new one, copying over,
                // and storing the new one. Here, i == m_wsQueues.Length.
                WorkStealingQueueExtensable<Task>[] queues = new WorkStealingQueueExtensable<Task>[i * 2];
                Array.Copy(m_wsQueues, queues, i);
                queues[i] = wsq;
                m_wsQueues = queues;
            }
        }

        /// <summary>Remove a work-stealing queue from the set of queues.</summary>
        /// <param name="wsq">The work-stealing queue to remove.</param>
        private void RemoveWsq(WorkStealingQueueExtensable<Task> wsq)
        {
            lock (m_wsQueues)
            {
                // Find the queue, and if/when we find it, null out its array slot
                for (int i = 0; i < m_wsQueues.Length; i++)
                {
                    if (m_wsQueues[i] == wsq)
                    {
                        m_wsQueues[i] = null;
                    }
                }
            }
        }

        /// <summary>
        /// The dispatch loop run by each thread in the scheduler.
        /// </summary>
        protected internal void DispatchLoop()
        {
            this.DispatchLoop(CancellationToken.None);
        }

        protected internal virtual void DispatchLoop(CancellationToken cancellationToken)
        {
            // Create a new queue for this thread, and store it in TLS for later retrieval,
            // unless TLS already indicates the presence of a local queue.
            WorkStealingQueueExtensable<Task> wsq = m_wsq;
            if (wsq == null)
            {
                wsq = new WorkStealingQueueExtensable<Task>();
                m_wsq = wsq;
            }

            // Add it to the set of queues for this scheduler.
            AddWsq(wsq);

            try
            {
                // Until there's no more work to do...
                // ...or cancellation has been requested externally
                while (!cancellationToken.IsCancellationRequested)
                {
                    Task wi = null;

                    // Search order: (1) local WSQ, (2) global Q, (3) steals from other queues.
                    if (!wsq.LocalPop(ref wi))
                    {
                        // We weren't able to get a task from the local WSQ
                        bool searchedForSteals = false;
                        while (true)
                        {
                            lock (m_queue)
                            {
                                // If shutdown was requested, exit the thread.
                                if (m_shutdown || cancellationToken.IsCancellationRequested)
                                    return;

                                // (2) try the global queue.
                                if (m_queue.Count != 0)
                                {
                                    // We found a work item! Grab it ...
                                    wi = m_queue.Dequeue();
                                    break;
                                }
                                else if (searchedForSteals)
                                {
                                    // Note that we're not waiting for work, and then wait
                                    m_threadsWaiting++;
                                    try { Monitor.Wait(m_queue); }
                                    finally { m_threadsWaiting--; }

                                    // If we were signaled due to shutdown, exit the thread.
                                    if (m_shutdown || cancellationToken.IsCancellationRequested)
                                        return;

                                    searchedForSteals = false;
                                    continue;
                                }
                            }

                            // (3) try to steal.
                            WorkStealingQueueExtensable<Task>[] wsQueues = m_wsQueues;
                            int i;
                            for (i = 0; i < wsQueues.Length; i++)
                            {
                                WorkStealingQueueExtensable<Task> q = wsQueues[i];
                                if (q != null && q != wsq && q.TrySteal(ref wi)) break;
                            }

                            if (i != wsQueues.Length) break;

                            searchedForSteals = true;
                        }
                    }

                    // ...and Invoke it.
                    TryExecuteTask(wi);
                }
            }
            finally
            {
                // Remove work-stealing task queue for current thread 
                // from the set of queues for this scheduler.
                RemoveWsq(wsq);

                // Transfer all its tasks to the global queue.
                if (this.transferLocalTasksFromDepartingThreads)
                    this.TransferLocalTasks(wsq);
            }
        }

        private void TransferLocalTasks(WorkStealingQueueExtensable<Task> wsq)
        {
            if (!wsq.IsEmpty)
            {
                lock (m_queue)
                {
                    Task task = null;
                    while (wsq.LocalPop(ref task))
                        m_queue.Enqueue(task);

                    if (m_threadsWaiting > 0)
                        Monitor.PulseAll(m_queue);
                }
            }
        }

        /// <summary>
        /// Signal the scheduler to shutdown and wait for all threads to finish.
        /// </summary>
        public virtual void Dispose()
        {
            if (m_shutdown)
                return;   // idempotence

            m_shutdown = true;
            if (m_queue != null && m_threads.IsValueCreated)
            {
                var threads = m_threads.Value;
                lock (m_queue) Monitor.PulseAll(m_queue);
                for (int i = 0; i < threads.Length; i++) threads[i].Join();
                m_threads = null;
                m_queue.Clear();
            }
        }

        /// <summary>
        /// Signals all blocked threads to resume.
        /// Should be invoked after requesting cancellation through the <see cref="CancellationToken"/> tokens
        /// passed from external invocations of the <see cref="DispatchLoop(CancellationToken)"/> method.
        /// </summary>
        public void PulseAll()
        {
            lock (m_queue) 
                Monitor.PulseAll(m_queue);
        }

        #endregion
    }
    
}
