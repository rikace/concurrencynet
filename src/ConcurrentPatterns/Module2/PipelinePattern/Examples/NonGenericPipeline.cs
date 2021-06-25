using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Processing;

namespace Pipeline.NonGenericPipeline
{
    public interface INonGenericPipeline
    {
        void Execute(object input);
        event Action<object> Completed;
    }

    public class NonGenericPipeline : INonGenericPipeline
    {
        List<Func<object, object>> _pipelineSteps = new List<Func<object, object>>();
        BlockingCollection<object>[] _buffers;

        public event Action<object> Completed;
        public void AddStep(Func<object, object> stepFunc) => _pipelineSteps.Add(stepFunc);
        public void Execute(object input) => _buffers.FirstOrDefault()?.Add(input);

        public void AddStep<TStepIn, TStepOut>(Func<TStepIn, TStepOut> stepFunc)
        {
            _pipelineSteps.Add(objInput =>
                stepFunc.Invoke((TStepIn) (object) objInput));
        }

        public INonGenericPipeline GetPipeline()
        {
            _buffers = _pipelineSteps
                .Select(step => new BlockingCollection<object>())
                .ToArray();

            int bufferIndex = 0;
            foreach (var pipelineStep in _pipelineSteps)
            {
                var bufferIndexLocal = bufferIndex;
                Task.Run(() =>
                {
                    foreach (var input in _buffers[bufferIndexLocal].GetConsumingEnumerable())
                    {
                        var output = pipelineStep.Invoke(input);
                        bool isLastStep = bufferIndexLocal == _pipelineSteps.Count - 1;
                        if (isLastStep)
                            Completed?.Invoke(output);
                        else
                            _buffers[bufferIndexLocal + 1].Add(output);
                    }
                });
                bufferIndex++;
            }

            return this;
        }
    }
}
