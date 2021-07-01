namespace FSharp.Parallelx.AgentEx

[<AutoOpen>]
module AgentUtils =
    open System
    open System.Threading
    
    type Agent<'a> = MailboxProcessor<'a>

    type Reply<'T> =
        | Success of 'T
        | Error of exn
    with
        member e.Value =
            match e with
            | Success t -> t
            | Error e -> raise e

    and ReplyChannel<'T> internal (rc : AsyncReplyChannel<Reply<'T>>) =
        member __.Reply (t : 'T) = rc.Reply <| Success t
        member __.ReplyWithError (e : exn) = rc.Reply <| Error e

    and MailboxProcessor<'T> with
        member m.PostAndReply (msgB : ReplyChannel<'R> -> 'T) =
            m.PostAndReply(fun ch -> msgB (ReplyChannel<_>(ch))).Value    

        
    let inline (<--) (agent:Agent<_>) msg = agent.Post msg    
    let public (<-!) (a:Agent<_>) msg = a.PostAndAsyncReply msg
        
    /// Puts a message into a mailbox, no waiting.
    let inline put (a:'a) (mb:Agent<'a>) = mb.Post a

    /// Creates an async computation that completes when a message is available in a mailbox.
    let inline take (mb:Agent<'a>) = async.Delay mb.Receive

    
    [<RequireQualifiedAccess>]
    module Agent =

        let cancelWith cancellationToken body = new Agent<_> (body,cancellationToken)
                    
        let reportErrorsTo (supervisor: Agent<exn>) (agent: Agent<_>) =
           agent.Error.Add(fun error -> supervisor.Post error); agent           
           
        let withMonitor monitor transform (agent:Agent<_>) = 
            agent.Error.Add (fun error -> monitor <-- transform error); agent            
                
        let start (agent:Agent<_>) = agent.Start (); agent
        
        //  let supervisor =  trapError self onError
        //                    |> Agent.cancelWith shutdown.Token
        //                    |> Agent.start
        //
        //  let worker message onComplete onError = 
        //    message
        //    |> batchActions onComplete onError
        //    |> Agent.cancelWith shutdown.Token
        //    |> Agent.withMonitor supervisor (routeEx message)
        //    |> Agent.start           
        
    let supervisor f =
       Agent<System.Exception>.Start(fun inbox ->
         async { while true do
                   let! err = inbox.Receive()
                   f err })        
