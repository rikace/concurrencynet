using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CSharp.Parallelx.Threads
{
    public abstract class AffineThread : IDisposable
    {
        #region Fields

        private ThreadStart threadStart;
        private Thread managedThread;
        private ManualResetEvent initializedEvent;

        private readonly Lazy<ProcessThread> processThread;

        private ulong? processorAffinity;
        private int? targetProcessorID;
        private bool isStarted = false;

        [ThreadStatic]
        public static AffineThread Current;

        #endregion

        #region Properties

        /// <summary>
        /// The .NET managed thread.
        /// </summary>
        public Thread ManagedThread
        {
            get { return this.managedThread; }
        }

        /// <summary>
        /// The operating system process thread.
        /// </summary>
        public ProcessThread ProcessThread
        {
            get { return processThread.Value; }
        }
        
        /// <summary>
        /// The processors on which the associated thread can run,
        /// or <see langword="null"/> to refrain from setting this option.
        /// </summary>
        /// <remarks>
        /// Configures <seealso cref="ProcessThread.ProcessorAffinity"/>.
        /// May only be set before <see cref="Start"/> is called.
        /// </remarks>
        public ulong? ProcessorAffinity
        {
            get { return this.processorAffinity; }
            set { this.SetProcessorAffinity(value); }
        }
        
        /// <summary>
        /// The zero-based index of the target processr for this thread to run on.
        /// </summary>
        public int? TargetProcessorID
        {
            get { return this.targetProcessorID; }
            set { this.SetTargetProcessorID(value); }
        }

        public string Name { get; set; }

        #endregion

        #region Constructors

        protected AffineThread()
        {
            this.processThread = new Lazy<ProcessThread>(this.GetProcessThread);
        }

        #endregion

        #region Factory Methods

        public static AffineThread CreateBlank()
        {
            if (IsRunningOnWindows())
                return new AffineThreadWindows();
            else
                return new AffineThreadLinux();
        }

        public static AffineThread CreateSpawn(ThreadStart threadStart)
        {
            if (threadStart == null)
                throw new ArgumentNullException("threadStart");

            AffineThread affineThread = CreateBlank();

            affineThread.threadStart = threadStart;
            affineThread.managedThread = new Thread(affineThread.Run);
            affineThread.initializedEvent = new ManualResetEvent(false);

            return affineThread;
        }

        public static AffineThread CreateAffine(ThreadStart threadStart, int targetProcessor, bool launch)
        {
            AffineThread affineThread = CreateSpawn(threadStart);
            affineThread.TargetProcessorID = targetProcessor;
            affineThread.Name = string.Format("Affine Thread for Processor {0}", targetProcessor);

            Thread managedThread = affineThread.ManagedThread;
            managedThread.IsBackground = false;
            managedThread.Name = affineThread.Name;

            if (launch)
            {
                affineThread.Start();
                affineThread.WaitUntilRunning();
            }

            return affineThread;
        }

        public static AffineThread[] CreateBatch(ProcessorSet processorSet, ThreadStart threadStart, bool launch)
        {
            var affineThreadsSequence = processorSet.Select(processorID =>
                CreateAffine(threadStart, processorID, launch: false));

            AffineThread[] affineThreads = affineThreadsSequence.ToArray();

            if (launch)
                LaunchBatch(affineThreads);

            return affineThreads;
        }

        public static void LaunchBatch(IEnumerable<AffineThread> affineThreads)
        {
            foreach (AffineThread affineThread in affineThreads)
                affineThread.Start();
            
            foreach (AffineThread affineThread in affineThreads)
                affineThread.WaitUntilRunning();
        }

        #endregion

        #region Config Methods

        private void SetProcessorAffinity(ulong? processorAffinity)
        {
            if (this.isStarted)
                throw new InvalidOperationException("Processor affinity cannot be set after thread has been started.");

            this.targetProcessorID = null;
            this.processorAffinity = processorAffinity;
        }

        private void SetTargetProcessorID(int? processorID)
        {
            if (this.isStarted)
                throw new InvalidOperationException("Processor affinity cannot be set after thread has been started.");
            
            if (processorID != null)
                VerifyProcessorID(processorID.Value);

            this.targetProcessorID = processorID;

            if (processorID == null)
                this.processorAffinity = null;
            else
                this.processorAffinity = CalculateProcessorMask(processorID.Value);
        }

        internal static void VerifyProcessorID(int processorID)
        {
            if (processorID < 0)
                throw new ArgumentOutOfRangeException("processorID");

            if (processorID >= EnvironmentCached.ProcessorCount)
                throw new ArgumentOutOfRangeException("processorID",
                    "The specified processor, " + processorID + ", is not available on current system.");

            if (processorID >= 64)
                throw new ArgumentOutOfRangeException("processorID",
                    "The current implementation only supports up to 64 processors.");
        }
        
        #endregion

        #region Start Methods

        /// <summary>
        /// Starts the managed thread.
        /// </summary>
        public void Start()
        {
            if (this.isStarted)
                throw new ThreadStateException("The thread has already been started.");
            
            this.isStarted = true;
            managedThread.Start();
        }
        
        /// <summary>
        /// Waits until the thread has been initialized and is running.
        /// </summary>
        /// <remarks>
        /// Useful to exclude setup initialization time from performance measurements.
        /// </remarks>
        public void WaitUntilRunning()
        {
            this.initializedEvent.WaitOne();
        }
        
        private void Run()
        {
            // Fixes managed thread to OS thread.
            // "Notifies a host that managed code is about to execute instructions that
            //  depend on the identity of the current physical operating system thread."
            Thread.BeginThreadAffinity();

            try
            {
                Current = this;

                if (this.ProcessorAffinity != null)
                {
                    this.SetAffinity();
                    this.VerifyAffinity();
                }

                try
                {
                    // Thread is now running.
                    this.initializedEvent.Set();

                    // Invoke ThreadStart or ParameterizedThreadStart delegate to run user-specified code.
                    this.threadStart();
                }
                finally
                {
                    // Only reset if originally set.
                    if (this.ProcessorAffinity != null)
                        this.ResetAffinity();
                }
            }
            finally
            {
                // Unlinks managed thread from OS thread.
                // "Notifies a host that managed code has finished executing instructions that
                //  depend on the identity of the current physical operating system thread."
                Thread.EndThreadAffinity();
            }
        }

        #endregion

        #region Affinity Methods

        protected abstract void SetAffinity();
        protected abstract void ResetAffinity();

        /// <summary>
        /// Verify that thread is running on correct processor.
        /// </summary>
        protected virtual void VerifyAffinity()
        {
            int currentProcessorID = GetProcessorID();
            ulong targetProcessorAffinity = this.ProcessorAffinity.Value;

            VerifyAffinity(currentProcessorID, targetProcessorAffinity);
        }

        internal static void VerifyAffinity(int currentProcessorID, ulong targetProcessorAffinity)
        {
            ulong currentProcessorBit = CalculateProcessorMask(currentProcessorID);

            if ((targetProcessorAffinity & currentProcessorBit) == 0)
                throw new ApplicationException(
                    "The current thread is not running on an allowed processor. " +
                    "Target processor affinity: " + targetProcessorAffinity + ". " +
                    "Actual processor ID: " + currentProcessorID + ".");
        }

        #endregion
        
        #region Helper Methods

        /// <summary>
        /// Returns <see langword="true"/> if the current process is running on Mono.
        /// </summary>
        public static bool IsRunningOnMono()
        {
            return EnvironmentCached.IsRunningOnMono;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the current process is running on Windows.
        /// </summary>
        public static bool IsRunningOnWindows()
        {
            return EnvironmentCached.OSVersion.Platform == PlatformID.Win32NT;
        }

        protected internal static ulong CalculateProcessorMask(int processorID)
        {
            // (long)Math.Pow(2, processorID);
            return 1UL << processorID;
        }

        protected static ulong CalculateFullProcessorMask()
        {
            // Per Process.ProcessorAffinity:
            // The default value is 2^n - 1, where n is the number of processors.
            // http://msdn.microsoft.com/en-us/library/system.diagnostics.process.processoraffinity.aspx

            if (EnvironmentCached.ProcessorCount >= 64)
                return 0xFFFFFFFFFFFFFFFF;

            return CalculateProcessorMask(EnvironmentCached.ProcessorCount) - 1;
        }

        #endregion

        #region Access Methods

        /// <summary>
        /// Gets the operating system process thread corresponding to the managed thread.
        /// </summary>
        protected abstract ProcessThread GetProcessThread();

        /// <summary>
        /// Gets the processor ID that the current thread is running on.
        /// </summary>
        public abstract int GetProcessorID();

        public override string ToString()
        {
            return this.Name ?? base.ToString();
        }

        #endregion
        
        #region Dispose Methods

        public void Dispose()
        {
            // Assumes that thread has been stopped by user code.
            this.managedThread.Join();
            this.initializedEvent.Dispose();
        }

        #endregion
    }
}