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
            // TODO LAB
            // Implement logic that runs the "projection" for
            // each item in the "collection" with degree of parallelism "degreeOfParallelism"
            // NOTE the "queue" (ConcurrentQueue) could help, but it is not required
            var queue = new ConcurrentQueue<T>(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism)
                .Select(async _ =>
                {
                    T item;
                    while (queue.TryDequeue(out item))
                    {
                        await projection(item);
                    }
                });

            await Task.WhenAll(tasks);
        }

        public static async Task<TR[]> ExecuteInParallel<T, TR>(this IEnumerable<T> collection,
            Func<T, Task<TR>> projection,
            int degreeOfParallelism)
        {
            // TODO LAB
            // Implement logic that runs the "projection" for
            // each item in the "collection" with degree of parallelism "degreeOfParallelism"
            // Similar to previous implementation but this time the projection run async (Func<T, Task<R>>)
            // NOTE the "queue" (ConcurrentQueue) could help
            //      - For example, the result of each iteration could be saved into local "results" queue,
            //        which is then returned as IEnumerable, and/or aggregated
            var queue = new ConcurrentQueue<T>(collection);
            IEnumerable<Task<List<TR>>> tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                List<TR> localResults = new List<TR>();
                T item;
                while (queue.TryDequeue(out item))
                {
                    var result = await projection(item);
                    localResults.Add(result);
                }

                return localResults;
            });

            var results = await Task.WhenAll(tasks.ToList());
            return results.SelectMany(i => i).ToArray();
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            return Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(dop)
                select Task.Run(async delegate
                {
                    using (partition)
                        while (partition.MoveNext())
                            await body(partition.Current);
                }));
        }

        public static Task ForEachAsyncConcurrent<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            var partitions = Partitioner.Create(source).GetPartitions(dop);
            var tasks = partitions.Select(async partition =>
            {
                using (partition)
                    while (partition.MoveNext())
                        await body(partition.Current);
            });

            return Task.WhenAll(tasks);
        }

        public static async Task ExecuteInParallelWithDegreeOfParallelism<T>(this IEnumerable<T> collection,
            Func<T, Task> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                T item;
                while (queue.TryDequeue(out item))
                    await processor(item);
            });

            await Task.WhenAll(tasks);
        }

        private static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
                @this.Add(element);
        }

        public static async Task<IEnumerable<R>> ProjectInParallelWithDegreeOfParallelism<T, R>(
            this IEnumerable<T> collection,
            Func<T, Task<R>> processor,
            int degreeOfParallelism)
        {
            var results = new ConcurrentBag<R>();

            Task[] tasks = null;

            // TODO LAB
            // Implement logic that runs the "processor" for
            // each item "collection" with degree of parallelism "degreeOfParallelism"
            // The result of each iteration is saved into local "results" queue,
            // which is return as IEnumerable

            await Task.WhenAll(tasks);
            return results;
        }

        public static async Task<IEnumerable<R>> ProjectInParallelWithDegreeOfParallelism_Solution<T, R>(
            this IEnumerable<T> collection,
            Func<T, Task<R>> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var results = new ConcurrentBag<R>();
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                List<R> localResults = new List<R>();
                T item;
                while (queue.TryDequeue(out item))
                {
                    var result = await processor(item);
                    localResults.Add(result);
                }

                results.AddRange(localResults);
            });

            await Task.WhenAll(tasks);
            return results;
        }
    }
}
