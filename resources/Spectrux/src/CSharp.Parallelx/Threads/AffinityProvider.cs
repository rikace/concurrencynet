using System.Threading;

namespace CSharp.Parallelx.Threads
{
    /// <summary>
    /// Provides affinity support to existent threads
    /// (rather than spawning dedicated threads internally).
    /// </summary>
    /// <remarks>
    /// This class is a quick-and-dirty construct that latches on the functionality provided
    /// by the <see cref="AffineThread"/> family, but makes it available for the main thread.
    /// </remarks>
    public static class AffinityProvider
    {
        #region Methods

        public static void SetAffinity(int targetProcessorID)
        {
            Thread.BeginThreadAffinity();

            AffineThread.VerifyProcessorID(targetProcessorID);
            ulong processorMask = AffineThread.CalculateProcessorMask(targetProcessorID);
            int currentProcessorID;

            if (AffineThread.IsRunningOnWindows())
            {
                AffineThreadWindows.SetAffinityInner(processorMask);
                currentProcessorID = AffineThreadWindows.GetCurrentProcessorID();
            }
            else
            {
                AffineThreadLinux.SetAffinityInner(processorMask);
                currentProcessorID = AffineThreadLinux.GetCurrentProcessorID();
            }

            AffineThread.VerifyAffinity(currentProcessorID, processorMask);
        }

        #endregion
    }
}