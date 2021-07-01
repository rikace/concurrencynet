open System
open Akka
open Akka.Actor
open Akka.FSharp

type Message =
    | Greet of string
    | Idle
    | Up
    | Down
    | Exec
    | Get


[<EntryPoint>]
let main argv =

    let system = System.create "ActorSystem" <| Configuration.defaultConfig()

    let fsmActor =
        spawn system "fsm-actor"
        <| fun inbox ->
            let rec idle state = actor {
                printfn "Idle"
                let! msg = inbox.Receive()
                match msg with
                | Get ->
                    let sender = inbox.Sender()
                    sender.Tell state
                    return! idle state
                | Up -> return! up state
                | Down -> return! down state
                | _ -> return! idle state }

            and up state = actor {
                printfn "UP"
                let! msg = inbox.Receive()
                match msg with
                | Exec -> return! up (state + 1)
                | Down -> return! down state
                | Get ->
                    let sender = inbox.Sender()
                    sender.Tell state
                    return! idle state
                | Idle -> return! idle 0
                | _ -> return! up state}

            and down state = actor {
                printfn "Down"
                let! msg = inbox.Receive()
                match msg with
                | Exec -> return! down (state - 1)
                | Up -> return! up state
                | Get ->
                    let sender = inbox.Sender()
                    sender.Tell state
                    return! idle state
                | Idle -> return! idle 0
                | _ -> return! down state }
            idle 0

    fsmActor.Tell Idle
    fsmActor.Tell Up
    fsmActor.Tell Exec
    fsmActor.Tell Exec

    let state1 =
        let response = fsmActor <? Get
        response |> Async.RunSynchronously
    printfn "The State 1 is %A" state1

    fsmActor.Tell Down
    fsmActor.Tell Exec

    let state2 =
        let response = fsmActor <? Get
        response |> Async.RunSynchronously
    printfn "The State 2 is %A" state2

    Console.WriteLine("Completed")
    Console.ReadLine() |> ignore
    0
