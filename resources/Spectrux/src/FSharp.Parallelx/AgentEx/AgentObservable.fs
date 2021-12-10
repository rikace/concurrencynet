namespace FSharp.Parallelx.AgentEx

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic

module TestBacthAgent =
            
    type BatchProcessing<'T>(batchSize, timeout, ?eventContext:SynchronizationContext) =
        let batchEvent = new Event<'T[]>()
        let cts = new CancellationTokenSource()
            
        let reportBatch batch =
            match eventContext with 
            | None -> batchEvent.Trigger(batch)
            | Some ctx -> ctx.Post((fun _ -> batchEvent.Trigger(batch)), null)
                
        let body (agent: Agent<'T>) =
            let rec loop remainingTime messages = async {
                let start = DateTime.Now
                let! msg = agent.TryReceive(timeout = max 0 remainingTime)
                let elapsed = int (DateTime.Now - start).TotalMilliseconds
                match msg with 
                | Some(msg) when List.length messages = batchSize - 1 ->
                    reportBatch (msg :: messages |> List.rev |> Array.ofList)
                    return! loop timeout []
                | Some(msg) ->
                    return! loop (remainingTime - elapsed) (msg::messages)
                | None when List.length messages <> 0 -> 
                    reportBatch (messages |> List.rev |> Array.ofList)
                    return! loop timeout []
                | None -> 
                    return! loop timeout [] }
            loop timeout []
        let agent : Agent<'T> = Agent.Start(body, cts.Token)
        
        
        [<CLIEvent>]
        member x.BatchProduced = batchEvent.Publish
        member x.Enqueue v = agent.Post(v)
    
        interface IDisposable with
            member x.Dispose() = cts.Cancel()        

module AgentObservervable =
    module Subject =
        /// Subject state maintained inside of the mailbox loop
        module State =
            type t<'T> = {
                observers : IObserver<'T> list
                stopped   : bool
            }

            let empty() = {observers=[]; stopped=false}

        /// Messages required for the mailbox loop
        module Message =
            type t<'T> =
            | Add       of IObserver<'T>
            | Remove    of IObserver<'T>
            | Next      of 'T
            | Error     of exn
            | Completed

        /// Type t that implements IObservable<'T> and IObserver<'T>
        type t<'T>() =

            let error() = raise(new System.InvalidOperationException("Subject already completed"))

            let mbox = MailboxProcessor<Message.t<'T>>.Start(fun inbox ->
                let rec loop(t:State.t<'T>) = async {
                    let! req = inbox.Receive()

                    match req with
                    | Message.Add(observer) ->
                        if not(t.stopped) then
                            return! loop ({t with observers = t.observers @ [observer]})
                        else error()

                    | Message.Remove(observer) ->
                        if not(t.stopped) then
                            let t = {t with observers = t.observers |> List.filter(fun f -> f <> observer)}
                            return! loop t
                        else error()

                    | Message.Next(value) ->
                        if not(t.stopped) then
                            t.observers |> List.iter(fun o -> o.OnNext(value))
                            return! loop t
                        else error()

                    | Message.Error(err) ->
                        if not(t.stopped) then
                            t.observers |> List.iter(fun o -> o.OnError(err))
                            return! loop t
                        else error()

                    | Message.Completed ->
                        if not(t.stopped) then
                            t.observers |> List.iter(fun o -> o.OnCompleted())
                            let t = {t with stopped = true}
                            return! loop t
                        else error()
                }
                loop (State.empty())
            )

            /// Raises OnNext in all the observers
            member x.Next value  = Message.Next(value)  |> mbox.Post
            /// Raises OnError in all the observers
            member x.Error ex    = Message.Error(ex)    |> mbox.Post
            /// Raises OnCompleted in all the observers
            member x.Completed() = Message.Completed    |> mbox.Post

            interface IObserver<'T> with
                member x.OnNext value   = x.Next(value)
                member x.OnError ex     = x.Error(ex)
                member x.OnCompleted()  = x.Completed()

            interface IObservable<'T> with
                member x.Subscribe(observer:IObserver<'T>) =
                    observer |> Message.Add |> mbox.Post
                    { new IDisposable with
                        member x.Dispose() =
                            observer |> Message.Remove |> mbox.Post }
