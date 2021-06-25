using ParallelPatterns;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using ReactiveAgent.Agents;

namespace TPLAgent
{
   public class PingPongAgents
    {
        // Implement two Agent that send messages to each others
        // playing Ping/Pong
        // NOTE: Add delay between messages
        public static void Start()
        {
            IAgent<string> logger, ping = null;
            IAgent<string> pong = null;

            logger = Agent.Start((string msg) => WriteLine(msg));

            ping = Agent.Start((string msg) =>
            {
                if (msg == "STOP") return;

                // TODO
                // Add code implementation here

            });

            pong = Agent.Start(0, (int count, string msg) =>
            {
                int newCount = count + 1;
                string nextMsg = (newCount < 5) ? "PONG" : "STOP";

                // TODO
                // Add code implementation here
                
                return newCount;
            });

            ping.Post("START");

        }
    }
}
