using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataParallelism
{
    public static class ForkJoin
    {
        public static Task Tee<T>(
            this Task<T> task,
            Action<T> tee)
        {
            var tcs = new TaskCompletionSource<T>();
            task.ContinueWith(delegate
            {
                if (task.IsFaulted) tcs.TrySetException(task.Exception.InnerExceptions);
                else if (task.IsCanceled) tcs.TrySetCanceled();
                else
                {
                    try
                    {
                        var result = task.Result;
                        tee(result);
                        tcs.SetResult(result);
                    }
                    catch (Exception exc)
                    {
                        tcs.TrySetException(exc);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        public static R Invoke<T, R>(Func<R, T, R> reduce, Func<R> seedInit, params Func<Task<T>>[] operations)
        {
            // TODO RT
            // Implement a parallel fork-join
            // Note that the operations run in different tasks "Func<Task<T>>[]"
            // Use either "Parallel.For" collect and aggregate the results
            //             or PLINQ
            //             or run multiple Tasks in parallel and wait/aggregate the results

            return default;
        }

        public static R Invoke<T, R>(Func<R, T, R> reduce, Func<R> seedInit, params Func<T>[] operations)
        {
            // TODO RT
            // Implement a parallel fork-join
            // Note that the operations run in different tasks "Func<Task<T>>[]"
            // Use either "Parallel.For" collect and aggregate the results
            //             or PLINQ
            //             or run multiple Tasks in parallel and wait/aggregate the results

            return default;
        }
    }
}
