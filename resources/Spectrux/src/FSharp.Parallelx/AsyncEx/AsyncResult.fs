namespace FSharp.Parallelx.AsyncEx

module AsyncHandler =

    open System
    open FSharp.Parallelx.ResultEx
    
     [<NoComparison;NoEquality>]
    type AsyncResult<'a> = | AR of Async<Result<'a, exn>>
    with
        static member ofChoice value =
            match value with
            | Choice1Of2 value -> Result.Ok value
            | Choice2Of2 e -> Result.Error e

        static member ofOption optValue =
            match optValue with
            | Some value -> Result.Ok value
            | None -> Result.Error (Exception())

    let ofAsyncResult (AR x) = x
    let arr f = AR f

    [<RequireQualifiedAccess>]
    module AsyncResult =     
        let handler operation = AR <| async {
            let! result = Async.Catch operation
            return result |> AsyncResult<_>.ofChoice }
                
        // Implementation of mapHanlder Async-Combinator
        let mapHandler (continuation:'a -> Async<'b>)  (comp : AsyncResult<'a>) = async {
            //Evaluate the outcome of the first future
            let! result = ofAsyncResult comp  
            // Apply the mapping on success
            match result with
            | Result.Ok r -> return! handler (continuation r) |> ofAsyncResult 
            | Result.Error e -> return Result.Error e      
        }
        
        let wrap (computation:Async<'a>) =
            AR <| async {
                let! choice = (Async.Catch computation)
                return (AsyncResult<'a>.ofChoice choice)
            }
    
        let wrapOptionAsync (computation:Async<'a option>) =
            AR <| async {
                let! choice = computation
                return (AsyncResult<'a>.ofOption choice)
            }

        let value x = 
            wrap (async { return x })
    
        ///Map the success outcome of a future
        let flatMap (f:'a -> AsyncResult<'b>) (future: AsyncResult<'a>) = 
            AR <| async {
                let! outcome = ofAsyncResult future
                match outcome with
                | Result.Ok value -> return! (f >> ofAsyncResult) value
                | Result.Error e -> return (Result.Error e)
            }
    
        let mapAr f = 
            let f' value = wrap (async { return (f value) })
            in flatMap f'

        let compose f g = f >> (flatMap g)

        let retn x = Ok x |> Async.retn |> AR
         
        let map mapper asyncResult =
            asyncResult |> Async.map (Result.map mapper) |> AR
            
    
        let bind (binder : 'a -> AsyncResult<'b>) (asyncResult : AsyncResult<'a>) : AsyncResult<'b> = 
            let fSuccess value = 
                value |> (binder >> ofAsyncResult)
            
            let fFailure errs = 
                errs
                |> Error
                |> Async.retn
            
            asyncResult
            |> ofAsyncResult
            |> Async.bind (Result.either fSuccess fFailure)
            |> AR          
    
        let ofTask aTask = 
            aTask
            |> Async.AwaitTask 
            |> Async.Catch 
            |> Async.map AsyncResult<_>.ofChoice
            |> AR
    
        let map2 f xR yR =
            Async.map2 (Result.map2 f) xR yR
    
        let apply fAR xAR =
            map2 (fun f x -> f x) fAR xAR
            
//        let apply (ap : AsyncResult<'a -> 'b>) (asyncResult : AsyncResult<'a>) : AsyncResult<'b> = async {
//            let! result = asyncResult |> Async.StartChild
//            let! fap = ap |> Async.StartChild
//            let! fapResult = fap
//            let! fResult = result
//            match fapResult, fResult with
//            | Ok ap, Ok result -> return ap result |> Ok
//            | Error err, _
//            | _, Error err -> return Error err    }
        
        let inline lift2 f m1 m2 =
            let (>>=) x f = bind f x
            m1 >>= fun x1 -> m2 >>= fun x2 -> (f x1 x2)
            
        let inline lift3 f m1 m2 m3 =
            let (>>=) x f = bind f x
            m1 >>= fun x1 -> m2 >>= fun x2 -> m3 >>= fun x3 -> (f x1 x2 x3)               

            

//    /// Builder type for error handling in async computation expressions.
//    type AsyncTrialBuilder() = 
//        member __.Return value : AsyncResult<'a, 'b> = 
//            value
//            |> Ok
//            |> Async.retn
//            |> AR
//        
//        member __.ReturnFrom(asyncResult : AsyncResult<'a, 'b>) = asyncResult
//        
//        member this.Zero() : AsyncResult<unit, 'b> = this.Return()
//        
//        member __.Delay(generator : unit -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
//            async.Delay(generator >> AsyncResult.ofAsyncResult) |> AR
//        
//        member __.Bind(asyncResult : AsyncResult<'a, 'c>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
//            let fSuccess value = 
//                value |> (binder >> AsyncResult.ofAsyncResult)
//            
//            let fFailure errs = 
//                errs
//                |> Error
//                |> Async.retn
//            
//            asyncResult
//            |> AsyncResult.ofAsyncResult
//            |> Async.bind (Result.either fSuccess fFailure)
//            |> AR
//        
//        member this.Bind(result : Result<'a, 'c>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
//            this.Bind(result
//                      |> Async.retn
//                      |> AR, binder)
//        
//        member __.Bind(async : Async<'a>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
//            async
//            |> Async.bind (binder >> AsyncResult.ofAsyncResult)
//            |> AR
//        
//        member __.TryWith(asyncResult : AsyncResult<'a, 'b>, catchHandler : exn -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
//            async.TryWith(asyncResult |> AsyncResult.ofAsyncResult, (catchHandler >> AsyncResult.ofAsyncResult)) |> AR
//        
//        member __.TryFinally(asyncResult : AsyncResult<'a, 'b>, compensation : unit -> unit) : AsyncResult<'a, 'b> = 
//            async.TryFinally(asyncResult |> AsyncResult.ofAsyncResult, compensation) |> AR
//        
//        member __.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
//            async.Using(resource, (binder >> AsyncResult.ofAsyncResult)) |> AR
//    
//    
//        member __.Run f = f ()
//
//        member this.Combine (asyncResult: AsyncResult<'a, 'c>, binder: 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> =
//            this.Bind(asyncResult, binder)
//            
//        member this.Combine(ar1, ar2) = ar1 |> AsyncResult.bind (fun _ -> ar2)
//
//        member this.Combine (result: Result<'a, 'c>, binder: 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> =
//            this.Bind(result, binder)
//
//        member this.While (guard: unit -> bool, body: unit -> AsyncResult<unit, 'a>) =
//            if not <| guard () then this.Zero()
//            else this.Bind(body (), fun () -> this.While (guard, body))
//    
//        member this.For(s: 'a seq, body: 'a -> AsyncResult<unit, 'b>) =
//            this.Using(s.GetEnumerator (), fun e ->
//              this.While(e.MoveNext,
//                this.Delay(fun () -> body(e.Current))))    
//    
////        member x.For(vals, f) =
////          match vals with
////          | [] -> Async.unit
////          | v::vs -> f v |> bind (fun () -> x.For(vs, f))
////    
////        member x.Delay(f:unit -> Async<_>) =
////          { new Async<_> with
////              member x.Start(h) = f().Start(h) }
////    
////        member x.While(c, f) = 
////          if not (c ()) then unit ()
////          else f |> bind (fun () -> x.While(c, f))
//
//    // Wraps async computations in an error handling computation expression.
//    let asyncTrial = AsyncTrialBuilder()        
//                         
//    
//    
//    



