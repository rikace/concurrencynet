namespace CSharp.Parallelx.Pipelines
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using System;

    public static class Pipeline
    {
        public static Pipeline<TInput, TOutput> Create<TInput, TOutput>(Func<TInput, TOutput> func,
            int degreeOfParallelism = 1)
        {
            if (func == null) throw new ArgumentNullException("func");
            if (degreeOfParallelism < 1) throw new ArgumentOutOfRangeException("degreeOfParallelism");
            return new Pipeline<TInput, TOutput>(func, degreeOfParallelism);
        }
    }

    public class Pipeline<TInput, TOutput>
    {
        private readonly Func<TInput, TOutput> _stageFunc;
        private readonly int _degreeOfParallelism;

        internal Pipeline(Func<TInput, TOutput> func, int degreeOfParallelism)
        {
            _stageFunc = func;
            _degreeOfParallelism = degreeOfParallelism;
        }

        public Pipeline<TInput, TNextOutput> Next<TNextOutput>(Func<TOutput, TNextOutput> func,
            int degreeOfParallelism = 1)
        {
            if (func == null) throw new ArgumentNullException("func");
            if (degreeOfParallelism < 1) throw new ArgumentOutOfRangeException("degreeOfParallelism");
            return new InternalPipeline<TNextOutput>(this, func, degreeOfParallelism);
        }

        public IEnumerable<TOutput> Process(IEnumerable<TInput> source,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (source == null) throw new ArgumentNullException("source");
            return ProcessNoArgValidation(source, cancellationToken);
        }

        private IEnumerable<TOutput> ProcessNoArgValidation(IEnumerable<TInput> source,
            CancellationToken cancellationToken)
        {
            using (var output = new BlockingCollection<TOutput>())
            {
                var processingTask = Task.Run(() =>
                {
                    try
                    {
                        ProcessCore(source, cancellationToken, output);
                    }
                    finally
                    {
                        output.CompleteAdding();
                    }
                });

                foreach (var result in output.GetConsumingEnumerable(cancellationToken))
                {
                    yield return result;
                }

                processingTask.Wait();
            }
        }

        protected virtual void ProcessCore(IEnumerable<TInput> source, CancellationToken cancellationToken,
            BlockingCollection<TOutput> output)
        {
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _degreeOfParallelism,
                //TaskScheduler = Scheduler
            };
            Parallel.ForEach(source, options, item => output.Add(_stageFunc(item)));
        }

        private sealed class InternalPipeline<TNextOutput> : Pipeline<TInput, TNextOutput>
        {
            private readonly Pipeline<TInput, TOutput> _beginningPipeline;
            private readonly Func<TOutput, TNextOutput> _lastStageFunc;

            public InternalPipeline(Pipeline<TInput, TOutput> beginningPipeline, Func<TOutput, TNextOutput> func,
                int degreeOfParallelism = 1) : base(null, degreeOfParallelism)
            {
                _beginningPipeline = beginningPipeline;
                _lastStageFunc = func;
            }

            protected override void ProcessCore(
                IEnumerable<TInput> source, CancellationToken cancellationToken, BlockingCollection<TNextOutput> output)
            {
                var options = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _degreeOfParallelism,
                };
                Parallel.ForEach(_beginningPipeline.Process(source, cancellationToken), options,
                    item => output.Add(_lastStageFunc(item)));
            }
        }
    }
}