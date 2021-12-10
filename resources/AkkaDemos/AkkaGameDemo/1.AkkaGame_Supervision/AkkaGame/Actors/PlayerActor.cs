using System;
using System.IO;
using Akka.Actor;
using AkkaGame.Messages;

namespace AkkaGame.Actors
{
    class PlayerActor : ReceiveActor
    {
        private readonly string _playerName;
        private int _health;        

        public PlayerActor(string playerName, int startingHealth)
        {
            _playerName = playerName;
            _health = startingHealth;

            LogHelper.WriteLine($"{_playerName} created");

            Receive<HitMessage>(message => HitPlayer(message));
            
            Receive<DisplayStatusMessage>(message => 
                DisplayPlayerStatus());
            
            Receive<CauseErrorMessage>(message =>
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
        }

        private void HitPlayer(HitMessage message)
        {
            LogHelper.WriteLineCyan($"{_playerName} received HitMessage");

            _health -= message.Damage;
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