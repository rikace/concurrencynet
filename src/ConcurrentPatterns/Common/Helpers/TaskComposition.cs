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

            return tcs.Task;
        }

        public static Task<TOut> SelectMany<TIn, TOut>(this Task<TIn> task, Func<TIn, Task<TOut>> projection) => Then(task, projection);

        public static Task<TOut> Select<TIn, TOut>(this Task<TIn> task, Func<TIn, TOut> projection) => Then(task, projection);

        public static Task<TOut> SelectMany<TIn, TMid, TOut>(
            this Task<TIn> input,
            Func<TIn, Task<TMid>> f,
            Func<TIn, TMid, TOut> projection)
            => Then(input, outer =>
                Then(f(outer), inner =>
                    Task.FromResult(projection(outer, inner))
                )
            );
    }
}
