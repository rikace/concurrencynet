using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using CommonHelpers;
using ReactiveAgent.Agents.Dataflow;

namespace ReactiveAgent.Agents
{
    // TODO LAB
    // implement a function or a design/approach to compose Agents
    // rewrite the example composing the Agent as pipeline
    public static class WordCountAgentsExample
    {
        static char[] punctuation = Enumerable.Range(0, 256).Select(c => (char) c)
            .Where(c => Char.IsWhiteSpace(c) || Char.IsPunctuation(c)).ToArray();

        public static async Task Run()
        {
            IAgent<string> printer = Agent.Start((string msg) =>
                WriteLine($"{msg} on thread {Thread.CurrentThread.ManagedThreadId}"));

            // TODO LAB
            // Add a property that exposes an IObservable<'R> to the IAgent interface
            // The implementation of this property should stream the result of the Agent
            // Then, implement similar logic of the "counter" Agent using the Observable exposed
            IReplyAgent<string, (string, int)> counter =
                Agent.Start(new Dictionary<string, int>(),
                    (Dictionary<string, int> state, string word) =>
                    {
                        printer.Post($"counter received for word {word} message");
                        int count;
                        if (state.TryGetValue(word, out count))
                            state[word] = count + 1;
                        else state.Add(word, 1);
                        printer.Post($"counter {state[word]} received for word {word}");
                        return state;
                    }, (state, word) => (state, (word, state[word])));

            var parser = new StatelessDataflowAgent<string>(async line =>
                {
                    await printer.Send("parser received message");
                    line.Split(punctuation).ForAll(async word =>
                        await counter.Send(word.ToUpper()));
                });

            //   Producer/consumer using TPL Dataflow
            IAgent<string> reader =
                new StatelessDataflowAgent<string>(async (string filePath) =>
                {
                    await printer.Send("reader received message");
                    var lines = await File.ReadAllLinesAsync(filePath);
                    lines.ForAll(async line => await parser.Send(line));
                });


            const string contentPath = "../../../../../Data/Shakespeare";
            foreach (var filePath in Directory.EnumerateFiles(contentPath, "*.txt"))
            {
                if (System.IO.File.Exists(filePath))
                    await reader.Send(filePath);
            }

            // TODO LAB
            // add missing code here
            // so we can access the new "AsObservable" property from the "counter" instance of an agent (transformed to RX Agent)
            // to "subscribe" to the stream to print the result

            Console.ReadLine();

            var wordCount_This = await counter.Ask("this");
            var wordCount_Wind = await counter.Ask("wind");
        }
    }
}
