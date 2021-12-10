using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CSharp.Parallelx.Performance
{
    public static  class AsyncMemoize
    {
        public static Func<T, R> Cache<T, R>(Func<T, R> func)
        {
            var table = new ConcurrentDictionary<T, R>();
            return key => table.GetOrAdd(key, func);
        }

        public static Func<T, Task<R>> CacheTask<T, R>(Func<T, Task<R>> func)
        {
            var table = new ConcurrentDictionary<T, LazyAsync<R>>();
            return key => table.GetOrAdd(key,
                k => new LazyAsync<R>(() => func(k))).Value;
        }
        
    }
}  