using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using ProducerConsumer;

namespace Benchmark.ParallelPatterns
{
    [SimpleJob(baseline: true)]
    [RPlotExporter, RankColumn]
    public class ManyJobsBenchmark
    {
        private AutoResetEvent _autoResetEvent;

        public ManyJobsBenchmark()
        {
            _autoResetEvent = new AutoResetEvent(false);
        }

        [Benchmark]
        public void BlockingCollectionQueue()
        {
            var jobQueue = new BlockingCollectionQueue<Action>(a => a.Invoke());
            int jobs = 100000;
            for (int i = 0; i < jobs - 1; i++)
            {
                jobQueue.Enqueue(() => { });
            }

            jobQueue.Enqueue(() => _autoResetEvent.Set());
            _autoResetEvent.WaitOne();
            jobQueue.Stop();
        }


        [Benchmark]
        public void RxQueue()
        {
            var jobQueue = new RxQueue<Action>(a => a.Invoke());
            int jobs = 100000;
            for (int i = 0; i < jobs - 1; i++)
            {
                jobQueue.Enqueue(() => { });
            }

            jobQueue.Enqueue(() => _autoResetEvent.Set());
            _autoResetEvent.WaitOne();
            jobQueue.Stop();
        }

        [Benchmark]
        public void ChannelsQueue()
        {
            var jobQueue = new ChannelsQueue<Action>(a => a.Invoke());
            int jobs = 100000;
            for (int i = 0; i < jobs - 1; i++)
            {
                jobQueue.Enqueue(() => { });
            }

            jobQueue.Enqueue(() => _autoResetEvent.Set());
            _autoResetEvent.WaitOne();
            jobQueue.Stop();
        }

        [Benchmark]
        public void TPLDataflowQueue()
        {
            var jobQueue = new TPLDataflowQueue<Action>(a => a.Invoke());
            int jobs = 100000;
            for (int i = 0; i < jobs - 1; i++)
            {
                jobQueue.Enqueue(() => { });
            }

            jobQueue.Enqueue(() => _autoResetEvent.Set());
            _autoResetEvent.WaitOne();
            jobQueue.Stop();
        }
    }
}
