using System;
using BenchmarkDotNet.Running;

namespace Benchmark.ParallelPatterns
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ManyJobsBenchmark>();
            BenchmarkRunner.Run<SingleJob>();
        }
    }
}
