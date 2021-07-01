﻿using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;
using ChatMessages;

namespace ChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ConfigurationFactory.ParseString(@"
            akka {  
                actor {
                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                }
                remote {
                    helios.tcp {
                        transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                        applied-adapters = []
                        transport-protocol = tcp
                        port = 8081
                        hostname = localhost
                    }
                }
            }
            ");

            Console.Title = $"Chat Server : {System.Diagnostics.Process.GetCurrentProcess().Id}";

            using (var system = ActorSystem.Create("MyServer", config))
            {
                system.ActorOf<ChatServerActor>("ChatServer");

                Console.ReadLine();
            }
        }
    }

    class ChatServerActor : TypedActor,
        IHandle<SayRequest>,
        IHandle<ConnectRequest>,
        IHandle<NickRequest>,
        IHandle<Disconnect>,
        IHandle<ChannelsRequest>,
        ILogReceive

    {
        private readonly HashSet<IActorRef> _clients = new HashSet<IActorRef>();

        public void Handle(SayRequest message)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("User {0} said {1}", message.Username, message.Text);
            var response = new SayResponse
            {
                Username = message.Username,
                Text = message.Text,
            };
            Console.ForegroundColor = color;
            foreach (var client in _clients) client.Tell(response, Self);
        }

        public void Handle(ConnectRequest message)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("User {0} has connected", message.Username);
            _clients.Add(this.Sender);
            Sender.Tell(new ConnectResponse
            {
                Message = $"Hello {message.Username} and welcome to Akka .NET chat example",
            }, Self);
            Console.ForegroundColor = color;
        }

        public void Handle(NickRequest message)
        {
            var response = new NickResponse
            {
                OldUsername = message.OldUsername,
                NewUsername = message.NewUsername,
            };

            foreach (var client in _clients) client.Tell(response, Self);
        }

        public void Handle(Disconnect message)
        {

        }

        public void Handle(ChannelsRequest message)
        {

        }
    }
}