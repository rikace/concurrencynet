using System;
using System.IO;
using Akka.Actor;
using Akka.Persistence;
using AkkaGame.Messages;

namespace AkkaGame.Actors
{
    class PlayerActorState
    {
        public string PlayerName { get; set; }
        public int Health { get; set; }

        public override string ToString()
        {
            return $"[PlayerActorState {PlayerName} {Health}]";
        }
    }
    
    
    class PlayerActor : ReceivePersistentActor
    {
        private int _eventCount;
        private PlayerActorState _state;

        public override string PersistenceId => $"playet-{_state.PlayerName}";

        public PlayerActor(string playerName, int startingHealth)
        {
            _state = new PlayerActorState
            {
                PlayerName = playerName,
                Health = startingHealth
            };

            LogHelper.WriteLine($"{_state.PlayerName} created");

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
            
            Recover<SnapshotOffer>(offer =>
            {
                LogHelper.WriteLine($"{_state.PlayerName} received SnapshotOffer from snapshot store, updating state");

                _state = (PlayerActorState)offer.Snapshot;

                LogHelper.WriteLine($"{_state.PlayerName} state {_state} set from snapshot");
            });
            
            Recover<HitMessage>(message =>
            {
                LogHelper.WriteLine($"{_state.PlayerName} replaying HitMessage {message} from journal");
                _state.Health -= message.Damage;
            });
        }

        private void HitPlayer(HitMessage message)
        {
            LogHelper.WriteLineCyan($"{_state.PlayerName} received HitMessage");

            LogHelper.WriteLineCyan($"{_state.PlayerName} persisting HitMessage");

            Persist(message, hitMessage =>
            {
                LogHelper.WriteLine($"{_state.PlayerName} persisted HitMessage ok, updating actor state");
                _state.Health -= message.Damage;
                
                _eventCount++;

                if (_eventCount == 3)
                {
                    LogHelper.WriteLine($"{_state.PlayerName} saving snapshot");

                    SaveSnapshot(_state);

                    LogHelper.WriteLine($"{_state.PlayerName} resetting event count to 0");

                    _eventCount = 0;
                }

            });      
        }

        private void DisplayPlayerStatus()
        {
            LogHelper.WriteLineCyan($"{_state.PlayerName} received DisplayStatusMessage");

            Console.WriteLine($"{_state.PlayerName} has {_state.Health} health");
        }

        private void SimulateIOError()
        {
            LogHelper.WriteLineRed($"{_state.PlayerName} received CauseErrorMessage");

            throw new IOException($"Simulated io-exception in player: {_state.PlayerName}");
        }
        
        private void SimulateApplicationError()
        {
            LogHelper.WriteLineRed($"{_state.PlayerName} received CauseErrorMessage");

            throw new ApplicationException($"Simulated application exception in player: {_state.PlayerName}");
        }
    }
}