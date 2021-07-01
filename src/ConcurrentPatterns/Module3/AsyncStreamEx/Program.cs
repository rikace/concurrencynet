using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using Helpers;

namespace AsyncStreamEx
{
    class Program
    {
        static int SumFromOneToCount(int count)
        {
            ConsoleExt.WriteLine("SumFromOneToCount called!");

            var sum = 1;
            for (var i = 0; i < count; i++)
                sum = sum + i;

            return sum;
        }

        static IEnumerable<int> SumFromOneToCountYield(int count)
        {
            ConsoleExt.WriteLine("SumFromOneToCountYield called!");

            var sum = 1;
            for (var i = 0; i < count; i++)
            {
                sum = sum + i;
                yield return sum;
            }
        }

        static async Task<int> SumFromOneToCountAsync(int count)
        {
            ConsoleExt.WriteLine("SumFromOneToCountAsync called!");

            var result = await Task.Run(() =>
            {
                var sum = 1;
                for (var i = 0; i < count; i++)
                    sum = sum + i;
                return sum;
            });

            return result;
        }

        // TODO convert this as demo to IAsyncEnumerable
        static async IAsyncEnumerable<int> SumFromOneToCountTaskIEnumerable(int count)
        {
            ConsoleExt.WriteLine("SumFromOneToCountAsyncIEnumerable called!");
            yield return await Task.Run(() =>
            {
                var sum = 1;
                for (var i = 0; i < count; i++)
                {
                    sum = sum + i;

                }

                return sum;
            });
        }

        static IEnumerable<int> ProduceAsyncSumSequence(int count)
        {
            ConsoleExt.WriteLineAsync("ProduceAsyncSumSequence Called");

            var sum = 1;
            for (var i = 0; i < count; i++)
            {
                sum = sum + i;
                Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();
                yield return sum;
            }
        }

        static async Task ConsumeAsyncSumSequence(IAsyncEnumerable<int> sequence)
        {
            ConsoleExt.WriteLineAsync("ConsumeAsyncSumSequence Called");

            await sequence.ForEachAsync(value =>
            {
                ConsoleExt.WriteLineAsync($"Consuming the value: {value}");
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            });
        }

        static async Task Main(string[] args)
        {
            // TODO / STEP 1
            // IAsyncEnumerable<int> pullBasedAsyncSequence = ProduceAsyncSumSequence(5).ToAsyncEnumerable();
            // var consumingTask = Task.Run(() => ConsumeAsyncSumSequence(pullBasedAsyncSequence));
            //
            // // Just for demo! Wait until the task is finished!
            // consumingTask.Wait();
            //
            // ConsoleExt.WriteLineAsync("Async Streams Demo Done!");
            // Console.ReadLine();


            // TODO / STEP 2
            // uncomment and fix the code
            IAsyncEnumerable<int> seq = SumFromOneToCountTaskIEnumerable(5);

            await foreach (var item in seq)
            {
                Console.WriteLine($"Value {item} - Thread ID# {Thread.CurrentThread.ManagedThreadId}");
            }

            Console.WriteLine("Complete");
            Console.ReadLine();
        }
    }
}
