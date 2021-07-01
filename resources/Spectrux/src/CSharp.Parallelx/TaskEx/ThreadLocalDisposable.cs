using System.Xml;

namespace CSharp.Parallelx.ParallelSIMD
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    public sealed class ThreadLocalDisposable<T> : IDisposable where T : IDisposable
    {
        private readonly ConcurrentBag<T> _values;
        private readonly ThreadLocal<T> _threadLocal;

        public ThreadLocalDisposable(Func<T> valueFactory)
        {
            _values = new ConcurrentBag<T>();
            _threadLocal = new ThreadLocal<T>(() =>
            {
                var value = valueFactory();
                _values.Add(value);
                return value;
            });
        }

        public bool IsValueCreated => _threadLocal.IsValueCreated;

        public T Value => _threadLocal.Value;

        public override string ToString()
        {
            return _threadLocal.ToString();
        }

        public void Dispose()
        {
             _threadLocal.Dispose();
            Array.ForEach(_values.ToArray(), v => v.Dispose());
        }
    }
}