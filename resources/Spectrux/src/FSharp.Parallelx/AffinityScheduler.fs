namespace FSharp.Parallelx

module AffinityScheduler =
        
    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Collections.Concurrent

    [<Sealed>]
    type WorkerAgent(shared: ConcurrentQueue<IThreadPoolWorkItem>) =
        let personal = ConcurrentQueue<IThreadPoolWorkItem>()
        let resetEvent = new ManualResetEventSlim(true, spinCount=100)
            
        let swap (l: byref<'t>, r: byref<'t>) =
            let tmp = l
            l <- r
            r <- tmp
                
        let loop() =
            let mutable first = personal
            let mutable second = shared
            let mutable counter = 0
            while true do
                let mutable item = null
                if first.TryDequeue(&item) || second.TryDequeue(&item)
                then item.Execute()
                else 
                    resetEvent.Wait()
                    resetEvent.Reset()
                counter <- (counter + 1) % 32
                if counter = 0 then swap(&first, &second)
                            
        let thread = new Thread(ThreadStart(loop))
        member this.Schedule(item) = 
            personal.Enqueue(item)
            this.WakeUp()
        member __.WakeUp() = 
            if not resetEvent.IsSet then
                resetEvent.Set()
        member __.Start() = thread.Start()
        member __.Dispose() =
            resetEvent.Dispose()
            thread.Abort()
        interface IDisposable with member this.Dispose() = this.Dispose()

    [<Sealed>]
    type ThreadPool(size: int) =

        static let shared = lazy (new ThreadPool(Environment.ProcessorCount))

        let mutable i: int = 0
        let sharedQ = ConcurrentQueue<IThreadPoolWorkItem>()
        let agents: WorkerAgent[] = Array.init size <| fun _ -> new WorkerAgent(sharedQ)
        do 
            for agent in agents do
                agent.Start() 
        
        static member Global with get() = shared.Value

        member this.Queue(fn: unit -> unit) = this.UnsafeQueueUserWorkItem { new IThreadPoolWorkItem with member __.Execute() = fn () }
            
        member this.Queue(affinityId, fn: unit -> unit) = 
            this.UnsafeQueueUserWorkItem ({ new IThreadPoolWorkItem with member __.Execute() = fn () }, affinityId)

        member __.UnsafeQueueUserWorkItem(item) = 
            sharedQ.Enqueue item
            i <- Interlocked.Increment(&i) % size
            agents.[i].WakeUp()

        member this.UnsafeQueueUserWorkItem(item, affinityId) = 
            agents.[affinityId % size].Schedule(item)
            
        member tp.QueueUserWorkItem(fn, s) =
            let affinityId = s.GetHashCode()
            tp.UnsafeQueueUserWorkItem({ new IThreadPoolWorkItem with member __.Execute() = fn s }, affinityId)

        member __.Dispose() = 
            for agent in agents do
                agent.Dispose()
        interface IDisposable with member this.Dispose() = this.Dispose()

    ///---------------------
    /// EXAMPLE USAGE
    ///---------------------

    let call () =
        let threadId = Thread.CurrentThread.ManagedThreadId
        printfn "Calling from thread %i" threadId
    
    let test () =
            
        /// schedule all calls on the same thread
        let affinityId = 1
        for i=0 to 100 do
            ThreadPool.Global.Queue(affinityId, call)
            
        /// schedule without specific thread requirements
        for i=0 to 100 do
            ThreadPool.Global.Queue(call)
    