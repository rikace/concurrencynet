using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Routing;

namespace AkkaActor.Demos
{
    class Program
    {
        static void BecomeExample()
        {
            Console.WriteLine("Welcome to World of Akka Craft");
            Console.WriteLine("Use 'hit' to fight");
            Console.WriteLine("Use 'ress' to ressurect the dead");
            using (ActorSystem system = ActorSystem.Create("foo"))
            {
                var player = system.ActorOf<Player>();
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input == "hit")
                        player.Tell(new Hit());
                    if (input == "ress")
                        player.Tell(new Resurrect());
                }
            }
        }

        static void Routing()
        {
            var config = ConfigurationFactory.ParseString(@"
                            akka {  
                                actor {
                                    deployment {
                                        /localactor {
                                            router = round-robin-pool
                                            nr-of-instances = 3
                                        }
                                    }
                                }");
            using (var system = ActorSystem.Create("system1", config))
            {
                //create a local group router (see config)
                var local = system.ActorOf(Props.Create(() => new SimpleActor())
                    //.WithRouter(FromConfig.Instance), "localactor");
                    .WithRouter(new RoundRobinPool(5)));

                //these messages should reach the workers via the routed local ref
                local.Tell("Local message 1");
                local.Tell("Local message 2");
                local.Tell("Local message 3");
                local.Tell("Local message 4");
                local.Tell("Local message 5");

                Console.ReadLine();
            }
        }

        static void MakeFirstActor()
        {
            using (var system = ActorSystem.Create("mySystem"))
            {
                var helloActor = system.ActorOf<GreetingActor>();
                helloActor.Tell(new Greet("y'all'"));

                Console.ReadLine();
            }
        }
        static async Task Main(string[] args)
        {
            GreetingTest.Run();

            // HandleActor.Start();
            // Communication.Start();
            // RoutingDemo.Start();
            // await SupervisorTest.Run();



            //Routing();
            // StrategyDemo.Start();
            // BecomeExample();
            // UntypedDemo.Start();
            // ReceiveActorDemo.Start();

            Console.ReadLine();
        }
    }
}
