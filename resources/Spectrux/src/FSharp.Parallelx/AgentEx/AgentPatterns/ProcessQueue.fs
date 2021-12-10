namespace FSharp.Parallelx.AgentEx.Patterns

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic

type ProcessQueueMessage =
    | Start
    | Stop
    | Pause
    | Job of (unit -> unit)

type ProcessQueue() =
    let mb = MailboxProcessor.Start(fun inbox ->
        let jobs = new System.Collections.Generic.Queue<_>()
        let rec loop state =
            async {
                match state with
                | Start ->
                    while jobs.Count > 0 do
                        let f = jobs.Dequeue()
                        f()
                | _ -> ()

                let! msg = inbox.Receive()
                match msg with
                | Start -> return! loop Start
                | Pause -> return! loop Pause
                | Job(f) -> jobs.Enqueue(f); return! loop state 
                | Stop -> return ()
            }
        loop Start)

    member this.Resume() = mb.Post(Start)
    member this.Stop() = mb.Post(Stop)
    member this.Pause() = mb.Post(Pause)
    member this.QueueJob(f) = mb.Post(Job f)