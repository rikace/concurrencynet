using System;

namespace CSharp.Parallelx
{
    /// <summary>
    /// Provides a cache for frequently-accessed information from the <see cref="Environment"/> class.
    /// </summary>
    public static class EnvironmentCached
    {
        #region Fields

        private static readonly Lazy<int> processorCount =
            new Lazy<int>(() => Environment.ProcessorCount);

        private static readonly Lazy<OperatingSystem> osVersion =
            new Lazy<OperatingSystem>(() => Environment.OSVersion);

        private static readonly Lazy<bool> isRunningOnMono =
            new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of processors on the current machine.
        /// </summary>
        public static int ProcessorCount
        {
            get { return processorCount.Value; }
        }

        /// <summary>
        /// Gets an <see cref="OperatingSystem"/> object that contains the current platform identifier and version number.
        /// </summary>
        public static OperatingSystem OSVersion
        {
            get { return osVersion.Value; }
        }
                
        /// <summary>
        /// Returns <see langword="true"/> if the current process is running on Mono.
        /// </summary>
        /// <remarks>
        /// http://www.mono-project.com/Guide:_Porting_Winforms_Applications#Runtime_Conditionals
        /// </remarks>
        public static bool IsRunningOnMono
        {
            get { return isRunningOnMono.Value; }
        }

        #endregion
    }
}
