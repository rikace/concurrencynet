using System;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;

namespace AkkaActor.Demos
{
    class Subscriber : ReceiveActor
    {
        public Subscriber(string TopicName)
        {
            var mediator = DistributedPubSub.Get(Context.System);
            mediator.Mediator.Tell(new Subscribe(TopicName, Self));
            Receive<SubscribeAck>(ack => Console.WriteLine($"Subscribed to '{TopicName}'"));
            Receive<string>(s => Console.WriteLine($"Received a message: '{s}'"));
        }
    }


    public class PubSubMediator : ReceiveActor
    {
        public PubSubMediator()
        {
            using (var system = ActorSystem.Create("Mediator-Systsm"))
            {
// install-package Akka.Cluster.Tools -pre
                var mediator = DistributedPubSub.Get(system).Mediator;
                const string TopicName = "topic-name";

// Distributed pub/sub subscriber actor


// publish messages on distributed pub/sub topic
                mediator.Tell(new Publish(TopicName, "hello world"));
                mediator.Tell(new Subscribe(TopicName, Self));


                var actorRef = system.ActorOf(Props.Create(() => new SimpleActor()), "my-actor");
// register an actor - it must be an actor living on the current node
                mediator.Tell(new Put(actorRef));

                var message = "Some message";

// send message to an single actor anywhere on any node in the cluster
                mediator.Tell(new Send("/user/my-actor", message, localAffinity: false));

// send message to all actors registered under the same path on different nodes
                mediator.Tell(new SendToAll("/user/my-actor", message, excludeSelf: false));
            }
        }
    }
}