using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CSharp.Parallelx.Channels
{
    public class ChannelQueue
    {
        public async Task Run()
        {
            var unboundedChannelOptions = new UnboundedChannelOptions()
            {
                SingleWriter = true
            };
            var channel = Channel.CreateUnbounded<int>(unboundedChannelOptions);
            var writer = channel.Writer;

            await Task.WhenAll(Task.Run(() => Producer(writer, 100)),
                Task.Run(() => Consumer(channel.Reader, "R1")),
                Task.Run(() => Consumer(channel.Reader, "R2")));
        }

        private async Task Consumer(ChannelReader<int> reader, string name)
        {
            while (await reader.WaitToReadAsync())
            {
                var value = await reader.ReadAsync();
                Console.WriteLine($"{name} - Thread id#{Thread.CurrentThread.ManagedThreadId} - value {value}");
            }
        }

        private async Task Producer(ChannelWriter<int> writer, int count)
        {
            for (int i = 0; i < count; i++)
            {
                await writer.WaitToWriteAsync();
                await writer.WriteAsync(i).ConfigureAwait(false);
            }

            writer.Complete();
        }
    }

    public class ChannelQueue<T>
    {
        private ChannelReader<T> _reader;
        private ChannelWriter<T> _writer;

        public ChannelQueue(Action<T> action)
        {
            var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions {SingleReader = true});
            _reader = channel.Reader;
            _writer = channel.Writer;
            Task.Run(async () =>
            {
                await foreach (var item in ReadAllAsync())
                    action.Invoke(item);
            });
        }

        public void Enqueue(T item) => _writer.TryWrite(item);

        public async ValueTask EnqueueAsync(T item, CancellationToken token)
        {
            while (await _writer.WaitToWriteAsync(token).ConfigureAwait(false))
            {
                if (_writer.TryWrite(item))
                    return;
            }

            throw new ChannelClosedException();
        }

        public void Stop() => _writer.Complete();

        public async ValueTask<T> ReadAsync(CancellationToken token)
        {
            while (true)
            {
                if (!await _reader.WaitToReadAsync(token).ConfigureAwait(false))
                    throw new ChannelClosedException();
                if (_reader.TryRead(out T item))
                    return item;
            }
        }

        public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken token = default)
        {
            while (await _reader.WaitToReadAsync(token).ConfigureAwait(false))
            while (_reader.TryRead(out T item))
                yield return item;
        }
    }
}