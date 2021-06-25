using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ProducerConsumer
{
    public class BlockingCollectionQueue<T>
    {
        private BlockingCollection<T> _jobs = new BlockingCollection<T>();
        private Action<T> _action;

        public BlockingCollectionQueue(Action<T> action)
        {
            _action = action;
            var thread = new Thread(new ThreadStart(OnStart));
            thread.IsBackground = true;
            thread.Start();
        }

        public void Enqueue(T job) =>
            _jobs.Add(job);

        private void OnStart()
        {
            foreach (var job in _jobs.GetConsumingEnumerable(CancellationToken.None))
                _action(job);
        }

        public void Stop() =>
            _jobs.CompleteAdding();
    }
}
