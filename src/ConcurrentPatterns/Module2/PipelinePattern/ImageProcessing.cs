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
    public class ImageProcessingPipeline
    {
        private readonly string source;
        private readonly string destination;

        private ImageProcessingHelpers imageProcessingHelpers;

        public ImageProcessingPipeline(string source, string destination)
        {
            this.source = source;
            this.destination = destination;
        }

        public void Run()
        {
            imageProcessingHelpers = new ImageProcessingHelpers(destination);

            // Bonus: use the cancellation token to stop the computation
            var cts = new CancellationTokenSource();

            var files = Directory.GetFiles(source, "*.jpg");

            // TODO
            // Complete the ParallelPatterns missing code
            // "ParallelPatterns/ParallelPatterns.cs"
            var imagePipe =
                ParallelPatterns.Pipeline<string, ImageInfo>.Create(imageProcessingHelpers.LoadImage_Step1);

            imagePipe.Then(imageProcessingHelpers.ScaleImage_Step2)
                .Then(imageProcessingHelpers.ConvertTo3D_Step3)
                .Then(imageProcessingHelpers.SaveImage_Step4);

            foreach (string fileName in files)
                imagePipe.Enqueue(fileName);
        }
    }
}
