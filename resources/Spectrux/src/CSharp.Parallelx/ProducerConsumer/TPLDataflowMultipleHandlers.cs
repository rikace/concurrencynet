using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace CSharp.Parallelx.ProducerConsumer
{

    public class TPLDataflowMultipleHandlers
    {
        private ActionBlock<string> _jobs;

        public TPLDataflowMultipleHandlers()
        {
            var executionDataflowBlockOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 2,
            };

            _jobs = new ActionBlock<string>((job) =>
            {
                Thread.Sleep(10);
                // following is just for example's sake
                Console.WriteLine($"job:{job}, {Thread.CurrentThread.ManagedThreadId}");
            }, executionDataflowBlockOptions);
        }

        public void Enqueue(string job)
        {
            _jobs.Post(job);
        }
    }
}