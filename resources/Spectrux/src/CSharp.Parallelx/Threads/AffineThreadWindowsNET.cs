using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CSharp.Parallelx.Threads
{
    public class AffineThreadWindowsNET : AffineThread
    {
        #region Constructors

        internal AffineThreadWindowsNET()
            : base()
        { }

        #endregion
        
        #region Affinity Methods
        
        protected override void SetAffinity()
        {
            // Internally performs SetThreadAffinityMask system call.
            this.ProcessThread.ProcessorAffinity = new IntPtr((long)this.ProcessorAffinity.Value);

            // Change should be immediate:
            // "If the new thread affinity mask does not specify the processor that is currently running the thread,
            //  the thread is rescheduled on one of the allowable processors."
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms686247(v=vs.85).aspx
        }

        protected override void ResetAffinity()
        {
            ulong fullMask = CalculateFullProcessorMask();
            this.ProcessThread.ProcessorAffinity = new IntPtr((long)fullMask);
        }
                
        #endregion
        
        #region Helper Methods

        /// <summary>
        /// Gets the operating system process thread corresponding to the managed thread.
        /// </summary>
        protected override ProcessThread GetProcessThread()
        {
            // http://stackoverflow.com/questions/12328751/set-thread-processor-affinity-in-microsoft-net

            int tid = GetCurrentThreadId();

            Process process = Process.GetCurrentProcess();
            IEnumerable<ProcessThread> processThreads = process.Threads.Cast<ProcessThread>();
            IEnumerable<ProcessThread> currentThreads = processThreads.Where(pt => pt.Id == tid);
            return currentThreads.Single();
        }

        /// <summary>
        /// Gets the processor ID that the current thread is running on.
        /// </summary>
        public override int GetProcessorID()
        {
            return GetCurrentProcessorNumber();
        }
        
        #endregion

        #region Windows P/Invoke Methods

        /// <summary>
        /// Retrieves the thread identifier of the calling thread.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();

        /// <summary>
        /// Retrieves the number of the processor the current thread was running on during the call to this function.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern int GetCurrentProcessorNumber();

        #endregion
    }
}