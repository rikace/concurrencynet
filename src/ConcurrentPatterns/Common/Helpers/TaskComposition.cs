namespace ParallelPatterns.TaskComposition
{
    using System;
    using System.Threading.Tasks;

    public static partial class TaskComposition
    {
        // TODO LAB
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        //
        // The idea is to implement an higher order function "Then" that
        // runs the "task" and passes the output type "TIn" into the continuation
        // (CPS) function "next" to map the result into a type "TOut"
        public static Task<TOut> Then<TIn, TOut>(
            this Task<TIn> task,
            Func<TIn, TOut> next)
        {
            var tcs = new TaskCompletionSource<TOut>();

            // TODO
            // complete code Missing here
            task.ContinueWith(_ =>
            {
                if (task.IsFaulted) tcs.TrySetException(task.Exception.InnerExceptions);
                else if (task.IsCanceled) tcs.TrySetCanceled();
                else
                {
                    try
                    {
                        tcs.SetResult(next(task.Result));
                    }
                    catch (Exception exc)
                    {
                        tcs.TrySetException(exc);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        // TODO LAB
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        // This is similar implementation of the previous one plus a continuation step
        public static Task<TOut> Then<TIn, TOut>(
            this Task<TIn> task,
            Func<TIn, Task<TOut>> next)
        {
            var tcs = new TaskCompletionSource<TOut>();

            // TODO
            // complete code Missing here
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
                        else
                            t.ContinueWith(delegate
                            {
                                if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerExceptions);
                                else if (t.IsCanceled) tcs.TrySetCanceled();
                                else tcs.TrySetResult(t.Result);
                            }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    catch (Exception exc)
                    {
                        tcs.TrySetException(exc);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
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
                        else
                            t.ContinueWith(delegate
                            {
                                if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerExceptions);
                                else if (t.IsCanceled) tcs.TrySetCanceled();
                                else tcs.TrySetResult(t.Result);
                            }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    catch (Exception exc)
                    {
                        tcs.TrySetException(exc);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }


        public static Task<TOut> Select<TIn, TOut>(
            this Task<TIn> task,
            Func<TIn, TOut> projection)
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

        public static Task<TOut> SelectMany<TIn, TMid, TOut>(
            this Task<TIn> input,
            Func<TIn, Task<TMid>> f,
            Func<TIn, TMid, TOut> projection)
            => SelectMany(input, outer =>
                SelectMany(f(outer), inner =>
                    Task.FromResult(projection(outer, inner))
                )
            );
    }
}
