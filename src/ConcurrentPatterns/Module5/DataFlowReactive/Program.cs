using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommonHelpers;
using ParallelForkJoin;
using static Helpers.Helpers;
using FileEx = Helpers.FileEx;

namespace DataFlowPipeline
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var files = Directory.GetFiles("./../../../../Data/Text", "*.txt");

            var cts = new CancellationTokenSource();

            // TODO
            // complete the "DataFlowCrawler" implementation
            await RunWebCrawler();

            // DEMO
            // await ExecuteCompression.Start();
            // WordsCounterDataflow.Start(cts.Token);
            // await RunFuzzyMatch(files);
            // await TestForkJoin(files);

            Console.WriteLine("Finished. Press any key to exit.");
            Console.ReadLine();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task RunWebCrawler()
        {
            var urls = new List<string>();

            urls.Add("https://www.cnn.com");
            urls.Add("https://www.bbc.com");
            urls.Add("https://www.amazon.com");
            urls.Add("https://www.jet.com");
            urls.Add("https://www.cnn.com");

            ServicePointManager.DefaultConnectionLimit = 100;

            string baseFolderName = @"./Images";

            DataflowReactive.DataFlowCrawler.Start(urls, async (url, buffer) =>
            {
                string fileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".jpg";

                if (!Directory.Exists(baseFolderName))
                    Directory.CreateDirectory(baseFolderName);

                string name = $"{baseFolderName}/{fileName}";
                Console.WriteLine($"Saving image {fileName}");

                using (Stream srm = File.OpenWrite(name))
                {
                    await srm.WriteAsync(buffer, 0, buffer.Length);
                }
            });

            Console.WriteLine("Hit ANY KEY to exit...");
            Console.ReadKey();
        }

        static Task TestForkJoin(IEnumerable<string> files)
        {
            var result = ForkJoinDataFlow.ForkJoin<string, string, ConcurrentDictionary<string, int>>(files,
                file => FileEx.ReadAllLinesAsync(file),
                () => new ConcurrentDictionary<string, int>(),
                (state, line) =>
                {
                    var words = line.Split(Delimiters);
                    words.ForAll(word =>
                    {
                        var cleanupWord = word.RemoveNumbers().Cleanup();
                        if (!string.IsNullOrEmpty(cleanupWord))
                            state.AddOrUpdate(cleanupWord, x => 1, (x, count) => count + 1);
                    });

                    return state;
                });
            return result;
        }
    }
}
