using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CSharp.Parallelx.ProducerConsumer
{

    public class RxQueue : IJobQueue<Action>
    {
        Subject<Action> _jobs = new Subject<Action>();

        public RxQueue()
        {
            _jobs.ObserveOn(Scheduler.Default)
                .Subscribe(job => { job.Invoke(); });
        }

        public void Enqueue(Action job)
        {
            _jobs.OnNext(job);
        }

        public void Stop()
        {
            _jobs.Dispose();
        }
    }
}