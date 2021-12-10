using System;
using System.Threading;
using static System.Console;
using Akka.Actor;
using AkkaGame.Actors;
using AkkaGame.Messages;

namespace AkkaGame
{
    class Program
    {
        private static ActorSystem System { get; set; }
        private static IActorRef PlayerCoordinator { get; set; }

        static void Main(string[] args)
        {
            System = ActorSystem.Create("Game", ConfigurationLoader.Load());
            
            // Persist in Memory 
            //System = ActorSystem.Create("Game");

            PlayerCoordinator = System.ActorOf<PlayerCoordinatorActor>("PlayerCoordinator");

            ForegroundColor = ConsoleColor.White;

            DisplayInstructions();


            while (true)
            {
                Thread.Sleep(2000); // ensure console color set back to white
                ForegroundColor = ConsoleColor.White;

                var action = ReadLine();

                var playerName = action.Split(' ')[0];

                if (action.Contains("create"))
                {
                    CreatePlayer(playerName);
                }
                else if (action.Contains("hit"))
                {
                    var damage = int.Parse(action.Split(' ')[2]);

                    HitPlayer(playerName, damage);
                }
                else if (action.Contains("display"))
                {
                    DisplayPlayer(playerName);
                }
                else if (action.Contains("error-io"))
                {
                    ErrorPlayer(playerName, ExceptionType.IO);
                }
                else if (action.Contains("error-app"))
                {
                    ErrorPlayer(playerName, ExceptionType.Application);
                }
                else
                {
                    WriteLine("Unknown command");
                }
            }
        }

        private static void ErrorPlayer(string playerName, ExceptionType exceptionType)
        {
            System.ActorSelection($"/user/PlayerCoordinator/{playerName}")
                .Tell(new CauseErrorMessage(exceptionType));
        }

        private static void DisplayPlayer(string playerName)
        {
            System.ActorSelection($"/user/PlayerCoordinator/{playerName}")
                .Tell(new DisplayStatusMessage());
        }

        private static void HitPlayer(string playerName, int damage)
        {
            System.ActorSelection($"/user/PlayerCoordinator/{playerName}")
                .Tell(new HitMessage(damage));
        }

        private static void CreatePlayer(string playerName)
        {
            PlayerCoordinator.Tell(new CreatePlayerMessage(playerName));
        }

        private static void DisplayInstructions()
        {
            Thread.Sleep(2000); // ensure console color set back to white
            ForegroundColor = ConsoleColor.White;

            WriteLine("Available commands:");
            WriteLine("<playername> create");
            WriteLine("<playername> hit");
            WriteLine("<playername> display");
            WriteLine("<playername> error-io");
            WriteLine("<playername> error-app");
        }
    }
}