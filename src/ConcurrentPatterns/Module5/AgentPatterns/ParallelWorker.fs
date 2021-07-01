namespace AgentPatterns
open System.IO

module ParallelWorker =
    open System
    open System.Threading

    // Parallel agent-based
    // Agents (MailboxProcessor) provide building-block for other
    // primitives - like parallelWorker

    // TODO
    // implement an Agent coordinator that send
    // a message to process to a collection sub-agents (children)
    // in a round-robin fashion

    let parallelCoordinator n f =
        MailboxProcessor.Start(fun inbox ->
            let workers = Array.init n (fun i -> MailboxProcessor.Start(f))

            // missing code
            // create a recursive/async lopp with an
            // internal state to track the child-agent index
            // where a message was swend last
            //async.Return(())

            let rec loop i = async {
                let! msg = inbox.Receive()
                workers.[i].Post(msg)
                return! loop ((i+1) % n)
            }
            loop 0)


    // TODO : 5.18
    // A reusable parallel worker model built on F# agents
    // implement a parallel worker based on MailboxProcessor, which coordinates the work in a Round-Robin fashion
    // between a set of children MailboxProcessor(s)
    // use an Array initializer to create the collection of MailboxProcessor(s)
    // the internal state should keep track of the index of the child to the send  the next message
    let parallelWorker n (f:_ -> Async<unit>) =
        // TODO : use the "parallelCoordinator" for the implementation

        parallelCoordinator n (fun inbox ->
            let rec loop() = async {
                let! msg = inbox.Receive()
                let! result = f msg

                return! loop()
            }
            loop()
        )

    let tprintfn s =
        async.Return (printfn "Executing %s on thread %i" s Thread.CurrentThread.ManagedThreadId)
    let paralleltprintfn s = async {
        printfn "Executing %s on thread %i" s Thread.CurrentThread.ManagedThreadId
        Thread.Sleep(300)
    }

    let echo = parallelWorker 1 tprintfn
    let echos = parallelWorker 4 paralleltprintfn

    let messages = ["a";"b";"c";"d";"e";"f";"g";"h";"i";"l";"m";"n";"o";"p";"q";"r";"s";"t"]
    printfn "...Just one guy doing the work"
    messages |> Seq.iter (fun msg -> echo.Post(msg))
    Thread.Sleep 1000
    printfn "...With a little help from his friends"
    messages |> Seq.iter (fun msg -> echos.Post(msg))

module ParallelAgentPipeline =
    open ParallelWorker
    open SixLabors.ImageSharp
    open SixLabors.ImageSharp.PixelFormats
    open AgentPatterns
    open System.Threading
    open ImageHandlers
    open System.IO
    open Helpers

    let images = Directory.GetFiles("../../../../../Data/paintings", "*.jpg") // OK

    let imageProcessPipeline (destination:string) (imageSrc:string) = async {
        if Directory.Exists destination |> not then
            Directory.CreateDirectory destination |> ignore
        let imageDestination = Path.Combine(destination, Path.GetFileName(imageSrc))
        load imageSrc
        |> resize 400 400
        |> convert3D
        |> setFilter ImageFilters.Green
        |> saveImage destination }

    // TODO :
    //      ParallelAgentPipeline
    //      implement a reusable parallel worker model built on F# agents
    //      complete the TODOs
    let start () =
        let agentImage =
            parallelWorker 4 (imageProcessPipeline "../../../../../Data/paintings//Output") // OK
        images
        |> Seq.iter(fun image -> agentImage.Post image)


// SOLUTIONS
module AgentParallelWorker =

    open System
    open System.Threading
    open AgentEx

    // Parallel MailboxProcessor workers
    type MailboxProcessor<'a> with
        static member public parallelWorker' (workers:int)
                (behavior:MailboxProcessor<'a> -> Async<unit>)
                (?errorHandler:exn -> unit) (?cts:CancellationToken) =

            let cts = defaultArg cts (CancellationToken())
            let errorHandler = defaultArg errorHandler ignore
            let agent = new MailboxProcessor<'a>((fun inbox ->
                let agents = Array.init workers (fun _ ->
                    let child = MailboxProcessor.Start(behavior, cts)
                    child.Error.Subscribe(errorHandler) |> ignore
                    child)
                cts.Register(fun () ->
                    agents |> Array.iter(
                        fun a -> (a :> IDisposable).Dispose()))
                |> ignore

                let rec loop i = async {
                    let! msg = inbox.Receive()
                    agents.[i].Post(msg)
                    return! loop((i+1) % workers)
                }
                loop 0), cts)
            agent.Start()

    type AgentDisposable<'T>(f:MailboxProcessor<'T> -> Async<unit>,
                                ?cancelToken:System.Threading.CancellationTokenSource) =
        let cancelToken = defaultArg cancelToken (new CancellationTokenSource())
        let agent = MailboxProcessor.Start(f, cancelToken.Token)

        member x.Agent = agent
        interface IDisposable with
            member x.Dispose() = (agent :> IDisposable).Dispose()
                                 cancelToken.Cancel()

    type AgentDisposable<'T> with
        member inline this.withSupervisor (supervisor: Agent<exn>, transform) =
            this.Agent.Error.Add(fun error -> supervisor.Post(transform(error))); this

        member this.withSupervisor (supervisor: Agent<exn>) =
            this.Agent.Error.Add(supervisor.Post); this


    type MailboxProcessor<'a> with
        static member public parallelWorker (workers:int, behavior:MailboxProcessor<'a> -> Async<unit>, ?errorHandler, ?cancelToken:CancellationTokenSource) =
            let cancelToken = defaultArg cancelToken (new System.Threading.CancellationTokenSource())
            let thisletCancelToken = cancelToken.Token
            let errorHandler = defaultArg errorHandler ignore
            let supervisor = Agent<System.Exception>.Start(fun inbox -> async {
                                while true do
                                    let! error = inbox.Receive()
                                    errorHandler error })
            let agent = new MailboxProcessor<'a>((fun inbox ->
                let agents = Array.init workers (fun _ ->
                    (new AgentDisposable<'a>(behavior, cancelToken))
                        .withSupervisor supervisor )
                thisletCancelToken.Register(fun () ->
                    agents |> Array.iter(fun agent -> (agent :> IDisposable).Dispose())
                ) |> ignore
                let rec loop i = async {
                    let! msg = inbox.Receive()
                    agents.[i].Agent.Post(msg)
                    return! loop((i+1) % workers)
                }
                loop 0), thisletCancelToken)
            agent.Start()
            agent
