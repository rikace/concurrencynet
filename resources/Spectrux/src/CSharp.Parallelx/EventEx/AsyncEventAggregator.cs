namespace CSharp.Parallelx.EventEx
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    
    public sealed class AsyncEventHub
    {
        private const string EventTypeNotFoundExceptionMessage = @"Event type not found!";
        private const string SubscribersNotFoundExceptionMessage = @"Subscribers not found!";
        private const string FailedToAddSubscribersExceptionMessage = @"Failed to add subscribers!";
        private const string FailedToGetEventHandlerTaskFactoriesExceptionMessage = @"Failed to get event handler task factories!";
        private const string FailedToAddEventHandlerTaskFactoriesExceptionMessage = @"Failed to add event handler task factories!";
        private const string FailedToGetSubscribersExceptionMessage = @"Failed to get subscribers!";
        private const string FailedToRemoveEventHandlerTaskFactories = @"Failed to remove event handler task factories!";

        private readonly TaskFactory _factory;

        /// <summary>
        ///     Dictionary(EventType, Dictionary(Sender, EventHandlerTaskFactories))
        /// </summary>
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, ConcurrentBag<object>>> _hub;

        public AsyncEventHub()
        {
            _factory = Task.Factory;

            _hub = new ConcurrentDictionary<Type, ConcurrentDictionary<object, ConcurrentBag<object>>>();
        }

        public Task<Task[]> Publish<TEvent>(object sender, Task<TEvent> eventDataTask)
        {
            var taskCompletionSource = new TaskCompletionSource<Task[]>();
            ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;
            
            _factory.StartNew(
                () =>
                    {
                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {

                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers.Count > 0)
                                {
                                    _factory.ContinueWhenAll(
                                        new ConcurrentBag<Task>(
                                            new ConcurrentBag<object>(subscribers.Keys)
                                                .Where(p => p != sender && subscribers.ContainsKey(p))
                                                .Select(p =>
                                                    {
                                                        ConcurrentBag<object> eventHandlerTaskFactories;

                                                        bool isFailed = !subscribers.TryGetValue(p, out eventHandlerTaskFactories);

                                                        return new
                                                            {
                                                                IsFailed = isFailed,
                                                                EventHandlerTaskFactories = eventHandlerTaskFactories
                                                            };
                                                    })
                                                .SelectMany(
                                                    p =>
                                                        {
                                                            if (p.IsFailed)
                                                            {
                                                                var innerTaskCompletionSource = new TaskCompletionSource<Task>();
                                                                innerTaskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                                                return new ConcurrentBag<Task>(new[] {innerTaskCompletionSource.Task});
                                                            }

                                                            return new ConcurrentBag<Task>(
                                                                p.EventHandlerTaskFactories
                                                                 .Select(q =>
                                                                 {
                                                                     try
                                                                     {
                                                                         return ((Func<Task<TEvent>, Task>)q)(eventDataTask);
                                                                     }
                                                                     catch (Exception ex)
                                                                     {
                                                                         return _factory.FromException<object>(ex);
                                                                     }
                                                                 }));
                                                        }))
                                            .ToArray(),
                                        taskCompletionSource.SetResult);
                                }
                                else
                                {
                                    taskCompletionSource.SetException(new Exception(SubscribersNotFoundExceptionMessage));
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(SubscribersNotFoundExceptionMessage));
                            }
                        }
                        else
                        {
                            taskCompletionSource.SetException(new Exception(EventTypeNotFoundExceptionMessage));
                        }
                    });

            return taskCompletionSource.Task;
        }

        public Task Subscribe<TEvent>(object sender, Func<Task<TEvent>, Task> eventHandlerTaskFactory)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _factory.StartNew(
                () =>
                    {
                        ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;
                        ConcurrentBag<object> eventHandlerTaskFactories;

                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {
                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers.ContainsKey(sender))
                                {
                                    if (subscribers.TryGetValue(sender, out eventHandlerTaskFactories))
                                    {
                                        eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                        taskCompletionSource.SetResult(null);
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                                else
                                {
                                    eventHandlerTaskFactories = new ConcurrentBag<object>();

                                    if (subscribers.TryAdd(sender, eventHandlerTaskFactories))
                                    {
                                        eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                        taskCompletionSource.SetResult(null);
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToAddEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                            }
                        }
                        else
                        {
                            subscribers = new ConcurrentDictionary<object, ConcurrentBag<object>>();

                            if (_hub.TryAdd(eventType, subscribers))
                            {
                                eventHandlerTaskFactories = new ConcurrentBag<object>();

                                if (subscribers.TryAdd(sender, eventHandlerTaskFactories))
                                {
                                    eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                    taskCompletionSource.SetResult(null);
                                }
                                else
                                {
                                    taskCompletionSource.SetException(new Exception(FailedToAddEventHandlerTaskFactoriesExceptionMessage));
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToAddSubscribersExceptionMessage));
                            }
                        }
                    });

            return taskCompletionSource.Task;
        }

        public Task Unsubscribe<TEvent>(object sender)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _factory.StartNew(
                () =>
                    {
                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {
                            ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;

                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers == null)
                                {
                                    taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                                }
                                else
                                {
                                    if (subscribers.ContainsKey(sender))
                                    {
                                        ConcurrentBag<object> eventHandlerTaskFactories;

                                        if (subscribers.TryRemove(sender, out eventHandlerTaskFactories))
                                        {
                                            taskCompletionSource.SetResult(null);
                                        }
                                        else
                                        {
                                            taskCompletionSource.SetException(new Exception(FailedToRemoveEventHandlerTaskFactories));
                                        }
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                            }
                        }
                        else
                        {
                            taskCompletionSource.SetException(new Exception(EventTypeNotFoundExceptionMessage));
                        }
                    });

            return taskCompletionSource.Task;
        }
    }
    
    public static class AsyncEventHubExtensions
    {
        private static readonly AsyncEventHub AsyncEventHub = new AsyncEventHub();

        public static Task<Task[]> Publish<TEvent>(this object sender, Task<TEvent> eventData)
        {
            return AsyncEventHub.Publish(sender, eventData);
        }

        public static Task Subscribe<TEvent>(this object sender, Func<Task<TEvent>, Task> eventHandlerTaskFactory)
        {
            return AsyncEventHub.Subscribe(sender, eventHandlerTaskFactory);
        }

        public static Task Unsubscribe<TEvent>(this object sender)
        {
            return AsyncEventHub.Unsubscribe<TEvent>(sender);
        }
    }
    
    public static class TaskExtensions
    {
        public static Task<T> AsTask<T>(this T value)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetResult(value);
            return taskCompletionSource.Task;
        }

        /// <summary>Creates a Task that has completed in the Faulted state with the specified exception.</summary>
        /// <typeparam name="TResult">Specifies the type of payload for the new Task.</typeparam>
        /// <param name="factory">The target TaskFactory.</param>
        /// <param name="exception">The exception with which the Task should fault.</param>
        /// <returns>The completed Task.</returns>
        public static Task<TResult> FromException<TResult>(this TaskFactory factory, Exception exception)
        {
            var tcs = new TaskCompletionSource<TResult>(factory.CreationOptions);
            tcs.SetException(exception);
            return tcs.Task;
        }
    }
    
    /*
     
                 var p1 = new Program();
            var p2 = new Program();

            p1.Subscribe<Ping>(
                async p =>
                    {
                        Console.Write("Ping... ");
                        await Task.Delay(250);
                        await p1.Publish(new Pong().AsTask());
                    });

            p2.Subscribe<Pong>(
                async p =>
                    {
                        Console.WriteLine("Pong!");
                        await Task.Delay(500);
                        await p2.Publish(new Ping().AsTask());
                    });

            p2.Publish(new Ping().AsTask());

            Console.ReadLine();

            p1.Unsubscribe<Ping>();
            p2.Unsubscribe<Pong>(); 
     */
}