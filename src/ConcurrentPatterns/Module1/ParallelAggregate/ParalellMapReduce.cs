namespace DataParallelism.MapReduce
{
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using DataParallelism.Reduce;

    // TODO : Implement a Map-Reduce Function (as extension method - reusable)
    public static class ParallelMapReduce
    {
        // TODO LAB
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
            => default; // source.AsParallel()

        public static TResult[] MapReduce<TSource, TMapped, TKey, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>, IEnumerable<TResult>> reduce,
            int M, int R)
            => default;

        public static ParallelQuery<TResult> MapReduce<TSource, TMapped, TKey, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>,
                IEnumerable<TResult>> reduce)
            => default;
    }
}
