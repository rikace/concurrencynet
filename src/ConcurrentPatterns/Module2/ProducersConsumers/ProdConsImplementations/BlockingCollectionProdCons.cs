using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImageInfo = Helpers.ImageProcessingHelpers.ImageInfo;

namespace ProducerConsumer
{
    // TODO
    // convert the Steps to be able to handle multi/producers and multi/consumers
    // without threads contention.
    // For example look into the "BlockingCollection<>.TryTakeFromAny" API
    // See MultiThreadedProdCons
    public class BlockingCollectionProdCons
    {
        public BlockingCollection<string> inputData = null;
        public BlockingCollection<ImageInfo> stage2Data = null;
        public BlockingCollection<ImageInfo> stage3Data = null;
        public BlockingCollection<ImageInfo> stage4Data = null;
        private ImageProcessingHelpers imageProcessingHelpers;

        public async Task Stage1()
        {
            Console.WriteLine($"Stage 1 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!inputData.IsCompleted)
            {
                if (inputData.TryTake(out var receivedItem, 50))
                {
                    var outputItem = await imageProcessingHelpers.LoadImage_Step1(receivedItem);
                    stage2Data.TryAdd(outputItem);
                    Console.WriteLine($"Stage 1 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 1 - Could not get data");
            }
            stage2Data?.CompleteAdding();
        }

        public async Task Stage2()
        {
            Console.WriteLine($"Stage 2 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage2Data.IsCompleted)
            {
                if (stage2Data.TryTake(out var receivedItem, 50))
                {
                    var outputItem = await imageProcessingHelpers.ScaleImage_Step2(receivedItem);
                    stage3Data.TryAdd(outputItem);
                    Console.WriteLine($"Stage 2 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 2 - Could not get data");
            }
            stage3Data?.CompleteAdding();
        }

        public async Task Stage3()
        {
            Console.WriteLine($"Stage 3 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage3Data.IsCompleted)
            {
                if (stage3Data.TryTake(out var receivedItem, 50))
                {
                    var outputItem = await imageProcessingHelpers.ConvertTo3D_Step3(receivedItem);
                    stage4Data.TryAdd(outputItem);
                    Console.WriteLine($"Stage 3 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
                else
                    Console.WriteLine("Stage 3 - Could not get data");
            }
            stage4Data?.CompleteAdding();
        }

        public async Task Stage4()
        {
            Console.WriteLine($"Stage 4 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (!stage4Data.IsCompleted)
            {
                if (stage4Data.TryTake(out var receivedItem, 50))
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

            inputData = new BlockingCollection<string>(10);
            stage2Data = new BlockingCollection<ImageInfo>(10);
            stage3Data = new BlockingCollection<ImageInfo>(10);
            stage4Data = new BlockingCollection<ImageInfo>(10);

            var taskStage1 = Task.Run(Stage1);
            var taskStage2 = Task.Run(Stage2);
            var taskStage3 = Task.Run(Stage3);
            var taskStage4 = Task.Run(Stage4);

            var images = Directory.GetFiles(source, "*.jpg");
            foreach (var image in images)
            {
                inputData.Add(image);
            }

            inputData.CompleteAdding();

            Task.WaitAll(taskStage1, taskStage2, taskStage3, taskStage4);
        }
    }
}
