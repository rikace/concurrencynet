using System;
using System.IO;
using ConsoleTaskEx;
using Helpers;
using Pipeline.PipelinePatterns;

namespace PipelinePattern
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourceImages = "../../../../../Data/Images"; // OK
            var destination = "./Images/Output";
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            BenchPerformance.Time("Execute Image Processing with DataFlowPipeline",
                () =>
                {
                    // TODO Complete the missing code in DataFlowPipeline
                    DataFlowPipeline<string, ImageProcessingHelpers.ImageInfo>.ExecuteImageProcessing(sourceImages,
                        destination);
                });

            Console.WriteLine("Completed");
            Console.ReadLine();

            BenchPerformance.Time("Execute Image Processing with Pipeline",
                () =>
                {
                    // TODO Complete the missing code in ImageProcessingPipeline
                    ImageProcessingPipeline imageProc = new ImageProcessingPipeline(sourceImages, destination);
                    imageProc.Run();
                });

            Console.WriteLine("Completed");
            Console.ReadLine();
        }
    }
}
