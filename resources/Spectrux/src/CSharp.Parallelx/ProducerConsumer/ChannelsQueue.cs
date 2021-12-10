using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CSharp.Parallelx.ProducerConsumer
{
    public class ChannelsQueue : IJobQueue<Action>
    {
        private ChannelWriter<Action> _writer;
        private ChannelReader<Action> _reader;

        public ChannelsQueue()
        {
            var channel = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions() {SingleReader = true});
            _reader = channel.Reader;
            _writer = channel.Writer;

            Task.Run(async () =>
            {
                while (await _reader.WaitToReadAsync())
                {
                    // Fast loop around available jobs
                    while (_reader.TryRead(out var job))
                    {
                        job.Invoke();
                    }
                }
            });
        }

        public void Enqueue(Action job)
        {
            _writer.TryWrite(job);
        }

        public void Stop()
        {
            _writer.Complete();
        }
    }

    public class ChannelsQueue<T>
    {
        private ChannelWriter<T> _writer;
        private ChannelReader<T> _reader;

        public ChannelsQueue(Action<T> action)
        {
            var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() {SingleReader = true});
            _reader = channel.Reader;
            _writer = channel.Writer;


            Task.Run(async () =>
            {
                await foreach (var item in ReadAllAsync())
                {
                    action.Invoke(item);
                }
            });
        }

        public void Enqueue(T job)
        {
            _writer.TryWrite(job);
        }

        public async ValueTask EnqueueAsync(T item, CancellationToken cancellationToken)
        {
            while (await _writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
                if (_writer.TryWrite(item))
                    return;

            throw new ChannelClosedException();
        }

        public void Stop()
        {
            _writer.Complete();
        }


        public async ValueTask<T> ReadAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new ChannelClosedException();

                if (_reader.TryRead(out T item))
                    return item;
            }
        }

        private async IAsyncEnumerable<T> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            while (_reader.TryRead(out T item))
                yield return item;
        }
    }
}