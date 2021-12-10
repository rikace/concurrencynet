using System;
using Akka.Actor;

namespace AkkaActor.Demos
{
    public class SimpleActor : ReceiveActor
    {
        public SimpleActor()
        {
            Receive<string>(message =>
                Console.WriteLine("{0} got {1}", Self.Path.ToStringWithAddress(), message));
        }
    }
}
