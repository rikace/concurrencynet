using System.Linq;

namespace ProducersConsumers.ProdConsImplementations
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Helpers;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using ImageInfo = Helpers.ImageProcessingHelpers.ImageInfo;

    public class MultiThreadedProdCons
    {
        public BlockingCollection<string>[] inputData = null;
        public BlockingCollection<ImageInfo>[] stage2Data = null;
        public BlockingCollection<ImageInfo>[] stage3Data = null;
        public BlockingCollection<ImageInfo>[] stage4Data = null;
        private ImageProcessingHelpers imageProcessingHelpers;

        public async Task Stage1()
        {
            Console.WriteLine($"Stage 1 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!inputData.All(bc => bc.IsCompleted))
            {
                if (BlockingCollection<string>.TryTakeFromAny(inputData, out var receivedItem, 50) >= 0)
                {
                    var outputItem = await imageProcessingHelpers.LoadImage_Step1(receivedItem);
                    BlockingCollection<ImageInfo>.AddToAny(stage2Data, outputItem);
                    Console.WriteLine($"Stage 1 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 1 - Could not get data");
            }

            foreach (var bc in stage2Data) bc.CompleteAdding();
        }

        public async Task Stage2()
        {
            Console.WriteLine($"Stage 2 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage2Data.All(bc => bc.IsCompleted))
            {
                if (BlockingCollection<ImageInfo>.TryTakeFromAny(stage2Data, out var receivedItem, 50) >= 0)
                {
                    var outputItem = await imageProcessingHelpers.ScaleImage_Step2(receivedItem);
                    BlockingCollection<ImageInfo>.AddToAny(stage3Data, outputItem);
                    Console.WriteLine($"Stage 2 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 3 - Could not get data");
            }

            foreach (var bc in stage3Data) bc.CompleteAdding();
        }

        public async Task Stage3()
        {
            Console.WriteLine($"Stage 3 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage3Data.All(bc => bc.IsCompleted))
            {
                if (BlockingCollection<ImageInfo>.TryTakeFromAny(stage3Data, out var receivedItem, 50) >= 0)
                {
                    var outputItem = await imageProcessingHelpers.ConvertTo3D_Step3(receivedItem);
                    BlockingCollection<ImageInfo>.AddToAny(stage4Data, outputItem);
                    Console.WriteLine($"Stage 3 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 3 - Could not get data");
            }

            foreach (var bc in stage4Data) bc.CompleteAdding();
        }

        public async Task Stage4()
        {
            Console.WriteLine($"Stage 4 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage4Data.All(bc => bc.IsCompleted))
            {
                if (BlockingCollection<ImageInfo>.TryTakeFromAny(stage4Data, out var receivedItem, 50) >= 0)
                {
                    var outputItem = await imageProcessingHelpers.SaveImage_Step4(receivedItem);
                    Console.WriteLine($"Stage 4 - save data {outputItem} with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 4 - Could not get data");
            }
        }


        public void Run(string source, string destination)
        {
            imageProcessingHelpers = new ImageProcessingHelpers(destination);

            inputData = new BlockingCollection<string>[2];
            for (int i = 0; i < inputData.Length; i++)
                inputData[i] = new BlockingCollection<string>(10);

            stage2Data = new BlockingCollection<ImageInfo>[2];
            for (int i = 0; i < stage2Data.Length; i++)
                stage2Data[i] = new BlockingCollection<ImageInfo>(10);

            stage3Data = new BlockingCollection<ImageInfo>[2];
            for (int i = 0; i < stage3Data.Length; i++)
                stage3Data[i] = new BlockingCollection<ImageInfo>(10);

            stage4Data = new BlockingCollection<ImageInfo>[2];
            for (int i = 0; i < stage4Data.Length; i++)
                stage4Data[i] = new BlockingCollection<ImageInfo>(10);

            var taskStage1 = Task.Run(Stage1);
            var taskStage2 = Task.Run(Stage2);
            var taskStage3 = Task.Run(Stage3);
            var taskStage4 = Task.Run(Stage4);

            var images = Directory.GetFiles(source, "*.jpg");
            foreach (var image in images)
            {
                BlockingCollection<string>.AddToAny(inputData, image);
            }

            foreach (var bc in inputData) bc.CompleteAdding();

            Task.WaitAll(taskStage1, taskStage2, taskStage3, taskStage4);
        }
    }
}
