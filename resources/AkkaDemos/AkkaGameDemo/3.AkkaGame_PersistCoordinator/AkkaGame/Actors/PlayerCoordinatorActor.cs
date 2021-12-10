using System.IO;
using Akka.Persistence;
using AkkaGame.Messages;

namespace AkkaGame.Actors
{
    using System;
    using Akka.Actor;

    internal class PlayerCoordinatorActor : ReceivePersistentActor
    {
        private const int DefaultStartingHealth = 100;

        public override string PersistenceId { get; } = "player-coordinator";

        public PlayerCoordinatorActor()
        {
            Command<CreatePlayerMessage>(message =>
            {
                LogHelper.WriteLine($"PlayerCoordinatorActor received CreatePlayerMessage for {message.PlayerName}");

                if (!Context.Child(message.PlayerName).Equals(ActorRefs.Nobody))
                {
                    LogHelper.WriteLine($"The Player Actor {message.PlayerName} already exists");
                }
                else
                {

                    Persist(message, createPlayerMessage =>
                    {
                        LogHelper.WriteLine(
                            $"PlayerCoordinatorActor persisted a CreatePlayerMessage for {message.PlayerName}");

                        Context.ActorOf(
                            Props.Create(() =>
                                new PlayerActor(message.PlayerName, DefaultStartingHealth)), message.PlayerName);
                    });
                }
            });

            Recover<CreatePlayerMessage>(createPlayerMessage =>
            {
                LogHelper.WriteLine($"PlayerCoordinatorActor replaying CreatePlayerMessage for {createPlayerMessage.PlayerName}");

                Context.ActorOf(
                    Props.Create(() =>
                        new PlayerActor(createPlayerMessage.PlayerName, DefaultStartingHealth)), createPlayerMessage.PlayerName);

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