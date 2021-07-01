namespace CSharp.Parallelx.Performance
{
    using System;
    using System.Runtime;

    class GCOptimzer : IDisposable
    {
        private readonly GCLatencyMode previous;

        GCOptimzer(GCLatencyMode latencyMode)
        {
            this.previous = GCSettings.LatencyMode;
            GCSettings.LatencyMode = latencyMode;
        }

        public void Dispose()
        {
            GCSettings.LatencyMode = previous;
        }
    }
}