using Reactive;
using System;

namespace ReactiveEx
{
    class Program
    {
        static void Main(string[] args)
        {
            // AsyncToObservable.Start();

            var ping = new Ping();
            var pong = new Pong();

            // TODO
            // register the Ping and Pong (Observable/Observer) to each other
            // var pongSubscription
            // var pingSubscription

            Console.WriteLine("Press any key to stop ...");

            var pongSubscription = ping.Subscribe(pong);
            var pingSubscription = pong.Subscribe(ping);

            Console.ReadKey();

            pongSubscription.Dispose();
            pingSubscription.Dispose();

            Console.WriteLine("Ping Pong has completed.");
        }
    }
}
