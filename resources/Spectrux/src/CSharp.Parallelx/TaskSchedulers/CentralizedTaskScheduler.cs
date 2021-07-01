using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp.Parallelx.TaskSchedulers
{
    public class CentralizedTaskScheduler : TaskScheduler, IDisposable
    {
        #region Fields

        private readonly int concurrencyLevel;
        private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>();
        private readonly Thread[] threads;
        private readonly AutoResetEvent threadInitializeEvent = new AutoResetEvent(false);
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;

        [ThreadStatic]
        private static bool isCurrentThreadProcessingItems;
        
        #endregion
        
        #region Constructors

        public CentralizedTaskScheduler() : this(Environment.ProcessorCount) 
        { }

        public CentralizedTaskScheduler(int concurrencyLevel)
        {
            this.concurrencyLevel = concurrencyLevel;
            this.cancellationToken = this.cancellationTokenSource.Token;
            this.threads = this.CreateThreads();
        }

        #endregion

        #region Methods

        protected virtual Thread[] CreateThreads()
        {
            var threads = new Thread[concurrencyLevel];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(DispatchLoop) { IsBackground = false };
                threads[i].Start();
                threadInitializeEvent.WaitOne();
            }

            return threads;
        }
        
        protected override void QueueTask(Task task)
        {
            this.queue.Add(task);
        }

        /// <summary>Executes a task on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued">Ignored.</param>
        /// <returns>Whether the task could be executed.</returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (isCurrentThreadProcessingItems == false)
                return false;

            return TryExecuteTask(task);
        }
        
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return this.queue;
        }
        
        private void DispatchLoop()
        {
            isCurrentThreadProcessingItems = true;
            threadInitializeEvent.Set();

            try
            {
                while (true)
                {
                    Task task = this.queue.Take(cancellationToken);
                    TryExecuteTask(task);
                }
            }
            catch (OperationCanceledException)
            { }
            finally
            {
                isCurrentThreadProcessingItems = false;
            }
        }

        public virtual void Dispose()
        {
            this.cancellationTokenSource.Cancel();

            foreach (Thread thread in threads)
                thread.Join();

            this.cancellationTokenSource.Dispose();
            this.threadInitializeEvent.Dispose();
            this.queue.Dispose();
        }

        #endregion
    }
}