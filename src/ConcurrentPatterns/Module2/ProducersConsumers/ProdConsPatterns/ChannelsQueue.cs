using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProducerConsumer
{
    public class ChannelsQueue<T>
    {
        private ChannelWriter<T> _writer;
        private Action<T> _action;

        public ChannelsQueue(Action<T> action)
        {
            _action = action;
            var channel = Channel.CreateUnbounded<T>();
            var reader = channel.Reader;
            _writer = channel.Writer;

            Task.Factory.StartNew(async () =>
            {
                // Wait while channel is not empty and still not completed
                while (await reader.WaitToReadAsync())
                {
                    var job = await reader.ReadAsync();
                    _action.Invoke(job);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public async Task Enqueue(T job) => await _writer.WriteAsync(job);

        public void Stop() => _writer.Complete();
    }
}
