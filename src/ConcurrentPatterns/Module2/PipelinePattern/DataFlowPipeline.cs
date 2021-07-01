using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helpers;
using ImageInfo = Helpers.ImageProcessingHelpers.ImageInfo;

namespace Pipeline.PipelinePatterns
{
    public class DataFlowPipeline<TIn, TOut>
    {
        private List<IDataflowBlock> _steps = new List<IDataflowBlock>();

        public void AddStep<TIn, TLocalOut>(Func<TIn, Task<TLocalOut>> stepFunc)
        {
            // TODO
            // Implement a step that
            // (1) create a TPL Dataflow block that handles the "stepFunc" function
            //     for mapping the input TIn to output TLocalOut
            var step = new TransformBlock<TIn, TLocalOut>(async input =>
                await stepFunc(input));
            if (_steps.Count > 0)
            {
                // (2) append/link the block created (1) to the last from the "_steps" list.

                var lastStep = _steps.Last();
                var targetBlock = (lastStep as ISourceBlock<TIn>);
                targetBlock.LinkTo(step, new DataflowLinkOptions());
            }

            _steps.Add(step);
        }

        public ITargetBlock<TIn> CreatePipeline(Func<TOut, Task> resultCallback)
        {
            var callBackStep = new ActionBlock<TOut>(resultCallback);
            var lastStep = _steps.Last();
            var targetBlock = (lastStep as ISourceBlock<TOut>);
            targetBlock.LinkTo(callBackStep);

            return (_steps.First() as ITargetBlock<TIn>);
        }

        // Example using TPL Dataflow
        // public static TransformBlock<string, string> CreatePipeline(Action<bool> resultCallback)
        // {
        //     var step1 = new TransformBlock<string, string>((sentence) => // do something (sentence));
        //         var step2 = new TransformBlock<string, int>((word) => word.Length);
        //     var step3 = new TransformBlock<int, bool>((length) => length % 2 == 1);
        //     var callbackStep = new ActionBlock<bool>(resultCallback);
        //     step1.LinkTo(step2, new DataflowLinkOptions());
        //     step2.LinkTo(step3, new DataflowLinkOptions());
        //     step3.LinkTo(callbackStep);
        //     return step1;
        // }

        public static void ExecuteImageProcessing(string source, string destination)
        {
            var imageProcessing = new ImageProcessingHelpers(destination);
            var images = Directory.GetFiles(source, "*.jpg");

            var pipelineBuilder = new DataFlowPipeline<string, ImageInfo>();
            pipelineBuilder.AddStep<string, ImageInfo>(input => imageProcessing.LoadImage_Step1(input));
            pipelineBuilder.AddStep<ImageInfo, ImageInfo>(input => imageProcessing.ScaleImage_Step2(input));
            pipelineBuilder.AddStep<ImageInfo, ImageInfo>(input => imageProcessing.ConvertTo3D_Step3(input));
            var pipeline =
                pipelineBuilder.CreatePipeline(resultCallback: input => imageProcessing.SaveImage_Step4(input));

            foreach (var image in images)
            {
                pipeline.Post(image);
            }

            // TODO RT sdd parellelism and biffer amd cancellation?
            // var options = new ExecutionDataflowBlockOptions()
            //     {
            //         MaxDegreeOfParallelism = dop,
            //         BoundedCapacity = 5,
            //     };
        }
    }
}

