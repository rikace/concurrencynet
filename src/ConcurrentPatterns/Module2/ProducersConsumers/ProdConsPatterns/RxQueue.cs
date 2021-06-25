using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ProducerConsumer
{
    public class RxQueue<T>
    {
        Subject<T> _jobs = new Subject<T>();
        private Action<T> _action;

        public RxQueue(Action<T> action)
        {
            _action = action;
            _jobs.ObserveOn(Scheduler.Default)
                .Subscribe(job => _action(job));
        }

        public void Enqueue(T job) =>
            _jobs.OnNext(job);

        public void Stop() =>
            _jobs.Dispose();
    }
}
