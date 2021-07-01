using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using CSharp.Parallelx.ProducerConsumer;

namespace CSharp.Parallelx.EventEx
{
    public class ChannelsQueuePubSub
    {
        private ChannelReader<object> _reader;
        private ChannelWriter<object> _writer;
        private Dictionary<Type, List<Action<object>>> _handlers = new Dictionary<Type, List<Action<object>>>();
 
        public ChannelsQueuePubSub()
        {
            var channel = Channel.CreateUnbounded<object>();
            _reader = channel.Reader;
            _writer = channel.Writer;
 
            Task.Factory.StartNew(async () =>
            {
                // Wait while channel is not empty and still not completed
                while (await _reader.WaitToReadAsync())
                {
                    var job = await _reader.ReadAsync();
                    bool handlerExists = 
                        _handlers.TryGetValue(job.GetType(), out List<Action<object>> actions);
                    if (handlerExists)
                    {
                        foreach (var action in actions)
                            action.Invoke(job);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }
 
        public void RegisterHandler<T>(Action<T> handleAction) where T : class
        {
            Action<object> actionWrapper = (job) => handleAction((T)job);
            if (_handlers.ContainsKey(typeof(T)))
                _handlers[typeof(T)].Add(actionWrapper);
            else
                _handlers[typeof(T)] = new List<Action<object>> {actionWrapper};
        }
 
        public async Task Enqueue<T>(T job) where T : class
        {
            await _writer.WriteAsync(job);
        }
 
        public void Stop()
        {
            _writer.Complete();
        }
    }
}