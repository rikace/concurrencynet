using Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonHelpers;
using ConcurrentPatterns;
using DataParallelism;
using DataParallelism.Map;
using DataParallelism.MapReduce;
using DataParallelism.Reduce;
using static Helpers.Helpers;

namespace ParallelPatterns
{
    class Program
    {

        static void Main(string[] args)
        {
            string dataFolder = "../../../../../Data/Text";
            var dir = new DirectoryInfo(dataFolder);
            var files = dir.GetFiles("*.txt");


            // TODO: bonus How do we inject a "stop words" predicate ?
            var stopWords = File.ReadAllLines($"{dataFolder}/StopWords.txt");

            // Step (1) implement ForkJoin
            // use TaskCompletionSource as possible option
            // Notes: Throttling, ProcessAsComplete
            // TaskForkJoin(files); // note this is a blocking function for demo purpose, we could return a Task


            // Step  (2) implement Parallel Map
            // Map(files);

            // Step (3) implement parallel Reduce
            // Reduce(files);

            // Step (4) implement parallel Map/Reduce
            // MapReduce(files);  // for loops do not compose!!

            Console.WriteLine("COMPLETE");
            Console.ReadLine();
        }

        private static void TaskForkJoin(FileInfo[] files)
        {
            var operationTasks = files
                .Select<FileInfo, Func<Task<string[]>>>(file => () => File.ReadAllLinesAsync(file.FullName)).ToArray();

            var result = ForkJoin.Invoke(
                reduce: (state, lines) =>
                {
                    // NOTE
                    // If you call ContainsKey, and then Add, you are checking if the key exists twice.
                    // if (state.ContainsKey(item))
                    //     state[item]++;
                    // else
                    //     state.TryAdd(item, 1);

                    foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
                    {
                        var words = line.Split(Delimiters);
                        words.ForAll(word =>
                        {
                            var cleanupWord = word.RemoveNumbers().Cleanup();
                            if (!string.IsNullOrEmpty(cleanupWord))
                            {
                                // TODO LAB bonus
                                // NOTE
                                // If you call ContainsKey, and then Add, you are checking if the key exists twice.
                                // What can we do better ?
                                if (state.ContainsKey(cleanupWord))
                                    state[cleanupWord]++;
                                else
                                    state.TryAdd(cleanupWord, 1);
                            }
                        });
                    }

                    return state;
                },
                seedInit: () => new ConcurrentDictionary<string, int>(),
                operations: operationTasks);

            var topMostUsedWords =
                result.OrderByDescending(kv => kv.Value).Take(5);

            foreach (var topMostUsedWord in topMostUsedWords)
            {
                Console.WriteLine($"The word \"{topMostUsedWord.Key}\" is used {topMostUsedWord.Value} times");
            }
        }

        private static void Map(IEnumerable<FileInfo> files)
        {
            var largerFile = files.OrderByDescending(f => f.Length).First();
            var lines = File.ReadAllLines(largerFile.FullName);

            // Select is like Map/Projection
            // However, in the context of Map/Reduce, we need to extract a Key used in the shuffle step to reference in the Reduce step.
            // For this case, we can use the Grouping concept.

            // TODO complete the ParallelMap.Map so that the return type can be use as conceptually a Key/Values type.
            // Look into the IGrouping (System.Linq.IGrouping).
            // the "object" type in the IEnumerable is a placeholder, replace it with the correct type.
            var wordGroups = ParallelMap.Map(lines,
                map: line => line.Split(Delimiters),
                keySelector: word => word);

            foreach (var kv in wordGroups)
            {
                // TODO do something with the result
                // Console.WriteLine($"the word {kv.Key} is mentioned {kv.Count()} times");
            }
        }

        private static void Reduce(IEnumerable<FileInfo> files)
        {
            var largerFile = files.OrderByDescending(f => f.Length).First();
            var lines = File.ReadAllLines(largerFile.FullName);

            var uniqueWords = ParallelReducer.Reduce(lines,
                seed: () => new ConcurrentHashSet<string>(),
                reduce: (acc, line) =>
                {
                    var words = line.Split(Delimiters);
                    words.ForAll(word =>
                    {
                        var cleanupWord = word.RemoveNumbers().Cleanup();
                        if (!string.IsNullOrEmpty(cleanupWord))
                            acc.Add(cleanupWord.ToUpper());
                    });
                    return acc;
                },
                accumulate: (overall, local) =>
                {
                    overall.UnionWith(local);
                    return overall;
                });

            Console.WriteLine($"There are {uniqueWords.Count()} unique words");
        }

        private static void MapReduce(IEnumerable<FileInfo> files)
        {
            var largerFile = files.OrderByDescending(f => f.Length).First();
            var content = File.ReadAllText(largerFile.FullName);

            using (var reader = new StringReader(content))
            {
                var query =
                    reader.EnumLines() // This could be an AsyncStream right? :)

                        // TODO : complete the map-reduce function
                        // Bonus, implement the override function that takes two values
                        // M and R which are respectively the level of parallelism for the Map and Reduce steps.
                        .MapReduce(
                            line => line.Split(Delimiters).Select(word => word.Cleanup().RemoveNumbers().ToLower()),
                            key => key,
                            g => new[] {new {Word = g.Key, Count = g.Count()}}
                        )
                        .ToList();

                // TODO (extra)
                // can you filter inside a MapReduce using the stopWords?
                // if so, where would inject the filter step and how?
                // Look into the file "ParallelFilerMap.cs"

                var words = query
                    .Where(element =>
                        !string.IsNullOrWhiteSpace(element.Word)
                        && !StopWords.Contains(element.Word))
                    .OrderByDescending(element => element.Count);

                foreach (var w in words.Take(10))
                {
                    Console.WriteLine($"Word: '{w.Word}', times used: '{w.Count}'");
                }

                Console.WriteLine($"Unique Words used: {query.Count()}");
            }
        }
    }
}
