using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharp.Parallelx
{
    public static class UtilityExtensions
    {
        /// <summary>Forces value creation of a Lazy instance.</summary>
        /// <typeparam name="T">Specifies the type of the value being lazily initialized.</typeparam>
        /// <param name="lazy">The Lazy instance.</param>
        /// <returns>The initialized Lazy instance.</returns>
        public static Lazy<T> Force<T>(this Lazy<T> lazy)
        {
#pragma warning disable 0219
            var ignored = lazy.Value;
#pragma warning restore 0219

            return lazy;
        }
        
        public static TResult[] SelectArray<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {            
            IList<TSource> list = source as IList<TSource>;
            if (list != null)
            {
                TResult[] result = new TResult[list.Count];

                for (int i = 0; i < result.Length; ++i)
                    result[i] = selector(list[i]);

                return result;
            }

            ICollection<TSource> collection = source as ICollection<TSource>;
            if (collection != null)
            {
                TResult[] result = new TResult[collection.Count];

                int i = 0;
                foreach (TSource element in source)
                    result[i++] = selector(element);

                return result;
            }

            return source.Select(selector).ToArray();
        }
        
        public static void DisposeAll(this IEnumerable<IDisposable> disposables)
        {
            foreach (IDisposable disposable in disposables)
                disposable.Dispose();
        }
    }
}
