using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataParallelism.Reduce;

namespace DataParallelism.MapReduce
{
    using Helpers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;

    // TODO LAB : Implement a Map-Reduce Function (as extension method - reusable)
    public static class ParallelMapReduce
    {
        // Compose the pre-implemented Map and Reduce function
        // After the implementation of the Map-Reduce
        // - How can you control/manage the degree of parallelism ?
        // - Improve the performance with a Partitioner
        // Suggestion, for performance improvement look into the "WithExecutionMode"
        public static TResult[] MapReduce<TSource, TMapped, TKey, TResult>(
            this IList<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>, TResult> reduce)
            => source.AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .SelectMany(map)
                .GroupBy(keySelector)
                .Reduce(reduce)
                .ToArray();

        // public static TResult[] MapReduce<TSource, TMapped, TKey, TResult>(
        //     this IEnumerable<TSource> source,
        //     Func<TSource, IEnumerable<TMapped>> map,
        //     Func<TMapped, TKey> keySelector,
        //     Func<IGrouping<TKey, TMapped>, IEnumerable<TResult>> reduce,
        //     int M, int R)
        //     => source.AsParallel()
        //         .WithDegreeOfParallelism(M)
        //         .SelectMany(map)
        //         .GroupBy(keySelector)
        //         .ToList().AsParallel()
        //         .WithDegreeOfParallelism(R)
        //         .SelectMany(reduce)
        //         .ToArray();
        //
        public static ParallelQuery<TResult> MapReduce<TSource, TMapped, TKey, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>,
                IEnumerable<TResult>> reduce)
            => source.AsParallel().SelectMany(map).GroupBy(keySelector).SelectMany(reduce);

        public static TResult[] MapReduce<TSource, TMapped, TKey, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>, IEnumerable<TResult>> reduce,
            int M, int R)
            => default;

        // public static ParallelQuery<TResult> MapReduce<TSource, TMapped, TKey, TResult>(
        //     this IEnumerable<TSource> source,
        //     Func<TSource, IEnumerable<TMapped>> map,
        //     Func<TMapped, TKey> keySelector,
        //     Func<IGrouping<TKey, TMapped>,
        //         IEnumerable<TResult>> reduce)
        //     => default;
    }
}
