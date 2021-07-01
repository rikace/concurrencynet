namespace FSharp.Parallelx.Tasks

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open FSharp.Parallelx

module AsyncInterop =
    let private (|IsFaulted|IsCanceled|IsCompleted|) (task: Task) =
        if task.IsFaulted then IsFaulted task.Exception
        else if task.IsCanceled then IsCanceled
        else IsCompleted

    let private safeToken (ct: CancellationToken option) =
        match ct with
        | Some token -> token
        | None -> Async.DefaultCancellationToken

    let createCanceledException(token: CancellationToken option) =
        try
          match token with
          | Some t -> new OperationCanceledException(t) |> raise
          | None -> new OperationCanceledException() |> raise
        with :? OperationCanceledException as e -> e


    let asTaskT(async: Async<'T>, ct: CancellationToken option) =
        let tcs = TaskCompletionSource<'T>()
        Async.StartWithContinuations(
          async,
          tcs.SetResult,
          tcs.SetException,
          tcs.SetException, // Derived from original OperationCancelledException
          safeToken ct)
        tcs.Task

    let asValueTask(async: Async<Unit>, ct: CancellationToken option) =
        let tcs = TaskCompletionSource<Unit>()
        Async.StartWithContinuations(
          async,
          tcs.SetResult,
          tcs.SetException,
          tcs.SetException, // Derived from original OperationCancelledException
          safeToken ct)
        ValueTask(tcs.Task)

    let asValueTaskT(async: Async<'T>, ct: CancellationToken option) =
        let tcs = TaskCompletionSource<'T>()
        Async.StartWithContinuations(
          async,
          tcs.SetResult,
          tcs.SetException,
          tcs.SetException, // Derived from original OperationCancelledException
          safeToken ct)
        ValueTask<'T>(tcs.Task)

    let asAsync(task: Task, ct: CancellationToken option) =
        Async.FromContinuations(
          fun (completed, caught, canceled) ->
            task.ContinueWith(
              new Action<Task>(fun _ ->
                match task with
                | IsFaulted exn -> caught(exn)
                | IsCanceled -> canceled(createCanceledException ct) // TODO: how to extract implicit caught exceptions from task?
                | IsCompleted -> completed(())),
              safeToken ct)
            |> ignore)

    let asAsyncT(task: Task<'T>, ct: CancellationToken option) =
        Async.FromContinuations(
          fun (completed, caught, canceled) ->
            task.ContinueWith(
              new Action<Task<'T>>(fun _ ->
                match task with
                | IsFaulted exn -> caught(exn)
                | IsCanceled -> canceled(createCanceledException ct) // TODO: how to extract implicit caught exceptions from task?
                | IsCompleted -> completed(task.Result)),
              safeToken ct)
            |> ignore)



type TaskResult<'T> =
    /// Task was canceled
    | Canceled
    /// Unhandled exception in task
    | Error of exn
    /// Task completed successfully
    | Successful of 'T
    with
    static member run (t: unit -> Task<_>) =
        try
            let task = t()
            task.Result |> TaskResult.Successful
        with
        | :? OperationCanceledException -> TaskResult.Canceled
        | :? AggregateException as e ->
            match e.InnerException with
            | :? TaskCanceledException -> TaskResult.Canceled
            | _ -> TaskResult.Error e
        | e -> TaskResult.Error e

module TaskExtensions =

    let inline runAndIgnore (f:'a -> unit) v =
        fun () -> Task.Run(new Action(fun () -> f v))

    let run (t: unit -> Task<_>) =
        try
            let task = t()
            task.Result |> TaskResult.Successful
        with
        | :? OperationCanceledException -> TaskResult.Canceled
        | :? AggregateException as e ->
            match e.InnerException with
            | :? TaskCanceledException -> TaskResult.Canceled
            | _ -> TaskResult.Error e
        | e -> TaskResult.Error e


    let toTaskUnit (t:Task) =
      let continuation _ = ()
      t.ContinueWith continuation

    let inline ofUnit (t : Task)=
        let inline cont (t : Task) =
            if t.IsFaulted
            then raise t.Exception
            else ()
        t.ContinueWith cont


    /// Creates a task that runs the given task and ignores its result.
    let inline Ignore t = Task.bind (fun _ -> Task.returnM ()) t

    /// Creates a task that executes a specified task.
    /// If this task completes successfully, then this function returns Choice1Of2 with the returned value.
    /// If this task raises an exception before it completes then return Choice2Of2 with the raised exception.
    let catch (t:Task<'a>) =
        task {
            try
                let! r = t
                return Choice1Of2 r
            with e ->
                return Choice2Of2 e
        }

    let catchCts (task: Task<_>) =
      let tcs = new TaskCompletionSource<Result<_, _>>()
      task.ContinueWith(fun (innerTask: Task<_>) ->
        if innerTask.IsFaulted && (isNull >> not) innerTask.Exception then
          tcs.SetResult(Result.Error innerTask.Exception.InnerException)
        elif innerTask.IsCanceled then
          tcs.SetResult(Result.Error (OperationCanceledException() :> exn))
        else
          tcs.SetResult(Result.Ok innerTask.Result)
      ) |> ignore
      tcs.Task

    let inline private applyTask fromInc toExc stride f =
        let mutable i = fromInc
        while i < toExc do
            f i
            i <- i + stride


    let toAsync (t: Task<'T>): Async<'T> =
        let abegin (cb: AsyncCallback, state: obj) : IAsyncResult =
            match cb with
            | null -> upcast t
            | cb ->
                t.ContinueWith(fun (_ : Task<_>) -> cb.Invoke t) |> ignore
                upcast t
        let aend (r: IAsyncResult) =
            (r :?> Task<'T>).Result
        Async.FromBeginEnd(abegin, aend)


    let thenTask (input : Task<'T>, binder : 'T -> Task<'U>) =
        let tcs = new TaskCompletionSource<'U>()
        input.ContinueWith((fun (task:Task<'T>) ->
           if (task.IsFaulted) then
                tcs.SetException(task.Exception.InnerExceptions)
           elif (task.IsCanceled) then tcs.SetCanceled()
           else
                try
                    (binder(task.Result)).ContinueWith((fun (nextTask:Task<'U>) ->
                        if nextTask.IsFaulted then tcs.SetException(nextTask.Exception.InnerException)
                        elif nextTask.IsCanceled then tcs.SetCanceled()
                        else
                            tcs.SetResult(nextTask.Result))
                            ,TaskContinuationOptions.ExecuteSynchronously) |> ignore
                with
                | ex -> tcs.SetException(ex)), TaskContinuationOptions.ExecuteSynchronously) |> ignore
        tcs.Task

    type System.Threading.Tasks.Task with
        // Creates a task that executes all the given tasks.
        static member Parallel (tasks : seq<unit -> Task<'a>>) =
            tasks
            |> Seq.map (fun t -> t())
            |> Array.ofSeq
            |> Task.WhenAll

        /// Creates a task that executes all the given tasks.
        /// The paralelism is throttled, so that at most `throttle` tasks run at one time.
        static member ParallelWithTrottle throttle (tasks : seq<unit -> Task<'a>>) : (Task<'a[]>) =
            let semaphore = new SemaphoreSlim(throttle)
            let throttleTask (t:unit->Task<'a>) () : Task<'a> =
                task {
                    do! semaphore.WaitAsync() |> toTaskUnit
                    let! result = catch <| t()
                    semaphore.Release() |> ignore
                    return match result with
                           | Choice1Of2 r -> r
                           | Choice2Of2 e -> raise e
                }
            tasks
            |> Seq.map throttleTask
            |> Task.Parallel

        /// Returns a cancellation token which is cancelled when the IVar is set.
        static member intoCancellationToken (cts:CancellationTokenSource) (t:Task<_>) =
            t.ContinueWith (fun (t:Task<_>) -> cts.Cancel ()) |> ignore

        /// Returns a cancellation token which is cancelled when the IVar is set.
        static member asCancellationToken (t:Task<_>) =
            let cts = new CancellationTokenSource ()
            Task.intoCancellationToken cts t
            cts.Token



        static member ForStride (fromInclusive : int) (toExclusive :int) (stride : int) (f : int -> unit) =
            let numStrides = (toExclusive-fromInclusive)/stride
            if numStrides > 0 then
                let numTasks = Math.Min(Environment.ProcessorCount,numStrides)
                let stridesPerTask = numStrides/numTasks
                let elementsPerTask = stridesPerTask * stride;
                let mutable remainderStrides = numStrides - (stridesPerTask*numTasks)

                let taskArray : Task[] = Array.zeroCreate numTasks
                let mutable index = 0
                for i = 0 to taskArray.Length-1 do
                    let toExc =
                        if remainderStrides = 0 then
                            index + elementsPerTask
                        else
                            remainderStrides <- remainderStrides - 1
                            index + elementsPerTask + stride
                    let fromInc = index;

                    taskArray.[i] <- Task.Factory.StartNew(fun () -> applyTask fromInc toExc stride f)
                    index <- toExc

                Task.WaitAll(taskArray)

        static member inline ofUnit (t : Task)=
          let inline cont (t : Task) =
              if t.IsFaulted
              then raise t.Exception
              else ()
          t.ContinueWith cont

        static member toAsync (t: Task<'T>): Async<'T> =
            let abegin (cb: AsyncCallback, state: obj) : IAsyncResult =
                match cb with
                | null -> upcast t
                | cb ->
                    t.ContinueWith(fun (_ : Task<_>) -> cb.Invoke t) |> ignore
                    upcast t
            let aend (r: IAsyncResult) =
                (r :?> Task<'T>).Result
            Async.FromBeginEnd(abegin, aend)

        static member asAsync (task: Task<'T>, token: CancellationToken option) =
          Async.FromContinuations(
            fun (completed, caught, canceled) ->
              let token = defaultArg token Async.DefaultCancellationToken
              task.ContinueWith(
                new Action<Task<'T>>(fun _ ->
                  if task.IsFaulted then caught(task.Exception)
                  else if task.IsCanceled then canceled(new OperationCanceledException(token) |> raise)
                  else completed(task.Result)),
                  token)
              |> ignore)

        static member asAsync (task: Task, token: CancellationToken option) =
          Async.FromContinuations(
            fun (completed, caught, canceled) ->
              let token = defaultArg token Async.DefaultCancellationToken
              task.ContinueWith(
                new Action<Task>(fun _ ->
                  if task.IsFaulted then caught(task.Exception)
                  else if task.IsCanceled then canceled(new OperationCanceledException(token) |> raise)
                  else completed()),
                  token)
              |> ignore)

    [<Extension>]
    type TaskInteropExtensions =
        [<Extension>]
        static member AsAsync (task: Task) =
          Task.asAsync (task, None)

        [<Extension>]
        static member AsAsync (task: Task, token: CancellationToken) =
          Task.asAsync (task, Some token)

        [<Extension>]
        static member AsAsync (task: Task<'T>) =
          Task.asAsync (task, None)

        [<Extension>]
        static member AsAsync (task: Task<'T>, token: CancellationToken) =
          Task.asAsync (task, Some token)

