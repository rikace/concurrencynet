using System;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;

namespace CSharp.Parallelx.ParallelSIMD
{
    public static class ParallelSIMD
    {
        public static async Task ExecuteInParallel<T>(this IEnumerable<T> collection,
            Func<T, Task> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                T item;
                while (queue.TryDequeue(out item))
                {
                    await processor(item);
                }
            });

            await Task.WhenAll(tasks);
        }
        
        public static async Task<R[]> ExecuteInParallel<T, R>(this IEnumerable<T> collection,
            Func<T, Task<R>> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            { 
                List<R> localResults = new List<R>();
                T item;
                while (queue.TryDequeue(out item))
                {
                   var result = await processor(item);
                    localResults.Add(result);
                }

                return localResults;
            });

            var results = await Task.WhenAll(tasks.ToList());
            return results.SelectMany(i => i).ToArray();
        }
    }
}