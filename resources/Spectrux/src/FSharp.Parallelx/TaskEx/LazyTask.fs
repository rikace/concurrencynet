namespace FSharp.Parallelx.TaskEx

module LazyTask =
    
    open System
    open System.Threading
    open System.Threading.Tasks

    [<RequireQualifiedAccess>]
    module Task =
        
        let private complete (promise: TaskCompletionSource<'a>) (t: Task<'a>) =
            if t.IsCompletedSuccessfully then promise.TrySetResult(t.Result)
            elif t.IsCanceled then promise.TrySetCanceled()
            else promise.TrySetException(t.Exception)
            |> ignore
        
        /// Redirects the result of provided `task` execution into given TaskCompletionSource,
        /// completing it, cancelling or rejecting depending on a given task output.
        let fulfill (promise: TaskCompletionSource<'a>) (task: Task<'a>) =
            if task.IsCompleted then complete promise task // short path for immediately completed tasks
            else task.ContinueWith(Action<_>(complete promise), TaskContinuationOptions.ExecuteSynchronously|||TaskContinuationOptions.AttachedToParent) |> ignore

    /// An object, which is supposed to work like a lazy wrapper,
    /// but for functions that are returning results asynchronously.
    [<Sealed>]
    type LazyTask<'a>(fn: unit -> Task<'a>) = 
        let mutable result: TaskCompletionSource<'a> = null
        let getValue() =
            let promise = TaskCompletionSource<'a>()
            let old = Interlocked.CompareExchange(&result, promise, null)
            if isNull old then
                (Task.Run<'a>(Func<Task<'a>>(fn)) |> Task.fulfill promise)
                promise.Task
            else old.Task
        
        /// Checks if current lazy cell contains a computed (initialized) value.
        member this.HasValue =
            let value = Volatile.Read(&result)
            not (isNull value) && value.Task.IsCompletedSuccessfully
            
        /// Returns a task, which holds a result of initializing function passed to this LazyTask.
        /// Once computed, the same value will be returned in all subsequent calls.
        member this.Value =
            let old = Volatile.Read(&result)
            if isNull old
            then getValue() // since `getValue()` allocates, we only call it if current lazy task was never called (in causal past)
            else old.Task // > 99% of the time value is already computed so no need to reallocate
            

