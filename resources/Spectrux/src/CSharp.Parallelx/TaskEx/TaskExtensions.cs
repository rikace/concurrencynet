namespace CSharp.Parallelx.ParallelSIMD
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {

        public static Task ToTask(this WaitHandle wh, CancellationToken token = default, int timeout = -1)
        {
            var tcs = new TaskCompletionSource<object>();

            RegisteredWaitHandle registeredHandle = null;
            var cancelTokenReg = token.Register(() => tcs.TrySetCanceled());

            if (token.IsCancellationRequested)
                return tcs.Task;

            registeredHandle = ThreadPool.UnsafeRegisterWaitForSingleObject(
                wh, (state, timeoutObj) =>
                {
                    cancelTokenReg.Dispose();
                    if (timeoutObj) tcs.TrySetException(new TimeoutException());
                    else
                        tcs.TrySetResult(null);
                    GC.KeepAlive(registeredHandle);
                }, null, timeout, true);
            return tcs.Task;
        }


        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            return Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(dop)
                select Task.Run(async delegate
                {
                    using (partition)
                        while (partition.MoveNext())
                            await body(partition.Current);
                }));
        }

        public static Task ForEachAsyncConcurrent<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            var partitions = Partitioner.Create(source).GetPartitions(dop);
            var tasks = partitions.Select(async partition =>
            {
                using (partition)
                    while (partition.MoveNext())
                        await body(partition.Current);
            });

            return Task.WhenAll(tasks);
        }

        public static async Task WithCancellation(this Task task, TimeSpan cancelAfter,  CancellationToken token = default)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            linkedTokenSource.CancelAfter(cancelAfter);

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = linkedTokenSource.Token.Register(state =>
            {
                ((TaskCompletionSource<object>)state).TrySetResult(null);
            }, tcs, useSynchronizationContext: false);

            var resultTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
            if (resultTask == tcs.Task)
            {
                // Operation cancelled
                throw new OperationCanceledException(token.IsCancellationRequested ? token : linkedTokenSource.Token);
            }

            await task.ConfigureAwait(false);
        }
        
        
        public static async Task ExecuteInParallelWithDegreeOfParallelism<T>(this IEnumerable<T> collection,
            Func<T, Task> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                T item;
                while (queue.TryDequeue(out item))
                    await processor(item);
            });

            await Task.WhenAll(tasks);
        }

        private static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
                @this.Add(element);
        }

        public static async Task<IEnumerable<R>> ProjectInParallelWithDegreeOfParallelism<T, R>(
            this IEnumerable<T> collection,
            Func<T, Task<R>> processor,
            int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
            var results = new ConcurrentBag<R>();
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                List<R> localResults = new List<R>();
                T item;
                while (queue.TryDequeue(out item))
                {
                    var result = await processor(item);
                    localResults.Add(result);
                }

                results.AddRange(localResults);
            });

            await Task.WhenAll(tasks);
            return results;
        }
        
               public static Task<T> Catch<T, TError>(this Task<T> task, Func<TError, T> onError) where TError : Exception
        {
            var tcs = new TaskCompletionSource<T>();    // #A
            task.ContinueWith(innerTask =>
            {
                if (innerTask.IsFaulted && innerTask?.Exception?.InnerException is TError)
                    tcs.SetResult(onError((TError)innerTask.Exception.InnerException)); // #B
                else if (innerTask.IsCanceled)
                    tcs.SetCanceled();      // #B
                else if (innerTask.IsFaulted)
                    tcs.SetException(innerTask?.Exception?.InnerException ?? throw new InvalidOperationException()); // #B
                else
                    tcs.SetResult(innerTask.Result);  // #B
            });
            return tcs.Task;
        }

        //Listing 10.24  C# asynchronous lift functions
        public static Task<TOut> LifTMid<TIn, TMid, TOut>(Func<TIn, TMid, TOut> selector, Task<TIn> item1, Task<TMid> item2) // #A
        {
            // Func<TIn, Func<TMid, R>> curry = x => y => selector(x, y);    // #B

            var lifted1 = Pure(Functional.Curry(selector));              // #C
            var lifted2 = Apply(item1, lifted1);    // #D
            return Apply(item2, lifted2);           // #D
        }

        public static Task<TOut> LifTOut<TIn, TMid1, TMid2, TOut>(Func<TIn, TMid1, TMid2, TOut> selector, Task<TIn> item1, Task<TMid1> item2, Task<TMid2> item3)    // #A
        {
            //Func<TIn, Func<TMid1, Func<TMid2, TOut>>> curry = x => y => z => selector(x, y, z); // #B

            var lifted1 = Pure(Functional.Curry(selector));              // #C
            var lifted2 = Apply(item1, lifted1);    // #D
            var lifted3 = Apply(item2, lifted2);    // #D
            return Apply(item3, lifted3);           // #D
        }

        public static Task<TOut> Fmap<TIn, TOut>(this Task<TIn> input, Func<TIn, TOut> map) => input.ContinueWith(t => map(t.Result));

        public static Task<TOut> Map<TIn, TOut>(this Task<TIn> input, Func<TIn, TOut> map) => input.ContinueWith(t => map(t.Result));

        public static Task<T> Return<T>(this T input) => Task.FromResult(input);

        public static Task<T> Pure<T>(T input) => Task.FromResult(input);

        public static Task<TOut> Apply<TIn, TOut>(this Task<TIn> task, Task<Func<TIn, TOut>> liftedFn)
        {
            var tcs = new TaskCompletionSource<TOut>();
            liftedFn.ContinueWith(innerLiftTask =>
                task.ContinueWith(innerTask =>
                    tcs.SetResult(innerLiftTask.Result(innerTask.Result))
            ));
            return tcs.Task;
        }

        public static Task<TOut> Apply<TIn, TOut>(this Task<Func<TIn, TOut>> liftedFn, Task<TIn> task) => task.Apply(liftedFn);

        public static Task<TOut> Select<TIn, TOut>(this Task<TIn> task, Func<TIn, TOut> projection)
        {
            var r = new TaskCompletionSource<TOut>();
            task.ContinueWith(self =>
            {
                if (self.IsFaulted) r.SetException(self.Exception.InnerExceptions);
                else if (self.IsCanceled) r.SetCanceled();
                else r.SetResult(projection(self.Result));
            });
            return r.Task;
        }

        public static Task<Func<TMid, TOut>> Apply<TIn, TMid, TOut>(this Task<Func<TIn, TMid, TOut>> liftedFn, Task<TIn> input)
            => input.Apply(liftedFn.Fmap(Functional.Curry));

        public static Task<TOut> Bind<TIn, TOut>(this Task<TIn> input, Func<TIn, Task<TOut>> f)
        {
            var tcs = new TaskCompletionSource<TOut>();
            input.ContinueWith(x =>
                f(x.Result).ContinueWith(y =>
                    tcs.SetResult(y.Result)));
            return tcs.Task;
        }

        public static Task<TOut> SelectMany<TIn, TOut>(this Task<TIn> first, Func<TIn, Task<TOut>> next)
        {
            var tcs = new TaskCompletionSource<TOut>();
            first.ContinueWith(delegate
            {
                if (first.IsFaulted) tcs.TrySetException(first.Exception.InnerExceptions);
                else if (first.IsCanceled) tcs.TrySetCanceled();
                else
                {
                    try
                    {
                        var t = next(first.Result);
                        if (t == null) tcs.TrySetCanceled();
                        else t.ContinueWith(delegate
                        {
                            if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerExceptions);
                            else if (t.IsCanceled) tcs.TrySetCanceled();
                            else tcs.TrySetResult(t.Result);
                        }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    catch (Exception exc) { tcs.TrySetException(exc); }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        public static Task<TOut> SelectMany<TIn, TMid, TOut>(
          this Task<TIn> input, Func<TIn, Task<TMid>> f, Func<TIn, TMid, TOut> projection)
        {
            return Bind(input, outer =>
                   Bind(f(outer), inner =>
                   Return(projection(outer, inner))));
        }

        public static Task<TOut> Next<TIn, TOut>(this Task<TIn> task, Func<TIn, Task<TOut>> next)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<TOut>();
            task.ContinueWith(delegate
            {
                if (task.IsFaulted) tcs.TrySetException(task.Exception.InnerExceptions);
                else if (task.IsCanceled) tcs.TrySetCanceled();
                else
                {
                    try
                    {
                        var t = next(task.Result);
                        if (t == null) tcs.TrySetCanceled();
                        else t.ContinueWith(delegate
                        {
                            if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerExceptions);
                            else if (t.IsCanceled) tcs.TrySetCanceled();
                            else tcs.TrySetResult(t.Result);
                        }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    catch (Exception exc) { tcs.TrySetException(exc); }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        public static IEnumerable<Task<T>> ProcessAsComplete<T>(this IEnumerable<Task<T>> inputTasks)
        {
            // Copy the input so we know it’ll be stable, and we don’t evaluate it twice
            var inputTaskList = inputTasks.ToList();
            // Could use Enumerable.Range here, if we wanted…
            var completionSourceList = new List<TaskCompletionSource<T>>(inputTaskList.Count);
            for (int i = 0; i < inputTaskList.Count; i++)
            {
                completionSourceList.Add(new TaskCompletionSource<T>());
            }

            // At any one time, this is "the index of the box we’ve just filled".
            // It would be nice to make it nextIndex and start with 0, but Interlocked.Increment
            // returns the incremented value…
            int prevIndex = -1;

            // We don’t have to create this outside the loop, but it makes it clearer
            // that the continuation is the same for all tasks.
            Action<Task<T>> continuation = completedTask =>
            {
                int index = Interlocked.Increment(ref prevIndex);
                var source = completionSourceList[index];
                switch (completedTask.Status)
                {
                    case TaskStatus.Canceled:
                        source.TrySetCanceled();
                        break;
                    case TaskStatus.Faulted:
                        source.TrySetException(completedTask.Exception.InnerExceptions);
                        break;
                    case TaskStatus.RanToCompletion:
                        source.TrySetResult(completedTask.Result);
                        break;
                    default:
                        throw new ArgumentException("Task was not completed");
                }
            };

            foreach (var inputTask in inputTaskList)
            {
                inputTask.ContinueWith(continuation,
                                       CancellationToken.None,
                                       TaskContinuationOptions.ExecuteSynchronously,
                                       TaskScheduler.Default);
            }

            return completionSourceList.Select(source => source.Task);
        }

        public static Task<IEnumerable<T>> Traverese<T>(this IEnumerable<Task<T>> sequence)
        {
            return sequence.Aggregate(
                Task.FromResult(Enumerable.Empty<T>()),
                (eventualAccumulator, eventualItem) =>
                    from accumulator in eventualAccumulator
                    from item in eventualItem
                    select accumulator.Concat(new[] { item }).ToArray().AsEnumerable());
        }
    }
}