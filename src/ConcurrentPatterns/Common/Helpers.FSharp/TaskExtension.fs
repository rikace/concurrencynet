namespace Pipeline.FSharp.TTTT

open System.Runtime.CompilerServices
open System.Threading.Tasks
open System
//  Task Extension in F# to enable Task LINQ-style operators


[<Extension>]
module TaskCompositionEx =
    
    [<Sealed; Extension; CompiledName("TaskEx")>]
    type TaskEx =
        [<Extension>]
        static member Then (input : Task<'T>, 
                            binder :Func<'T, Task<'U>>) =
            let tcs = new TaskCompletionSource<'U>()
            input.ContinueWith(fun (task:Task<'T>) ->
               if (task.IsFaulted) then
                    tcs.SetException(task.Exception.InnerExceptions)
               elif (task.IsCanceled) then tcs.SetCanceled()
               else
                    try
                       (binder.Invoke(task.Result)).ContinueWith(
                            fun(nextTask:Task<'U>) -> tcs.SetResult(nextTask.Result))
                       |> ignore
                    with
                    | ex -> tcs.SetException(ex)) |> ignore
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
        [<Extension>]
        static member Then (input : Task<'T>, 
                            binder :Func<'T, 'U>) =
           let tcs = new TaskCompletionSource<'U>()
           input.ContinueWith(fun (task:Task<'T>) ->
              if task.IsFaulted then
                   tcs.SetException(task.Exception.InnerExceptions)
              elif task.IsCanceled then tcs.SetCanceled()
              else
                   try
                       tcs.SetResult(binder.Invoke(task.Result))
                   with
                   | ex -> tcs.SetException(ex)) |> ignore
           tcs.Task

        [<Extension>]
        static member Select (task : Task<'T>, selector :Func<'T, 'U>) : Task<'U> =
           let r = new TaskCompletionSource<'U>()
           task.ContinueWith(fun (t:Task<'T>) ->
               if t.IsFaulted then r.SetException(t.Exception.InnerExceptions)
               elif t.IsCanceled then r.SetCanceled()
               else r.SetResult(selector.Invoke(t.Result));
               selector.Invoke(t.Result)
           ) |> ignore
           r.Task

        [<Extension>]
        static member SelectMany (input : Task<'T>, binder : Func<'T, Task<'U>>) =
           let tcs = new TaskCompletionSource<'U>()
           input.ContinueWith(fun (task:Task<'T>) ->
              if task.IsFaulted then
                   tcs.SetException(task.Exception.InnerExceptions)
              elif task.IsCanceled then tcs.SetCanceled()
              else
                   try
                       let t = binder.Invoke(task.Result)
                       if t = null then tcs.SetCanceled()
                       else
                           t.ContinueWith(fun(nextT:Task<'U>) ->
                               if nextT.IsFaulted then tcs.TrySetException(nextT.Exception.InnerExceptions);
                               elif nextT.IsCanceled then tcs.TrySetCanceled()
                               else tcs.TrySetResult(nextT.Result);
                           , TaskContinuationOptions.ExecuteSynchronously)
                           |> ignore
                   with
                   | ex -> tcs.SetException(ex)) |> ignore
           tcs.Task 

        [<Extension>]
        static member  SelectMany (input : Task<'T>, 
                                   binder :Func<'T, Task<'U>>, 
                                   projection:Func<'T, 'U, 'R>) =
           TaskEx.SelectMany(input, Func<_,_>(fun outer ->
               TaskEx.SelectMany(binder.Invoke(outer), Func<_,_>(fun inner ->
                   Task.FromResult(projection.Invoke(outer, inner))))))