using System;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp.Parallelx.TaskSchedulers
{
    public class FreeTaskScheduler : WorkStealingTaskSchedulerExtensible
    {
        #region Fields

        private bool useBackgroundThreads = false;

        private readonly ThreadLocal<bool> isCurrentThreadProcessingItems = new ThreadLocal<bool>();

        private bool preferInliningSynchronousContinuationTasks = true;
        
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="Thread.IsBackground"/> property of the worker threads.
        /// </summary>
        public bool UseBackgroundThreads
        {
            get { return this.useBackgroundThreads; }
            set
            {
                this.useBackgroundThreads = value;
                foreach (Thread thread in this.WorkerThreads)
                    thread.IsBackground = value;
            }
        }

        /// <summary>
        /// Whether the current thread is processing work items in the current task scheduler.
        /// </summary>
        /// <remarks>
        /// Adapted from LimitedConcurrencyLevelTaskScheduler
        /// </remarks>
        public bool IsCurrentThreadProcessingItems
        {
            get { return isCurrentThreadProcessingItems.Value; }
            set { isCurrentThreadProcessingItems.Value = value; }
        }
        
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a task scheduler that will use as many threads as there are processors.
        /// </summary>
        public FreeTaskScheduler() 
            : this(EnvironmentCached.ProcessorCount)
        { }

        /// <summary>
        /// Initializes a task scheduler that will use the specified number of threads.
        /// </summary>
        public FreeTaskScheduler(int concurrencyLevel)
            : this(concurrencyLevel, runThreads: true)
        { }

        /// <summary>
        /// Initializes a task scheduler that will use the specified number of threads.
        /// </summary>
        public FreeTaskScheduler(int concurrencyLevel, bool runThreads)
            : base(concurrencyLevel)
        {
            this.CreateDedicatedThreadsForLongRunningTasks = false;
            this.TransferLocalTasksFromDepartingThreads = true;
            this.UseVolatileReadAfterLocalPush = true;

            // Run threads eagerly.
            if (runThreads)
                this.RunThreads();
        }
        
        #endregion

        #region Methods

        protected override Thread CreateThread()
        {
            Thread thread = base.CreateThread();
            thread.IsBackground = this.useBackgroundThreads;
            return thread;
        }

        /// <summary>
        /// The dispatch loop run by each thread in the scheduler.
        /// </summary>
        protected internal override void DispatchLoop(CancellationToken cancellationToken)
        {
            this.IsCurrentThreadProcessingItems = true;

            try
            {
                base.DispatchLoop(cancellationToken);
            }
            finally
            {
                this.IsCurrentThreadProcessingItems = false;
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // From LimitedConcurrencyLevelTaskScheduler:
            // If this thread isn't processing tasks, we don't support inlining.
            
            // However, if the task is a synchronous continuation, 
            // we will inline it anyway so as to avoid a bug in Mono.
            // http://stackoverflow.com/questions/17663484/mono-issue-with-synchronous-continuation-tasks

            // Checking whether the task is a synchronous continuation
            // is an expensive operation, and performed sparingly.

            if (this.IsCurrentThreadProcessingItems ||
                this.preferInliningSynchronousContinuationTasks &&
                IsSynchronousContinuationTask(task))
            {
                return base.TryExecuteTaskInline(task, taskWasPreviouslyQueued);
            }

            return false; 
        }

        protected static bool IsSynchronousContinuationTask(Task task)
        {
            string stackTrace = Environment.StackTrace;
            
            if (EnvironmentCached.IsRunningOnMono)
                return stackTrace.Contains("System.Threading.Tasks.Task.RunSynchronouslyCore") 
                    && stackTrace.Contains("System.Threading.Tasks.TaskContinuation.Execute");
            else
                return stackTrace.Contains("System.Threading.Tasks.TaskContinuation.InlineIfPossibleOrElseQueue");
        }

        public override void Dispose()
        {
            base.Dispose();
            
            this.isCurrentThreadProcessingItems.Dispose();
        }
        
        #endregion
    }
}

