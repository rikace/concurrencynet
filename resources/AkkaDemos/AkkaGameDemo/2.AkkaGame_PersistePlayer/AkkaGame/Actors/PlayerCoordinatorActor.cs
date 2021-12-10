using System.IO;
using AkkaGame.Messages;

namespace AkkaGame.Actors
{
    using System;
    using Akka.Actor;

    internal class PlayerCoordinatorActor : ReceiveActor
    {
        private const int DefaultStartingHealth = 100;

        public PlayerCoordinatorActor()
        {
            Receive<CreatePlayerMessage>(message =>
            {
                LogHelper.WriteLine($"PlayerCoordinatorActor received CreatePlayerMessage for {message.PlayerName}");

                Context.ActorOf(
                    Props.Create(() =>
                        new PlayerActor(message.PlayerName, DefaultStartingHealth)), message.PlayerName);
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                maxNrOfRetries: 10,
                withinTimeRange: TimeSpan.FromMinutes(1),
                localOnlyDecider: ex =>
                {
                    switch (ex)
                    {
                        case ApplicationException ae:
                            return Directive.Resume;
                        
                        case IOException ioe:
                            return Directive.Restart;
                        
                        default:
                            return Directive.Escalate;
                    }
                });
        }
    }
}