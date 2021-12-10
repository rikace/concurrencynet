using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CSharp.Parallelx.EventEx
{

    public interface IEventAggregator : IDisposable
    {
        IObservable<TEvent> GetEvent<TEvent>();
        void Publish<TEvent>(TEvent sampleEvent);
    }

    // based on http://joseoncode.com/2010/04/29/event-aggregator-with-reactive-extensions/
    // and http://machadogj.com/2011/3/yet-another-event-aggregator-using-rx.html
    public class RxEventAggregator : IEventAggregator
    {
        private readonly Subject<object> subject;

        public RxEventAggregator()
        {
            subject = new Subject<object>();
            subject.ObserveOn(TaskPoolScheduler.Default);
            subject.SubscribeOn(TaskPoolScheduler.Default);
        }
        public IObservable<TEvent> GetEvent<TEvent>()
        {
            return subject.OfType<TEvent>().AsObservable();
        }

        public void Publish<TEvent>(TEvent sampleEvent)
        {
            subject.OnNext(sampleEvent);
        }

        bool disposed;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            subject.Dispose();

            disposed = true;
        }
    }
    
    public class RxQueuePubSub
    {
        Subject<object> _jobs = new Subject<object>();
        private IConnectableObservable<object> _connectableObservable;
 
        public RxQueuePubSub()
        {
            _connectableObservable = _jobs.ObserveOn(TaskPoolScheduler.Default).Publish();
            _connectableObservable.Connect();
        }
 
        public void Enqueue<TEvent>(TEvent job) where TEvent : class
        {
            _jobs.OnNext(job);
        }
 
        public void RegisterHandler<T>(Action<T> handleAction) where T : class
        {
            _connectableObservable.OfType<T>().Subscribe(handleAction);
        }
    }
}
/*
 
             var eventPublisher = new EventAggregator();

            // act
            eventPublisher.GetEvent<SampleEvent>()
                .Where(se => se.Status == 1)
                .Subscribe(se => eventWasRaised = true);

            eventPublisher.Publish(new SampleEvent { Status = 1 }); 
 */