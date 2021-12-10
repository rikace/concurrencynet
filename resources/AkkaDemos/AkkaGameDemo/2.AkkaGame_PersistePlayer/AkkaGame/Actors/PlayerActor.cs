using System;
using System.IO;
using Akka.Actor;
using Akka.Persistence;
using AkkaGame.Messages;

namespace AkkaGame.Actors
{
    class PlayerActor : ReceivePersistentActor
    {
        private readonly string _playerName;
        private int _health;

        public override string PersistenceId => $"player-{_playerName}";

        public PlayerActor(string playerName, int startingHealth)
        {
            _playerName = playerName;
            _health = startingHealth;

            LogHelper.WriteLine($"{_playerName} created");

            Command<HitMessage>(message => HitPlayer(message));
            Command<DisplayStatusMessage>(message => DisplayPlayerStatus());
            Command<CauseErrorMessage>(message =>
            {
                switch (message.ExceptionType)
                {
                    case ExceptionType.Application:
                        SimulateApplicationError();
                        break;
                    case ExceptionType.IO:
                        SimulateIOError();
                        break;
                }
            });
            
            Recover<HitMessage>(message =>
            {
                LogHelper.WriteLine($"{_playerName} replaying HitMessage {message} from journal");
                _health -= message.Damage;
            });
        }

        private void HitPlayer(HitMessage message)
        {
            LogHelper.WriteLineCyan($"{_playerName} received HitMessage");

            LogHelper.WriteLineCyan($"{_playerName} persisting HitMessage");

            Persist(message, hitMessage =>
            {
                LogHelper.WriteLine($"{_playerName} persisted HitMessage ok, updating actor state");
                _health -= message.Damage;
            });      
        }

        private void DisplayPlayerStatus()
        {
            LogHelper.WriteLineCyan($"{_playerName} received DisplayStatusMessage");

            Console.WriteLine($"{_playerName} has {_health} health");
        }

        private void SimulateIOError()
        {
            LogHelper.WriteLineRed($"{_playerName} received CauseErrorMessage");

            throw new IOException($"Simulated io-exception in player: {_playerName}");
        }
        
        private void SimulateApplicationError()
        {
            LogHelper.WriteLineRed($"{_playerName} received CauseErrorMessage");

            throw new ApplicationException($"Simulated application exception in player: {_playerName}");
        }
    }
}