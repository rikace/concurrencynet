using System;
using System.Collections.Generic;
using System.Threading;

namespace CSharp.Parallelx.ProducerConsumer
{
    public class NoDedicatedThreadQueue : IJobQueue<Action>
    {
        private Queue<Action> _jobs = new Queue<Action>();
        private bool _delegateQueuedOrRunning = false;
 
        public void Enqueue(Action job)
        {
            lock (_jobs)
            {
                _jobs.Enqueue(job);
                if (!_delegateQueuedOrRunning)
                {
                    _delegateQueuedOrRunning = true;
                    ThreadPool.UnsafeQueueUserWorkItem(ProcessQueuedItems, null);
                }
            }
        }
 
        private void ProcessQueuedItems(object ignored)
        {
            while (true)
            {
                Action job;
                lock (_jobs)
                {
                    if (_jobs.Count == 0)
                    {
                        _delegateQueuedOrRunning = false;
                        break;
                    }
 
                    job = _jobs.Dequeue();
                }
 
                try
                {
                    job.Invoke();
                }
                catch
                {
                    ThreadPool.UnsafeQueueUserWorkItem(ProcessQueuedItems, null);
                    throw;
                }
            }
        }
        public void Stop()
        {
        }
    }
}