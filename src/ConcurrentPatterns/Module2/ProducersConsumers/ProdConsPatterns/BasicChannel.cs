using System.Runtime.CompilerServices;

namespace ProducerConsumer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    public class BasicChannel
    {
        public static void Start()
        {
            Task.Run(async () => { await ChannelRun(2000, 1, 100, 5); });
            Console.WriteLine("running");
            Console.ReadLine();
        }

        static async Task ChannelRun(int delayMs, int numberOfReaders,
            int howManyMessages = 100, int maxCapacity = 10)
        {
            var finalDelayMs = 25;
            var finalNumberOfReaders = 1;

            if (delayMs >= 25)
                finalDelayMs = delayMs;

            if (numberOfReaders >= 1)
                finalNumberOfReaders = numberOfReaders;


            //use a bounded channel is useful if you have a slow consumer
            //unbounded may lead to OutOfMemoryException
            var channel = Channel.CreateBounded<string>(maxCapacity);

            var reader = channel.Reader;
            var writer = channel.Writer;

            async Task Read(ChannelReader<string> theReader, int readerNumber)
            {
                //while when channel is not complete
                while (await theReader.WaitToReadAsync())
                {
                    while (theReader.TryRead(out var theMessage))
                    {
                        Console.WriteLine(
                            $"Reader {readerNumber} read '{theMessage}' at {DateTime.Now.ToLongTimeString()}");
                        //simulate some work
                        await Task.Delay(delayMs);
                    }
                }
            }

            var tasks = new List<Task>();
            for (int i = 0; i < finalNumberOfReaders; i++)
            {
                tasks.Add(Task.Run(() => Read(reader, i + 1)));
                await Task.Delay(10);
            }

            //Write message to the channel, but since Read has Delay
            //we will get back pressure applied to the writer, which causes it to block
            //when writing. Unbounded channels do not block ever
            for (int i = 0; i < howManyMessages; i++)
            {
                Console.WriteLine($"Writing at {DateTime.Now.ToLongTimeString()}");
                await writer.WriteAsync($"SomeText message '{i}");
            }

            //Tell readers we are complete with writing, to stop them awaiting
            //WaitToReadAsync() forever
            writer.Complete();


            await reader.Completion;
            await Task.WhenAll(tasks);
        }
    }
}
