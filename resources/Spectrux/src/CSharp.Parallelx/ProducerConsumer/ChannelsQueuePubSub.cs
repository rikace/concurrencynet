using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CSharp.Parallelx.ProducerConsumer
{
    public class ChannelsQueuePubSub
    {
        private ChannelWriter<IJob> _writer;
        private Dictionary<Type, Action<IJob>> _handlers = new Dictionary<Type, Action<IJob>>();

        public ChannelsQueuePubSub()
        {
            var channel = Channel.CreateUnbounded<IJob>();
            var reader = channel.Reader;
            _writer = channel.Writer;

            Task.Factory.StartNew(async () =>
            {
                // Wait while channel is not empty and still not completed
                while (await reader.WaitToReadAsync())
                {
                    var job = await reader.ReadAsync();
                    bool handlerExists =
                        _handlers.TryGetValue(job.GetType(), out Action<IJob> value);
                    if (handlerExists)
                    {
                        value.Invoke(job);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void RegisterHandler<T>(Action<T> handleAction) where T : IJob
        {
            Action<IJob> actionWrapper = (job) => handleAction((T) job);
            _handlers.Add(typeof(T), actionWrapper);
        }

        public async Task Enqueue(IJob job)
        {
            await _writer.WriteAsync(job);
        }

        public void Stop()
        {
            _writer.Complete();
        }
    }
}

