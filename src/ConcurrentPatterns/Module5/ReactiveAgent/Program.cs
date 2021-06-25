using ReactiveAgent.Agents;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ReactiveAgent.Agents.Dataflow;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using CommonHelpers;
using ParallelPatterns;
using TPLAgent;
using static Helpers.Helpers;
using File = Helpers.FileEx;

namespace ReactiveAgent.CS
{
    public class Program
    {
        public static void Main(string[] args)
        {

            // TODO
            // PingPongAgents.Start();
            // AgentAggregate.Run();

            // DEMO
            //  WordCountAgentsExample.Run().Wait();


            Console.WriteLine("Finished. Press any key to exit.");
            Console.ReadLine();
        }
    }
}
