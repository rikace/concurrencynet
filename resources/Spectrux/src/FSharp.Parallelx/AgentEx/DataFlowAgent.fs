namespace FSharp.Parallelx.AgentEx

open System
open System.Threading
open System.Threading.Tasks

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open AgentTypes

#nowarn "40"


[<AutoOpen>]
module TPLDataflowEx =
    open System
    open System.Threading
    open System.Threading.Tasks.Dataflow
                
    /// Helper to just invoke the three 'funcs' once.
    let internal invokeOnce funcs =
      let counter = ref 0
      let invokeOnce' f x =
        if (Interlocked.CompareExchange (counter, 1, 0) = 0) then
          f x
      let (a, b, c) = funcs
      (invokeOnce' a, invokeOnce' b, invokeOnce' c)
      
    type Microsoft.FSharp.Control.Async with
      /// Spawn an async with a timeout, throwing <see cref="System.TimeoutException" />
      /// after the timeout.
      static member WithTimeout(timeout : TimeSpan, computation : Async<'T>) : Async<'T> =
        let callback (success, error, cancellation) =
          let (success, error, cancellation) = invokeOnce (success, error, cancellation)
          let fetchResult = async {
            try
              let! result = computation
              success result
            with ex ->
              error ex }
          let timeoutExpired = async {
            do! Async.Sleep (int timeout.TotalMilliseconds)
            let ex = new TimeoutException ("Timeout expired") :> Exception
            error ex }

          Async.StartImmediate fetchResult
          Async.StartImmediate timeoutExpired

        Async.FromContinuations callback        
    type TaskResultCell<'T>() =
      let source = new TaskCompletionSource<'T>()

      /// Complete the async result cell, setting the value. If this invocation was
      /// the first invocation, returns true, otherwise if there already is a value
      /// set, return false.
      member x.complete result =
        source.TrySetResult result

      /// Await the result of the AsyncResultCell, yielding Some(:'T)
      /// after the timeout or otherwise None.
      member x.awaitResult(?timeout : TimeSpan) = async {
        match timeout with
        | None ->
          let! res = source.Task |> Async.AwaitTask
          return Some res
        | Some time ->
          try
            let! res = Async.WithTimeout(time, Async.AwaitTask(source.Task))
            return Some res
          with
          | :? TimeoutException as e ->
            return None
        }
      
    [<Sealed>]
    type AsyncReplyChannel<'Reply> internal (replyf : 'Reply -> unit) =
        member x.Reply(reply) = replyf(reply)
    
    [<Sealed>]
    type internal AsyncResultCell<'a>() =
        let source = new TaskCompletionSource<'a>()
    
        member x.RegisterResult result = source.SetResult(result)
    
        member x.AsyncWaitResult =
            Async.FromContinuations(fun (cont,_,_) -> 
             //   let apply = fun (task:Task<_>) -> cont (task.Result)
                source.Task.ContinueWith(fun (task:Task<_>) -> cont (task.Result)) |> ignore)
    
        member x.GetWaitHandle(timeout:int) =
            async { let waithandle = source.Task.Wait(timeout)
                    return waithandle }
    
        member x.GrabResult() = source.Task.Result
    
        member x.TryWaitResultSynchronously(timeout:int) = 
            //early completion check
            if source.Task.IsCompleted then 
                Some source.Task.Result
            //now force a wait for the task to complete
            else 
                if source.Task.Wait(timeout) then 
                    Some source.Task.Result
                else None      
      
    type ITargetBlock<'a> with     
        member x.AsyncSend<'a> (msg, ct) = DataflowBlock.SendAsync<'a>(x,msg,ct) |> Async.AwaitTask
        member x.AsyncSend<'a> msg = x.AsyncSend(msg, CancellationToken.None)

    type ISourceBlock<'a> with
        member x.AsyncReceive<'a> (ct : CancellationToken) = DataflowBlock.ReceiveAsync<'a>(x,ct) |> Async.AwaitTask
        member x.AsyncReceive<'a> (timeout : TimeSpan, ct : CancellationToken) = DataflowBlock.ReceiveAsync<'a>(x,timeout,ct) |> Async.AwaitTask |> Async.Catch
        member x.AsyncReceive<'a> (timeout : TimeSpan) = x.AsyncReceive(timeout, CancellationToken.None)
        member x.AsyncReceive<'a> () = x.AsyncReceive(CancellationToken.None)
        member x.AsyncOutputAvailable<'a> ct = x.OutputAvailableAsync(ct) |> Async.AwaitTask
        member x.AsyncOutputAvailable<'a> () = x.AsyncOutputAvailable(CancellationToken.None)


[<Sealed>]
type DataflowAgent<'Msg>(initial, ?cancelToken, ?dataflowOptions) =
    let cancellationToken = defaultArg cancelToken Async.DefaultCancellationToken
    let mutable started = false
    let errorEvent = new Event<System.Exception>()
    let options = defaultArg dataflowOptions <| DataflowBlockOptions()
    let incomingMessages = new BufferBlock<'Msg>(options)
    let mutable defaultTimeout = Timeout.Infinite
    
    member x.CurrentQueueLength() = incomingMessages.Count

    member x.DefaultTimeout
        with get() = defaultTimeout
        and set(value) = defaultTimeout <- value

    [<CLIEvent>]
    member this.Error = errorEvent.Publish

    member x.Start() =
        if started
            then raise (new InvalidOperationException("Already Started."))
        else
            started <- true
            let comp = async { try do! initial x
                                with error -> errorEvent.Trigger error }
            Async.Start(computation = comp, cancellationToken = cancellationToken)

    member x.Receive(?timeout) =
        Async.AwaitTask <| incomingMessages.ReceiveAsync()

    member x.TryReceive(?timeout) =
        let ts = TimeSpan.FromMilliseconds(float <| defaultArg timeout defaultTimeout)
        Async.AwaitTask <| incomingMessages.ReceiveAsync(ts)
                                .ContinueWith(fun (tt:Task<_>) ->
                                                    if tt.IsCanceled || tt.IsFaulted then None
                                                    else Some tt.Result)

    member x.Post(item) =
        let posted = incomingMessages.Post(item)
        if not posted then
            raise (InvalidOperationException("Incoming message buffer full."))

    member x.TryPostAndReply(replyChannelMsg, ?timeout) :'Reply option =
        let timeout = defaultArg timeout defaultTimeout
        let resultCell = AsyncResultCell<_>()
        let msg = replyChannelMsg(new AsyncReplyChannel<_>(fun reply -> resultCell.RegisterResult(reply)))
        if incomingMessages.Post(msg) then
            resultCell.TryWaitResultSynchronously(timeout)
        else None

    member x.PostAndReply(replyChannelMsg, ?timeout) : 'Reply =
        match x.TryPostAndReply(replyChannelMsg, ?timeout = timeout) with
        | None ->  raise (TimeoutException("PostAndReply timed out"))
        | Some result -> result

    member x.PostAndTryAsyncReply(replyChannelMsg, ?timeout): Async<'Reply option> =
        let timeout = defaultArg timeout defaultTimeout
        let resultCell = AsyncResultCell<_>()
        let msg = replyChannelMsg(new AsyncReplyChannel<_>(fun reply -> resultCell.RegisterResult(reply)))
        let posted = incomingMessages.Post(msg)
        if posted then
            match timeout with
            |   Threading.Timeout.Infinite ->
                    async { let! result = resultCell.AsyncWaitResult
                            return Some(result) }
            |   _ ->
                    async { let! ok =  resultCell.GetWaitHandle(timeout)
                            let res = (if ok then Some(resultCell.GrabResult()) else None)
                            return res }
        else async{return None}

    member x.PostAndAsyncReply( replyChannelMsg, ?timeout) =
            let timeout = defaultArg timeout defaultTimeout
            match timeout with
            |   Threading.Timeout.Infinite ->
                let resCell = AsyncResultCell<_>()
                let msg = replyChannelMsg (AsyncReplyChannel<_>(fun reply -> resCell.RegisterResult(reply) ))
                let posted = incomingMessages.Post(msg)
                if posted then
                    resCell.AsyncWaitResult
                else
                    raise (InvalidOperationException("Incoming message buffer full."))
            |   _ ->
                let asyncReply = x.PostAndTryAsyncReply(replyChannelMsg, timeout=timeout)
                async { let! res = asyncReply
                        match res with
                        | None ->  return! raise (TimeoutException("PostAndAsyncReply TimedOut"))
                        | Some res -> return res }

    member x.TryScan((scanner: 'Msg -> Async<_> option), timeout): Async<_ option> =
        let ts = TimeSpan.FromMilliseconds( float timeout)
        let rec loopformsg = async {
            let! msg = Async.AwaitTask <| incomingMessages.ReceiveAsync(ts)
                                            .ContinueWith(fun (tt:Task<_>) ->
                                                if tt.IsCanceled || tt.IsFaulted then None
                                                else Some tt.Result)
            match msg with
            | Some m->  let res = scanner m
                        match res with
                        | None -> return! loopformsg
                        | Some res -> return! res
            | None -> return None}
        loopformsg

    member x.Scan(scanner, timeout) =
        async { let! res = x.TryScan(scanner, timeout)
                match res with
                | None -> return raise(TimeoutException("Scan TimedOut"))
                | Some res -> return res }

    static member Start(initial, ?cancellationToken, ?dataflowOptions) =
        let dfa = DataflowAgent<'Msg>(initial, ?cancelToken = cancellationToken, ?dataflowOptions = dataflowOptions)
        dfa.Start();dfa
        
        

module TPLDataflow =

    open System
    open System.Threading.Tasks
    open System.Threading.Tasks.Dataflow

    let inline action f = new Action<_>(f)
    let inline predicate f = new Predicate<_>(f)
    let private defaultExecutionOptions = new ExecutionDataflowBlockOptions()
    let private defaultDataflowLinkOptions = new DataflowLinkOptions()
    let inline startAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

    /// <summary>Initializes a new instance of the <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" /> class with the specified action.</summary>  
    /// <param name="f">The function to invoke with each data element received.</param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" />.</param>
    let actionBlockWithOptions f executionOptions = ActionBlock<_>(f |> action, executionOptions)

    /// <summary>Initializes a new instance of the <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" /> class with the specified action.</summary>  
    /// <param name="f">The function to invoke with each data element received.</param>
    let actionBlock f = actionBlockWithOptions f defaultExecutionOptions

    /// <summary>Initializes a new instance of the <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" /> class with the specified action.</summary>  
    /// <param name="f">The async function to invoke with each data element received.</param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" />.</param>
    let actionBlockAsyncWithOptions (f: 'a -> Async<unit>) executionOptions = ActionBlock<_>(f >> startAsPlainTask, executionOptions)

    /// <summary>Initializes a new instance of the <see cref="T:System.Threading.Tasks.Dataflow.ActionBlock`1" /> class with the specified action.</summary>  
    /// <param name="f">The async function to invoke with each data element received.</param>
    let actionBlockAsync f = actionBlockAsyncWithOptions f defaultExecutionOptions

    
    /// <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" /> with the specified <see cref="T:System.Func`2" /></summary>
    /// <param name="f">The function to tranform each element received.</param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" />.</param>
    let mapBlockWithOptions (f: 'a -> 'b) executionOptions = TransformBlock<_,_>(f, executionOptions)
    
    /// <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" /> with the specified <see cref="T:System.Func`2" /></summary>
    /// <param name="f">The function to tranform each element received.</param>
    let mapBlock f = mapBlockWithOptions f defaultExecutionOptions
    
    /// <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" /> with the specified <see cref="T:System.Func`2" /></summary>
    /// <param name="f">The async function to tranform each element received.</param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" />.</param>
    let mapBlockAsyncWithOptions (f : 'a -> Async<'b>) executionOptions = TransformBlock<'a,'b>(f >> Async.StartAsTask, executionOptions)
    
    /// <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformBlock`2" /> with the specified <see cref="T:System.Func`2" /></summary>
    /// <param name="f">The async function to tranform each element received.</param>
    let mapBlockAsync f = mapBlockAsyncWithOptions f defaultExecutionOptions  
  
    ///  <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /> with the specified function</summary>  
    /// <param name="f">The function to tranform each element received. All of the data from the returned in the <see cref="T:System.Collections.Generic.IEnumerable`1" /> will be made available as output from this <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /></param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" />.</param>
    let flatpMapBlockWithOptions (f : 'a -> seq<'b>) executionOptions = TransformManyBlock<_,_>(f, executionOptions)

    ///  <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /> with the specified function</summary>  
    /// <param name="f">The function to tranform each element received. All of the data from the returned in the <see cref="T:System.Collections.Generic.IEnumer
    let flatpMapBlock (f : 'a -> seq<'b>) = flatpMapBlockWithOptions f defaultExecutionOptions    

    ///  <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /> with the specified function</summary>  
    /// <param name="f">The async function to tranform each element received. All of the data from the returned in the <see cref="T:System.Collections.Generic.IEnumerable`1" /> will be made available as output from this <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /></param>
    /// <param name="executionOptions">The options with which to configure this <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" />.</param>
    let flatpMapBlockAsyncWithOptions (f : 'a -> Async<seq<'b>>) executionOptions =  TransformManyBlock(f >> Async.StartAsTask, executionOptions)

    ///  <summary>Initializes a new <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /> with the specified function</summary>  
    /// <param name="f">The async function to tranform each element received. All of the data from the returned in the <see cref="T:System.Collections.Generic.IEnumerable`1" /> will be made available as output from this <see cref="T:System.Threading.Tasks.Dataflow.TransformManyBlock`2" /></param>
    let flatpMapBlockAsync (f : 'a -> Async<seq<'b>>) = flatpMapBlockAsyncWithOptions f defaultExecutionOptions

    /// <summary>Links the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" /> to the specified <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary>   
    /// <param name="target">The <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" /> to which to connect the source.</param>
    /// <param name="dataflowOptions">>A <see cref="T:System.Threading.Tasks.Dataflow.DataflowLinkOptions" /> instance that configures the link.</param>
    /// <param name="filter">The filter a message must pass in order for it to propagate from the source to the target.</param>
    /// <param name="source">The source from which to link.</param>
    /// <returns>An IDisposable that, upon calling Dispose, will unlink the source from the target.</returns>      
    let linkToWithOptionsAndFilter target dataflowOptions filter (source : ISourceBlock<_>) =
        source.LinkTo(target, dataflowOptions, filter |> predicate)

    /// <summary>Links the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" /> to the specified <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary>   
    /// <param name="target">The <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" /> to which to connect the source.</param>
    /// <param name="dataflowOptions">>A <see cref="T:System.Threading.Tasks.Dataflow.DataflowLinkOptions" /> instance that configures the link.</param>
    /// <param name="source">The source from which to link.</param>
    /// <returns>An IDisposable that, upon calling Dispose, will unlink the source from the target.</returns>      
    let linkToWithOptions target dataflowOptions (source : ISourceBlock<_>) =
        source.LinkTo(target, dataflowOptions)

    /// <summary>Links the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" /> to the specified <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary>   
    /// <param name="target">The <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" /> to which to connect the source.</param>
    /// <param name="filter">The filter a message must pass in order for it to propagate from the source to the target.</param>
    /// <param name="source">The source from which to link.</param>
    /// <returns>An IDisposable that, upon calling Dispose, will unlink the source from the target.</returns>      
    let linkToWithFilter target filter (source : ISourceBlock<_>) =
        source.LinkTo(target, filter |> predicate)

    /// <summary>Links the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" /> to the specified <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary>   
    /// <param name="target">The <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" /> to which to connect the source.</param>
    /// <param name="source">The source from which to link.</param>
    /// <returns>An IDisposable that, upon calling Dispose, will unlink the source from the target.</returns>      
    let linkTo target (source : ISourceBlock<_>) =
        linkToWithOptions target defaultDataflowLinkOptions source

    /// <summary>Signals to the <see cref="T:System.Threading.Tasks.Dataflow.IDataflowBlock" /> that it should not accept nor produce any more messages nor consume any more postponed messages.</summary>
    /// <param name="source">The source to signal completion</param>
    let complete (source : IDataflowBlock) = source.Complete()
   
    /// <summary>Causes the <see cref="T:System.Threading.Tasks.Dataflow.IDataflowBlock" /> to complete in a <see cref="F:System.Threading.Tasks.TaskStatus.Faulted" /> state.</summary>
    /// <param name="ex">The <see cref="T:System.Exception" /> that caused the faulting.</param>
    /// <param name="source">The source to signal failure</param>
    let fail ex (source : IDataflowBlock) = source.Fault ex

    /// <summary>Links the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" /> to the specified <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary>   
    /// <param name="source">The source from which to link.</param> 
    /// <param name="target">The <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" /> to which to connect the source.</param>
    ///<returns>An IDisposable that, upon calling Dispose, will unlink the source from the target.</returns>  
    let (&>) source target = linkTo target source

    /// <summary>Creates a new <see cref="T:System.IObservable`1" /> abstraction over the <see cref="T:System.Threading.Tasks.Dataflow.ISourceBlock`1" />.</summary>
    /// <param name="source">The source to wrap</param>
    let toObservable (source : ISourceBlock<_>) = source.AsObservable()
    
    /// <summary>Creates a new <see cref="T:System.IObserver`1" /> abstraction over the <see cref="T:System.Threading.Tasks.Dataflow.ITargetBlock`1" />.</summary> 
    /// <param name="target">The target to wrap</param>
    let toObserver (target : ITargetBlock<_>) = target.AsObserver()

    /// <summary>Asynchronously offers a message to the target message block, allowing for postponement.</summary>
    /// <param name="message">The message being offered to the target.</param>
    /// <param name="target">The target to which to post the data.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that represents the asynchronous send. If the target accepts and consumes the offered element during the call to <see cref="M:System.Threading.Tasks.Dataflow.DataflowBlock.SendAsync``1(System.Threading.Tasks.Dataflow.ITargetBlock{``0},``0)" />, upon return from the call the resulting <see cref="T:System.Threading.Tasks.Task`1" /> will be completed and its <see cref="P:System.Threading.Tasks.Task`1.Result" /> property will return true. If the target declines the offered element during the call, upon return from the call the resulting <see cref="T:System.Threading.Tasks.Task`1" /> will be completed and its <see cref="P:System.Threading.Tasks.Task`1.Result" /> property will return false. If the target postpones the offered element, the element will be buffered until such time that the target consumes or releases it, at which point the task will complete, with its <see cref="P:System.Threading.Tasks.Task`1.Result" /> indicating whether the message was consumed. If the target never attempts to consume or release the message, the returned task will never complete.</returns>   
    let (!>) message (target : ITargetBlock<_>)  = target.AsyncSend(message)
     
    /// <summary>Asynchronously offers a message to the target message block, allowing for postponement.</summary>
    /// <param name="message">The message being offered to the target.</param>
    /// <param name="target">The target to which to post the data.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that represents the asynchronous send. If the target accepts and consumes the offered element during the call to <see cref="M:System.Threading.Tasks.Dataflow.DataflowBlock.SendAsync``1(System.Threading.Tasks.Dataflow.ITargetBlock{``0},``0)" />, upon return from the call the resulting <see cref="T:System.Threading.Tasks.Task`1" /> will be completed and its <see cref="P:System.Threading.Tasks.Task`1.Result" /> property will return true. If the target declines the offered element during the call, upon return from the call the resulting <see cref="T:System.Threading.Tasks.Task`1" /> will be completed and its <see cref="P:System.Threading.Tasks.Task`1.Result" /> property will return false. If the target postpones the offered element, the element will be buffered until such time that the target consumes or releases it, at which point the task will complete, with its <see cref="P:System.Threading.Tasks.Task`1.Result" /> indicating whether the message was consumed. If the target never attempts to consume or release the message, the returned task will never complete.</returns> 
    let (<!) (target : ITargetBlock<_>) message  = message !> target
            