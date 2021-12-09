using System.Threading.Tasks;

namespace ConcurrentPatterns
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public static class Memoization
    {
        public static Func<T, R> Memoize<T, R>(Func<T, R> func) where T : IComparable
        {
            Dictionary<T, R> cache = new Dictionary<T, R>();
            return arg =>
            {
                if (cache.ContainsKey(arg))
                    return cache[arg];
                return (cache[arg] = func(arg));
            };
        }

        // TODO LAB
        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior

        // Thread-safe memoization function
        public static Func<T, R> MemoizeThreadSafe<T, R>(Func<T, R> func) where T : IComparable
        {
            ConcurrentDictionary<T, R> cache = new ConcurrentDictionary<T, R>();
            return arg => cache.GetOrAdd(arg, a => func(a));
        }

        public static Func<T, R> MemoizeLazyThreadSafe<T, R>(Func<T, R> func) where T : IComparable
        {
            ConcurrentDictionary<T, Lazy<R>> cache = new ConcurrentDictionary<T, Lazy<R>>();
            return arg => cache.GetOrAdd(arg, a => new Lazy<R>(() => func(a))).Value;
        }

        // TODO LAB
        // Thread-Safe Memoization function with safe lazy evaluation
        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior
        public static Func<T, Task<R>> MemoizeThreadSafe<T, R>(Func<T, Task<R>> func) where T : IComparable
        {
            ConcurrentDictionary<T, Lazy<Task<R>>> cache = new ConcurrentDictionary<T, Lazy<Task<R>>>();
            return arg => cache.GetOrAdd(arg, a => new Lazy<Task<R>>(() => func(a))).Value;
        }
    }
}
