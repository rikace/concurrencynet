namespace FSharp.Parallelx

module ConcurrentValues =
    
  [<AutoOpen>]
  module Core =
    open System
    open System.Collections.Generic
    open System.Threading
    open System.Threading.Tasks
    open FSharp.Parallelx
    open FSharp.Parallelx.AsyncEx
    
    [<Sealed>] 
    type TVar<'T when 'T : not struct> internal (value: 'T) =
        static let nextId = ref 0
        let _id = Interlocked.Increment(nextId)        
        let _value = ref value        
        let spinner = lazy (new SpinWait())
    
        let neq a b = obj.ReferenceEquals(a,b) |> not
        
        let rec swap value =
            let tempValue = !_value
            if Interlocked.CompareExchange<'T>(_value, value, tempValue) |> neq tempValue then
                spinner.Value.SpinOnce()
                swap value

        member private __.Id = _id
        member internal __.Value 
            with get () = !_value
            and set value = swap value
        
        interface IComparable<TVar<'T>> with
            member __.CompareTo(other) = _id.CompareTo(other.Id)
  
        static member Atomic(tvar : TVar<'T>, newValue : 'T) = tvar.Value <- newValue

    [<RequireQualifiedAccess>]
    module TVar =
      let create (value : 'T) : TVar<'T> = TVar<'T>(value)
      
      let write (tvar : TVar<'T>) newValue = tvar.Value <- newValue
        
      let read (tvar : TVar<'T>) = tvar.Value
        
  
      let update (s:TVar<'a>) (a:'a) =
       let newTail = create a
       let cell = ref s
       let tail = Interlocked.Exchange(cell, newTail)
       cell


    
    /// A write-once concurrent variable.
    type IVar<'a> = TaskCompletionSource<'a>
    
    /// Operations on write-once variables.
    module IVar =
    
      let awaitTaskCancellationAsError (t:Task<'a>) : Async<'a> =
        Async.FromContinuations <| fun (ok,err,_) ->
          t.ContinueWith (fun (t:Task<'a>) ->
            if t.IsFaulted then err t.Exception
            elif t.IsCanceled then err (OperationCanceledException("Task wrapped with Async has been cancelled."))
            elif t.IsCompleted then ok t.Result
            else failwith "invalid Task state!") |> ignore
          
        
      /// Creates an empty IVar structure.
      let inline create () = new IVar<'a>()
    
      /// Creates a IVar structure and initializes it with a value.
      let inline createFull a =
        let ivar = create()
        ivar.SetResult(a)
        ivar
    
      /// Writes a value to an IVar.
      /// A value can only be written once, after which the behavior is undefined and may throw.
      let inline put a (i:IVar<'a>) = 
        i.SetResult(a)
    
      let inline tryPut a (i:IVar<'a>) = 
        i.TrySetResult (a)
    
      /// Writes an error to an IVar to be propagated to readers.
      let inline error (ex:exn) (i:IVar<'a>) = 
        i.SetException(ex)
    
      let inline tryError (ex:exn) (i:IVar<'a>) = 
        i.TrySetException(ex)
    
      /// Writes a cancellation to an IVar to be propagated to readers.
      let inline cancel (i:IVar<'a>) = 
        i.SetCanceled()
    
      let inline tryCancel (i:IVar<'a>) = 
        i.TrySetCanceled()
    
      /// Creates an async computation which returns the value contained in an IVar.
      let inline get (i:IVar<'a>) : Async<'a> = 
        i.Task |> awaitTaskCancellationAsError
    
      /// Creates an async computation which returns the value contained in an IVar.
      let inline getWithTimeout (timeout:TimeSpan) (timeoutResult:unit -> 'a) (i:IVar<'a>) : Async<'a> = async {
        use _timer = new Timer((fun _ -> tryPut (timeoutResult ()) i |> ignore), null, (int timeout.TotalMilliseconds), Timeout.Infinite)
        return! i.Task |> awaitTaskCancellationAsError }
    
      /// Returns a cancellation token which is cancelled when the IVar is set.
      let inline intoCancellationToken (cts:CancellationTokenSource) (i:IVar<_>) =
        i.Task.ContinueWith (fun (t:Task<_>) -> cts.Cancel ()) |> ignore
    
      /// Returns a cancellation token which is cancelled when the IVar is set.
      let inline asCancellationToken (i:IVar<_>) =
        let cts = new CancellationTokenSource ()
        intoCancellationToken cts i
        cts.Token
    
    
    
    
    type private MVarReq<'a> =
      | PutAsync of Async<'a> * IVar<'a>
      | UpdateAsync of update:('a -> Async<'a>)
      | PutOrUpdateAsync of update:('a option -> Async<'a>) * IVar<'a>
      | Get of IVar<'a>
      | Take of IVar<'a>
    
    /// A serialized variable.
    type MVar<'a> internal (?a:'a) =
    
      let [<VolatileField>] mutable state : 'a option = None
    
      let mbp = MailboxProcessor.Start (fun mbp -> async {
        let rec init () = async {
          return! mbp.Scan (function
            | PutAsync (a,rep) ->
              Some (async {
                try
                  let! a = a
                  state <- Some a
                  IVar.put a rep
                  return! loop a
                with ex ->
                  state <- None
                  IVar.error ex rep
                  return! init () })
            | PutOrUpdateAsync (update,rep) ->
              Some (async {
                try
                  let! a = update None
                  state <- Some a
                  IVar.put a rep
                  return! loop (a)
                with ex ->
                  state <- None
                  IVar.error ex rep
                  return! init () })
            | _ ->
              None) }
        and loop (a:'a) = async {
          let! msg = mbp.Receive()
          match msg with
          | PutAsync (a',rep) ->
            try
              let! a = a'
              state <- Some a
              IVar.put a rep
              return! loop (a)
            with ex ->
              state <- Some a
              IVar.error ex rep
              return! loop (a)
          | PutOrUpdateAsync (update,rep) ->
            try
              let! a = update (Some a)
              state <- Some a
              IVar.put a rep
              return! loop (a)
            with ex ->
              state <- Some a
              IVar.error ex rep
              return! loop (a)
          | Get rep ->
            IVar.put a rep
            return! loop (a)
          | Take (rep) ->
            state <- None
            IVar.put a rep
            return! init ()
          | UpdateAsync f ->
            let! a = f a
            return! loop a }
        match a with
        | Some a ->
          state <- Some a
          return! loop (a)
        | None -> 
          return! init () })
    
      do mbp.Error.Add (fun x -> printfn "|MVar|ERROR|%O" x) // shouldn't happen
      
      let postAndAsyncReply f = async {
        let ivar = IVar.create ()
        mbp.Post (f ivar)
        return! IVar.get ivar }
    
      member __.Get () : Async<'a> =
        postAndAsyncReply (Get)
    
      member __.Take () : Async<'a> =
        postAndAsyncReply (fun tcs -> Take(tcs))
    
      member __.GetFast () : 'a option =
        state
    
      member __.Put (a:'a) : Async<'a> =
        __.PutAsync (async.Return a)
    
      member __.PutAsync (a:Async<'a>) : Async<'a> =
        postAndAsyncReply (fun ch -> PutAsync (a,ch))
    
      member __.UpdateStateAsync (update:'a -> Async<'a * 's>) : Async<'s> = async {
        let rep = IVar.create ()
        let up a = async {
          try
            let! (a,s) = update a
            state <- Some a
            IVar.put s rep
            return a
          with ex ->
            state <- Some a
            IVar.error ex rep
            return a  }
        mbp.Post (UpdateAsync up)
        return! IVar.get rep }
    
      member __.PutOrUpdateAsync (update:'a option -> Async<'a>) : Async<'a> =
        postAndAsyncReply (fun ch -> PutOrUpdateAsync (update,ch))
    
      member __.Update (f:'a -> 'a) : Async<'a> =
        __.UpdateAsync (f >> async.Return)
    
      member __.UpdateAsync (update:'a -> Async<'a>) : Async<'a> =
        __.UpdateStateAsync (update >> Async.map diag)
    
      interface IDisposable with
        member __.Dispose () = (mbp :> IDisposable).Dispose()
    
    /// Operations on serialized variables.
    module MVar =
      
      /// Creates an empty MVar.
      let create () : MVar<'a> =
        new MVar<_>()
    
      /// Creates a full MVar.
      let createFull (a:'a) : MVar<'a> =
        new MVar<_>(a)
    
      /// Gets the value of the MVar.
      let get (c:MVar<'a>) : Async<'a> =
        async.Delay (c.Get)
    
      /// Takes an item from the MVar.
      let take (c:MVar<'a>) : Async<'a> =
        async.Delay (c.Take)
      
      /// Returns the last known value, if any, without serialization.
      /// NB: unsafe because the value may be null, but helpful for supporting overlapping
      /// operations.
      let getFastUnsafe (c:MVar<'a>) : 'a option =
        c.GetFast ()
    
      /// Puts an item into the MVar, returning the item that was put.
      /// Returns if the MVar is either empty or full.
      let put (a:'a) (c:MVar<'a>) : Async<'a> =
        async.Delay (fun () -> c.Put a)
    
      /// Puts an item into the MVar, returning the item that was put.
      /// Returns if the MVar is either empty or full.
      let putAsync (a:Async<'a>) (c:MVar<'a>) : Async<'a> =
        async.Delay (fun () -> c.PutAsync a)
    
      /// Puts a new value into an MVar or updates an existing value.
      /// Returns the value that was put or the updated value.
      let putOrUpdateAsync (update:'a option -> Async<'a>) (c:MVar<'a>) : Async<'a> =
        async.Delay (fun () -> c.PutOrUpdateAsync update)
    
      /// Updates an item in the MVar.
      /// Returns when an item is available to update.
      let updateStateAsync (update:'a -> Async<'a * 's>) (c:MVar<'a>) : Async<'s> =
        async.Delay (fun () -> c.UpdateStateAsync update)
    
      /// Updates an item in the MVar.
      /// Returns when an item is available to update.
      let update (update:'a -> 'a) (c:MVar<'a>) : Async<'a> =
        async.Delay (fun () -> c.Update update)
    
      /// Updates an item in the MVar.
      /// Returns when an item is available to update.
      let updateAsync (update:'a -> Async<'a>) (c:MVar<'a>) : Async<'a> =
        async.Delay (fun () -> c.UpdateAsync update)