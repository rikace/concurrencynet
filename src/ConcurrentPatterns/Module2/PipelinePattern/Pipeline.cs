using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelPatterns
{
    public class Pipeline<TInput, TOutput>
    {
        private readonly Func<TInput, Task<TOutput>> _processTask;
        private readonly Func<TInput, TOutput> _processor;

        private readonly BlockingCollection<TInput>[] _input;

        private readonly CancellationToken _token;
        private const int Count = 3;

        private Pipeline(
            Func<TInput, Task<TOutput>> processor,
            BlockingCollection<TInput>[] input = null,
            CancellationToken token = new CancellationToken())
        {
            _input = input ?? Enumerable.Range(0, Count - 1).Select(_ => new BlockingCollection<TInput>(10)).ToArray();

            Output = new BlockingCollection<TOutput>[_input.Length];
            for (var i = 0; i < Output.Length; i++)
                Output[i] = null == _input[i] ? null : new BlockingCollection<TOutput>(Count);

            _processTask = processor;
            _token = token;
            Task.Factory.StartNew(Run, _token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private Pipeline(
            Func<TInput, TOutput> processor,
            BlockingCollection<TInput>[] input = null,
            CancellationToken token = new CancellationToken())
        {
            _input = input ?? Enumerable.Range(0, Count - 1).Select(_ => new BlockingCollection<TInput>(10)).ToArray();

            Output = new BlockingCollection<TOutput>[_input.Length];
            for (var i = 0; i < Output.Length; i++)
                Output[i] = null == _input[i] ? null : new BlockingCollection<TOutput>(Count);

            _processor = processor;
            _token = token;
            Task.Factory.StartNew(Run, _token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static Pipeline<TInput, TOutput> Create(
            Func<TInput, TOutput> processor,
            CancellationToken token = new CancellationToken())
            => new Pipeline<TInput, TOutput>(processor, token: token);

        public static Pipeline<TInput, TOutput> Create(
            Func<TInput, Task<TOutput>> processor,
            CancellationToken token = new CancellationToken())
            => new Pipeline<TInput, TOutput>(processor, token: token);

        private BlockingCollection<TOutput>[] Output { get; }

        public Pipeline<TOutput, TMid> Then<TMid>(
            Func<TOutput, Task<TMid>> project,
            CancellationToken token = new CancellationToken())
            => new Pipeline<TOutput, TMid>(project, Output, token);

        public Pipeline<TOutput, TMid> Then<TMid>(
            Func<TOutput, TMid> project,
            CancellationToken token = new CancellationToken())
            => new Pipeline<TOutput, TMid>(project, Output, token);

        // TODO LAB
        public void Enqueue(TInput item)
        {
            // TODO complete missing code
            // We nee to enqueue the input into a callBack
            // for the collection "continuations" avoiding contentions

        }

        private async Task Run()
        {
            var sw = new SpinWait();

            // TODO LAB
            // Add missing code, steps to implement
            // 1 - take an item from the _input collection

            // 2 - process the item with the internal function
            //          either _processor or _processTask according to the active one
            // 3 - push the result to the Output collect
            // Bonus :  avoid contention in case of empty queue.
            //          Check "SpinWait" (use above instance "sw")


            if (Output != null)
            {
                foreach (var bc in Output) bc.CompleteAdding();
            }
        }
    }
}
