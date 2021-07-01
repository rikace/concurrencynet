using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Helpers;
using ImageInfo = Helpers.ImageProcessingHelpers.ImageInfo;

namespace ProducersConsumers
{
    public class ChannelProdsCons
    {
        public Channel<string> inputData = null;
        public Channel<ImageInfo> stage2Data = null;
        public Channel<ImageInfo> stage3Data = null;
        public Channel<ImageInfo> stage4Data = null;
        private ImageProcessingHelpers imageProcessingHelpers;

        public async Task Stage1()
        {
            Console.WriteLine($"Stage 1 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");

            //  await foreach (var item in inputData.Reader.ReadAllAsync())
            while (await inputData.Reader.WaitToReadAsync())
            {
                var receivedItem = await inputData.Reader.ReadAsync();
                var outputItem = await imageProcessingHelpers.LoadImage_Step1(receivedItem);
                await stage2Data.Writer.WriteAsync(outputItem);
                Console.WriteLine($"Stage 1 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            }

            stage2Data.Writer.Complete();
        }

        public async Task Stage2()
        {
            Console.WriteLine($"Stage 2 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (await stage2Data.Reader.WaitToReadAsync())
            {
                var receivedItem = await stage2Data.Reader.ReadAsync();
                var outputItem = await imageProcessingHelpers.ScaleImage_Step2(receivedItem);
                await stage3Data.Writer.WriteAsync(outputItem);
                Console.WriteLine($"Stage 2 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            }

            stage3Data.Writer.Complete();
        }

        public async Task Stage3()
        {
            Console.WriteLine($"Stage 3 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (await stage3Data.Reader.WaitToReadAsync())
            {
                var receivedItem = await stage3Data.Reader.ReadAsync();
                var outputItem = await imageProcessingHelpers.ConvertTo3D_Step3(receivedItem);
                await stage4Data.Writer.WriteAsync(outputItem);
                Console.WriteLine($"Stage 3 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            }

            stage4Data.Writer.Complete();
        }

        public async Task Stage4()
        {
            Console.WriteLine($"Stage 4 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            while (await stage4Data.Reader.WaitToReadAsync())
            {
                var receivedItem = await stage4Data.Reader.ReadAsync();
                var outputItem = await imageProcessingHelpers.SaveImage_Step4(receivedItem);
                Console.WriteLine($"Stage 4 - save data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            }
        }

        public void Run(string source, string destination)
        {
            imageProcessingHelpers = new ImageProcessingHelpers(destination);

            inputData = Channel.CreateBounded<string>(10);
            stage2Data = Channel.CreateBounded<ImageInfo>(10);
            stage3Data = Channel.CreateBounded<ImageInfo>(10);
            stage4Data = Channel.CreateBounded<ImageInfo>(10);

            // TODO
            // (1) Convert/Refactor each Steps so that they take as input
            //     parameters relative Channel reader (to read data from) and
            //     the Channel writer (to write into it). This will allow to create
            //     reusable components to create a Pipeline.
            //     Bonus: pass also a cancellation token to stop the data flow between channels
            var taskStage1 = Task.Run(Stage1);
            var taskStage2 = Task.Run(Stage2);
            var taskStage3 = Task.Run(Stage3);
            var taskStage4 = Task.Run(Stage4);

            // (2) Create a Broadcast Channel component. See "Broadcast" method below.
            //     This Broadcast function should take as input
            //     a Channel Reader (to read data from) and an int value. This number is used to create
            //     a set of Channel readers (the same number as the value input) that broadcast in each channel
            //     the same input passed into the main Channel Reader.
            //     Signature: input: (ChannelReader<T> channel, int n) - output IEnumerable<ChannelReader<T>>
            //     The idea is when pushing an item to the Channel input, the same item is broadcast to
            //     the Channel readers created

            // (3) Replace the last step (Step 4) using the new "Broadcast" created (2), this step should
            //     dispatch the items received to the Step4 implementation and (for demo purpose only) to the
            //     function "SendImage" below.

            // (4) Create a Join Channel component. See "Join" method below.
            //     This Join function should take as input a set/collection of Channel readers to send data into, and should
            //     return a single Channel Reader. This output channel is used to funnel into it all the data passed
            //     to any of the input channels. This is the opposite of the Broadcast function.

            // (5) Modify the input of this Run function to accepts an array of sources:
            //     "public void Run(string[] sources, string destination)"
            //     - Uncomment the relative code in the Main program to run the update Run method.
            //     - Initialize a set of Channel readers, one for each of the "source" strings passed
            //       (use for example a for/loop to iterate the source array to create a channel and add to a List).
            //     - Initialize a channel Reader using the new created Join function passing the relative parameters
            //     - replace the "inputData" channel from the Step1 with this new Join-ed channel


            var images = Directory.GetFiles(source, "*.jpg");
            foreach (var image in images)
            {
                inputData.Writer.TryWrite(image);
            }

            inputData.Writer.Complete();

            Task.WaitAll(taskStage1, taskStage2, taskStage3, taskStage4);
        }

        static IList<ChannelReader<T>> Broadcast<T>(ChannelReader<T> ch, int n)
        {
            // (1) create an array of Channel<T>
            // (2) initialize the array with a new unbounded channel for item

            Task.Run(async () =>
            {
                // (3) use Async Stream to read all the items from the input "channel"
                //     and write back to the Outputs channels of the new created array

                // (4) close all the outputs channels
            });

            // (5) returns an array of Channel Readers from  the output array
            return default;
        }

        static ChannelReader<T> Join<T>(params ChannelReader<T>[] inputs)
        {
            // (1) create an output Channel
            var output = Channel.CreateUnbounded<T>();

            Task.Run(async () =>
            {
                // (2) create an inline function to process a given ChannelReader
                //     by reading all the incoming items and write them back to the
                //     output channel

                // (3) execute the inline function created in step (2) for each "ChannelReader" inputs
                //     and await for all the Tasks generated
                // (4) complete/close the Writer Channel

                // NOTE: you can try to use the AsyncEnumerable
            });

            // (4) return the ChannelReader
            return output;
        }

        private async Task SendImage(ImageInfo item)
        {
            Console.WriteLine($"Sending image {item.Name} with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
            await Task.Delay(2000);
        }
    }
}
