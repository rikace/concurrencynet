using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataParallelism.Map
{
    public static class ParallelMap
    {
        // (1)
        // start with Map, follow this method signature
        // The IGrouping is achieved with the keySelector function, this is arbitrary and you can implement the Map function without it
        public static IEnumerable<object> Map<TSource, TKey, TMapped>(this IList<TSource> source,
                Func<TSource, IEnumerable<TMapped>> map, Func<TMapped, TKey> keySelector)
            // replace null with the implementation
            => default;

        public static IEnumerable<IGrouping<TKey, TMapped>> Map2<TSource, TKey, TMapped>(this IList<TSource> source,
                Func<TSource, IEnumerable<TMapped>> map, Func<TMapped, TKey> keySelector)
            // replace null with the implementation
            => default; //source.AsParallel()
    }
}
