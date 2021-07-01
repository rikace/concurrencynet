using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Agent =  ReactiveAgent.Agents.Agent;

namespace ReactiveAgent
{
    public class AgentAggregate
    {
        static string CreateFileNameFromUrl(string url) =>
            Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

        public static void Run()
        {
            //   Producer/consumer using TPL Dataflow
            List<string> urls = new List<string>
            {
                @"https://www.google.com",
                @"https://www.amazon.com",
                @"https://www.bing.com",
                @"https://www.google.com",
                @"https://www.facebook.com"
            };


            // TODO 5.3
            // Agent fold over state and messages - Aggregate
            urls.Aggregate(ImmutableDictionary<string, string>.Empty,
                (state, url) =>
                {
                    if (!state.TryGetValue(url, out string content))
                        using (var webClient = new WebClient())
                        {
                            System.Console.WriteLine($"Downloading '{url}' sync ...");
                            content = webClient.DownloadString(url);
                            File.WriteAllText(CreateFileNameFromUrl(url), content);
                            return state.Add(url, content);
                        }

                    return state;
                });

            // TODO : 5.3
            // (1) replace the implementation using the urls.Aggregate with a new one that uses an Agent
            // Suggestion, instead of the Dictionary you should try to use an immutable structure

            var agentStateful_TODO = Agent.Start<Dictionary<string, string>>(msg => { });
            // (2) complete this code
            urls.ForEach(url =>
            {
                /* agentStateful.Post(url) */
            });

            #region Solution

            var agentStateful = Agent.Start(ImmutableDictionary<string, string>.Empty,
                async (ImmutableDictionary<string, string> state, string url) =>
                {
                    if (!state.TryGetValue(url, out string content))
                        using (var webClient = new WebClient())
                        {
                            content = await webClient.DownloadStringTaskAsync(url);
                            await File.WriteAllTextAsync(CreateFileNameFromUrl(url), content);
                            return state.Add(url, content);
                        }

                    return state;
                });

            urls.ForEach(url => agentStateful.Post(url));

            #endregion
        }
    }
}
