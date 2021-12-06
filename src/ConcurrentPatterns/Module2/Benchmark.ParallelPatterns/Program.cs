using System;
using BenchmarkDotNet.Running;

namespace Benchmark.ParallelPatterns
{
    class Program
    {
        static void Main(string[] args)
        {
            // dotnet run -c RELEASE --project Benchmarking.csproj

            BenchmarkRunner.Run<ManyJobsBenchmark>();
            BenchmarkRunner.Run<SingleJob>();
        }
    }
}
