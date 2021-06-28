﻿using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ParallelPatterns;

namespace BenchmarkParallelPatterns
{
    [SimpleJob(baseline: true)] //, CoreJob, CoreRtJob]
    [RPlotExporter, RankColumn]
    public class BenchmarkMonteCarlo
    {
        [Params(1000, 10000, 20000000)] public int N;

        private int iterations;

        [GlobalSetup]
        public void Setup()
        {
            iterations = N;
        }

        [Benchmark]
        public double BasicMonteCarlo() => PiMontecarlo.BaseCalculate(iterations);

        [Benchmark]
        public double MultiTasksMonteCarlo() => PiMontecarlo.MultiTasksCalculate(iterations);

        [Benchmark]
        public double ParallelForMonteCarlo() => PiMontecarlo.ParallelForCalculate(iterations);

         [Benchmark]
         public double ParallelPiMonteCarlo() => PiMontecarlo.ParallelPiMonteCarlo(iterations);
    }


    [SimpleJob(baseline: true)]
    [RPlotExporter, RankColumn]
    public class BenchmarkQuickSort
    {
        [Params(1000, 10000, 1000000)] public int N;

        private int[] iterations;

        [GlobalSetup]
        public void Setup()
        {
            Random rand = new Random((int) DateTime.Now.Ticks);
            var A = new int[N];
            for (int i = 0; i < N; ++i)
                A[i] = rand.Next();
            iterations = A;
        }

        [Benchmark]
        public void Sequential() => QuickSort.QuickSort_Sequential(iterations);

        [Benchmark]
        public void Parallel() => QuickSort.QuickSort_Parallel(iterations);

        [Benchmark]
        public void ParallelDepth() => QuickSort.QuickSort_Parallel_Threshold(iterations);
    }


    class Program
    {
        static void Main(string[] args)
        {
            // to run from command line:
            // dotnet run -c RELEASE --project Benchmarking.csproj

            //BenchmarkRunner.Run<BenchmarkQuickSort>();

            // dotnet run -c RELEASE --project Benchmarking.csproj
            BenchmarkRunner.Run<BenchmarkMonteCarlo>();

            // BenchmarkRunner.Run<StringsWithSpan>();
        }
    }
}
