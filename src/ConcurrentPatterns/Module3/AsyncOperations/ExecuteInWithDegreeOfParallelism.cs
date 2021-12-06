using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncOperations
{
    // TODO LAB
    // ParallelSIMD
    public static class ExecuteInWithDegreeOfParallelism
    {
        public static async Task ExecuteInParallel<T>(this IEnumerable<T> collection,
            Func<T, Task> projection,
            int degreeOfParallelism)
        {
            // TODO
            // Implement logic that runs the "projection" for
            // each item in the "collection" with degree of parallelism "degreeOfParallelism"
            // NOTE the "queue" (ConcurrentQueue) could help, but it is not required
            var queue = new ConcurrentQueue<T>(collection);

            Task[] tasks = null;

            await Task.WhenAll(tasks);
        }

        public static async Task<TR[]> ExecuteInParallel<T, TR>(this IEnumerable<T> collection,
            Func<T, Task<TR>> projection,
            int degreeOfParallelism)
        {
            // TODO
            // Implement logic that runs the "projection" for
            // each item in the "collection" with degree of parallelism "degreeOfParallelism"
            // Similar to previous implementation but this time the projection run async (Func<T, Task<R>>)
            // NOTE the "queue" (ConcurrentQueue) could help
            //      - For example, the result of each iteration could be saved into local "results" queue,
            //        which is then returned as IEnumerable, and/or aggregated
            var queue = new ConcurrentQueue<T>(collection);

            IEnumerable<Task<List<TR>>> tasks = null;

            var results = await Task.WhenAll(tasks.ToList());
            return results.SelectMany(i => i).ToArray();
        }

        public static Task ThrottleTasks<T>(this IEnumerable<T> source, int maxDegreeOfParallelism, Func<T, Task> body)
        {
            return Task.WhenAll(
                // TODO LAB
                // implement code that controls the degree of parallelism by throttling the number of
                // active Tasks.
                // you can use the RequestGate class, or partition the tasks in running groups, or ??

            );
        }
    }
}
