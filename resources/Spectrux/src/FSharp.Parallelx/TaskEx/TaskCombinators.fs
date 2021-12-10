namespace FSharp.Parallelx.Tasks

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Collections.Concurrent
open FSharp.Parallelx

[<RequireQualifiedAccess>]
module Task =
        
    let inline create (a:'a) : Task<'a> =
        Task.FromResult a
    
    let inline join (t:Task<Task<'a>>) : Task<'a> =
        t.Unwrap()
    
    let inline extend (f:Task<'a> -> 'b) (t:Task<'a>) : Task<'b> =
        t.ContinueWith f
    
    let inline map (f:'a -> 'b) (t:Task<'a>) : Task<'b> =
        extend (fun t -> f t.Result) t
    
    let inline bind (f:'a -> Task<'b>) (t:Task<'a>) : Task<'b> =
        extend (fun t -> f t.Result) t |> join
        
    let mapCts (projection:'a -> 'b) (task: Task<'a>) =
        let r = new TaskCompletionSource<'b>()
        task.ContinueWith(fun (self: Task<_>) ->
            if self.IsFaulted then r.SetException(self.Exception.InnerExceptions)
            elif self.IsCanceled then r.SetCanceled()
            else r.SetResult(projection(self.Result))) |> ignore
        r.Task

    let bindCts (next:'a -> Task<'b>) (first: Task<'a>) =
        let tcs = new TaskCompletionSource<'b>()
        first.ContinueWith((fun (_:Task<_>) ->
            if first.IsFaulted then tcs.TrySetException(first.Exception.InnerExceptions) |> ignore
            elif first.IsCanceled then tcs.TrySetCanceled() |> ignore
            else
                try
                    let t = next first.Result
                    if isNull t then tcs.TrySetCanceled() |> ignore
                    else t.ContinueWith((fun (_:Task<_>) ->
                        if t.IsFaulted then tcs.TrySetException(t.Exception.InnerExceptions) |> ignore
                        elif t.IsCanceled then tcs.TrySetCanceled() |> ignore
                        else tcs.TrySetResult(t.Result) |> ignore
                    ), TaskContinuationOptions.ExecuteSynchronously) |> ignore
                with
                | exc -> tcs.TrySetException(exc) |> ignore

        ), TaskContinuationOptions.ExecuteSynchronously) |> ignore
        tcs.Task
        
    /// Transforms a Task's first value by using a specified mapping function.
    let inline map' f (m: Task<_>) =
        m.ContinueWith(fun (t: Task<_>) -> f t.Result)
            
    let singleton value = value |> Task.FromResult

    let bind' (f : 'a -> Task<'b>) (x : Task<'a>) = task {
      let! x = x
      return! f x
      }

    let apply' f x =
        bind (fun f' ->
          bind (fun x' -> singleton(f' x')) x) f

    let map'' f x = x |> bind (f >> singleton)

    let map2 f x y =
        (apply' (apply' (singleton f) x) y)

    let map3 f x y z =
        apply' (map2 f x y) z
        

    let inline bindWithOptions (token: CancellationToken) (continuationOptions: TaskContinuationOptions) (scheduler: TaskScheduler) (f: 'T -> Task<'U>) (m: Task<'T>) =
        m.ContinueWith((fun (x: Task<_>) -> f x.Result), token, continuationOptions, scheduler).Unwrap()

    let inline bind'' (f: 'T -> Task<'U>) (m: Task<'T>) =
        m.ContinueWith(fun (x: Task<_>) -> f x.Result).Unwrap()

    let inline returnM a =
        let s = TaskCompletionSource()
        s.SetResult a
        s.Task

    /// Promote a function to a monad/applicative, scanning the monadic/applicative arguments from left to right.
    let inline lift2 f a b =
        // a >>= fun aa -> b >>= fun bb -> f aa bb |> returnM        
        bind (fun aa -> bind(fun bb -> f aa bb |> returnM) b) a 

    /// Sequential application
    let inline apply f x = lift2 id f x
        
    let inline kleisli (f:'a -> Task<'b>) (g:'b -> Task<'c>) (x:'a) = bind g (f x)  // (f x) >>= g
     
module TaskOperators =    
    /// Sequential application
    let inline (<*>) f x = Task.apply x f

    /// Infix map
    let inline (<!>) f x = Task.map f x

    /// Sequence actions, discarding the value of the first argument.
    let inline ( *>) a b = Task.lift2 (fun _ z -> z) a b

    /// Sequence actions, discarding the value of the second argument.
    let inline ( <*) a b = Task.lift2 (fun z _ -> z) a b
        
    /// Sequentially compose two actions, passing any value produced by the first as an argument to the second.
    let inline (>>=) m f = Task.bind f m

    /// Flipped >>=
    let inline (=<<) f m = Task.bind f m

    /// Left-to-right Kleisli composition
    let inline (>=>) f g = fun x -> f x >>= g
    
    /// Right-to-left Kleisli composition
    let inline (<=<) x = flip (>=>) x
    
    
module TaskResult =
   open System
   
   type TaskResult<'a> = | TR of Task<Result<'a, exn>>
   
   let ofTaskResult (TR x) = x
   let arr f = TR f
   
   let handler (operation : Task<'a>) =
       let tcs = new TaskCompletionSource<Result<'a, exn>>()
       operation.ContinueWith(
          new Action<Task>(fun _ ->
           if operation.IsFaulted then tcs.SetResult(Result.Error (operation.Exception.InnerException))
            else if operation.IsCanceled then tcs.SetResult(Result.Error (new OperationCanceledException() :> Exception))
            else tcs.SetResult(Result.Ok(operation.Result)))) |> ignore
       tcs.Task |> TR
        
   let map (f: 'a -> 'b) (task : TaskResult<'a>) =
       let operation = (ofTaskResult task)
       let tcs = new TaskCompletionSource<Result<'b, exn>>()
       operation.ContinueWith(fun (t: Task<Result<'a, exn>>) ->
           new Action<Task>(fun _ ->
               if operation.IsFaulted then tcs.SetResult(Result.Error (operation.Exception.InnerException))
               else if operation.IsCanceled then tcs.SetResult(Result.Error (new OperationCanceledException() :> Exception))
               else
                   match t.Result with
                   | Ok res ->  tcs.SetResult(Result.Ok(f res))
                   | Error err -> tcs.SetResult(Result.Error(err))
           )) |> ignore
       tcs.Task |> TR               

   let bind (f: 'a -> TaskResult<'b>) (task : TaskResult<'a>) =
       let operation = (ofTaskResult task)
       let tcs = new TaskCompletionSource<Result<'b, exn>>()
       operation.ContinueWith(fun (t1: Task<Result<'a, exn>>) ->
           new Action<Task>(fun _ ->
               if operation.IsFaulted then tcs.SetResult(Result.Error (operation.Exception.InnerException))
               else if operation.IsCanceled then tcs.SetResult(Result.Error (new OperationCanceledException() :> Exception))
               else
                   match t1.Result with
                   | Ok res -> ((f res) |> ofTaskResult)
                                   .ContinueWith(fun (t2: Task<Result<'b, exn>>) ->
                                            if t2.IsFaulted then tcs.SetResult(Result.Error (operation.Exception.InnerException))
                                            else if t2.IsCanceled then tcs.SetResult(Result.Error (new OperationCanceledException() :> Exception))
                                            else
                                                match t2.Result with
                                                | Error err -> tcs.SetResult(Result.Error(err))
                                                | Ok res -> tcs.SetResult(Result.Ok res)) |> ignore
                   | Error err -> tcs.SetResult(Result.Error(err))
           )) |> ignore
       tcs.Task |> TR   