using AgentEx;
using ParallelPatterns.TaskComposition;

namespace ParallelPatterns
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reactive.Disposables;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using FuzzyMatch;
    using ParallelPatterns.Common;
    using static FuzzyMatch.JaroWinklerModule.FuzyMatchStructures;
    using static ParallelPatterns.Common.FuzzyMatchHelpers;

using ReactiveAgent.Agents;

    public partial class ParallelFuzzyMatch
    {
        public static async Task RunFuzzyMatchDataFlow(string[] wordsLookup, IList<string> files)
        {
            var cts = new CancellationTokenSource();
            var opt = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 10,
                // TODO, change this value and check what is happening
                MaxDegreeOfParallelism = 1,
                CancellationToken = cts.Token
            };

            int fileCount = files.Count;

            var inputBlock = new BufferBlock<string>(opt);

            var readLinesBlock =
                new TransformBlock<string, string>(
                    async file => await File.ReadAllTextAsync(file, cts.Token), opt);

            var splitWordsBlock =
                new TransformBlock<string, HashSet<string>>(
                    text => WordRegex.Value.Split(text).Where(w => !IgnoreWords.Contains(w)).AsSet(), opt);

            var batch =
                new BatchBlock<HashSet<string>>(fileCount);

            var foundMatchesBlock =
                new TransformBlock<HashSet<string>[], WordDistanceStruct[]>(
                    async wordSet =>
                    {
                        var wordSetFlatten = wordSet.Flatten().AsSet();
                        var matches =
                            await wordsLookup.Traverse(wl =>
                                JaroWinklerModule.bestMatchTask(wordSetFlatten, wl, threshold));
                        return matches.Flatten().ToArray();
                    }, opt);


            // TODO LAB
            // (1)  Implement a block name "printBlock", which prints the output of
            //      the foundMatchesBlock using the "PrintSummary" method
            //      Then link the block to the "foundMatchesBlock" block
            //      var printBlock = // missing code
            // (2)  Link the "printBlock" to the "foundMatchesBlock" block

            // TODO LAB
            // After have completed the previous step, remove or unlink the printBlock, and replace the output of the "foundMatchesBlock" block
            // with Reactive Extensions "AsObservable", maintaining the call to the "PrintSummary" method
            // Play with different RX high-order function constructors
            // TODO foundMatchesBlock ...

            var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

            IDisposable disposeAll = new CompositeDisposable(
                inputBlock.LinkTo(readLinesBlock, linkOptions),
                readLinesBlock.LinkTo(splitWordsBlock, linkOptions),
                splitWordsBlock.LinkTo(batch, linkOptions),
                batch.LinkTo(foundMatchesBlock, linkOptions)
                // TODO uncomment this code after
                // implemented TODO (5)
                // foundMatchesBlock.LinkTo(printBlock)
            );

            cts.Token.Register(disposeAll.Dispose);

            foreach (var file in files)
                await inputBlock.SendAsync(file, cts.Token);

            inputBlock.Complete();
            await foundMatchesBlock.Completion.ContinueWith(_ => disposeAll.Dispose());
        }

        // C# example
        public static async Task RunFuzzyMatchAgent(string[] wordsLookup, IList<string> files)
        {
            var cts = new CancellationTokenSource();
            var opt = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 10,
                MaxDegreeOfParallelism = 4,
                CancellationToken = cts.Token
            };

            var inputBlock = new BufferBlock<string>(opt);

            var readLinesBlock =
                new TransformBlock<string, string>(
                    async file => await File.ReadAllTextAsync(file, cts.Token), opt);

            var splitWordsBlock =
                new TransformBlock<string, string[]>(
                    text => WordRegex.Value.Split(text).Where(w => !IgnoreWords.Contains(w)).AsSet().ToArray(), opt);

            var foundMatchesBlock =
                new TransformBlock<string[], WordDistanceStruct[]>(async wordSet =>
                {
                    var matches =
                        await wordsLookup.Traverse(wl => JaroWinklerModule.bestMatchTask(wordSet, wl, threshold));
                    return matches.Flatten().ToArray();
                }, opt);


            var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

            // TODO LAB (for C#)
            // Implement a stateful agent using the TPL Dataflow.
            // The Agent should have an internal state protected from external access.
            // The function passed in the constructor applies a project/reduce to the incoming messages and in the current state,
            // to return a new state
            // (see AgentAggregator.cs)
            var agent = Agent.StartWithRx(new Dictionary<string, HashSet<string>>(),
                (Dictionary<string, HashSet<string>> state, WordDistanceStruct[] matches) =>
                {
                    var matchesDic = matches
                        .GroupBy(w => w.Word)
                        .ToDictionary(
                            k => k.Key,
                            v => v.Select(w => w.Match).AsSet());

                    var newState = Clone(state);
                    foreach (var match in matchesDic)
                    {
                        if (newState.TryGetValue(match.Key, out HashSet<string> values))
                        {
                            values.AddRange(match.Value);
                            newState[match.Key] = values;
                        }
                        else
                            newState.Add(match.Key, match.Value);
                    }

                    return newState;
                });

            IDisposable disposeAll = new CompositeDisposable(
                inputBlock.LinkTo(readLinesBlock, linkOptions),
                readLinesBlock.LinkTo(splitWordsBlock, linkOptions),
                splitWordsBlock.LinkTo(foundMatchesBlock, linkOptions),
                foundMatchesBlock.LinkTo(agent),
                agent.Observable()
                    .Subscribe(
                        summaryMathces => PrintSummary(summaryMathces))
            );

            cts.Token.Register(disposeAll.Dispose);

            foreach (var file in files)
                await inputBlock.SendAsync(file, cts.Token);

            inputBlock.Complete();
            await foundMatchesBlock.Completion.ContinueWith(_ =>  disposeAll.Dispose());
        }
    }
}
