namespace Pipeline.FSharp.TTTT

open System.Runtime.CompilerServices
open System.Threading.Tasks
open System
//  Task Extension in F# to enable Task LINQ-style operators


[<Extension>]
module TaskCompositionEx =

    [<Sealed; Extension; CompiledName("TaskEx")>]
    type TaskEx =

        // TODO (1)
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        [<Extension>]
        static member Then (input : Task<'T>,
                            binder :Func<'T, Task<'U>>) =
            let tcs = new TaskCompletionSource<'U>()
            // TODO
            // complete code Missing here
            tcs.Task

        // TODO (1)
        // implement missing code
        [<Extension>]
        static member  Then (input : Task<'T>,
                             binder :Func<'T, Task<'U>>,
                             projection:Func<'T, 'U, 'R>) =
           TaskEx.Then(input, Func<_,_>(fun outer ->
               TaskEx.Then(binder.Invoke(outer), Func<_,_>(fun inner ->
                   Task.FromResult(projection.Invoke(outer, inner))))))

        // TODO (1)
        // implement missing code
        // Use the "TaskCompletionSource<TOut>" object to ease the implementation.
        [<Extension>]
        static member Then (input : Task<'T>,
                            binder :Func<'T, 'U>) =
           let tcs = new TaskCompletionSource<'U>()
            // TODO
            // complete code Missing here
           tcs.Task

        [<Extension>]
        static member Select (task : Task<'T>, selector :Func<'T, 'U>) : Task<'U> =
           let r = new TaskCompletionSource<'U>()
           // TODO (1)
           // implement missing code
           // YOU CAN REPLACE THE "TASK THEN" IMPLEMENTATION
           r.Task

        [<Extension>]
        static member SelectMany (input : Task<'T>, binder : Func<'T, Task<'U>>) =
           let tcs = new TaskCompletionSource<'U>()
           // TODO (1)
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
