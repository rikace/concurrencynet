namespace ProducersConsumers.ProdConsImplementations
{
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

    public class ChannelMultiProdMultiCons
    {
            public Channel<ImageInfo> stage2Data = null;
            public Channel<ImageInfo> stage3Data = null;
            public Channel<ImageInfo> stage4Data = null;
            private ImageProcessingHelpers imageProcessingHelpers;

            public async Task Stage1(ChannelReader<string> reader, ChannelWriter<ImageInfo> writer)
            {
                Console.WriteLine($"Stage 1 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");

                //  await foreach (var item in inputData.Reader.ReadAllAsync())
                while (await reader.WaitToReadAsync())
                {
                    var receivedItem = await reader.ReadAsync();
                    var outputItem = await imageProcessingHelpers.LoadImage_Step1(receivedItem);
                    await writer.WriteAsync(outputItem);
                    Console.WriteLine($"Stage 1 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }

                writer.Complete();
            }

            public async Task Stage2(ChannelReader<ImageInfo> reader, ChannelWriter<ImageInfo> writer)
            {
                Console.WriteLine($"Stage 2 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                while (await reader.WaitToReadAsync())
                {
                    var receivedItem = await reader.ReadAsync();
                    var outputItem = await imageProcessingHelpers.ScaleImage_Step2(receivedItem);
                    await writer.WriteAsync(outputItem);
                    Console.WriteLine($"Stage 2 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }

                stage3Data.Writer.Complete();
            }

            public async Task Stage3(ChannelReader<ImageInfo> reader, ChannelWriter<ImageInfo> writer)
            {
                Console.WriteLine($"Stage 3 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                while (await reader.WaitToReadAsync())
                {
                    var receivedItem = await reader.ReadAsync();
                    var outputItem = await imageProcessingHelpers.ConvertTo3D_Step3(receivedItem);
                    await writer.WriteAsync(outputItem);
                    Console.WriteLine($"Stage 3 - add data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }

                stage4Data.Writer.Complete();
            }

            public async Task Stage4(ChannelReader<ImageInfo> reader)
            {
                Console.WriteLine($"Stage 4 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                while (await reader.WaitToReadAsync())
                {
                    var receivedItem = await reader.ReadAsync();
                    var outputItem = await imageProcessingHelpers.SaveImage_Step4(receivedItem);
                    Console.WriteLine($"Stage 4 - save data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
            }

            public async Task Stage5(ChannelReader<ImageInfo> reader)
            {
                Console.WriteLine($"Stage 5 - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                while (await reader.WaitToReadAsync())
                {
                    var receivedItem = await reader.ReadAsync();
                    await SendImage(receivedItem);
                    Console.WriteLine($"Stage 5 - send data with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                }
            }

            private async Task SendImage(ImageInfo item)
            {
                Console.WriteLine($"Sending image {item.Name} with Thread ID #{Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(2000);
            }

            public async Task Stage4Broadcast(ChannelReader<ImageInfo> reader)
            {
                var channels = Broadcast(reader, 2);

                Console.WriteLine($"Stage 4 Broadcast - is running with Thread ID #{Thread.CurrentThread.ManagedThreadId}");

                var stage4Task = Task.Run(async () => Stage4(channels[0]));
                var stage5Task = Task.Run(async () => Stage5(channels[1]));
                await Task.WhenAll(stage4Task, stage5Task);
            }

            static IList<ChannelReader<T>> Broadcast<T>(ChannelReader<T> channel, int n)
            {
                // (1) create an array of Channel<T>
                var outputs = new Channel<T>[n];

                // (2) initialize the array with a new unbounded channel for item
                for (int i = 0; i < n; i++)
                    outputs[i] = Channel.CreateUnbounded<T>();

                Task.Run(async () =>
                {
                    // (3) use Async Stream to read all the items from the input "channel"
                    //     and write back to the Outputs channels of the new created array
                    await foreach (var item in channel.ReadAllAsync())
                    {
                        foreach (var output in outputs)
                            await output.Writer.WriteAsync(item);
                    }

                    // (4) close all the outputs channels
                    foreach (var ch in outputs)
                        ch.Writer.Complete();
                });

                // (5) returns an array of Channel Readers from  the output array
                return outputs.Select(ch => ch.Reader).ToArray();
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
                    async Task Process(ChannelReader<T> input)
                    {
                        await foreach (var item in input.ReadAllAsync())
                            await output.Writer.WriteAsync(item);
                    }

                    // (3) execute the inline function created in step (2) for each "ChannelReader" inputs
                    //     and await for all the Tasks generated
                    await Task.WhenAll(inputs.Select(i => Process(i)).ToArray());
                    // (4) complete/close the Writer Channel
                    output.Writer.Complete();
                });

                // (4) return the ChannelReader
                return output;
            }

            static ChannelReader<string> Generator(string sourceImages)
            {
                var images = Directory.GetFiles(sourceImages, "*.jpg");
                var channel = Channel.CreateBounded<string>(10);
                var rnd = new Random();

                Task.Run(async () =>
                {
                    foreach (var image in images)
                    {
                        await channel.Writer.WriteAsync(image);
                        await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3)));
                    }
                    channel.Writer.Complete();
                });

                return channel.Reader;
            }
            public void Run(string[] sources, string destination)
            {
                imageProcessingHelpers = new ImageProcessingHelpers(destination);

                List<ChannelReader<string>> channels = new List<ChannelReader<string>>();
                foreach (var source in sources)
                    channels.Add(Generator(source));

                var channelInput = Join<string>(channels.ToArray());

                stage2Data = Channel.CreateBounded<ImageInfo>(10);
                stage3Data = Channel.CreateBounded<ImageInfo>(10);
                stage4Data = Channel.CreateBounded<ImageInfo>(10);

                var taskStage1 = Task.Run(() => Stage1(channelInput, stage2Data.Writer));
                var taskStage2 = Task.Run(() => Stage2(stage2Data.Reader, stage3Data.Writer));
                var taskStage3 = Task.Run(() => Stage3(stage3Data.Reader, stage4Data.Writer));
                var taskStage4 = Task.Run(() => Stage4Broadcast(stage4Data.Reader));

                Task.WaitAll(taskStage1, taskStage2, taskStage3, taskStage4);
            }
    }
}