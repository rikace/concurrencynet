using Reactive;
using System;

namespace ReactiveEx
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO / DEMO
            // AsyncToObservable.Start();

            var ping = new Ping();
            var pong = new Pong();

            Console.WriteLine("Press any key to stop ...");

            // TODO
            // register the Ping and Pong (Observable/Observer) to each other
            // var pongSubscription
            // var pingSubscription

            Console.ReadKey();

            // Close / Unregister the Ping and Pong registrations
            // pongSubscription
            // pingSubscription

            Console.WriteLine("Ping Pong has completed.");
        }
    }
}
