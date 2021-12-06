using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using ParallelPatterns.Common;

namespace DataflowPipeline
{
    public class WordsCounterDataflow
    {
        // TODO LAB
        // convert into producer/consumer word counter with agent
        // then convert any step with RX for testing
        public static void Start(CancellationToken cTok)
        {
            const int bc = 1;
            var opt = new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = bc,
                MaxDegreeOfParallelism = 4,
                CancellationToken = cTok
            };
            // Download a book as a string
            var downloadBook = new TransformBlock<string, string>(async filePath =>
            {
                Console.WriteLine("Downloading the book...");
                return await File.ReadAllTextAsync(filePath);
            }, opt);

            // splits text into an array of strings.
            var createWordList = new TransformBlock<string, string[]>(text =>
            {
                Console.WriteLine("Creating list of words...");

                // Remove punctuation
                char[] tokens = text.ToArray();
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!char.IsLetter(tokens[i]))
                        tokens[i] = ' ';
                }

                text = new string(tokens);

                return text.Split(new char[] {' '},
                    StringSplitOptions.RemoveEmptyEntries);
            }, new ExecutionDataflowBlockOptions() {BoundedCapacity = bc});

            var broadcast = new BroadcastBlock<string[]>(s => s);
            var accumulator = new BufferBlock<string[]>();

            // Remove short words and return the count
            var filterWordList = new TransformBlock<string[], int>(words =>
            {
                Console.WriteLine("Counting words...");

                var wordList = words.Where(word => word.Length > 3).OrderBy(word => word)
                    .Distinct().ToArray();
                return wordList.Count();
            }, new ExecutionDataflowBlockOptions() {BoundedCapacity = bc});

            var printWordCount = new ActionBlock<int>(wordcount =>
            {
                Console.WriteLine("Found {0} words",
                    wordcount);
            });

            // TODO LAB
            // Link the block to form the correct pipeline shape
            // for the word-counter

            // TODO LAB
            // use Reactive/Extensions (Observable) in the last step of the pipeline
            // - Register the output of the last Dataflow block as Observable
            // - Create an Observable step to maintain the state of the outputs in a collection
            //   that removes duplicates.
            // - Subscribe the observable to print the count of the unique words parsed
            //     Ex: Subscribe(words => Console.WriteLine("Observable -> Found {0} words", words.Count));

            // NOTE: each completion task in the pipeline creates a continuation task
            //       that marks the next block in the pipeline as completed.
            //       A completed dataflow block processes any buffered elements, but does
            //       not accept new elements.

            // Add missing code here

            // ...


            try
            {
                downloadBook.Completion.ContinueWith(t =>
                {
                    if (t.IsFaulted) ((IDataflowBlock) createWordList).Fault(t.Exception);
                    else createWordList.Complete();
                });
                createWordList.Completion.ContinueWith(t =>
                {
                    if (t.IsFaulted) ((IDataflowBlock) filterWordList).Fault(t.Exception);
                    else filterWordList.Complete();
                });
                filterWordList.Completion.ContinueWith(t =>
                {
                    if (t.IsFaulted) ((IDataflowBlock) printWordCount).Fault(t.Exception);
                    else printWordCount.Complete();
                });

                // Download Origin of Species

                const string contentPath = "../../../../../Data/Shakespeare";
                foreach (var filePath in Directory.EnumerateFiles(contentPath, "*.txt"))
                {
                    downloadBook.Post(filePath);
                }

                // Mark the head of the pipeline as complete.
                downloadBook.Complete();
                printWordCount.Completion.Wait(cTok);

                Console.WriteLine("Finished. Press any key to exit.");
                Console.ReadLine();
            }
            catch (AggregateException ae)
            {
                foreach (Exception ex in ae.InnerExceptions)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
