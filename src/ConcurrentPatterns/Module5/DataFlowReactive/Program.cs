using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommonHelpers;
using Dataflow.FuzzyMatch;
using Dataflow.WebCrawler;
using DataflowPipeline;
using DataFlowPipeline.Compression;
using ParallelForkJoin;
using ReactiveAgent;
using static Helpers.Helpers;
using FileEx = Helpers.FileEx;

namespace DataFlowPipeline
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            var files = Directory.GetFiles("../../../../../Data/Text", "*.txt");

            await TestForkJoin(files);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            // WordsCounterDataflow.Start(cts.Token);

            // DemoBlocks();

            // await RunWebCrawler();
            //
            // await ExecuteCompression.Start();
            //
            //await RunFuzzyMatch(files);
            //
            // Console.WriteLine("Finished. Press any key to exit.");
            // Console.ReadLine();
        }

        static void DemoBlocks()
        {
            DataflowTransformActionBlocks.Run();
            // DemoBuildingBlocks.BufferBlock_Simple();
            // DemoBuildingBlocks.BufferBlock_BoundCapacity();
            // DemoBuildingBlocks.ActionBlock_Simple();
            // DemoBuildingBlocks.ActionBlock_Parallel();
            // DemoBuildingBlocks.ActionBlock_Linking();
            // DemoBuildingBlocks.ActionBlock_Propagate();
            // DemoBuildingBlocks.TransformBlock_Simple();
            // DemoBuildingBlocks.BatchBlock_Simple();
            // DemoBuildingBlocks.TransformManyBlock_Simple();
            // DemoBuildingBlocks.BroadcastBlock_Simple();
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

            DataFlowImageCrawler.Start(urls, 2, async (url, buffer) =>
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

        static async Task RunFuzzyMatch(IEnumerable<string> files)
        {
            IList<string> filesOrdered =
                files.Select(file => new FileInfo(file))
                    .OrderBy(f => f.Length)
                    .Select(f => f.FullName)
                    .Take(10).ToList();

            var wordsToSearch = new string[]
                {"ENGLISH", "RICHARD", "STEALLING", "MAGIC", "STARS", "MOON", "CASTLE"};
            await ParallelFuzzyMatch.RunFuzzyMatchDataFlow(wordsToSearch, filesOrdered);
        }

        static async Task TestForkJoin(IEnumerable<string> files)
        {
            var result = await ForkJoinDataFlow.ForkJoin<string, string, ConcurrentDictionary<string, int>>(
                files,
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

            foreach (var item in result)
            {
                Console.WriteLine($"The word {item.Key} was mentioned {item.Value}");
            }


        }
    }
}
