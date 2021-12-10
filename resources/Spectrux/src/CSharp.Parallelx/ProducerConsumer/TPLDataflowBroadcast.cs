using System;
using System.Threading.Tasks.Dataflow;

namespace CSharp.Parallelx.ProducerConsumer
{
    public class TPLDataflowBroadcast
    {
        private BroadcastBlock<string> _jobs;

        public TPLDataflowBroadcast()
        {
            // The delegate 'job=>job' allows to transform the job, like Select in LINQ
            _jobs = new BroadcastBlock<string>(job => job);

            var act1 = new ActionBlock<string>((job) => { Console.WriteLine(job); });
            var act2 = new ActionBlock<string>((job) => { LogToFile(job); });
            _jobs.LinkTo(act1);
            _jobs.LinkTo(act2);
        }

        private void LogToFile(string job)
        {
            //...
        }

        public void Enqueue(string job)
        {
            _jobs.Post(job);
        }
    }
}
