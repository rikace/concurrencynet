using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp.Parallelx
{
    public static class ParallelReducer
    {
        // var arr = Enumerable.Range(0, 1000).ToList();
        // var res = Reduce(arr, n => n, (a, b) => a + b);
        
        public static R Reduce<T, R>(this IEnumerable<T> data, Func<T, R> selector, Func<R, R, R> reducer, CancellationToken token = new CancellationToken())  
         where R : class 
        {
            var partitioner = Partitioner.Create(data, EnumerablePartitionerOptions.NoBuffering);
            var results = ImmutableArray<R>.Empty;
	
            Parallel.ForEach(partitioner,
                new ParallelOptions { TaskScheduler = TaskScheduler.Default, CancellationToken = token, MaxDegreeOfParallelism = Environment.ProcessorCount },
                () => new List<R>(),
                (item, loopState, local) =>
                {
                    local.Add(selector(item));
                    return local;
                },
                final =>  ImmutableInterlocked.InterlockedExchange(ref results,  results.AddRange(final))
            );
            return results.AsParallel().Aggregate(reducer);
        }
    }
}