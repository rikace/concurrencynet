namespace FSharp.Parallelx.ChannelEx

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks



  //  let putString = Channel<string>("puts")
  //  let putInt = Channel<int>("puti")
  //  let get = SyncChannel<string>("get")
  //
  //  get.repl.Reply("Echo " + v)
  //  putString.Call(sprintf "World (#%d)" i) 
  //  get.AsyncCall()
  //
  //  let put = SyncChannel<string, unit>()
  //  let get = SyncChannel<string>()
  //  let empty = Channel<unit>()
  //  let contains = Channel<string>()
  //  
  //  contains.Put(s)
type Pool private () =
    let queue = new BlockingCollection<_>(ConcurrentBag())
    let work() =
        while true do queue.Take()()

    let long = TaskCreationOptions.LongRunning
    let task = Task.Factory.StartNew(work, long)
    static let self = Pool()
    member private this.Add f = queue.Add f
    static member Spawn(f:unit -> unit) = self.Add f


module TaskPool =
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Threading.Tasks
    open System.Threading
    open System.Linq

    open System
    type private Context = {cont:unit -> unit; context:ExecutionContext}

    type TaskPool private () =
        let workers = 2
        let cts = new CancellationTokenSource()
        let queue = Array.init workers (fun _ -> new BlockingCollection<Context>(ConcurrentQueue()))
        let work() =
            while not <| queue.All(fun bc -> bc.IsCompleted) && not cts.IsCancellationRequested do
            let ctx = ref Unchecked.defaultof<Context>
            if BlockingCollection<_>.TryTakeFromAny(queue, ctx) >= 0 then
                let ctx = ctx.Value
                let ec = ctx.context.CreateCopy()
                ExecutionContext.Run(ec, (fun _ -> ctx.cont()), null)
        let long = TaskCreationOptions.LongRunning
        let tasks = Array.init workers (fun _ -> new Task(work, cts.Token, long))
        do tasks |> Array.iter(fun task -> task.Start())

        static let self = TaskPool()
        
        member private this.Stop() =
            for bc in queue do bc.CompleteAdding()
            cts.Cancel()
        member private this.Add continutaion =
            let ctx = {cont = continutaion; context = ExecutionContext.Capture() }
            BlockingCollection<_>.TryAddToAny(queue, ctx) |> ignore
       
        static member Add(continuation:unit -> unit) = self.Add continuation
        static member Stop() = self.Stop()
        
        
        
type Future<'T> = ('T -> unit) -> unit

type FutureEx<'T> = 
  abstract Start : ('T -> unit) -> unit

[<Sealed>]
type Future() =
    static let self = Future()
    
    /// Creates a computation that composes 'a' with the result of 'f'
    let bind (f:'a -> FutureEx<'b>) (a:FutureEx<'a>) : FutureEx<'b> = 
        { new FutureEx<'b> with
            member x.Start(g) =
                a.Start(fun a ->
                let ab = f a
                ab.Start(g) ) }

    /// A computation that immediately returns the given value
    let unit v = 
        { new FutureEx<_> with
            member x.Start(f) = f v }

    /// Start the computation and do nothing when it finishes.
    let start (a:FutureEx<_>) =
        a.Start(fun () -> ())
    
    
    member inline this.Return(x: 'T) : Future<'T> =
        fun f -> f x
    member inline this.ReturnFrom(x: Future<'T>) = x
    member inline this.Bind
        (x: Future<'T1>, f: 'T1 -> Future<'T2>) : Future<'T2> =
        fun k -> x (fun v -> f v k)
    static member inline Start(x: Future<unit>) =
        // Pool.Spawn(fun () -> x ignore)
        TaskPool.TaskPool.Add(fun () -> x ignore)
        
    static member inline RunSynchronously(x: Future<'T>) : 'T =
        let res = ref Unchecked.defaultof<_>
        let sem = new System.Threading.SemaphoreSlim(0)
        Pool.Spawn(fun () ->
            x (fun v ->
                res := v
                ignore (sem.Release())))
        sem.Wait()
        !res
    static member inline FromContinuations
        (f : ('T -> unit) *
             (exn -> unit) *
             (System.OperationCanceledException -> unit)
                -> unit) : Future<'T> =
        fun k -> f (k, ignore, ignore)


module FutureExp = 
    /// An async computation is an object that can be started.
    /// It eventually calls the given continuation with a result 'T.
    type Future<'T> = 
      abstract Start : ('T -> unit) -> unit
    
    module Future = 
      /// Creates a computation that composes 'a' with the result of 'f'
      let bind (f:'a -> Future<'b>) (a:Future<'a>) : Future<'b> = 
        { new Future<'b> with
            member x.Start(g) =
              a.Start(fun a ->
                let ab = f a
                ab.Start(g) ) }
    
      /// A computation that immediately returns the given value
      let unit v = 
        { new Future<_> with
            member x.Start(f) = f v }
    
      /// Start the computation and do nothing when it finishes.
      let start (a:Future<_>) =
        a.Start(fun () -> ())
    
      /// Defines a computation builed for asyncs.
      /// For and While are defined in terms of bind and unit.
      type FutureBuilder() = 
        member x.Return(v) = unit v
        member x.Bind(a, f) = bind f a
        member x.Zero() = unit ()
    
        member x.For(vals, f) =
          match vals with
          | [] -> unit ()
          | v::vs -> f v |> bind (fun () -> x.For(vs, f))
    
        member x.Delay(f:unit -> Future<_>) =
          { new Future<_> with
              member x.Start(h) = f().Start(h) }
    
        member x.While(c, f) = 
          if not (c ()) then unit ()
          else f |> bind (fun () -> x.While(c, f))
    
        member x.ReturnFrom(a) = a
              
    let future = Future.FutureBuilder()
    
//      type AsyncSeqBuilder() =
//    member x.Yield(v) = singleton v
//    // This looks weird, but it is needed to allow:
//    //
//    //   while foo do
//    //     do! something
//    //
//    // because F# translates body as Bind(something, fun () -> Return())
//    member x.Return(()) = empty
//    member x.YieldFrom(s) = s
//    member x.Zero () = empty
//    member x.Bind (inp:Async<'T>, body : 'T -> AsyncSeq<'U>) : AsyncSeq<'U> = 
//      async.Bind(inp, body)
//    member x.Combine (seq1:AsyncSeq<'T>,seq2:AsyncSeq<'T>) = 
//      append seq1 seq2
//    member x.While (gd, seq:AsyncSeq<'T>) = 
//      if gd() then x.Combine(seq,x.Delay(fun () -> x.While (gd, seq))) else x.Zero()
//    member x.Delay (f:unit -> AsyncSeq<'T>) = 
//      async.Delay(f)


module FutureSeqExp =
    
    let future = FutureExp.Future.FutureBuilder()
    /// An asynchronous sequence is a computation that asynchronously produces 
    /// a next cell of a linked list - the next cell can be either empty (Nil)
    /// or it can be a value, followed by another asynchronous sequence.
    type FutureSeq<'T> = FutureExp.Future<FutureSeqRes<'T>>
    and FutureSeqRes<'T> = 
      | Nil
      | Cons of 'T * FutureSeq<'T>
        
    /// Generates an asynchronous sequence that reads the 
    /// specified files one by one and returns them.
    let rec readFiles read files : FutureSeq<_> = future {
        match files with
        | [] -> return Nil
        | f::files ->
            let! d = read f
            return Cons(d, readFiles read files) }    
    
    
    /// Iterate over the whole asynchronous sequence as fast as
    /// possible and run the specified function `f` for each value.
    let rec run f (aseq:FutureSeq<_>) = future { 
        let! next = aseq
        match next with
        | Nil -> return ()
        | Cons(v, vs) -> 
            f v 
            return! run f vs }
    
    /// A function that iterates over an asynchronous sequence 
    /// and returns a new async sequence with transformed values
    let rec map f (aseq:FutureSeq<'T>) : FutureSeq<'R> = future { 
        let! next = aseq
        match next with
        | Nil -> return Nil
        | Cons(v, vs) -> return Cons(f v, map f vs) }
      
     /// Creates an empty asynchronou sequence that immediately ends
    [<GeneralizableValue>]
    let empty<'T> : FutureSeq<'T> = 
        future { return Nil }
 
    /// Creates an asynchronous sequence that generates a single element and then ends
    let singleton (v:'T) : FutureSeq<'T> = 
        future { return Cons(v, empty) }

    /// Yields all elements of the first asynchronous sequence and then 
    /// all elements of the second asynchronous sequence.
    let rec append (seq1: FutureSeq<'T>) (seq2: FutureSeq<'T>) : FutureSeq<'T> = 
        future { let! v1 = seq1
            match v1 with 
            | Nil -> return! seq2
            | Cons (h,t) -> return Cons(h,append t seq2) }
    
    
    /// Helper async computation that calls another 
    /// computation and ignores whatever value it returns
    let futureIgnore a = future {
      let! _ = a
      return () }      
      
    
//  [ for i in 0 .. 364 -> sprintf "/data/%d.json" i ]
//  |> FutureSeq.readFiles
//  // TODO: Choose one of the following to either wait for a click, or wait for 250ms
//  // |> AsyncSeq.delay (asyncIgnore (Async.awaitObservable (Observable.on "click" Section4.next)))
//  // |> AsyncSeq.delay (Async.sleep 250)
//  |> FutureSeq.map jsonParse<Prices>
//  |> FutureSeq.map (fun p -> p.rates |> Array.find (fun r -> r.code = "GBP"))
//  |> FutureSeq.run (fun gbp -> Section4.current.innerText <- sprintf "GBP: %A" gbp.value)
//  |> Future.start          
      
      
//      
//  /// Tries to get the next element of an asynchronous sequence
//  /// and returns either the value or an exception
//  let internal tryNext (input:AsyncSeq<_>) = async { 
//    try 
//      let! v = input
//      return Choice1Of2 v
//    with e -> 
//      return Choice2Of2 e }
//
//  /// Implements the 'TryWith' functionality for computation builder
//  let rec internal tryWith (input : AsyncSeq<'T>) handler =  asyncSeq { 
//    let! v = tryNext input
//    match v with 
//    | Choice1Of2 Nil -> ()
//    | Choice1Of2 (Cons (h, t)) -> 
//        yield h
//        yield! tryWith t handler
//    | Choice2Of2 rest -> 
//        yield! handler rest }
// 
//  /// Implements the 'TryFinally' functionality for computation builder
//  let rec internal tryFinally (input : AsyncSeq<'T>) compensation = asyncSeq {
//    let! v = tryNext input
//    match v with 
//    | Choice1Of2 Nil -> 
//        compensation()
//    | Choice1Of2 (Cons (h, t)) -> 
//        yield h
//        yield! tryFinally t compensation
//    | Choice2Of2 e -> 
//        compensation()
//        yield! raise e }
//
//  /// Creates an asynchronou sequence that iterates over the given input sequence.
//  /// For every input element, it calls the the specified function and iterates
//  /// over all elements generated by that asynchronous sequence.
//  /// This is the 'bind' operation of the computation expression (exposed using
//  /// the 'for' keyword in asyncSeq computation).
//  let rec collect f (input : AsyncSeq<'T>) : AsyncSeq<'TResult> = asyncSeq {
//    let! v = input
//    match v with
//    | Nil -> ()
//    | Cons(h, t) ->
//        yield! f h
//        yield! collect f t }
//
//
//  // Add additional methods to the 'asyncSeq' computation builder
//  type AsyncSeqBuilder with
//    member x.TryFinally (body: AsyncSeq<'T>, compensation) = 
//      tryFinally body compensation   
//    member x.TryWith (body: AsyncSeq<_>, handler: (exn -> AsyncSeq<_>)) = 
//      tryWith body handler
//    member x.Using (resource:#IDisposable, binder) = 
//      tryFinally (binder resource) (fun () -> 
//        if box resource <> null then resource.Dispose())
//
//    /// For loop that iterates over a synchronous sequence (and generates
//    /// all elements generated by the asynchronous body)
//    member x.For(seq:seq<'T>, action:'T -> AsyncSeq<'TResult>) = 
//      let enum = seq.GetEnumerator()
//      x.TryFinally(x.While((fun () -> enum.MoveNext()), x.Delay(fun () -> 
//        action enum.Current)), (fun () -> 
//          if enum <> null then enum.Dispose() ))
//
//    /// Asynchronous for loop - for all elements from the input sequence,
//    /// generate all elements produced by the body (asynchronously). See
//    /// also the AsyncSeq.collect function.
//    member x.For (seq:AsyncSeq<'T>, action:'T -> AsyncSeq<'TResult>) = 
//      collect action seq
//
//
//  // Add asynchronous for loop to the 'async' computation builder
//  type Microsoft.FSharp.Control.AsyncBuilder with
//    member x.For (seq:AsyncSeq<'T>, action:'T -> Async<unit>) = 
//      async.Bind(seq, function
//        | Nil -> async.Zero()
//        | Cons(h, t) -> async.Combine(action h, x.For(t, action)))
//
//  // --------------------------------------------------------------------------
//  // Additional combinators (implemented as async/asyncSeq computations)
//
//  /// Builds a new asynchronous sequence whose elements are generated by 
//  /// applying the specified function to all elements of the input sequence.
//  ///
//  /// The specified function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let mapAsync f (input : AsyncSeq<'T>) : AsyncSeq<'TResult> = asyncSeq {
//    for itm in input do 
//      let! v = f itm
//      yield v }
//
//  /// Asynchronously iterates over the input sequence and generates 'x' for 
//  /// every input element for which the specified asynchronous function 
//  /// returned 'Some(x)' 
//  ///
//  /// The specified function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let chooseAsync f (input : AsyncSeq<'T>) : AsyncSeq<'R> = asyncSeq {
//    for itm in input do
//      let! v = f itm
//      match v with 
//      | Some v -> yield v 
//      | _ -> () }
//
//  /// Builds a new asynchronous sequence whose elements are those from the
//  /// input sequence for which the specified function returned true.
//  ///
//  /// The specified function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let filterAsync f (input : AsyncSeq<'T>) = asyncSeq {
//    for v in input do
//      let! b = f v
//      if b then yield v }
//
//  /// Asynchronously returns the last element that was generated by the
//  /// given asynchronous sequence (or the specified default value).
//  let rec lastOrDefault def (input : AsyncSeq<'T>) = async {
//    let! v = input
//    match v with 
//    | Nil -> return def
//    | Cons(h, t) -> return! lastOrDefault h t }
//
//  /// Asynchronously returns the first element that was generated by the
//  /// given asynchronous sequence (or the specified default value).
//  let firstOrDefault def (input : AsyncSeq<'T>) = async {
//    let! v = input
//    match v with 
//    | Nil -> return def
//    | Cons(h, _) -> return h }
//
//  /// Aggregates the elements of the input asynchronous sequence using the
//  /// specified 'aggregation' function. The result is an asynchronous 
//  /// sequence of intermediate aggregation result.
//  ///
//  /// The aggregation function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let rec scanAsync f (state:'TState) (input : AsyncSeq<'T>) = asyncSeq {
//    let! v = input
//    match v with
//    | Nil -> ()
//    | Cons(h, t) ->
//        let! v = f state h
//        yield v
//        yield! t |> scanAsync f v }
//
//  /// Iterates over the input sequence and calls the specified function for
//  /// every value (to perform some side-effect asynchronously).
//  ///
//  /// The specified function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let rec iterAsync f (input : AsyncSeq<'T>) = async {
//    for itm in input do 
//      do! f itm }
//
//  /// Returns an asynchronous sequence that returns pairs containing an element
//  /// from the input sequence and its predecessor. Empty sequence is returned for
//  /// singleton input sequence.
//  let rec pairwise (input : AsyncSeq<'T>) = asyncSeq {
//    let! v = input
//    match v with
//    | Nil -> ()
//    | Cons(h, t) ->
//        let prev = ref h
//        for v in t do
//          yield (!prev, v)
//          prev := v }
//
//  /// Aggregates the elements of the input asynchronous sequence using the
//  /// specified 'aggregation' function. The result is an asynchronous 
//  /// workflow that returns the final result.
//  ///
//  /// The aggregation function is asynchronous (and the input sequence will
//  /// be asked for the next element after the processing of an element completes).
//  let rec foldAsync f (state:'TState) (input : AsyncSeq<'T>) = 
//    input |> scanAsync f state |> lastOrDefault state
//
//  /// Same as AsyncSeq.foldAsync, but the specified function is synchronous
//  /// and returns the result of aggregation immediately.
//  let rec fold f (state:'TState) (input : AsyncSeq<'T>) = 
//    foldAsync (fun st v -> f st v |> async.Return) state input 
//
//  /// Same as AsyncSeq.scanAsync, but the specified function is synchronous
//  /// and returns the result of aggregation immediately.
//  let rec scan f (state:'TState) (input : AsyncSeq<'T>) = 
//    scanAsync (fun st v -> f st v |> async.Return) state input 
//
//  /// Same as AsyncSeq.mapAsync, but the specified function is synchronous
//  /// and returns the result of projection immediately.
//  let map f (input : AsyncSeq<'T>) = 
//    mapAsync (f >> async.Return) input
//
//  /// Same as AsyncSeq.iterAsync, but the specified function is synchronous
//  /// and performs the side-effect immediately.
//  let iter f (input : AsyncSeq<'T>) = 
//    iterAsync (f >> async.Return) input
//
//  /// Same as AsyncSeq.chooseAsync, but the specified function is synchronous
//  /// and processes the input element immediately.
//  let choose f (input : AsyncSeq<'T>) = 
//    chooseAsync (f >> async.Return) input
//
//  /// Same as AsyncSeq.filterAsync, but the specified predicate is synchronous
//  /// and processes the input element immediately.
//  let filter f (input : AsyncSeq<'T>) =
//    filterAsync (f >> async.Return) input
//    
//  // --------------------------------------------------------------------------
//  // Converting from/to synchronous sequences or IObservables
//
//  /// Creates an asynchronous sequence that lazily takes element from an
//  /// input synchronous sequence and returns them one-by-one.
//  let ofSeq (input : seq<'T>) = asyncSeq {
//    for el in input do 
//      yield el }
//
//  /// A helper type for implementation of buffering when converting 
//  /// observable to an asynchronous sequence
//  type internal BufferMessage<'T> = 
//    | Get of AsyncReplyChannel<'T>
//    | Put of 'T
//
//  /// Converts observable to an asynchronous sequence using an agent with
//  /// a body specified as the argument. The returnd async sequence repeatedly 
//  /// sends 'Get' message to the agent to get the next element. The observable
//  /// sends 'Put' message to the agent (as new inputs are generated).
//  let internal ofObservableUsingAgent (input : System.IObservable<_>) f = 
//    asyncSeq {  
//      use agent = AutoCancelAgent.Start(f)
//      use d = input |> Observable.asUpdates
//                    |> Observable.subscribe (Put >> agent.Post)
//      
//      let rec loop() = asyncSeq {
//        let! msg = agent.PostAndAsyncReply(Get)
//        match msg with
//        | ObservableUpdate.Error e -> raise e
//        | ObservableUpdate.Completed -> ()
//        | ObservableUpdate.Next v ->
//            yield v
//            yield! loop() }
//      yield! loop() }
//
//  /// Converts observable to an asynchronous sequence. Values that are produced
//  /// by the observable while the asynchronous sequence is blocked are stored to 
//  /// an unbounded buffer and are returned as next elements of the async sequence.
//  let ofObservableBuffered (input : System.IObservable<_>) = 
//    ofObservableUsingAgent input (fun mbox -> async {
//        let buffer = new System.Collections.Generic.Queue<_>()
//        let repls = new System.Collections.Generic.Queue<_>()
//        while true do
//          // Receive next message (when observable ends, caller will
//          // cancel the agent, so we need timeout to allow cancleation)
//          let! msg = mbox.TryReceive(200)
//          match msg with 
//          | Some(Put(v)) -> buffer.Enqueue(v)
//          | Some(Get(repl)) -> repls.Enqueue(repl)
//          | _ -> () 
//          // Process matching calls from buffers
//          while buffer.Count > 0 && repls.Count > 0 do
//            repls.Dequeue().Reply(buffer.Dequeue())  })
//
//
//  /// Converts observable to an asynchronous sequence. Values that are produced
//  /// by the observable while the asynchronous sequence is blocked are discarded
//  /// (this function doesn't guarantee that asynchronou ssequence will return 
//  /// all values produced by the observable)
//  let ofObservable (input : System.IObservable<_>) = 
//    ofObservableUsingAgent input (fun mbox -> async {
//      while true do 
//        // Allow timeout (when the observable ends, caller will
//        // cancel the agent, so we need timeout to allow cancellation)
//        let! msg = mbox.TryReceive(200)
//        match msg with 
//        | Some(Put _) | None -> 
//            () // Ignore put or no message 
//        | Some(Get repl) ->
//            // Reader is blocked, so next will be Put
//            // (caller will not stop the agent at this point,
//            // so timeout is not necessary)
//            let! v = mbox.Receive()
//            match v with 
//            | Put v -> repl.Reply(v)
//            | _ -> failwith "Unexpected Get" })
//
//  /// Converts asynchronous sequence to an IObservable<_>. When the client subscribes
//  /// to the observable, a new copy of asynchronous sequence is started and is 
//  /// sequentially iterated over (at the maximal possible speed). Disposing of the 
//  /// observer cancels the iteration over asynchronous sequence. 
//  let toObservable (aseq:AsyncSeq<_>) =
//    let start (obs:IObserver<_>) =
//      async {
//        try 
//          for v in aseq do obs.OnNext(v)
//          obs.OnCompleted()
//        with e ->
//          obs.OnError(e) }
//      |> Async.StartDisposable
//    { new IObservable<_> with
//        member x.Subscribe(obs) = start obs }
//
//  /// Converts asynchronous sequence to a synchronous blocking sequence.
//  /// The elements of the asynchronous sequence are consumed lazily.
//  let toBlockingSeq (input : AsyncSeq<'T>) = 
//    // Write all elements to a blocking buffer and then add None to denote end
//    let buf = new BlockingQueueAgent<_>(1)
//    async {
//      do! iterAsync (Some >> buf.AsyncAdd) input
//      do! buf.AsyncAdd(None) } |> Async.Start
//
//    // Read elements from the blocking buffer & return a sequences
//    let rec loop () = seq {
//      match buf.Get() with
//      | None -> ()
//      | Some v -> 
//          yield v
//          yield! loop() }
//    loop ()
//
//  /// Create a new asynchronous sequence that caches all elements of the 
//  /// sequence specified as the input. When accessing the resulting sequence
//  /// multiple times, the input will still be evaluated only once
//  let rec cache (input : AsyncSeq<'T>) = 
//    let agent = Agent<AsyncReplyChannel<_>>.Start(fun agent -> async {
//      let! repl = agent.Receive()
//      let! next = input
//      let res = 
//        match next with 
//        | Nil -> Nil
//        | Cons(h, t) -> Cons(h, cache t)
//      repl.Reply(res)
//      while true do
//        let! repl = agent.Receive()
//        repl.Reply(res) })
//    async { return! agent.PostAndAsyncReply(id) }
//
//  // --------------------------------------------------------------------------
//
//  /// Combines two asynchronous sequences into a sequence of pairs. 
//  /// The values from sequences are retrieved in parallel. 
//  let rec zip (input1 : AsyncSeq<'T1>) (input2 : AsyncSeq<'T2>) : AsyncSeq<_> = async {
//    let! ft = input1 |> Async.StartChild
//    let! s = input2
//    let! f = ft
//    match f, s with 
//    | Cons(hf, tf), Cons(hs, ts) ->
//        return Cons( (hf, hs), zip tf ts)
//    | _ -> return Nil }
//
//  /// Returns elements from an asynchronous sequence while the specified 
//  /// predicate holds. The predicate is evaluated asynchronously.
//  let rec takeWhileAsync p (input : AsyncSeq<'T>) : AsyncSeq<_> = async {
//    let! v = input
//    match v with
//    | Cons(h, t) -> 
//        let! res = p h
//        if res then 
//          return Cons(h, takeWhileAsync p t)
//        else return Nil
//    | Nil -> return Nil }
//
//  /// Skips elements from an asynchronous sequence while the specified 
//  /// predicate holds and then returns the rest of the sequence. The 
//  /// predicate is evaluated asynchronously.
//  let rec skipWhileAsync p (input : AsyncSeq<'T>) : AsyncSeq<_> = async {
//    let! v = input
//    match v with
//    | Cons(h, t) -> 
//        let! res = p h
//        if res then return! skipWhileAsync p t
//        else return! t
//    | Nil -> return Nil }
//
//  /// Returns elements from an asynchronous sequence while the specified 
//  /// predicate holds. The predicate is evaluated synchronously.
//  let rec takeWhile p (input : AsyncSeq<'T>) = 
//    takeWhileAsync (p >> async.Return) input
//
//  /// Skips elements from an asynchronous sequence while the specified 
//  /// predicate holds and then returns the rest of the sequence. The 
//  /// predicate is evaluated asynchronously.
//  let rec skipWhile p (input : AsyncSeq<'T>) = 
//    skipWhileAsync (p >> async.Return) input
//
//  /// Returns the first N elements of an asynchronous sequence
//  let rec take count (input : AsyncSeq<'T>) : AsyncSeq<_> = async {
//    if count > 0 then
//      let! v = input
//      match v with
//      | Cons(h, t) -> 
//          return Cons(h, take (count - 1) t)
//      | Nil -> return Nil 
//    else return Nil }
//
//  /// Skips the first N elements of an asynchronous sequence and
//  /// then returns the rest of the sequence unmodified.
//  let rec skip count (input : AsyncSeq<'T>) : AsyncSeq<_> = async {
//    if count > 0 then
//      let! v = input
//      match v with
//      | Cons(h, t) -> 
//          return! skip (count - 1) t
//      | Nil -> return Nil 
//    else return! input }
//
//[<AutoOpen>]
//module AsyncSeqExtensions = 
//  /// Builds an asynchronou sequence using the computation builder syntax
//  let asyncSeq = new AsyncSeq.AsyncSeqBuilder()
//
//  // Add asynchronous for loop to the 'async' computation builder
//  type Microsoft.FSharp.Control.AsyncBuilder with
//    member x.For (seq:AsyncSeq<'T>, action:'T -> Async<unit>) = 
//      async.Bind(seq, function
//        | Nil -> async.Zero()
//        | Cons(h, t) -> async.Combine(action h, x.For(t, action)))
//
//module Seq = 
//  open FSharp.Control
//
//  /// Converts asynchronous sequence to a synchronous blocking sequence.
//  /// The elements of the asynchronous sequence are consumed lazily.
//  let ofAsyncSeq (input : AsyncSeq<'T>) =
//    AsyncSeq.toBlockingSeq input

      
      
[<Sealed>]
type Channel<'T>() =
    let readers = Queue()
    let writers = Queue()

    member this.Read ok =
        let task =
            lock readers <| fun () ->
                if writers.Count = 0 then
                    readers.Enqueue ok
                    None
                else
                    Some (writers.Dequeue())
        match task with
        | None -> ()
        | Some (value, cont) ->
            Pool.Spawn cont
            ok value

    member this.Write(x: 'T, ok) =
        let task =
            lock readers <| fun () ->
                if readers.Count = 0 then
                    writers.Enqueue(x, ok)
                    None
                else
                    Some (readers.Dequeue())
        match task with
        | None -> ()
        | Some cont ->
            Pool.Spawn ok
            cont x

    member inline this.Read() =
        Async.FromContinuations(fun (ok, _, _) ->
            this.Read ok)

    member inline this.Write x =
        Async.FromContinuations(fun (ok, _, _) ->
            this.Write(x, ok))

module ChanAsync =
  let test (n: int) =
    let chan = Channel()
    let rec writer (i: int) =
        async {
            if i = 0 then
                return! chan.Write 0
            else
                do! chan.Write i
                return! writer (i - 1)
        }
    let rec reader sum =
        async {
            let! x = chan.Read()
            if x = 0
            then return sum
            else return! reader (sum + x)
        }
    Async.Start(writer n)
    let clock = System.Diagnostics.Stopwatch()
    clock.Start()
    let r = Async.RunSynchronously(reader 0)
    stdout.WriteLine("Hops per second: {0}",
        float n / clock.Elapsed.TotalSeconds)
    r


type internal ChannleCommand<'a> =
    | Read of ('a -> unit) * AsyncReplyChannel<unit>
    | Write of 'a * (unit -> unit) * AsyncReplyChannel<unit>

[<Sealed>]
type ChannelAgent<'a>() =

    let agent = MailboxProcessor.Start(fun inbox ->
        let readers = Queue()
        let writers = Queue()

        let rec loop() = async {
            let! msg = inbox.Receive()
            match msg with
            | Read(ok , reply) ->
                if writers.Count = 0 then
                    readers.Enqueue ok
                    reply.Reply( () )
                else
                    let (value, cont) =writers.Dequeue()
                    Pool.Spawn cont
                    reply.Reply( (ok value) )

                return! loop()
            | Write(x, ok, reply) ->
                if readers.Count = 0 then
                    writers.Enqueue(x, ok)
                    reply.Reply( () )
                else
                    let cont = readers.Dequeue()
                    Pool.Spawn ok
                    reply.Reply( (cont x) )
                return! loop() }
        loop())


    member this.Read(ok: 'a -> unit)  =  // ('a -> unit) -> unit
        agent.PostAndAsyncReply(fun f -> Read(ok, f)) |> Async.RunSynchronously

    member this.Write(x: 'a, ok:unit -> unit)  =
        agent.PostAndAsyncReply(fun f -> Write(x, ok, f)) |> Async.RunSynchronously

    member this.Read() =
        Async.FromContinuations(fun (ok, _,_) -> agent.PostAndAsyncReply(fun f -> Read(ok, f)) |> Async.RunSynchronously)

    member this.Write (x:'a) =
        Async.FromContinuations(fun (ok, _,_) ->
            agent.PostAndAsyncReply(fun f -> Write(x, ok, f)) |> Async.RunSynchronously )    

      
module ChannelEx =
    open FSharp.Parallelx.AgentEx.AgentTypes
    open FSharp.Parallelx.AgentEx.AgentUtils
    open System.Collections.Generic
        
    [<AbstractClass>]
    type AgentRef<'a>(name : string) =
        member val Name = name with get, set
        abstract Start : unit -> unit
        abstract Receive : unit -> Async<'a>
        abstract Post : 'a -> unit
        abstract PostAndTryAsyncReply : (IAsyncReplyChannel<'b> -> 'a) -> Async<'b option>      

    
    type AgentCh<'a>(name:string, comp, ?token) as this =
        inherit AgentRef<'a>(name)
        
        let agent = new MailboxProcessor<_>((fun inbox -> comp (this :> AgentRef<_>)), ?cancellationToken = token)     

        override x.Post(msg:'a) = agent.Post(msg)
        override x.PostAndTryAsyncReply(builder) = agent.PostAndTryAsyncReply(fun rc -> builder(new MailboxReplyChannel<_>(rc)))

        override x.Receive() = agent.Receive()
        override x.Start() = agent.Start()
               
        
    [<Interface>]
    type 'a IChannel =
        inherit IAsyncReplyChannel<'a>
        abstract Post : 'a -> unit
        
    [<RequireQualifiedAccess>]    
    module ChannelEx =
        let put (channel : IChannel<'a>) (item : 'a) = ()
        let get (channel : IChannel<'a>) = Unchecked.defaultof<'a>
            

                    
    [<Struct; CustomComparison; CustomEquality>]
    type ChannelKey =
        { ty : Type; name : string }
        
        interface IComparable<ChannelKey> with
            member x.CompareTo(ck) = 0                
        interface IComparable with
            member x.CompareTo(ck) = 0
        override x.Equals(o) = true
        override x.GetHashCode() = 1

    
    type ChannelMsg<'a, 'b> =
        | Get of 'a
        | Put of 'b
        
    type ChannelCoordinatorMsg<'a, 'b>=
        | Subscribe of ChannelKey * (Agent<ChannelMsg<'a, 'b>> -> Async<unit>)
        | Unsubscribe of ChannelKey
        | Forward of ChannelKey * ChannelMsg<'a, 'b>
        
            
    let create() : Map<ChannelKey, Agent<ChannelMsg<_,_>>> = Map.empty
    
    let createAgent f = new Agent<ChannelMsg<_,_>>(f)
    
    let behavior' = (fun (inbox : Agent<_>) -> async.Return ())
    
    let register (ref:AgentRef<'a>) agents =
        match Map.tryFind ref.Name agents with
        | Some(refs) ->
            Map.add ref.Name (ref :: refs) agents
        | None ->
            Map.add ref.Name [ref] agents
    
    let resolve id agents =
        Map.find id agents             
        
    type ChannelsCoordinator() =
        
        let agent = Agent<ChannelCoordinatorMsg<_,_>>.Start(fun inbox ->
            
            let rec loop (channels : Map<ChannelKey, Agent<ChannelMsg<_,_>>>) = async {
                
                let! msg = inbox.Receive()
                match msg with
                | Subscribe(k, f) -> return! loop (channels |> Map.add k (createAgent f))
                | Unsubscribe k -> return! loop (channels |> Map.remove k)
                | Forward (k, m) ->
                    match channels |> Map.tryFind k with
                    | Some c -> c.Post m
                    | None -> ()
                
            }
            loop Map.empty)
    
    let merge (op1 : Async<'a>) (op2 : Async<'b>) = async {
        let! opA = Async.StartChild op1
        let! opB = Async.StartChild op2
        let! resA = opA
        let! resB = opB
        return (resA, resB)
    }
        
//    type PoolScheduler() =
//        inherit TaskScheduler()
//        
//        // Tpl Dataflow 
//        // Buffer for input -> outpiy t taks 
//        // 
//        
//        let inlineTask = TaskContinuationOptions.ExecuteSynchronously
        
    type ChannelSt<'a, 'b> = { getCh : Agent<'a> ; putCh : Agent<'b> }
    
//    let putString = Channel<string>()
//let putInt = Channel<int>()
//let getString = ReplyChannel<string>()
//
//join {
//  match! getString, putString, putInt with
//  | repl, v, ? -> repl.Reply("Echo " + v)
//  | repl, ?, v -> repl.Reply("Echo " + (string v)) }
        
// Put values to 'putString' and 'putInt' channels
//async {
//  for i in 1 .. 5 do 
//    putString.Call("Hello!")
//    putInt.Call(i)
//    do! Async.Sleep(100) } |> Async.Start
//
//// Call 'getString' asynchronously to read replies
//async { 
//  while true do
//    let! repl = getString.AsyncCall()
//    printfn "got: %s" repl } |> Async.Start        
    
    type Pool private () =
        let queue = new BlockingCollection<_>(ConcurrentBag())
        let work() =
            while true do queue.Take()()
            
        let long = TaskCreationOptions.LongRunning
        let task = Task.Factory.StartNew(work, long)
        static let self = Pool()
        member private this.Add f = queue.Add f
        static member Spawn(f:unit -> unit) = self.Add f
     

module Channels =
    
    type internal ChannelCommand<'a> =
        | Read of ('a -> unit) * AsyncReplyChannel<unit>
        | Write of 'a * (unit -> unit) * AsyncReplyChannel<unit>   
        
    type IAgent =
        abstract Post : 'a -> unit 
        abstract Send : 'a -> unit 
        
    type Agent () =
        interface IAgent with 
            member __.Post a = ()
            member __.Send a = ()
            
