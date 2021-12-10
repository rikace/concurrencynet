using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CSharp.Parallelx.ProducerConsumer
{
    public class TPLDataflowSubscribers
    {
        private BroadcastBlock<IJob> _jobs;

        public TPLDataflowSubscribers()
        {
            _jobs = new BroadcastBlock<IJob>(job => job);
        }

        public void RegisterHandler<T>(Action<T> handleAction) where T : IJob
        {
            // We have to have a wrapper to work with IJob instead of T
            Action<IJob> actionWrapper = (job) => handleAction((T) job);

            // create the action block that executes the handler wrapper
            var actionBlock = new ActionBlock<IJob>((job) => actionWrapper(job));

            // Link with Predicate - only if a job is of type T
            _jobs.LinkTo(actionBlock, predicate: (job) => job is T);
        }

        public async Task Enqueue(IJob job)
        {
            await _jobs.SendAsync(job);
        }
    }
}