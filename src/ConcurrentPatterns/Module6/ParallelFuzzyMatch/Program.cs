using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ParallelPatterns
{
    public static class Program
    {
        private static readonly string[] WordsToSearch =
            {"ENGLISH", "RICHARD", "STEALING", "MAGIC", "STARS", "MOON", "CASTLE"};


        private static async Task Start(IList<string> files)
        {
            while (true)
            {
                Console.WriteLine(@"=============================================================
Press the number of the method you want to run and press ENTER
(1) RunFuzzyMatch Task Composition  (TODO 1)
(2) RunFuzzyMatch Task LINQ  (TODO 2)
(3) RunFuzzyMatch Pipeline  (TODO 3)
(4) RunFuzzyMatch Process Tasks as complete (TODO 4)
(5) RunFuzzyMatchDataFlow  (TODO 5 - 6)
(6) RunFuzzyMatch Agent C# (TODO 7 C#)
(q) Exit
=============================================================
");

                var choice = Console.ReadLine();
                if(choice.ToLower() == "q")
                    break;
                ;
                var indexChoice = int.Parse(choice);
                var watch = Stopwatch.StartNew();

                switch (indexChoice)
                {
                    case 1:
                        // TODO 1
                        await ParallelFuzzyMatch.RunFuzzyMatchTaskComposition(WordsToSearch, files);
                        break;
                    case 2:
                        // TODO 2
                        await ParallelFuzzyMatch.RunFuzzyMatchTaskLINQ(WordsToSearch, files);
                        break;
                    case 3:
                        // TODO 3
                        ParallelFuzzyMatch.RunFuzzyMatchPipelineCSharp(WordsToSearch, files);
                        break;
                    case 4:
                        // TODO 4
                        await ParallelFuzzyMatch.RunFuzzyMatchTaskProcessAsCompleteAbstracted(WordsToSearch, files);
                        break;
                    case 5:
                        // TODO 5
                        await ParallelFuzzyMatch.RunFuzzyMatchDataFlow(WordsToSearch, files);
                        break;
                    case 6:
                        // TODO 6
                        await ParallelFuzzyMatch.RunFuzzyMatchAgent(WordsToSearch, files);
                        break;
                    default:
                        throw new Exception("Selection not supported");
                }

                watch.Stop();

                Console.WriteLine($"<< DONE in {watch.Elapsed.ToString()} >>");
                Console.WriteLine("Press << ENTER >> to continue");
                Console.ReadLine();
            }
        }

        static async Task Main(string[] args)
        {
            const string contentPath = "../../../../Data/Shakespeare";
            IList<string> files =
                    Directory.EnumerateFiles(contentPath, "*.txt")
                        .Select(f => new FileInfo(f))
                        .OrderBy(f => f.Length)
                        .Select(f => f.FullName)
                        .Take(5).ToList();

            await Start(files);
            Console.ReadLine();
        }
    }
}
