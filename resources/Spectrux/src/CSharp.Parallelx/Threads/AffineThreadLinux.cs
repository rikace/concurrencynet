using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CSharp.Parallelx.Threads
{
    internal class AffineThreadLinux : AffineThread
    {
        #region Constructors

        internal AffineThreadLinux()
            : base()
        { }

        #endregion
        
        #region Affinity Methods

        protected override void SetAffinity()
        {
            SetAffinityInner(this.ProcessorAffinity.Value);
        }

        protected override void ResetAffinity()
        {
            ulong fullMask = CalculateFullProcessorMask();
            SetAffinityInner(fullMask);
        }

        internal static void SetAffinityInner(ulong processorMask)
        {
            SetAffinityInner(0, processorMask);
        }

        internal static void SetAffinityInner(int processID, ulong processorMask)
        {
            int result = sched_setaffinity(processID, new IntPtr(sizeof(ulong)), ref processorMask);
            if (result != 0)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new ApplicationException("Could not set affinity: " + processorMask + "; error code: " + errorCode);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the operating system process thread corresponding to the managed thread.
        /// </summary>
        protected override ProcessThread GetProcessThread()
        {
            throw new NotSupportedException("Not supported in Mono.");
        }
        
        /// <summary>
        /// Gets the processor ID that the current thread is running on.
        /// </summary>
        public override int GetProcessorID()
        {
            return GetCurrentProcessorID();
        }

        public static int GetCurrentProcessorID()
        {
            return sched_getcpu();
        }

        private static int[] GetLinuxThreadIDs()
        {
            // http://stackoverflow.com/a/14218991/1149773
            Process p = Process.GetCurrentProcess();
            int pid = p.Id;
            DirectoryInfo taskDir = new DirectoryInfo(String.Format("/proc/{0}/task", pid));
            DirectoryInfo[] threadDirs = taskDir.GetDirectories();
            int[] threadIDs = threadDirs.SelectArray(td => int.Parse(td.Name));
            return threadIDs;
        }

        #endregion
        
        #region Linux P/Invoke Methods
                
        /// <summary>
        /// Sets a process's CPU affinity mask.
        /// </summary>
        [DllImport("libc.so.6", SetLastError=true)]
        private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref ulong cpuset);
                
        [DllImport("libc.so.6", SetLastError=true)]
        private static extern int sched_getcpu();

        // http://stackoverflow.com/q/3586023/1149773
        [DllImport("libc.so.6")]
        private static extern int getpid();

        #endregion
    }
}