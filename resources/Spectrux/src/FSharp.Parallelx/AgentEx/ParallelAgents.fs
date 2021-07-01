namespace FSharp.Parallelx.AgentEx

module ParallelAgents =
        
    open System
    open Microsoft.FSharp.Control
    open System.Threading
                
    type AfterError<'state> =
    | ContinueProcessing of 'state
    | StopProcessing
    | RestartProcessing
        
    type MailboxProcessor<'a> with
    
        static member public parallelWorker (workers:int)   
                     (behavior:MailboxProcessor<'a> -> Async<unit>)     
                     (?errorHandler:exn -> unit) (?cts:CancellationToken) = 
            let cts = defaultArg cts (CancellationToken())        
            let errorHandler = defaultArg errorHandler ignore
            let agent = new MailboxProcessor<'a>((fun inbox ->
                let agents = Array.init workers (fun _ ->    
                        let child = MailboxProcessor.Start(behavior, cts)
                        child.Error.Subscribe(errorHandler) |> ignore
                        child)
                cts.Register(fun () -> agents |> Array.iter(fun a -> (a :> IDisposable).Dispose())) |> ignore
                let rec loop i = async {
                    let! msg = inbox.Receive()
                    agents.[i].Post(msg)    
                    return! loop((i+1) % workers)
                }
                loop 0), cts)
            agent.Start()
        
        static member public SpawnAgent<'b>(messageHandler :'a->'b->'b, initialState : 'b, ?timeout:'b -> int,
                                            ?timeoutHandler:'b -> AfterError<'b>, ?errorHandler:Exception -> 'a option -> 'b -> AfterError<'b>) : MailboxProcessor<'a> =
            let timeout = defaultArg timeout (fun _ -> -1)
            let timeoutHandler = defaultArg timeoutHandler (fun state -> ContinueProcessing(state))
            let errorHandler = defaultArg errorHandler (fun _ _ state -> ContinueProcessing(state))
            MailboxProcessor.Start(fun inbox ->
                let rec loop(state) = async {
                    let! msg = inbox.TryReceive(timeout(state))
                    try
                        match msg with
                        | None      -> match timeoutHandler state with
                                        | ContinueProcessing(newState)    -> return! loop(newState)
                                        | StopProcessing        -> return ()
                                        | RestartProcessing     -> return! loop(initialState)
                        | Some(m)   -> return! loop(messageHandler m state)
                    with
                    | ex -> match errorHandler ex msg state with
                            | ContinueProcessing(newState)    -> return! loop(newState)
                            | StopProcessing        -> return ()
                            | RestartProcessing     -> return! loop(initialState)
                    }
                loop(initialState))
    
        static member public SpawnWorker(messageHandler,  ?timeout, ?timeoutHandler,?errorHandler) =
            let timeout = defaultArg timeout (fun () -> -1)
            let timeoutHandler = defaultArg timeoutHandler (fun _ -> ContinueProcessing(()))
            let errorHandler = defaultArg errorHandler (fun _ _ -> ContinueProcessing(()))
            MailboxProcessor.SpawnAgent((fun msg _ -> messageHandler msg; ()), (), timeout, timeoutHandler, (fun ex msg _ -> errorHandler ex msg))
    
        static member public SpawnParallelWorker(messageHandler, howMany, ?timeout, ?timeoutHandler,?errorHandler) =
            let timeout = defaultArg timeout (fun () -> -1)
            let timeoutHandler = defaultArg timeoutHandler (fun _ -> ContinueProcessing(()))
            let errorHandler = defaultArg errorHandler (fun _ _ -> ContinueProcessing(()))
            MailboxProcessor<'a>.SpawnAgent((fun msg (workers:MailboxProcessor<'a> array, index) ->
                                                workers.[index].Post msg
                                                (workers, (index + 1) % howMany))  
                                            , (Array.init howMany (fun _ -> MailboxProcessor<'a>.SpawnWorker(messageHandler, timeout, timeoutHandler, errorHandler)), 0))
    
    
