using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CSharp.Parallelx.Threads
{
    public class AffineThreadWindows : AffineThread
    {
        #region Constructors

        internal AffineThreadWindows()
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
            IntPtr hThread = GetCurrentThread();
            IntPtr maskPtr = new IntPtr((long)processorMask);

            IntPtr result = SetThreadAffinityMask(hThread, maskPtr);
            if (result == IntPtr.Zero)
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
            return GetCurrentProcessorNumber();
        }

        #endregion

        #region Windows P/Invoke Methods

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

        /// <summary>
        /// Retrieves the number of the processor the current thread was running on during the call to this function.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern int GetCurrentProcessorNumber();

        #endregion
    }
}