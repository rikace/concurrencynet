using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataParallelism
{
    public static class ForkJoin
    {
        public static R Invoke<T, R>(Func<R, T, R> reduce, Func<R> seedInit, params Func<T>[] operations)
        {
            var tasks = (from op in operations
                select Task.Run(op)).ToArray();
            Task.WhenAll(tasks);
            return tasks.Select(t => t.Result).Aggregate(seedInit(), reduce);
        }

        public static R InvokeParChildRelationship<T, R>(Func<R, T, R> reduce, Func<R> seedInit,
            params Func<T>[] operations)
        {
            var results = new T[operations.Length];
            var task = Task.Run(() =>
            {
                for (int i = 0; i < operations.Length; i++)
                {
                    int index = i;
                    Task.Factory.StartNew(() => results[index] = operations[index](),
                        TaskCreationOptions.AttachedToParent);
                }
            });
            Task.WhenAny(task);
            return results.Aggregate(seedInit(), reduce);
        }

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

            var results = new T[operations.Length];
            var tasks = new Task[operations.Length];
            for (int i = 0; i < operations.Length; i++)
            {
                int index = i;
                var task = operations[index]().Tee(result => results[index] = result);
                tasks[index] = task;
            }

            Task.WaitAll(tasks);
            return results.Aggregate(seedInit(), reduce);
        }

        public static R InvokeParallelLoop<T, R>(Func<R, T, R> reduce, Func<R> seedInit, params Func<T>[] operations)
        {
            var results = new T[operations.Length];
            Parallel.For(0, operations.Length, i => results[i] = operations[i]());
            return results.Aggregate(seedInit(), reduce);
        }

        public static R InvokePLINQ<T, R>(Func<R, T, R> reduce, Func<R> seedInit, params Func<T>[] operations)
        {
            return operations.AsParallel().Select(f => f()).Aggregate(seedInit(), reduce);
        }
    }
}
