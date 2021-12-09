using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ParallelPatterns.TaskComposition;
using ImageHandler = Helpers.ImageHandler;
using ImageInfo = Helpers.ImageProcessingHelpers.ImageInfo;

namespace ConsoleTaskEx
{
    public class ImageProcessing
    {
        private readonly string source;
        private readonly string destination;
        private ImageProcessingHelpers imageProcessingHelpers;

        public ImageProcessing(string source, string destination)
        {
            this.source = source;
            this.destination = destination;
            this.imageProcessingHelpers = new ImageProcessingHelpers(destination);
        }

        public async Task RunContinuation()
        {
            var files = Directory.GetFiles(source, "*.jpg");

            // Task Continuation
            foreach (string fileName in files)
            {
                await imageProcessingHelpers.LoadImage_Step1(fileName)
                    .ContinueWith(imageInfo =>
                    {
                        // TODO LAB
                        // how can we implement an helper function to use/re-use to handle both
                        // task Error and task Cancellation (in a centralize manner)
                        return imageProcessingHelpers.ScaleImage_Step2(imageInfo.Result);
                    }).Unwrap()
                    .ContinueWith(imageInfo => { return imageProcessingHelpers.ConvertTo3D_Step3(imageInfo.Result); }).Unwrap()
                    .ContinueWith(imageInfo => { imageProcessingHelpers.SaveImage_Step4(imageInfo.Result); });
            }
        }

        public async Task RunTransformer()
        {
            // namespace
            // ParallelPatterns.TaskComposition

            // Bonus: use the cancellation token to stop the computation
            var cts = new CancellationTokenSource();

            var files = Directory.GetFiles(source, "*.jpg");

            // TODO LAB
            // Implement the missing code for the
            // Task Then
            // Task Select
            // Task SelectMany
            // in "Common/Helpers.TaskComposition.cs"

            Func<string, Task<ImageInfo>> transformer = imagePath =>
                from image in imageProcessingHelpers.LoadImage_Step1(imagePath)
                from scaleImage in imageProcessingHelpers.ScaleImage_Step2(image)
                from converted3DImage in imageProcessingHelpers.ConvertTo3D_Step3(scaleImage)
                select converted3DImage;

            foreach (string fileName in files)
                // Task Then / SelectMany pipeline
                await transformer(fileName).Then(imageProcessingHelpers.SaveImage_Step4);

                // Option using Task.WhenAll
                //await transformer(fileName).SelectMany(imageProcessingHelpers.SaveImage_Step4);

                // TODO LAB
                // execute "transformer" operations in parallel (Option using Task.WhenAll)
                // NOTE: we should Throttle the task, how can we control the number of tasks run in parallel?
                //       - Option using Task.WhenAll
                //       - Throttle the tasks

        }
    }
}
