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

        // TODO RT
        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior

        // Thread-safe memoization function
        public static Func<T, R> MemoizeThreadSafe<T, R>(Func<T, R> func) where T : IComparable
        {
            return default;
        }

        // TODO RT
        // Thread-Safe Memoization function with safe lazy evaluation
        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior
        public static Func<T, Task<R>> MemoizeThreadSafe<T, R>(Func<T, Task<R>> func) where T : IComparable
        {
            return default;
        }
    }
}
