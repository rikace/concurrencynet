namespace Pipeline.FSharp

open System.Runtime.CompilerServices
open System.Threading.Tasks
open System
//  Task Extension in F# to enable Task LINQ-style operators

[<Extension>]
module TaskCompositionEx =

    [<Sealed; Extension; CompiledName("TaskEx")>]
    type TaskEx =

        // TODO LAB
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        [<Extension>]
        static member inline Then (task : Task<'T>,
                            binder :Func<'T, Task<'U>>) =
            let tcs = new TaskCompletionSource<'U>()
            // TODO
            // implement missing code
            // Use the "TaskCompletionSource<'U>" object to ease the implementation.
            //
            // The idea is to implement an higher order function "Then" that
            // runs the "task" and passes the output type "'T" into the continuation
            // (CPS) function "binder" to map the result into a type "'U"
            tcs.Task

        // TODO LAB
        // implement missing code
        // This is similar implementation of the previous one plus a continuation step
        [<Extension>]
        static member  Then (input : Task<'T>,
                             binder :Func<'T, Task<'U>>,
                             projection:Func<'T, 'U, 'R>) =
           TaskEx.Then(input, Func<_,_>(fun outer ->
               TaskEx.Then(binder.Invoke(outer), Func<_,_>(fun inner ->
                   Task.FromResult(projection.Invoke(outer, inner))))))

        // TODO LAB
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        [<Extension>]
        static member Then (input : Task<'T>,
                            binder :Func<'T, 'U>) =
           let tcs = new TaskCompletionSource<'U>()
            // complete code Missing here
           tcs.Task

        [<Extension>]
        static member Select (task : Task<'T>, selector :Func<'T, 'U>) : Task<'U> =
           let r = new TaskCompletionSource<'U>()
           // implement missing code
           // YOU CAN REPLACE THE "TASK THEN" IMPLEMENTATION
           r.Task

        [<Extension>]
        static member SelectMany (input : Task<'T>, binder : Func<'T, Task<'U>>) =
           let tcs = new TaskCompletionSource<'U>()
           // implement missing code
           // YOU CAN REPLACE THE "TASK THEN" IMPLEMENTATION
           tcs.Task

        [<Extension>]
        static member  SelectMany (input : Task<'T>,
                                   binder :Func<'T, Task<'U>>,
                                   projection:Func<'T, 'U, 'R>) =
           TaskEx.SelectMany(input, Func<_,_>(fun outer ->
               TaskEx.SelectMany(binder.Invoke(outer), Func<_,_>(fun inner ->
                   Task.FromResult(projection.Invoke(outer, inner))))))
