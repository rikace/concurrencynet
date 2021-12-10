using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CSharp.Parallelx.ProducerConsumer
{

    public class MultiThreadQueue
    {
        BlockingCollection<string> _jobs = new BlockingCollection<string>();

        public MultiThreadQueue(int numThreads)
        {
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(OnHandlerStart)
                    {IsBackground = true}; //Mark 'false' if you want to prevent program exit until jobs finish
                thread.Start();
            }
        }

        public void Enqueue(string job)
        {
            if (!_jobs.IsAddingCompleted)
            {
                _jobs.Add(job);
            }
        }

        public void Stop()
        {
            //This will cause '_jobs.GetConsumingEnumerable' to stop blocking and exit when it's empty
            _jobs.CompleteAdding();
        }

        private void OnHandlerStart()
        {
            foreach (var job in _jobs.GetConsumingEnumerable(CancellationToken.None))
            {
                Console.WriteLine(job);
                Thread.Sleep(10);
            }
        }
    }
}