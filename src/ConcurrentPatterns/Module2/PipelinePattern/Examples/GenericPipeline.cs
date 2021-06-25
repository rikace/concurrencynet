using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pipeline.GenericPipeline
{
    public interface IGenericPipeline<TStepIn>
    {
        BlockingCollection<TStepIn> Buffer { get; set; }
    }

    public class GenericPipelineStep<TStepIn, TStepOut> : IGenericPipeline<TStepIn>
    {
        public BlockingCollection<TStepIn> Buffer { get; set; } = new BlockingCollection<TStepIn>();
        public Func<TStepIn, TStepOut> StepAction { get; set; }
    }

    public static class GenericPipelineExtensions
    {
        public static TOutput Step<TInput, TOutput, TInputOuter, TOutputOuter>
        (this TInput inputType,
            GenericPipeline<TInputOuter, TOutputOuter> pipelineBuilder,
            Func<TInput, TOutput> step)
        {
            var pipelineStep = pipelineBuilder.GenerateStep<TInput, TOutput>();
            pipelineStep.StepAction = step;
            return default(TOutput);
        }
    }

    public class GenericPipeline<TPipeIn, TPipeOut>
    {
        List<object> _pipelineSteps = new List<object>();

        public event Action<TPipeOut> Finished;

        public GenericPipeline(Func<TPipeIn, GenericPipeline<TPipeIn, TPipeOut>, TPipeOut> steps)
        {
            steps.Invoke(default(TPipeIn), this);
        }

        public void Execute(TPipeIn input)
        {
            var first = _pipelineSteps[0] as IGenericPipeline<TPipeIn>;
            first.Buffer.Add(input);
        }

        public GenericPipelineStep<TStepIn, TStepOut> GenerateStep<TStepIn, TStepOut>()
        {
            var pipelineStep = new GenericPipelineStep<TStepIn, TStepOut>();
            var stepIndex = _pipelineSteps.Count;

            Task.Run(() =>
            {
                IGenericPipeline<TStepOut> nextGenericPipeline = null;

                foreach (var input in pipelineStep.Buffer.GetConsumingEnumerable())
                {
                    bool isLastStep = stepIndex == _pipelineSteps.Count - 1;
                    var output = pipelineStep.StepAction(input);
                    if (isLastStep)
                    {
                        Finished?.Invoke((TPipeOut) (object) output);
                    }
                    else
                    {
                        nextGenericPipeline = nextGenericPipeline
                                              ?? (isLastStep
                                                  ? null
                                                  : _pipelineSteps[stepIndex + 1] as IGenericPipeline<TStepOut>);
                        nextGenericPipeline.Buffer.Add(output);
                    }
                }
            });

            _pipelineSteps.Add(pipelineStep);
            return pipelineStep;
        }
    }
}
