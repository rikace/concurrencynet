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

            // TODO LAB
            // register the Ping and Pong (Observable/Observer) to each other

            Console.WriteLine("Press any key to stop ...");
            Console.ReadKey();

            // TODO LAB
            // Close / Unregister the Ping and Pong registrations

            Console.WriteLine("Ping Pong has completed.");
        }
    }
}
