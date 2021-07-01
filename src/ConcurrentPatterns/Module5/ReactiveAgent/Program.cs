using ReactiveAgent.Agents;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ReactiveAgent.Agents.Dataflow;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using CommonHelpers;
using ParallelPatterns;
using static Helpers.Helpers;
using File = Helpers.FileEx;

namespace ReactiveAgent.CS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            // DataflowPipeline.DataflowPipeline.Start();

            // DEMO
            //   DataflowTransformActionBlocks.Run();

            // DEMO
            // PingPongAgents.Start();

            // DEMO
            //   WordCountAgentsExample.Run().Wait();

           await WordCountAgentsExample.Run();

           // AgentAggregate.Run();


            Console.WriteLine("Finished. Press any key to exit.");
            Console.ReadLine();
        }
    }
}
