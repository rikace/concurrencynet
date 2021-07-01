#load "Fibers.fs"
#r "../../packages//System.Threading/lib/"
type Term = 
    | Lambda of string * Term
    | App of Term * Term
    | Var of string

let rec (|Lambdas|_|) = function
    //  Is it a sequences of lambdas?
    | Lambda(p1, Lambdas(parameterNames, body)) -> Some(p1::parameterNames, body)
    //  Is this the inner-most lambda?
    | Lambda(p1, body)                          -> Some([p1], body)
    //  If the term wasn't even a lambda, let's not match.
    | term                                      -> None

let rec (|FSharp|) = function
    //  Here we use our new pattern
    | Lambdas(parameters, FSharp(body)) -> sprintf "fun %s -> %s" (String.concat " " parameters) body
    | App(FSharp(func), FSharp(arg))    -> sprintf "(%s %s)" func arg
    | Var(name)                         -> name


let rec toFSharp = function
    | Var(name)               -> name
    | Lambda(paramName, body) -> sprintf "(fun %s -> %s)" paramName (toFSharp body)
    | App(func, arg)          -> sprintf "(%s %s)" (toFSharp func) (toFSharp arg)
    

let ex1 = Lambda("a", Lambda("b", Lambda("c", Var("c"))))
ex1 |> toFSharp |> printfn "%s"



module P = Microsoft.FSharp.Quotations.Patterns
let rec fromExpr = function
    | P.Var(v)                              -> Term.Var(v.Name)
    | P.Lambda(var, LTerm(expr))            -> Term.Lambda(var.Name, expr)
    | P.Application(LTerm(ex1), LTerm(ex2)) -> Term.App(ex1, ex2)
    | x -> failwithf "Don't know how to deal with %A" x
and (|LTerm|) = fromExpr

let ID = fromExpr <@ fun x f -> f x @>
printfn "%A" ID

module MyActor =
    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Collections.Concurrent
    open System.Threading

    [<Struct>]
    type SystemMessage =
        | Die
        | Restart of exn
        
    module Status =
        let [<Literal>] Idle = 0
        let [<Literal>] Occupied = 1
        let [<Literal>] Stopped = 2

    [<Sealed>]
    type Actor<'state, 'msg>(initState: 'state, handler: 'state -> 'msg -> 'state) as this =
        static let deadLetters = Event<'msg>()
        static let callback: WaitCallback = new WaitCallback(fun o ->
            let actor = o :?> Actor<'state, 'msg>
            actor.Execute())
        let mutable status: int = Status.Idle
        let systemMessages = ConcurrentQueue<SystemMessage>()
        let userMessages = ConcurrentQueue<'msg>()
        let mutable state = initState
        
        let stop () =
            Interlocked.Exchange(&status, Status.Stopped) |> ignore
            for msg in userMessages do
                deadLetters.Trigger msg
        member private this.Schedule() =
            if Interlocked.CompareExchange(&status, Status.Occupied, Status.Idle) = Status.Idle
            //then ThreadPool.UnsafeQueueUserWorkItem(this, false) |> ignore
            then ThreadPool.UnsafeQueueUserWorkItem(callback, this) |> ignore
            
        member private this.Execute () =
            let rec loop runs =
                if Volatile.Read(&status) <> Status.Stopped then
                    if runs <> 0 then
                        let ok, sysMsg = systemMessages.TryDequeue()
                        if ok then
                            match sysMsg with
                            | Die ->
                                stop()
                                Status.Stopped
                            | Restart error ->
                                //printfn "Restarting actor due to %O" error 
                                state <- initState
                                loop (runs-1)
                        else
                            let ok, msg = userMessages.TryDequeue()
                            if ok then
                                state <- handler state msg
                                loop (runs-1)
                            else Status.Idle
                    else Status.Idle
                else Status.Stopped
            try
                let status' = loop 300
                if status' <> Status.Stopped then
                  Interlocked.Exchange(&status, Status.Idle) |> ignore
                  if systemMessages.Count <> 0 || userMessages.Count <> 0 then
                      this.Schedule()
            with err ->
                Interlocked.Exchange(&status, Status.Idle) |> ignore
                this.Post(Restart err)
        member this.Post(systemMessage: SystemMessage) =
            systemMessages.Enqueue systemMessage
            this.Schedule()
        member __.Post(message: 'msg) = 
            userMessages.Enqueue message
            this.Schedule()
        static member DeadLetters = deadLetters
        interface IDisposable with
            member __.Dispose() = stop()
            
        //interface System.Threading.IThreadPoolWorkItem with member __.Execute() = this.Execute()
            
    module TestActor =
         [<Struct>]
         type CounterMsg =
              | Inc
              | Done of TaskCompletionSource<unit>
              
         let actor =  new Actor<_,_>(1, fun state msg ->
            match msg with
            | Inc -> state + 1
            | Done promise ->
                promise.SetResult ()
                1)
         
        
         for i=1 to 1_000_000 do
            actor.Post Inc
         let promise = TaskCompletionSource()
         actor.Post (Done promise)
        
module CustomThreadPool =
    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Collections.Concurrent
    open System.Threading

    [<Interface>]
    type IThreadPoolWorkItem =
        abstract Execute : unit -> unit
        
    // 1 workAgent x CPU
    (*
     each WorkerAgent will have its own private queue, which we can use to assign
     work items to be executed only by this agent (and by extension its thread).
     We'll use a ConcurrentQueue for queue implementation to keep things simple,
     but keep in mind that the work pattern here (Multi-Producer/Single-Consumer) gives us a field to make it better.
     
     One of the nice properties of ManualResetEventSlim is that it allows us to optimize for frequent
     sleep/wake-up switches (which are expensive as they are executed in kernel mode) by running
     in a hot loop for a finite number of cycles before calling kernel code.
    *)
        
    type WorkerAgent(shared: ConcurrentQueue<IThreadPoolWorkItem>, cts:CancellationTokenSource) =
        let personal = ConcurrentQueue<IThreadPoolWorkItem>()
        let resetEvent = new ManualResetEventSlim(true, spinCount=100)
        
        (*
        the purpose of a swap functionis to periodically change the order in which we dequeue agent-owned and shared work queues.
        This let us to avoid starvation: a situation when work items from the second queue will never be executed,
        because of the constant flow of work items flowing over to the first one.
        *)
        let swap (l: byref<'t>, r: byref<'t>) =
            let tmp = l
            l <- r
            r <- tmp
                
        let loop() =
            let mutable first = personal
            let mutable second = shared
            let mutable counter = 0
            while true do
                let mutable item : IThreadPoolWorkItem = Unchecked.defaultof<_>
                if first.TryDequeue(&item) || second.TryDequeue(&item)
                then item.Execute()
                else
                    resetEvent.Wait() // put thread to sleep
                    resetEvent.Reset()                   
                counter <- (counter + 1) % 32
                if counter = 0 then swap(&first, &second)
        let thread =
            //Thread(ThreadStart(loop))        
            new Tasks.Task(loop, TaskCreationOptions.LongRunning)
        
        member __.Start() = thread.Start()
        member __.Dispose() =
            cts.Cancel()
            resetEvent.Dispose()
            cts.Dispose()
            thread.Dispose()
            
        member this.Schedule(item) = 
            personal.Enqueue(item)
            this.WakeUp()
            
        member __.WakeUp() = 
            if not resetEvent.IsSet then resetEvent.Set()
            
        interface IDisposable with member this.Dispose() = this.Dispose()    
    
        
    [<Sealed>]
    type ThreadPool(size: int, ?token:CancellationToken) =

        static let shared = lazy (new ThreadPool(Environment.ProcessorCount))
        
        let token = defaultArg token (CancellationToken())
        
        let cts = CancellationTokenSource.CreateLinkedTokenSource(token)

        let mutable i: int = 0
        let sharedQ = ConcurrentQueue<IThreadPoolWorkItem>()
        let agents: WorkerAgent[] = Array.init size <| fun _ -> new WorkerAgent(sharedQ, cts)
        do
            for agent in agents do
                agent.Start() 
        
        static member Global with get() = shared.Value

        member this.Queue(fn: unit -> unit) = this.UnsafeQueueUserWorkItem { new IThreadPoolWorkItem with member __.Execute() = fn () }
            
        member this.Queue(affinityId, fn: unit -> unit) = 
            this.UnsafeQueueUserWorkItem (affinityId, { new IThreadPoolWorkItem with member __.Execute() = fn () })

        member this.UnsafeQueueUserWorkItem(item) = 
            sharedQ.Enqueue item
            i <- Interlocked.Increment(&i) % size
            agents.[i].WakeUp()

        member this.UnsafeQueueUserWorkItem(affinityId, item) = 
            agents.[affinityId % size].Schedule(item)
            
        member this.QueueUserWorkItem(fn, s) =
            let affinityId = s.GetHashCode()
            this.UnsafeQueueUserWorkItem(affinityId, { new IThreadPoolWorkItem with member __.Execute() = fn s })

        member __.Dispose() = 
            for agent in agents do
                agent.Dispose()
        interface IDisposable with member this.Dispose() = this.Dispose()
        
        module testthreadpool =
            let call () =
                let threadId = Thread.CurrentThread.ManagedThreadId            
                printfn "Calling from thread %i" threadId
            
            /// schedule all calls on the same thread
            let affinityId = 1
            for i=0 to 100 do
                ThreadPool.Global.Queue(i % 2, call)
                
            /// schedule without specific thread requirements
            for i=0 to 100 do
                ThreadPool.Global.Queue(call)
                
       
// [DllImport("kernel32.dll")]  
// static extern IntPtr GetCurrentThread();
// [DllImport("kernel32.dll")]  
// static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
// SetThreadAffinityMask(GetCurrentThread(), new IntPtr(1 << (int)cpuID));

                
                
                
//////////////////////
open FSharp.Control

let inline (<--) (agent: ^a) (msg: 'b) =
    (^a: (member Post: 'b -> unit) (agent, msg))

let test =
    let mailboxProcessor = MailboxProcessor.Start(fun inbox -> async { return () }) 
    
    mailboxProcessor <-- ()
    

// async retry
let rec retry work resultOk retries = async {
  let! res = work
  if (resultOk res) || (retries = 0) then return res
  else return! retry work resultOk (retries - 1) }    







module AsyncEither =

    type AsyncEither<'a, 'b> =
        Async<Choice<'a, 'b>>

    let inline bind (m : AsyncEither<'a, 'b>) (f : 'a -> AsyncEither<'c, 'b>) : AsyncEither<'c, 'b> =
        async {
            let! c = m
            match c with
            | Choice1Of2 data ->
                return! f data
            | Choice2Of2 error ->
                return Choice2Of2 error
        }

    let returnM m : AsyncEither<'a, 'b> =
        async { return Choice1Of2 m }

    type AsyncEitherBuilder () =
        member x.Return m = returnM m
        member x.Bind (m, f) = bind m f
        member x.ReturnFrom m = m

    let asyncChoose = AsyncEitherBuilder()

    // example

    let remoteCall request =
        async {
            do! Async.Sleep 100                
            return Choice1Of2 request
        }

    let failedCall request =
        async {
            do! Async.Sleep 100
            return Choice2Of2 (exn ("I failed! :( - " + request))
        }

    let chainedSuccess message =
        asyncChoose {
            let! firstCall = remoteCall message
            let! secondCall = remoteCall (firstCall + " second")
            
            return secondCall }
        |> Async.RunSynchronously
        |> function
           | Choice1Of2 response ->
                sprintf "Yay, I worked: %s" response
           | Choice2Of2 e ->
                sprintf "Boo, a failure: %A" e

    //> chainedSuccess "hello world";;
    //> val it : string = "Yay, I worked: hello world second"

    let withFail message =
        asyncChoose {
            let! failure = failedCall message
            let! firstCall = remoteCall failure
            let! secondCall = remoteCall (firstCall + " second")
            
            return secondCall }
        |> Async.RunSynchronously
        |> function
           | Choice1Of2 response ->
                sprintf "Yay, I worked: %s" response
           | Choice2Of2 e ->
                sprintf "Boo, a failure: %A" e

    //> withFail "hello world";;
    //> val it : string =
    //  "Boo, a failure: System.Exception: I failed! :( - hello world"
    // The second two remote calls are never made.  


    ////////// Agent Events 


/// Type alias that gives convenient name to F# agent type
type Agent<'T> = MailboxProcessor<'T>

module Template =
// [snippet:Triggering events directly]
    /// Type alias that gives convenient name to F# agent type
    type Agent<'T> = MailboxProcessor<'T>
    
    /// Agent that implements batch processing
    type BatchProcessor<'T>(count) =
      // Event used to report aggregated batches to the user
      let batchEvent = new Event<'T[]>()
      // Trigger event on the thread where the agent is running
      let reportBatch batch =
        try
          // If the handler throws, we need to handle the exception
          batchEvent.Trigger(batch)
        with e ->
          printfn "Event handler failed: %A" e
    
      // Start an agent that implements the batching
      let agent = Agent<'T>.Start(fun inbox -> async {
        while true do
          // Repeatedly allocate a new queue 
          let queue = new ResizeArray<_>()
          // Add specified number of messages to the queue
          for i in 1 .. count do
            let! msg = inbox.Receive()
            queue.Add(msg)
          // Report the batch as an array
          reportBatch (queue.ToArray()) })
    
      /// Event that is triggered when a batch is collected
      member x.BatchProduced = batchEvent.Publish
      /// The method adds one object to the agent
      member x.Post(value) = agent.Post(value)
// [/snippet]

module ThreadPool =
// [snippet:Triggering events in a thread pool]    
    /// Agent that implements batch processing
    type BatchProcessor<'T>(count) =
      // Event used to report aggregated batches to the user
      let batchEvent = new Event<'T[]>()
      // Trigger event in a thread pool
      let reportBatch batch =
        // Create simple workflow & start it in the background
        async { batchEvent.Trigger(batch) } 
        |> Async.Start
    
      // Start an agent that implements the batching
      let agent = Agent<'T>.Start(fun inbox -> async {
        while true do
          // Repeatedly allocate a new queue 
          let queue = new ResizeArray<_>()
          // Add specified number of messages to the queue
          for i in 1 .. count do
            let! msg = inbox.Receive()
            queue.Add(msg)
          // Report the batch as an array
          reportBatch (queue.ToArray()) })
    
      /// Event that is triggered when a batch is collected
      member x.BatchProduced = batchEvent.Publish
      /// The method adds one object to the agent
      member x.Post(value) = agent.Post(value)
// [/snippet]


// [snippet:Reporting events using synchronization context]
open System.Threading

/// Agent that implements batch processing (eventContext can 
/// be provided to specify synchronization context for event reporting)
type BatchProcessor<'T>(count, ?eventContext:SynchronizationContext) =
  /// Event used to report aggregated batches to the user
  let batchEvent = new Event<'T[]>()

  /// Triggers event using the specified synchronization context
  /// (or directly, if no synchronization context is specified)
  let reportBatch batch =
    match eventContext with 
    | None -> 
        // No synchronization context - trigger as in the first case
        batchEvent.Trigger(batch)
    | Some ctx ->
        // Use the 'Post' method of the context to trigger the event
        ctx.Post((fun _ -> batchEvent.Trigger(batch)), null)

  (*[omit:(unchanged agent body)]*)
  // Start an agent that implements the batching
  let agent = Agent<'T>.Start(fun inbox -> async {
    while true do
      // Repeatedly allocate a new queue 
      let queue = new ResizeArray<_>()
      // Add specified number of messages to the queue
      for i in 1 .. count do
        let! msg = inbox.Receive()
        queue.Add(msg)
      // Report the batch as an array
      reportBatch (queue.ToArray()) })(*[/omit]*)

  /// Event that is triggered when a batch is collected
  member x.BatchProduced = batchEvent.Publish
  /// The method adds one object to the agent
  member x.Post(value) = agent.Post(value)
// [/snippet]

// [snippet:Capturing current (user-interface) context]
// Agent that will trigger events on the current (GUI) thread
let sync = SynchronizationContext.Current
let proc = BatchProcessor<_>(10, sync)

// Start some background work that will report batches to GUI thread
async {
  for i in 0 .. 1000 do 
    proc.Post(i) } |> Async.Start
// [/snippet]



module Async =
  /// Async.Start with timeout in seconds
  let StartWithTimeout (timeoutSecs:int) (computation:Async<unit>) =
    let c = new System.Threading.CancellationTokenSource(timeoutSecs*1000)
    Async.Start(computation, cancellationToken = c.Token)



  let (<||>) first second = async { let! result = Async.Parallel [|first; second |] in return (result.[0], result.[1]) }
  
    
module CompressionTests =
    open System
    open System.Collections.Generic
    
    [<Struct>]
    type dict = Dict of (int * dict) option array
    
    let decompress (compressed: _ seq) =
        let e = compressed.GetEnumerator()
        if not(e.MoveNext()) then seq [] else
          let mutable w = [|e.Current|]
          let mutable dict_size = 256
          let dictionary =
            ResizeArray(Seq.init dict_size (fun i -> [|i|]))
          let result = Collections.ResizeArray()
          result.Add e.Current
          while e.MoveNext() do
            let k = e.Current
            let entry =
              if k = dictionary.Count then Array.append w [|w.[0]|] else
                dictionary.[k]
            Array.append w [|entry.[0]|] |> dictionary.Add
            result.AddRange entry
            w <- entry
            dict_size <- dict_size + 1
          seq result
          
    let compress (uncompressed: _ seq) =
        seq { let n_in = 256
              let n_out = ref 256
              let create() = Dict(Array.create n_in None)
              let dictionary = Array.init n_in (fun i -> Some(i, create()))
              let branch = ref dictionary
              let k = ref 0
              for c in uncompressed do
                match (!branch).[c] with
                | Some(k', Dict branch') ->
                    branch := branch'
                    k := k'
                | None ->
                    yield !k
                    (!branch).[c] <- Some(!n_out, create())
                    incr n_out
                    match dictionary.[c] with
                    | Some(k', Dict branch') ->
                        branch := branch'
                        k := k'
                    | None -> assert false
              yield !k }
        
    let input = [|for i in 1 .. 4096 -> i % 256|]
    let compressed = Array.ofSeq (compress input)
    let decompressed = decompress compressed |> Array.ofSeq
      
    let test input =
        let compressed = compress input
        printf "Input size: %d\n" (Seq.length input)
        printf "Compressed size: %d\n" (Seq.length compressed)
        printf "Largest output: %d\n" (Seq.max compressed)
        if Array.ofSeq(decompress compressed) = input then
          printf "Recovered original exactly.\n"
        else
          printf "ERROR: Failed to recover original.\n"
     
    test input
    
    
    module Huffman =
        type private node =
          | Leaf of int
          | Node of (int * node) * (int * node)
        let rec private build roots =
          match List.sort roots with
          | [] -> invalidArg "roots" "Huffman"
          | [h] -> h
          | (p1, _ as t1)::(p2, _ as t2)::t ->
              build((p1 + p2, Node(t1, t2)) :: t)
        let private mk_tree (freqs: (byte * int) list) =
          build((0, Leaf -1)::List.map (fun (c, p) -> p, Leaf(int c)) freqs)
        let rec private mk_table = function
          | _, Leaf c -> Map[c, []]
          | _, Node(t1, t2) ->
              let cons b (c, t) = c, b::t
              Map
                [ for kv in mk_table t1 do
                    yield kv.Key, true::kv.Value
                  for kv in mk_table t2 do
                    yield kv.Key, false::kv.Value ]
        let encode freqs (input: byte seq) =
          let table = mk_table(mk_tree freqs)
          seq { for b in input do
                  for b in Map.find (int b) table do
                    yield b
                for b in Map.find -1 table do
                  yield b }
        let decode freqs (input: bool seq) =
          let tree = mk_tree freqs
          seq { let t = ref tree
                for bool in input do
                  match !t with
                  | _, Leaf -1 -> ()
                  | _, Leaf b ->
                      yield byte b
                      match bool, tree with
                      | true, (_, Node(t', _)) | false, (_, Node(_, t')) ->
                          t := t'
                      | _ -> invalidArg "tree" "decode"
                  | _, Node(t1, t2) ->
                      t := if bool then t1 else t2 }
    
    //////
    let input' path =
        seq { use stream = System.IO.File.OpenRead path
              let b = ref(stream.ReadByte())
              while !b <> -1 do
                yield byte !b
                b := stream.ReadByte() }
        
    let freqs (input: byte seq) =
        let a = Array.create 256 0
        for b in input do
          a.[int b] <- a.[int b] + 1
        [ for i in 0..255 do
            if a.[i] > 0 then
              yield byte i, a.[i] ]
        
    
    
    let testHufman filePath =
        let input = input' filePath
        let freqs = freqs input
        
        let compressed = Huffman.encode freqs input
        let decompressed = Huffman.decode freqs compressed
        printf "Input size: %d\n" (Seq.length input)
        printf "Compressed size: %d\n" (Seq.length compressed)        
        if Seq.compareWith compare input decompressed = 0 then 
          //Array.ofSeq(Huffman.decode freqs compressed) = input then
          printf "Recovered original exactly.\n"
        else
          printf "ERROR: Failed to recover original.\n"
          
    testHufman "./src/FSharp.Parallelx/Script.fsx"
     


module Playground =
    let fib n =
      let rec f n cont =
        match n with
        | 0 -> cont 0
        | 1 -> cont 1
        | n -> 
          f (n-2) (fun n2-> 
            f (n-1) (fun n1->
              cont(n1+n2)))
      f n id
      
    fib 125
    
    

    let isPrime num = 
      seq { 2L .. int64 (sqrt (float num)) } 
      |> Seq.forall (fun div -> num % div <> 0L)

    // Create a list with large prime numbers
    let primes nums = 
      nums |> List.map (fun v -> isPrime v, v) 
           |> List.filter fst |> List.map snd
       
           
    let breakByV1 n s = 
        let filter k (i,x) = ((i/n) = k)
        let index = Seq.mapi (fun i x -> (i,x))
        let rec loop s = 
            seq { if not (Seq.isEmpty s) then 
                    let k = (s |> Seq.head |> fst) / n
                    yield (s |> Seq.truncate n
                                |> Seq.map snd)
                    yield! loop (s |> Seq.skipWhile (filter k)) }
        loop (s |> index)
        
    module Tree =
        type Tree<'T> = 
          | Leaf of 'T 
          | Node of Tree<'T> * Tree<'T>

        /// Creates a ballanced tree from a non-empty list
        /// (odd elements are added to the left and even to the right)
        let rec ballancedOfList list =
          match list with 
          | [] -> failwith "Cannot create tree of empty list"
          | [n] -> Leaf n
          | _ -> 
              // Split the elements into odd and even using their index
              let left, right =
                list |> List.mapi (fun i v -> i, v)
                     |> List.partition (fun (i, v) -> i%2 = 0)
              // Create ballanced trees for both parts
              let left, right = List.map snd left, List.map snd right
              Node(ballancedOfList left, ballancedOfList right)        
        
        let primeTree = ballancedOfList (primes [1000L .. 100000L])
        
        
        let forall f tree =
          let rec loop tree =
            match tree with
            | Leaf v -> f v 
            | Node (left, right) ->
                // Process left and right branch
                loop left && loop right
          // Start the recursive processing & wait for the result
          loop tree

        
        
    module List = 
        let rec insertions x = function
            | []             -> [[x]]
            | (y :: ys) as l -> (x::l)::(List.map (fun x -> y::x) (insertions x ys))

        let rec permutations = function
            | []      -> seq [ [] ]
            | x :: xs -> Seq.collect (insertions x) (permutations xs)
            
                
    module Lazy =

      type LazyBuilder () =
        let force (x : Lazy<'T>) = x |> function | Lazy x -> x
        /// bind
        let (>>=) (x:Lazy<'a>) (f:('a -> Lazy<'a>)) : Lazy<'a> =
          lazy (x |> force |> f |> force)  
        /// zero
        let lazyzero = Seq.empty
        
        member this.Bind(x, f) = x >>= f
        member this.Return (x) = lazy x
        member this.ReturnFrom (x) = x
        member this.Zero() = lazyzero 
        member this.Delay f = f()

        member this.Combine (a,b) = 
          Seq.append a b 

        member this.Combine (a,b) = 
          Seq.append (Seq.singleton a) b

        member this.Combine (a,b) = 
          Seq.append a (Seq.singleton b) 

        member this.Combine (Lazy(a), Lazy(b)) = 
          lazy Seq.append a b 

        member this.Combine (a:Lazy<'T>, b:Lazy<seq<'T>>) = 
          (a,b) |> function | (Lazy x, Lazy y) -> lazy Seq.append (Seq.singleton x) y

        member this.Combine (a:Lazy<seq<'T>>, Lazy(b)) = 
          a |> function | Lazy x -> lazy Seq.append x (Seq.singleton b) 

        member this.Combine (a:Lazy<seq<'T>>, b:seq<Lazy<'T>>) =
          let notlazy (xs:seq<Lazy<'T>>) = xs |> Seq.map (fun (Lazy(a)) -> a)
          a |> function | Lazy x -> lazy Seq.append x (notlazy b) 

        member this.Combine (a:seq<Lazy<'T>>, b:Lazy<seq<'T>>) =
          let notlazy (xs:seq<Lazy<'T>>) = xs |> Seq.map (fun (Lazy(a)) -> a)
          b |> function | Lazy x -> lazy Seq.append (notlazy a) x  

        member this.For (s,f) =
          s |> Seq.map f 

        member this.Yield x = lazy x
        member this.YieldFrom x = x

        member this.Run (x:Lazy<'T>) = x
        member this.Run (xs:seq<Lazy<unit>>) = 
          xs |> Seq.reduce (fun a b -> this.Bind(a, fun _ -> b)) 
        member this.Run (xs:seq<Lazy<'T>>) = 
          xs |> Seq.map (fun (Lazy(a)) -> a) |> fun x -> lazy x

      let lazyz = LazyBuilder () 
      let lifM f (Lazy(a)) = lazy f a
      


open System.Collections.Generic

type System.Collections.Generic.List<'T> with
  member this.RemoveAtUnordered(index:int) =
    this.[index] <- this.[this.Count-1]
    this.RemoveAt(this.Count-1)
  member this.RemoveUnordered(item:'T) =
    let index = this.IndexOf(item)
    if index <> - 1 then this.RemoveAtUnordered(index)
    
    
module Seq = 
  /// Reduces input sequence by splitting it into two halves,
  /// reducing each half separately and then aggregating the results 
  /// using the given function. This means that the values are 
  /// aggregated into a ballanced tree, which can save stack space.
  let reduceBallanced f input =
    // Convert the input to an array (must be finite)
    let arr = input |> Array.ofSeq
    let rec reduce s t =
      if s + 1 >= t then arr.[s]
      else 
        // Aggregate two halves of the input separately
        let m = (s + t) / 2
        f (reduce s m) (reduce m t)
    reduce 0 arr.Length
    
type Tree = 
  | Node of Tree * Tree
  | Leaf of int
  /// Returns the size of a tree
  member x.Size = 
    match x with 
    | Leaf _ -> 1 
    | Node(t1, t2) -> 1 + (max t1.Size t2.Size)

// Aggregate 1000 leaves in a tree
let inputs = [ for n in 0 .. 100000 -> Leaf n]
#time "on"
// 'reduceBallanced' creates tree with size 11
let t1 = inputs |> Seq.reduceBallanced (fun a b -> Node(a, b))
t1.Size

// Ordinary 'reduce' creates tree with size 1001
let t2 = inputs |> Seq.reduce (fun a b -> Node(a, b))
t2.Size







type HashMap<'K, 'V when 'K: comparison>(cmp: IEqualityComparer<'K>) =
  let spineLength = 524287
  let spine = Array.create spineLength Map.empty

  member this.Count =
    Array.sumBy Seq.length spine

  member this.Index key =
    abs(cmp.GetHashCode key % spineLength)

  member this.Add(key, value) =
    let idx = this.Index key
    spine.[idx] <- Map.add key value spine.[idx]

  member this.Remove key =
    let idx = this.Index key
    spine.[idx] <- Map.remove key spine.[idx]

  member this.TryGetValue(key, value: byref<'V>) =
    let bucket = spine.[this.Index key]
    match Map.tryFind key bucket with
    | None -> false
    | Some v ->
        value <- v
        true
        
        
////////////////////
///
///
open System
open System.Threading

type ShouldRetry = ShouldRetry of (RetryCount * LastException -> bool * RetryDelay)
and RetryCount = int
and LastException = exn
and RetryDelay = TimeSpan
type RetryPolicy = RetryPolicy of ShouldRetry
    
type RetryPolicies() =
    static member NoRetry () : RetryPolicy =
        RetryPolicy( ShouldRetry (fun (retryCount, _) -> (retryCount < 1, TimeSpan.Zero)) )
    static member Retry (retryCount : int , intervalBewteenRetries : RetryDelay) : RetryPolicy =
        RetryPolicy( ShouldRetry (fun (currentRetryCount, _) -> (currentRetryCount < retryCount, intervalBewteenRetries)))
    static member Retry (currentRetryCount : int) : RetryPolicy =
        RetryPolicies.Retry(currentRetryCount, TimeSpan.Zero)

type RetryResult<'T> = 
    | RetrySuccess of 'T
    | RetryFailure of exn
    
type Retry<'T> = Retry of (RetryPolicy -> RetryResult<'T>)

type RetryBuilder() =
    member self.Return (value : 'T) : Retry<'T> = Retry (fun retryPolicy -> RetrySuccess value)

    member self.Bind (retry : Retry<'T>, bindFunc : 'T -> Retry<'U>) : Retry<'U> = 
        Retry (fun retryPolicy ->
            let (Retry retryFunc) = retry 
            match retryFunc retryPolicy with
            | RetrySuccess value ->
                let (Retry retryFunc') = bindFunc value
                retryFunc' retryPolicy
            | RetryFailure exn -> RetryFailure exn )

    member self.Delay (f : unit -> Retry<'T>) : Retry<'T> = 
        Retry (fun retryPolicy ->
            let resultCell : option<RetryResult<'T>> ref = ref None 
            let lastExceptionCell : exn ref = ref null
            let (RetryPolicy(ShouldRetry shouldRetry)) = retryPolicy
            let canRetryCell : bool ref = ref true
            let currentRetryCountCell : int ref = ref 0
            while !canRetryCell do
                try
                    let (Retry retryFunc) = f ()
                    let result = retryFunc retryPolicy
                    resultCell := Some result
                    canRetryCell := false
                with e -> 
                    lastExceptionCell := e
                    currentRetryCountCell := 1 + !currentRetryCountCell
                    match shouldRetry(!currentRetryCountCell, !lastExceptionCell) with
                    | (true, retryDelay) ->
                        Thread.Sleep(retryDelay)
                    | (false, _) -> 
                        canRetryCell := false
            
            match !resultCell with
            | Some result -> result
            | None -> RetryFailure !lastExceptionCell )

[<AutoOpen>]
module Retry = 
    let retry = new RetryBuilder()
    let retryWithPolicy (retryPolicy : RetryPolicy) (retry : Retry<'T>) = 
        Retry (fun _ -> let (Retry retryFunc) = retry in retryFunc retryPolicy)
    let run (retry : Retry<'T>) (retryPolicy : RetryPolicy) : RetryResult<'T> =
        let (Retry retryFunc) = retry
        retryFunc retryPolicy


// Example
    let test = 
        let random = new Random()
        retry {
            return 1 / random.Next(0, 2)
        }

    (test, RetryPolicies.NoRetry()) ||> run
    (test, RetryPolicies.Retry (10, RetryDelay(2L))) ||> run


module Trampoline =
    type TrampValue<'T> =
        | DelayValue of Delay<'T>
        | ReturnValue of Return<'T>
        | BindValue of IBind<'T>
    and ITramp<'T> = 
        abstract member Value : TrampValue<'T>
        abstract member Run : unit -> 'T

    and Delay<'T>(f : unit -> ITramp<'T>) =
        member self.Func = f 
        interface ITramp<'T> with
            member self.Value = DelayValue self
            member self.Run () = (f ()).Run()

    and Return<'T>(x :'T) = 
        member self.Value = x
        interface ITramp<'T> with
            member self.Value = ReturnValue self
            member self.Run () = x

    and IBind<'T> = 
        abstract Bind<'R> : ('T -> ITramp<'R>) -> ITramp<'R>
    and Bind<'T, 'R>(tramp : ITramp<'T>, f : ('T -> ITramp<'R>)) = 
        interface IBind<'R> with
            member self.Bind<'K>(f' : 'R -> ITramp<'K>) : ITramp<'K> =
                new Bind<'T, 'K>(tramp, fun t -> new Bind<'R, 'K>(f t, (fun r -> f' r)) :> _) :> _
        interface ITramp<'R> with
            member self.Value = BindValue self
            member self.Run () = 
                match tramp.Value with
                | BindValue b -> b.Bind(f).Run() 
                | ReturnValue r -> (f r.Value).Run()
                | DelayValue d -> (new Bind<'T, 'R>(d.Func (), f) :> ITramp<'R>).Run() 

    // Builder
    type TrampBuilder() = 
        member self.Return a = new Return<_>(a) :> ITramp<_>
        member self.Bind(tramp, f) = 
            new Bind<'T, 'R>(tramp, f) :> ITramp<'R>
        member self.Delay f = 
            new Delay<_>(f) :> ITramp<_>
       
    let tramp = new TrampBuilder()

    let run (tramp : ITramp<'T>) = tramp.Run()

    // Example
    let rec inc a = 
        tramp {
            if a = 1 then return 1
            else
                let! x = inc (a - 1)
                return x + 1
        } 

    inc 1000000 |> run
    
    let rec inc2 a =
        if a = 1 then 1
        else
            let x = inc2 (a - 1)
            x + 1

    inc2 1000000
    
module AtomCollection =
        
    open System.Threading

    type Transaction<'State,'Result> = T of ('State -> 'State * 'Result)

    type Atom<'S when 'S : not struct>(value : 'S) =
        let refCell = ref value
        
        let rec swap (f : 'S -> 'S) = 
            let currentValue = !refCell
            let result = Interlocked.CompareExchange<'S>(refCell, f currentValue, currentValue)
            if obj.ReferenceEquals(result, currentValue) then ()
            else Thread.SpinWait 20; swap f

        let transact (f : 'S -> 'S * 'R) =
            let output = ref Unchecked.defaultof<'R>
            let f' x = let t,s = f x in output := s ; t
            swap f' ; output.Value

        static member Create<'S> (x : 'S) = new Atom<'S>(x)

        member self.Value with get() : 'S = !refCell
        member self.Swap (f : 'S -> 'S) : unit = swap f
        member self.Commit<'R> (f : Transaction<'S,'R>) : 'R = 
            match f with T f0 -> transact f0 

        static member get : Transaction<'S,'S> = T (fun t -> t,t)
        static member set : 'S -> Transaction<'S,unit> = fun t -> T (fun _ -> t,())

    type TransactionBuilder() =
        let (!) = function T f -> f

        member __.Return (x : 'R) : Transaction<'S,'R> = T (fun t -> t,x)
        member __.ReturnFrom (f : Transaction<'S,'R>) = f
        member __.Bind (f : Transaction<'S,'T> , 
                        g : 'T -> Transaction<'S,'R>) : Transaction<'S,'R> =
            T (fun t -> let t',x = !f t in !(g x) t')

    let transact = new TransactionBuilder()
    
    type MapAtom<'T> () =
        let container : Atom<Map<string option, unit -> 'T>> = Atom.Create  Map.empty
        
        member __.Add (param: string option, x : unit -> 'T) =
            transact {
                let! (contents : Map<string option, unit -> 'T>) = Atom.get

                return! Atom.set <| (contents.Add(param, x))
            } |> container.Commit

        member __.TryFind (param: string option) =
            transact {
                let! (contents : Map<string option, unit -> 'T>) = Atom.get

                match param with               
                | _ when contents |> Seq.isEmpty -> return failwith "Map is empty"
                | p ->
                    return contents.TryFind p
            } |> container.Commit

        member __.Clear () =
            transact {
                let! contents = Atom.get

                do! Atom.set Map.empty

                return contents
            } |> container.Commit
            
    
    type Stack<'T> () =
        let container : Atom<'T list> = Atom.Create []

        member __.Add (x : 'T) =
            transact {
                let! contents = Atom.get

                return! Atom.set <| x :: contents
            } |> container.Commit

        member __.Pop () =
            transact {
                let! contents = Atom.get

                match contents with
                | [] -> return failwith "stack is empty!"
                | head :: tail ->
                    do! Atom.set tail
                    return head
            } |> container.Commit

        member __.Flush () =
            transact {
                let! contents = Atom.get

                do! Atom.set []

                return contents
            } |> container.Commit            
            
module DependencyStuff =
        
    type Mode = Singleton | Factory

    type private DependencyContainer<'T> () =
        // this implementation does not account for thread safety
        // a good way to go would be to wrap the container map
        // in an atom structure like the one in http://fssnip.net/bw
        static let container : Map<string option, unit -> 'T> ref = ref Map.empty

        static let t = typeof<'T>

        static let morph mode (f : unit -> 'T) =
            match mode with
            | Singleton ->
                let singleton = lazy(f ())
                fun () -> singleton.Value
            | Factory -> f

        static member Register (mode, factory : unit -> 'T, param) =
            if (!container).ContainsKey param then
                match param with
                | None -> failwithf "IoC : instance of type %s has already been registered" t.Name
                | Some param -> failwithf "IoC : instance of type %s with parameter \"%s\" has already been registered" t.Name param
            else container := (!container).Add(param, morph mode factory)
                

        static member IsRegistered param = (!container).ContainsKey param

        static member TryResolve param =
            match (!container).TryFind param with
            | None -> None
            | Some f ->
                try Some <| f ()
                with e -> 
                    match param with
                    | None -> failwithf "IoC : factory method for type %s has thrown an exception:\n %s" t.Name <| e.ToString()
                    | Some param -> 
                        failwithf "IoC : factory method for type %s with parameter \"%s\" has thrown an exception:\n %s" t.Name param <| e.ToString()

        static member Resolve param =
            match DependencyContainer<'T>.TryResolve param with
            | Some v -> v
            | None ->
                match param with
                | None -> failwithf "IoC : no instace of type %s has been registered" t.Name
                | Some param -> failwithf "IoC : no instance of type %s with parameter \"%s\" has been registered" t.Name param


    type IoC =
        static member Register<'T> (mode, factory, ?param) = DependencyContainer<'T>.Register(mode, factory, param)
        static member RegisterValue<'T> (value, ?param) = DependencyContainer<'T>.Register(Factory, (fun () -> value), param)
        static member TryResolve<'T> ?param = DependencyContainer<'T>.TryResolve param
        static member Resolve<'T> ?param = DependencyContainer<'T>.Resolve param
        static member IsRegistered<'T> ?param = DependencyContainer<'T>.IsRegistered param


    type IStuff =
        abstract Age : int

    type Stuff(age) =
        member __.Ciao() = printfn "ttt %d" age
        interface IStuff with
            member __.Age = age
            
            
    type IAnimal =
        abstract Name : string

    type Cat(name) =
        member __.Purr() = printfn "purrr"
        interface IAnimal with
            member __.Name = name
            
    IoC.Register<IAnimal>(Singleton, fun () -> new Cat("Mr. Bungle") :> IAnimal)
    IoC.Register<IStuff>(Singleton, fun () -> new Stuff(42) :> IStuff)
    IoC.RegisterValue<IAnimal>( { new IAnimal with member __.Name = "Bryony" }, "me")
    IoC.RegisterValue<IAnimal>( { new IAnimal with member __.Name = "Ricky" }, "me2")

    let cat = IoC.Resolve<IAnimal>() :?> Cat
    let me = IoC.Resolve<IAnimal> "me"
    let me2 = IoC.Resolve<IAnimal> "me2"

    cat.Purr()
    me.Name
    me2.Name
    
    let stuff = IoC.Resolve<IStuff>()
    stuff.Age

    IoC.RegisterValue(42, "magic number")
    IoC.Resolve<int> "magic number"
    