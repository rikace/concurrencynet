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
            return ((double)inCircle / iterations) * 4;
        }

        public static double ParallelForCalculate(int iterations)
        {
            var randomLockObject = new object();
            int inCircle = 0;
            var random = new Helpers.ThreadSafeRandom();

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
            return ((double)inCircle / iterations) * 4;
        }

        public static double ParallelPiMonteCarlo(int iterations)
        {
            int inCircle = 0;
            var random = new Helpers.ThreadSafeRandom();

            // TODO
            // Implement a better "Parallel.For" using these option constructs:
            // - ParallelOptions  (9// )doesn't make sense to use more threads than we have processors)
            // - ThreadLocal
            // UNCOMMENT : Parallel.For(0, iterations,
            //             name of ThreadLocal variable "tLocal

                    //double a, b;
                    //return tLocal += Math.Sqrt((a = random.NextDouble()) * a + (b = random.NextDouble()) * b) <= 1 ? 1 : 0;

            return ((double) inCircle / iterations) * 4;
        }
    }
}
