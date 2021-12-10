using System;
using System.Linq;
using System.Threading.Tasks;

namespace AkkaActor.Demos
{
    using Akka.Actor;


    public class ParentActor : ReceiveActor
    {
        private readonly IActorRef _volatileChildren;

        public ParentActor()
        {
            var props = Props.Create<VolatileChildActor>();

            _volatileChildren = Context.ActorOf(props, "children");

            Receive<ProcessANumber>(msg => { _volatileChildren.Tell(msg); });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                localOnlyDecider: ex => Directive.Resume);
        }
    }

    public class VolatileChildActor : ReceiveActor, IWithUnboundedStash
    {
        private int processedNumbers = 0;

        public VolatileChildActor()
        {
            Receive<ProcessANumber>(msg =>
            {
                if (processedNumbers == 6)
                {
                    processedNumbers = 0;
                    throw new Exception($"I already processed 6 numbers; too tired to process number {msg.Number}");
                }

                Console.WriteLine($"Processing number: {msg.Number}");
                processedNumbers++;
            });
        }
        public IStash Stash { get; set; }
    }

    public class ProcessANumber
    {
        public int Number { get; }

        public ProcessANumber(int number)
        {
            Number = number;
        }
    }

    public static class SupervisorTest
    {
        public static async Task Run()
        {
            var system = ActorSystem.Create("supervisionSystem");

            var parentActor = system.ActorOf(Props.Create<ParentActor>(), "parentActor");

            var numberRange = Enumerable.Range(1, 100);
            foreach (var number in numberRange)
            {
                parentActor.Tell(new ProcessANumber(number));
            }

            await system.WhenTerminated;
        }
    }
}
