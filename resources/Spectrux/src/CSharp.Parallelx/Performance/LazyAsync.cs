using System;
using System.Threading.Tasks;

namespace CSharp.Parallelx.Performance
{
    public class LazyAsync<T> : Lazy<Task<T>>
    {
        public LazyAsync(Func<Task<T>> taskFactory) 
            : base(() => Task.Factory.StartNew(taskFactory).Unwrap()) { }
        
        public LazyAsync(Func<T> taskFactory) :
            base(() => Task.Factory.StartNew(taskFactory)) { }
    }
}