namespace FSharp.Parallelx.AgentEx

module ConcurrentAgents =
    open FSharp.Parallelx.AsyncEx
    open FSharp.Control
    open FSharpx.Collections
    
    [<Measure>] type second
    type Seconds = int<second>
    type PerSecond = int<1/second>

    module Seconds =
        let fromInt (i:int) : Seconds =
            i * 1<second>

    module ParallelAgent = 
        
        type ParallelAgentRunningState =
            { IsWaiting: bool
              WorkRequestedCount: int
              WorkRunningCount: int
              WorkQueuedCount: int
              WorkCompletedCount: int }

        type ParallelAgentStatus =
            | Running of ParallelAgentRunningState
            | Done

        type ParallelAgentMessage =
            | WorkRequested of Async<unit>
            | WorkCompleted
            | WaitRequested
            | StatusRequested of AsyncReplyChannel<ParallelAgentStatus>

        type ParallelAgentState =
            { IsWaiting: bool
              WorkRequestedCount: int
              WorkRunningCount: int
              WorkCompletedCount: int
              WorkQueued: Queue<Async<unit>> }

        type ParallelAgentMailbox = MailboxProcessor<ParallelAgentMessage>

        type ParallelAgent(name: string, limit:int) =
            let initialState =
                { IsWaiting = false
                  WorkRequestedCount = 0
                  WorkRunningCount = 0
                  WorkCompletedCount = 0
                  WorkQueued = Queue.empty<Async<unit>> }

            let tryWork (inbox:ParallelAgentMailbox) (state:ParallelAgentState) =
                match state.WorkQueued with
                | Queue.Nil -> state
                | Queue.Cons (work, remainingQueue) when state.WorkRunningCount < limit ->
                    Async.Start(async {
                        do! work
                        inbox.Post(WorkCompleted)
                    })
                    { state with
                        WorkRunningCount = state.WorkRunningCount + 1
                        WorkQueued = remainingQueue }
                | _ -> state

            let evolve (inbox:ParallelAgentMailbox) 
                : ParallelAgentState -> ParallelAgentMessage -> ParallelAgentState =
                fun (state:ParallelAgentState) (msg:ParallelAgentMessage) ->
                    match msg with
                    | WorkRequested work ->
                        { state with
                            WorkRequestedCount = state.WorkRequestedCount + 1
                            WorkQueued = state.WorkQueued.Conj(work) }
                    | WorkCompleted ->
                        { state with
                            WorkRunningCount = state.WorkRunningCount - 1
                            WorkCompletedCount = state.WorkCompletedCount + 1 }
                    | StatusRequested replyChannel ->
                        let isComplete = state.WorkRequestedCount = state.WorkCompletedCount
                        if state.IsWaiting && isComplete then 
                            replyChannel.Reply(Done)
                        else
                            let status =
                                { IsWaiting = state.IsWaiting
                                  WorkRequestedCount = state.WorkRequestedCount
                                  WorkRunningCount = state.WorkRunningCount
                                  WorkQueuedCount = state.WorkQueued.Length
                                  WorkCompletedCount = state.WorkCompletedCount }
                            replyChannel.Reply(Running(status))
                        state
                    | WaitRequested -> 
                        { state with
                            IsWaiting = true }
                    |> tryWork inbox

            let agent = ParallelAgentMailbox.Start(fun inbox ->
                AsyncSeq.initInfiniteAsync (fun _ -> inbox.Receive())
                |> AsyncSeq.fold (evolve inbox) initialState
                |> Async.Ignore
            )

            let rec wait () =
                async {
                    match agent.PostAndReply(StatusRequested) with
                    | Done -> ()
                    | Running status ->
                        do! Async.Sleep(1000)
                        return! wait()
                }

            member __.Post(work:Async<unit>) =
                agent.Post(WorkRequested(work))

            member __.LogStatus() =
                agent.PostAndReply(StatusRequested)
                

            member __.Wait() =
                agent.Post(WaitRequested)
                wait()
                |> Async.RunSynchronously
        
        module RateAgent =

            type RateAgentWork = unit -> unit

            type RateAgentRunningState =
                { IsWaiting: bool
                  TokenCount: int
                  WorkQueuedCount: int }

            type RateAgentStatus =
                | Running of RateAgentRunningState
                | Done

            type RateAgentMessage =
                | WorkRequested of RateAgentWork
                | RefillRequested
                | WaitRequested
                | StatusRequested of AsyncReplyChannel<RateAgentStatus>

            type RateAgentState =
                { IsWaiting: bool
                  TokenCount: int
                  WorkQueued: Queue<RateAgentWork> }

            type RateAgentMailbox = MailboxProcessor<RateAgentMessage>

            type RateAgent(name:string, rateLimit:PerSecond) =

                let initialState =
                    { IsWaiting = false
                      TokenCount = 1<second> * rateLimit
                      WorkQueued = Queue.empty<RateAgentWork> }

                let tryWork (state:RateAgentState) =
                    let rec recurse (s:RateAgentState) =
                        match s.WorkQueued, s.TokenCount with
                        | Queue.Nil, _ -> s
                        | _, 0 -> s
                        | Queue.Cons (work, remainingQueue), tokenCount ->
                            work ()
                            let newState =
                                { s with
                                    TokenCount = tokenCount - 1
                                    WorkQueued = remainingQueue }
                            recurse newState
                    if state.TokenCount > 0 then recurse state
                    else state

                let evolve
                    : RateAgentState -> RateAgentMessage -> RateAgentState = 
                    fun (state:RateAgentState) (msg:RateAgentMessage) ->
                        match msg with
                        | WorkRequested work -> 
                            { state with
                                WorkQueued = state.WorkQueued.Conj(work) }
                        | RefillRequested -> 
                            { state with
                                TokenCount = 1<second> * rateLimit }
                        | WaitRequested -> 
                            { state with
                                IsWaiting = true }
                        | StatusRequested replyChannel ->
                            if state.IsWaiting && state.WorkQueued.IsEmpty then 
                                replyChannel.Reply(Done)
                            else
                                let status =
                                    { IsWaiting = state.IsWaiting
                                      TokenCount = state.TokenCount
                                      WorkQueuedCount = state.WorkQueued.Length }
                                replyChannel.Reply(Running(status))
                            state
                        |> tryWork

                let agent = RateAgentMailbox.Start(fun inbox ->
                    AsyncSeq.initInfiniteAsync (fun _ -> inbox.Receive())
                    |> AsyncSeq.fold evolve initialState
                    |> Async.Ignore
                )

                let rec wait () =
                    async {
                        match agent.PostAndReply(StatusRequested) with
                        | Done -> ()
                        | Running status ->
                            do! Async.Sleep(1000)
                            return! wait()
                    }
                
                let rec refill () =
                    async {
                        do! Async.Sleep(1000)
                        agent.Post(RefillRequested)
                        return! refill()
                    }

                do Async.Start(refill())

                member __.LogStatus() =
                    agent.PostAndReply(StatusRequested)
                    

                member __.Post(work:RateAgentWork) =
                    agent.Post(WorkRequested(work))

                member __.Wait() =
                    agent.Post(WaitRequested)
                    wait()
                    |> Async.RunSynchronously
                    
        module BufferAgent =

            type BufferAgentRunningState =
                { IsWaiting: bool
                  BufferSize: int
                  ProcessBufferRequestedCount: int
                  ProcessBufferCompletedCount: int }

            type BufferAgentStatus =
                | Running of BufferAgentRunningState
                | Done

            type BufferAgentMessage<'I> =
                | ItemReceived of 'I
                | WaitRequested
                | ProcessBufferCompleted
                | StatusRequested of AsyncReplyChannel<BufferAgentStatus>

            type BufferAgentState<'I> =
                { IsWaiting: bool
                  Buffer: 'I list 
                  ProcessBufferRequestedCount: int
                  ProcessBufferCompletedCount: int }

            type BufferAgentMailbox<'I> = MailboxProcessor<BufferAgentMessage<'I>>

            type BufferAgent<'I>(name:string, bufferSize:int,processBuffer:'I list -> Async<unit>) =
                let initialState =
                    { IsWaiting = false
                      Buffer = []
                      ProcessBufferRequestedCount = 0
                      ProcessBufferCompletedCount = 0 }

                let tryProcessBuffer (inbox:BufferAgentMailbox<'I>) (state:BufferAgentState<'I>) =
                    match state.Buffer with
                    | [] -> state
                    | buffer when ((buffer |> List.length) >= bufferSize) || state.IsWaiting ->
                        Async.Start(async {
                            do! processBuffer buffer
                            inbox.Post(ProcessBufferCompleted)
                        })
                        { state with
                            Buffer = []
                            ProcessBufferRequestedCount = state.ProcessBufferRequestedCount + 1 }
                    | _ -> state
                
                let evolve (inbox:BufferAgentMailbox<'I>)
                    : BufferAgentState<'I> -> BufferAgentMessage<'I> -> BufferAgentState<'I> =
                    fun (state:BufferAgentState<'I>) (msg:BufferAgentMessage<'I>) ->
                        match msg with
                        | ItemReceived item -> 
                            { state with
                                Buffer = item :: state.Buffer }
                        | WaitRequested -> 
                            { state with
                                IsWaiting = true }
                        | ProcessBufferCompleted -> 
                            { state with
                                ProcessBufferCompletedCount = state.ProcessBufferCompletedCount + 1 }
                        | StatusRequested replyChannel ->
                            let isComplete = state.ProcessBufferRequestedCount = state.ProcessBufferCompletedCount
                            if state.IsWaiting && isComplete then
                                replyChannel.Reply(Done)
                            else
                                let status =
                                    { IsWaiting = state.IsWaiting
                                      BufferSize = state.Buffer |> List.length
                                      ProcessBufferRequestedCount = state.ProcessBufferRequestedCount
                                      ProcessBufferCompletedCount = state.ProcessBufferCompletedCount }
                                replyChannel.Reply(Running(status))
                            state
                        |> tryProcessBuffer inbox

                let agent = BufferAgentMailbox.Start(fun inbox ->
                    AsyncSeq.initInfiniteAsync(fun _ -> inbox.Receive())
                    |> AsyncSeq.fold (evolve inbox) initialState
                    |> Async.Ignore
                )

                let rec wait () =
                    async {
                        match agent.PostAndReply(StatusRequested) with
                        | Done -> ()
                        | Running status ->
                            do! Async.Sleep(1000)
                            return! wait()
                    }

                member __.Post(item:'I) =
                    agent.Post(ItemReceived(item))

                member __.LogStatus() =
                    agent.PostAndReply(StatusRequested)
                    
                member __.Wait() =
                    agent.Post(WaitRequested)
                    wait()
                    |> Async.RunSynchronously
(*

    let rateAgent = RateAgent("Type", rateLimit)
    let parallelAgent = ParallelAgent("Type", parallelLimit)
    let bufferAgent = BufferAgent("Print", bufferSize, processBuffer)

    member __.Write(keyInfo:ConsoleKeyInfo) =
        match keyInfo.Key, keyInfo.Modifiers with
        | ConsoleKey.Enter, ConsoleModifiers.Control ->
            printfn "received wait"
            rateAgent.Wait()
            parallelAgent.Wait()
            bufferAgent.Wait()
            exit 0
        | ConsoleKey.Enter, _ ->
            rateAgent.LogStatus()
            parallelAgent.LogStatus()
            bufferAgent.LogStatus()
        | _ ->
            let work = async {
                do! Async.Sleep(1000)
                bufferAgent.Post(keyInfo.KeyChar)
            }
            rateAgent.Post(fun () ->
                Console.Write(keyInfo.KeyChar)
                parallelAgent.Post(work)
            )
            
*)                    