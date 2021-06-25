using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace ProducerConsumer
{
    public class TPLDataflowQueue<T>
    {
        private ActionBlock<T> _jobs;
        private Action<T> _action;

        public TPLDataflowQueue(Action<T> action)
        {
            var executionDataflowBlockOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 1,
            };

            _action = action;
            _jobs = new ActionBlock<T>((job) =>
            {
                Console.WriteLine($"job:{job}, thread: {Thread.CurrentThread.ManagedThreadId}" );
                _action.Invoke(job);
            }, executionDataflowBlockOptions);
        }

        public void Enqueue(T job) =>
            _jobs.Post(job);

        public void Stop() =>
            _jobs.Complete();
    }
}
