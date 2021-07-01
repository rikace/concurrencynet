namespace FSharp.Parallelx.ChannelEx

#nowarn "40"

module ChannelTransactionalAsync=
    
  open System
  open System.IO
  open System
  open System.Threading
  open System.Collections.Generic
  open System.Collections.Concurrent
  
  // ----------------------------------------------------------------------------
  // Helpers (that are used in the implementation of joins)
  // ----------------------------------------------------------------------------
  
  /// Represents a simple 'transactional' asynchronous workflow
  /// A value is given to the continuation, together with a function
  /// that should be called to accept or reject the value.
  type TransactionalAsync<'T> = 
    TA of (('T * (bool -> unit) -> unit) -> unit)
  
  type Future<'a> = F of (unit -> ('a -> unit) -> unit)
  
  /// Represents a signal that has some Boolean state and 
  /// triggers an event when the state changes to true.
  type ISignal =
    abstract Signalled : IObservable<unit>
    abstract State : bool
  
  
  /// Concrete signal that can be signalled using the 'Set' method
  type Signal() = 
    [<VolatileField>]
    let mutable state = false
    let signalled = new Event<_>()
    
    member x.Set(newState) =
      state <- newState
      if state then signalled.Trigger()
  
    member x.Publish = 
      { new ISignal with 
          member x.Signalled = signalled.Publish :> IObservable<_>
          member x.State = state }
  
  /// Operations for working with signals
  [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
  module Signal = 
    /// Create a signal whose state is calculated from the state of two other singals
    let merge op (sig1:ISignal) (sig2:ISignal) = 
      { new ISignal with
          member x.State = op sig1.State sig2.State
          member x.Signalled = 
            Observable.merge sig1.Signalled sig2.Signalled }
  //          |> Observable.filter (fun () -> x.State) }
  
    /// Signalled when both signals are signalled
    let both sig1 sig2 = merge (&&) sig1 sig2
    /// Signalled when any of the signals is signalled
    let either sig1 sig2 = merge (||) sig1 sig2
  
  
  [<AutoOpen>]
  module AsyncExtensions = 
    type ISignal with 
      /// Creates an asynchronous workflow that waits until a signal is signalled.
      member x.AsyncAwait() =
        Async.FromContinuations(fun (cont,econt,ccont) -> 
          // If already signalled, then run immediately
          if x.State then cont()
          else
            let lockObj = new obj()
            let called = ref false
  
            // Guarantees that the continuation is called only once
            let contOnce () = 
              if not called.Value then
                let doCall =
                  lock lockObj (fun () ->
                    if not called.Value then 
                      called := true; true
                    else false )
                if doCall then cont()
  
            // When signalled, call continuation & cleanup
            let rec finish() = 
              if remover <> null then remover.Dispose()
              contOnce()
              called := true
            and remover : IDisposable = 
              x.Signalled.Subscribe(finish)
  
            // When signalled while registering, call & cleanup
            if x.State then 
              remover.Dispose()
              contOnce() )
  
  
    type Microsoft.FSharp.Control.Async with
      /// Waits until transactional async produces a value 
      /// accepts it and returns it to a workflow.
      static member AwaitTransactionalAsync(TA ta) = 
        Async.FromContinuations(fun (cont, _, _) ->
          ta (fun (value, commit) ->
            commit true
            cont value ))
  
  
  // ----------------------------------------------------------------------------
  // Representation of a join pattern body
  // ----------------------------------------------------------------------------
  
  /// Represents a single operation that can be performed by the body
  /// (the operation is abstract and can be created only using 
  ///  operations provided by Channel or IReplyChannel)
  type JoinOperation = private Call of (unit -> unit)
  
  /// Represents a collection of operations performed by join pattern body
  type JoinReaction = list<JoinOperation>
  
  /// A reply channel that is used for replying from synchronous channels
  type IReplyChannel<'T> = 
    /// Creates a join pattern operation that sends reply back to the caller
    abstract Reply : 'T -> JoinOperation
  
  // ----------------------------------------------------------------------------
  // Implementation of Join channels
  // ----------------------------------------------------------------------------
  
  /// An abstract representation of a channels that are used in join calculus.
  /// It provides a signal signalling whether channel contains a value and an
  /// operation that can be used to request the value (implementing two-phase
  /// commit protocol)
  type IChannel<'T> = 
    /// Signalled when the channel contains a value
    abstract Available : ISignal
  
    /// When called, returns a transactional async containing the 
    /// value (when available) or None.
    abstract Query : unit -> TransactionalAsync<option<'T>>
  
  
  [<AutoOpen>]
  module ChannelExtensions = 
    type IChannel<'T> with 
      /// Asynchronously get the next available value from a channel.
      member x.Receive() = async {
        do! x.Available.AsyncAwait()
        let! v = Async.AwaitTransactionalAsync(x.Query())
        match v with
        | None -> 
            return! x.Receive()
        | Some v -> return v }
  
  /// Represents a concrete channel that can be used in join patterns
  type Channel<'T>(n) =
    let queue = new Queue<'T>()
    let evt = new Signal()
    let lockObj = new obj()
    let synchronized f = lock lockObj f
    
    /// Send message to the channel (without blocking)
    member x.Put(message:'T) =
      Call(fun () -> x.Call(message))
  
    /// Send message to the channel (without blocking)
    /// (This function should be used from outside of join pattern body)
    member x.Call(message:'T) =
      synchronized (fun () ->
        queue.Enqueue(message))
      evt.Set(true)
  
    interface IChannel<'T> with
      /// Signalled when the channel contains a value
      member x.Available = evt.Publish
  
      /// When called, returns a transactional async containing the 
      /// value (when available) or None.
      member x.Query() = TA (fun tcont ->
        Monitor.Enter(lockObj)
  
        let commit b1 b2 =
          // If commited, then remove the value
          if b1 && b2 then queue.Dequeue() |> ignore
          if queue.Count = 0 then evt.Set(false) |> ignore
          Monitor.Exit(lockObj)
  
        if queue.Count > 0 then 
          tcont (Some(queue.Peek()), commit true)
        else tcont (None, commit false))
   
  
  /// Represents a channel created by a projection 
  /// (The actual value is left in the source channel)
  type ProjectionChannel<'T, 'R>(f:'T -> 'R, channel:IChannel<'T>) =
    interface IChannel<'R> with 
      member x.Available = channel.Available 
      member x.Query() = TA (fun tcont ->
        let (TA ta) = channel.Query() 
        ta (fun (value, commit) -> tcont (Option.map f value, commit)) )
  
  /// Represents a channel created by a merging of two channels
  /// (The actual values are left in the source channels)    
  type MergeChannel<'T1, 'T2>(channel1:IChannel<'T1>, channel2:IChannel<'T2>) =
    interface IChannel<'T1 * 'T2> with
      member x.Available = Signal.both channel1.Available channel2.Available
      member x.Query() = TA (fun tcont ->
        
        // Query both channels and emit value only when both
        // contain a value. Comitting removes values from both sources.
        let (TA ta1) = channel1.Query()
        let (TA ta2) = channel2.Query()
  
        let evt = new CountdownEvent(2)
        let value1 = ref None
        let value2 = ref None
        let commit1 = ref None
        let commit2 = ref None
        
        // Comit to get the value from both channels
        let finalCommit b1 b2 = 
          commit1.Value.Value(b1 && b2)
          commit2.Value.Value(b1 && b2)
        
        // Called when value from a channel is received
        let continuation commitTo valueTo (value, commit) = 
          commitTo := Some commit
          valueTo := value
          if evt.Signal() then 
            match value1.Value, value2.Value with
            | Some v1, Some v2 -> 
                tcont (Some(v1, v2), finalCommit true)
            | _ ->
                tcont (None, finalCommit false)
        
        ta1 (continuation commit1 value1)
        ta2 (continuation commit2 value2) )
  
  
  /// Represents a channel created by choosing between two channels
  /// (The actual values are left in the source channels)
  type ChoiceChannel<'T>(channel1:IChannel<'T>, channel2:IChannel<'T>) =
    interface IChannel<'T> with
      member x.Available = Signal.either channel1.Available channel2.Available
      member x.Query() = TA (fun tcont ->
  
        // Query both channels, return the first non-None value that is 
        // available or 'None' if there is no value in eiter channel.
        let (TA ta1) = channel1.Query()
        let (TA ta2) = channel2.Query()
        
        let lockObj = new obj()
        let synchronized f = lock lockObj f
        let called = ref false
        let counter = ref 0
  
        // Call the resulting continuation with a first non-None result
        // (use locks to avoid races and calling the continuation twice)
        let continuation (value:option<_>, commit) =
          let call = synchronized(fun () ->
            incr counter
            // If we have not called continuation yet and
            // 1) we've got a value or 2) we've got second None
            if not called.Value && (value.IsSome || !counter = 2) then
              called := true
              (fun () -> tcont (value, fun b -> 
                commit (b && value.IsSome)))
            else 
              (fun () -> commit false))
          call ()
  
        ta1 continuation
        ta2 continuation)
  
  // ----------------------------------------------------------------------------
  // Simpler interface for channels
  // ----------------------------------------------------------------------------
  
  /// A synchronous channel. Calling the channel eventually gives a result, 
  /// so the call is logically blocking. (Thanks to async workflows, no actual
  /// threads are blocked when waiting)
  type SyncChannel<'TRes>(n) =
    inherit Channel<IReplyChannel<'TRes>>(n)
  
    /// Send message to the channel and resume the returned
    /// asynchronous workflow when a reply is available
    member x.AsyncCall() =
      Async.FromContinuations(fun (cont, _, _) ->
        x.Call({ new IReplyChannel<_> with
                   member x.Reply(v) = Call (fun () -> cont v) }))
  
  
  /// A synchronous channel with argument. Calling the channel eventually gives a 
  /// result, so the call is logically blocking. (Thanks to async workflows, no actual
  /// threads are blocked when waiting)
  type SyncChannel<'TArg, 'TRes>(n) =
    inherit Channel<'TArg * IReplyChannel<'TRes>>(n)
  
    /// Send message to the channel and resume the returned
    /// asynchronous workflow when a reply is available
    member x.AsyncCall(arg) =
      Async.FromContinuations(fun (cont, _, _) ->
        x.Call(arg, { new IReplyChannel<_> with
                        member x.Reply(v) = Call (fun () -> cont v) }))
  
  module Channel = 
    /// Creates an aliased channel that contains projected values
    let map f channel = 
      (new ProjectionChannel<_, _>(f, channel)) :> IChannel<_>
  
    /// Creates an aliased channel that merges two channels
    let merge ch1 ch2 = 
      (new MergeChannel<_, _>(ch1, ch2)) :> IChannel<_>
  
    /// Creates an aliased channel that contains values from either channel
    let choice ch1 ch2 =
      (new ChoiceChannel<_>(ch1, ch2)) :> IChannel<_>
  
    /// Runs a join program that is represented as a channel yielding reactions
    let run (join:IChannel<JoinReaction>) =
      async { while true do 
                // Get the next body and start it in background
                let! jops = join.Receive()
                Async.Start(async { for (Call f) in jops do f() }) }
      |> Async.Start
  
  
  // ----------------------------------------------------------------------------
  // Computation expression builders for joins & replies and public operations
  // ----------------------------------------------------------------------------
  
  [<AutoOpen>]
  module Builders =
    type JoinBuilder() =
      member x.Choose(a, b) = Channel.choice a b
      member x.Select(a, f) = Channel.map f a
      member x.Merge(a, b) = Channel.merge a b
      member x.Run(a) = Channel.run a
  
    type JoinReplyBuilder() = 
      member x.Yield(op) = [ op ]
      member x.Combine(op1, op2) = (op1 @ op2)
      member x.Delay(f) = f()
  
    /// Computation expression builder that can be used 
    /// for encoding join patterns
    
    let join = JoinBuilder()
    /// Computation expression builder that is used for 
    /// writing reactions in join patterns
    let react = JoinReplyBuilder()
    
    
  //module SampleChannels = 
  //
  //  open System
  //  open System.Threading  
  //  open ChannelExtensions
  //  open Channel
  //  open Builders
  //  open Signal  
  //  open AsyncModule
  //  open FSharp.Parallelx.AsyncEx
  //
  //  open FSharp.Parallelx.AsyncEx.AsyncModule
  //    
  //  let twoPutBuffer() =
  //    let putString = Channel<string>("puts")
  //    let putInt = Channel<int>("puti")
  //    let get = SyncChannel<string>("get")
  //  
  //    join {
  //      let! res = get, putString, putInt
  //      match res with
  //      | repl, v, ? -> return react { yield repl.Reply("Echo " + v) }
  //      | repl, ?, v -> return react { yield repl.Reply("Echo " + (string v)) } 
  //    }
  //  
  //  
  //    // Put 5 string messages to the putString channel
  //    async { for i in 1 .. 5 do 
  //              do! Async.Sleep(1000) 
  //              printfn "putting: World (#%d)" i
  //              putString.Call(sprintf "World (#%d)" i) }
  //    |> Async.Start
  //  
  //    // Put 5 int messages to the putInt channel
  //    async { do! Async.Sleep(2000)
  //            for i in 1 .. 5 do 
  //              do! Async.Sleep(1000) 
  //              printfn "putting: %d" (1000 + i)
  //              putInt.Call(1000 + i) }
  //    |> Async.Start
  //  
  //    // Repeatedly read messages by calling 'Get'
  //    async { while true do
  //              do! Async.Sleep(500)
  //              printfn "reading..."
  //              let! repl = get.AsyncCall()
  //              printfn "got: %s" repl }
  //    |> Async.Start
    
    
    // ----------------------------------------------------------------------------
    // One place buffer sample
    // ----------------------------------------------------------------------------
    
  //  let onePlaceBuffer() = 
  //    let put = SyncChannel<string, unit>()
  //    let get = SyncChannel<string>()
  //    let empty = Channel<unit>()
  //    let contains = Channel<string>()
  //    
  //    // Initially, the buffer is empty
  //    empty.Call(())
  //  
  //    join {
  //      match! put, empty, get, contains with 
  //      | (s, repl), (), ?, ? -> return react {
  //        yield contains.Put(s)
  //        yield repl.Reply() }
  //      | ?, ?, repl, v -> return react {
  //        yield repl.Reply(v)
  //        yield empty.Put(()) } }
  //  
  //    // Repeatedly try to put value into the buffer
  //    async { do! Async.Sleep(1000)
  //            for i in 0 .. 10 do
  //              printfn "putting: %d" i
  //              do! put.AsyncCall(string i)
  //              do! Async.Sleep(500) }
  //    |> Async.Start
  //  
  //    // Repeatedly read values from the buffer and print them
  //    async { while true do 
  //              do! Async.Sleep(250)
  //              let! v = get.AsyncCall()
  //              printfn "got: %s" v }
  //    |> Async.Start