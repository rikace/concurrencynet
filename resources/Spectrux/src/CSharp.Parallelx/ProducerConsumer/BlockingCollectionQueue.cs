using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace CSharp.Parallelx.ProducerConsumer
{
    interface IJobQueue<T> where T : class
    {
    }

    public interface IJob
    {
    }

    public class BlockingCollectionQueue : IJobQueue<Action>
    {
        private BlockingCollection<Action> _jobs = new BlockingCollection<Action>();

        public BlockingCollectionQueue()
        {
            var thread = new Thread(new ThreadStart(OnStart));
            thread.IsBackground = true;
            thread.Start();
        }

        public void Enqueue(Action job)
        {
            _jobs.Add(job);
        }

        private void OnStart()
        {
            foreach (var job in _jobs.GetConsumingEnumerable(CancellationToken.None))
            {
                job.Invoke();
            }
        }

        public void Stop()
        {
            _jobs.CompleteAdding();
        }
    }
}