using System.Threading;
using CSharp.Parallelx.Threads;

namespace CSharp.Parallelx.TaskSchedulers
{
    public class AffinityTaskScheduler : FreeTaskScheduler
    {
        #region Fields

        private readonly ProcessorSet processorSet;
        
        private AffineThread[] affineThreads;

        #endregion

        #region Properties

        public ProcessorSet ProcessorSet
        {
            get { return processorSet; }
        } 

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an affinity task scheduler that will run worker threads 
        /// on all available processors.
        /// </summary>
        public AffinityTaskScheduler() 
            : this(ProcessorSet.Full)
        { }
        
        /// <summary>
        /// Initializes an affinity task scheduler that will run worker threads 
        /// on the set of processors specified in <param name="processorSet"/>.
        /// </summary>
        public AffinityTaskScheduler(ProcessorSet processorSet)
            : base(processorSet.Count, runThreads: false)
        {
            this.processorSet = processorSet.Clone(asReadOnly: true);

            this.RunThreads();
        }
        
        #endregion

        #region Methods

        protected override Thread[] CreateThreads()
        {
            AffineThread[] affineThreads = AffineThread.CreateBatch(this.ProcessorSet, this.DispatchLoop, launch: true);
            Thread[] managedThreads = affineThreads.SelectArray(affineThread => affineThread.ManagedThread);

            foreach (Thread managedThread in managedThreads)
                managedThread.IsBackground = this.UseBackgroundThreads;

            this.affineThreads = affineThreads;
            return managedThreads;
        }
        
        public override void Dispose()
        {
            base.Dispose();

            if (this.affineThreads != null)
            {
                this.affineThreads.DisposeAll();
                this.affineThreads = null;
            }
        }
        
        #endregion
    }
}
