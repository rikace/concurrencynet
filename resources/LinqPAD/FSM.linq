<Query Kind="FSharpProgram">
  <NuGetReference>Microsoft.Tpl.Dataflow</NuGetReference>
  <NuGetReference>System.Collections.Immutable</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Collections.Immutable</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks.Dataflow</Namespace>
</Query>


type Message =
    | Idle
    | Up
    | Down
    | Exec
    | Get of AsyncReplyChannel<int>
 
let ctrl = MailboxProcessor.Start( fun inbox -> 
   
    let rec idle state = async {
        printfn "Idle"
        let! msg = inbox.Receive()
        match msg with
        | Get ch -> 
            ch.Reply state
            return! idle state
        | Up -> return! up state
        | Down -> return! down state
        | _ -> return! idle state }
     
    and up state = async {
        printfn "UP"
        let! msg = inbox.Receive()
        match msg with
        | Exec -> return! up (state + 1)
        | Down -> return! down state 
        | Get ch -> 
            ch.Reply state
            return! idle state        
        | Idle -> return! idle 0
        | _ -> return! up state}
        
    and down state = async {
        printfn "Down"
        let! msg = inbox.Receive()
        match msg with
        | Exec -> return! down (state - 1)
        | Up -> return! up state 
        | Get ch -> 
            ch.Reply state
            return! idle state        
        | Idle -> return! idle 0
        | _ -> return! down state}      
 
    // the initial state
    idle 0 )
    
ctrl.Post Idle 
ctrl.Post Up 
ctrl.Post Exec
ctrl.Post Exec

let state1 = ctrl.PostAndReply(fun ch -> Get ch) 
state1.Dump()

ctrl.Post Down
ctrl.Post Exec
let state2 = ctrl.PostAndReply(fun ch -> Get ch) 
state2.Dump()
 