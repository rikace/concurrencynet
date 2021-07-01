using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helpers;

namespace Reactive
{
    // TODO implement the interface "ISubject<Pong, Ping>"
	// public class Ping //: ISubject<Ping, Pong>

    public class Ping : ISubject<Pong, Ping>
    {
        // Notifies the observer of a new value in the sequence.
        public void OnNext(Pong value)
        {
            ConsoleExt.WriteLine("Ping received Pong.");
        }

        // Notifies the observer that an exception has occurred.
        public void OnError(Exception exception)
        {
            ConsoleExt.WriteLine("Ping experienced an exception and had to quit playing.");
        }

        // Notifies the observer of the end of the sequence.
        public void OnCompleted()
        {
            ConsoleExt.WriteLine("Ping finished.");
        }

        // Subscribes an observer to the observable sequence.
        public IDisposable Subscribe(IObserver<Ping> observer)
        {
            // TODO
            // implement an Observable timer that sends a notification
            // to the subscriber ("Pong") every 1 second
            // Suggestion, the trick is to send to the observer a message
            // that contains this instance of PING
            return null;

            /* Solutions
			 return Observable.Interval(TimeSpan.FromSeconds(2))
                .Where(n => n < 10)
                .Select(n => this)
                .Subscribe(observer);
			*/
        }
        // Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        public void Dispose()
        {
            OnCompleted();
        }
    }

    // TODO implement the interface "ISubject<Ping, Pong>"
    // public class Pong : ISubject<Ping, Pong>
    public class Pong : ISubject<Ping, Pong>
    {
        // Notifies the observer of a new value in the sequence.
        public void OnNext(Ping value)
        {
            ConsoleExt.WriteLine("Pong received Ping.");
        }

        // Notifies the observer that an exception has occurred.
        public void OnError(Exception exception)
        {
            ConsoleExt.WriteLine("Pong experienced an exception and had to quit playing.");
        }

        // Notifies the observer of the end of the sequence.
        public void OnCompleted()
        {
            ConsoleExt.WriteLine("Pong finished.");
        }


        // Subscribes an observer to the observable sequence.
        public IDisposable Subscribe(IObserver<Pong> observer)
        {
		    // TODO
            // implement an Observable timer that sends a notification
            // to the subscriber ("Ping") every 1.5 second
            // Suggestion, the trick is to send to the observer a message
            // that contains this instance of PONG
            return null;
			/* Solutions
            return Observable.Interval(TimeSpan.FromSeconds(1.5))
                .Where(n => n < 10)
                .Select(n => this)
                .Subscribe(observer);
		 */
        }

        // Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        public void Dispose()
        {
            OnCompleted();
        }
    }
}
