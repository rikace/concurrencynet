using System;
using System.Linq;
using System.Threading.Tasks;

namespace DataParallelism.Reduce
{
    using Helpers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;

    // TODO : 2.2
    // implement two parallel Reducer functions
    // requirements
    // 1 - reduce all the items in a collection starting from the first one
    // 3 - reduce all the items in a collection starting from a given initial value
    // Suggestion, look into the LINQ Aggregate
    // You could implement two different functions with different signatures
    // Tip : there are different ways to implement a parallel reducer, even using a Parallel For loop
    public static class ParallelReducer
    {
        // public static TValue Reduce<TValue>

        // parallel Reduce function implementation using Aggregate
        // Example of signature, but something is missing
        // public static TValue Reduce<TValue>(this IEnumerable<TValue> source) =>

        public static TValue Reduce<TValue>(this ParallelQuery<TValue> source, Func<TValue, TValue, TValue> func) =>
            ParallelEnumerable.Aggregate(source, (item1, item2) => func(item1, item2));

        // (2)
        // Implement the reduce function, this is a suggested signature but you can simplified it and/or expanded it
        public static TResult[] Reduce<TKey, TMapped, TResult>(
                this IEnumerable<IGrouping<TKey, TMapped>> source, Func<IGrouping<TKey, TMapped>, TResult> reduce)
            // replace null with the implementation
            => source.AsParallel()
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(reduce).ToArray();

        public static TAccumulate Reduce<TValue, TAccumulate>(this IEnumerable<TValue> source, Func<TAccumulate> seed,
            Func<TAccumulate, TValue, TAccumulate> reduce,
            Func<TAccumulate, TAccumulate, TAccumulate> accumulate) => default;


        public static TAccumulate ReduceOK<TValue, TAccumulate>(this IEnumerable<TValue> source, Func<TAccumulate> seed,
            Func<TAccumulate, TValue, TAccumulate> reduce,
            Func<TAccumulate, TAccumulate, TAccumulate> accumulate)=>
            source.AsParallel()
                .Aggregate(
                    seed: seed(),
                    updateAccumulatorFunc: (local, value) => reduce(local, value),
                    combineAccumulatorsFunc: (overall, local) => accumulate(overall, local),
                    resultSelector: overall => overall);

        public static Func<Func<TSource, TSource, TSource>, TSource> Reduce<TSource>(this IEnumerable<TSource> source)
            => func => source.AsParallel().Aggregate((item1, item2) => func(item1, item2));


        public static TResult[] Reduce<TSource, TKey, TMapped, TResult>(
            this IEnumerable<IGrouping<TKey, TMapped>> source, Func<IGrouping<TKey, TMapped>, TResult> reduce) =>
            source.AsParallel()
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(reduce).ToArray();

        public static TResult ReducePartitioner<TValue, TResult>(this IEnumerable<TValue> source,
            Func<TValue, TResult> selector, Func<TResult, TResult, TResult> reducer,
            CancellationToken? token = null)
        {
            var partitioner = Partitioner.Create(source, EnumerablePartitionerOptions.None);
            var pos = new ParallelOptions
            {
                CancellationToken = token ?? new CancellationToken(),
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var results = ImmutableArray<TResult>.Empty;

            Parallel.ForEach(partitioner,
                pos,
                () => new List<TResult>(),
                (item, loopState, local) =>
                {
                    local.Add(selector(item));
                    return local;
                },
                final =>
                    ImmutableInterlocked.Update(
                        ref results,
                        o => o.AddRange(final))
            );

            return results.AsParallel().Aggregate(reducer);
        }
    }
}
