using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelPatterns
{
    public static class PiMontecarlo
    {
        public static double BaseCalculate(int iterations)
        {
            int inCircle = 0;
            var random = new Random();
            for (int i = 0; i < iterations; i++)
            {
                var a = random.NextDouble();
                var b = random.NextDouble();

                var c = Math.Sqrt(a * a + b * b);
                if (c <= 1)
                    inCircle++;
            }

            return ((double) inCircle / iterations) * 4;
        }

        public static double MultiTasksCalculate(int iterations)
        {
            var procCount = Environment.ProcessorCount;

            // Distribute iterations evenly across processors
            var iterPerProc = iterations / procCount;

            // One array slot per processor
            var inCircleLocal = new int[procCount];
            var tasks = new Task[procCount];
            for (var proc = 0; proc < procCount; proc++)
            {
                var procIndex = proc; // Helper for closure
                // Start one task per processor
                tasks[proc] = Task.Run(() =>
                {
                    var inCircleLocalCounter = 0;
                    var random = new Random(procIndex);
                    for (var index = 0; index < iterPerProc; index++)
                    {
                        double a, b;
                        if (Math.Sqrt((a = random.NextDouble()) * a + (b = random.NextDouble()) * b) <= 1)
                            inCircleLocalCounter++;
                    }

                    inCircleLocal[procIndex] = inCircleLocalCounter;
                });
            }

            Task.WaitAll(tasks);

            var inCircle = inCircleLocal.Sum();
            return ((double) inCircle / iterations) * 4;
        }

        public static double ParallelForCalculate(int iterations)
        {
            var randomLockObject = new object();
            int inCircle = 0;
            var random = new Helpers.ThreadSafeRandom();

            // 2
            Parallel.For(0, iterations, i =>
            {
                double a, b;
                lock (randomLockObject)
                {
                    a = random.NextDouble();
                    b = random.NextDouble();
                }

                var c = Math.Sqrt(a * a + b * b);
                if (c <= 1)
                    Interlocked.Increment(ref inCircle);
            });

            return ((double) inCircle / iterations) * 4;
        }

        public static double ParallelPiMonteCarlo(int iterations)
        {
            int inCircle = 0;
            var random = new Helpers.ThreadSafeRandom();

            // Parallel.For/Foreach  + ThreadLocal + CAS

            // TODO LAB
            // Implement a better "Parallel.For" using these option constructs:
            // - ParallelOptions: doesn't make sense to use more threads than we have processors
            // - ThreadLocal

            // NOTE this is the calculation code to use in the parallel loop body
            // double a, b;
            // return tLocal += Math.Sqrt((a = random.NextDouble()) * a + (b = random.NextDouble()) * b) <= 1 ? 1 : 0;

            // UNCOMMENT : Parallel.For(0, iterations,
            //             name of ThreadLocal variable "tLocal


            return ((double) inCircle / iterations) * 4;
        }

        public static double PLINQCalculate(int iterations)
        {
            var random = new Helpers.ThreadSafeRandom();
            var inCircle = ParallelEnumerable.Range(0, iterations)
                // doesn't make sense to use more threads than we have processors
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(_ =>
                {
                    double a, b;
                    return Math.Sqrt((a = random.NextDouble()) * a + (b = random.NextDouble()) * b) <= 1;
                })
                .Aggregate<bool, int, int>(
                    0, // Seed
                    (agg, val) => val ? agg + 1 : agg, // Iterations
                    (agg, subTotal) => agg + subTotal, // Aggregating subtotals
                    result => result); // No projection of result needed

            // Implement the sum of the parallel query using the apposite operator
            // NOTE : check the "Aggregate" as possible solution
            return ((double) inCircle / iterations) * 4;
        }

        // TODO LAB
        public static double PLINQPartitionerCalculate(int iterations)
        {
            var random = new Helpers.ThreadSafeRandom();

                var inCircle = ParallelEnumerable.Range(0, iterations)
                // doesn't make sense to use more threads than we have processors
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(_ =>
                {
                    double a, b;
                    return Math.Sqrt((a = random.NextDouble()) * a + (b = random.NextDouble()) * b) <= 1;
                })
                .Sum(_ => 1);  // REMOVE THIS LINE TO COMPLETE THE TASK

            // TODO LAB
            // Use the previous implementation from "PLINQCalculate", in this case
            // apply a "Partitioner" to improve the performance

            return ((double) inCircle / iterations) * 4;
        }
    }
}
