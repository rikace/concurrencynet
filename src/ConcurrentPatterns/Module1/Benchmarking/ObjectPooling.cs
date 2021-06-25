using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BenchmarkParallelPatterns
{
    public class ObjectPooling
    {
       private StringBuilder cachedBuilder = new StringBuilder();

        public StringBuilder GetStringBuilderInterlocked()
        {
            // Try to borrow the cached builder, in a thread-safe way.
            // (If another thread is using the builder, this will return null.)
            StringBuilder builder = Interlocked.Exchange(ref cachedBuilder, null);
            if (builder == null)
                builder = new StringBuilder();
            else
                builder.Length = 0;

            // Whether we managed to borrow the cached builder or not,
            // we can replace it with the one we've used, now that we
            // know what to return.
            Volatile.Write(ref cachedBuilder, builder);
            return builder;
        }


        private bool pooledBuilderInUse = false;

        public StringBuilder GetStringBuilderLock()
        {
            StringBuilder builder = null;
            lock (cachedBuilder)
            {
                if (!pooledBuilderInUse)
                {
                    pooledBuilderInUse = true;
                    builder = cachedBuilder;
                    builder.Length = 0;
                }
            }

            try
            {
                builder = builder ?? new StringBuilder();
                // This will call all the actions in the multicast delegate.
                return builder;
            }
            finally
            {
                if (builder == cachedBuilder)
                    lock (cachedBuilder)
                        pooledBuilderInUse = false;
            }
        }

        [ThreadStatic] private static StringBuilder cachedBuilderStatic;

        public StringBuilder GetStringBuilderLockThreadStatic()
        {
            StringBuilder builder = cachedBuilder;
            if (cachedBuilder == null)
            {
                builder = new StringBuilder();
                cachedBuilder = builder;
            }
            else
                builder.Length = 0;

            return builder;
        }


        private ThreadLocal<StringBuilder> cachedBuilderThreadLocal =
            new ThreadLocal<StringBuilder>(() => new StringBuilder());

        public StringBuilder GetStringBuilderLockThreadLocal()
        {
            StringBuilder builder = cachedBuilderThreadLocal.Value;
            builder.Length = 0;
            return builder;
        }
    }
}
