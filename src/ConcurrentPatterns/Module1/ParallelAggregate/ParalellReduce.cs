namespace DataParallelism.Reduce
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
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
            => default; // source.AsParallel() ?

        public static TAccumulate Reduce<TValue, TAccumulate>(this IEnumerable<TValue> source, Func<TAccumulate> seed,
            Func<TAccumulate, TValue, TAccumulate> reduce,
            Func<TAccumulate, TAccumulate, TAccumulate> accumulate) => default;
    }
}
