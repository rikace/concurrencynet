#r "../CSharp.Parallelx/bin/Debug/netcoreapp3.0/CSharp.Parallelx.dll"

open System
open System.Threading.Tasks
open System.Collections.Generic

type myRt1 = {name:string}
type myRt2 = {age:int}

let agg = CSharp.Parallelx.EventEx.ChannelsQueuePubSub()

agg.RegisterHandler<string>(System.Action<_>(fun x -> printfn "Ciao int %s" x))
agg.RegisterHandler<myRt1>(System.Action<_>(fun x -> printfn "Ciao myRt1 %A" x))


agg.Enqueue("ricky").Wait()
agg.Enqueue({name="Bugghina"}).Wait()


// Register   (System.Action<'a> -> unit) when 'a : not struct
// Enqueue    val it : ('a -> System.Threading.Tasks.Task) when 'a : not struct

type Message =
    | Subscribe of (Type * string option) * Action<obj>
    | Publish of Type * obj
    
type IEventAggregator =  
    abstract Subscribe<'Event when 'Event : not struct > : System.Action<'Event> -> unit
    abstract Publish<'Event when 'Event : not struct > : 'Event -> unit
    
type SubjectAgent() =
    let subs = Dictionary<(Type * string option), System.Action<obj> list>()
    
    let agent = MailboxProcessor.Start(fun inbox ->
        let rec loop () = async {
            let! msg = inbox.Receive()
            match msg with
            | Subscribe((ty, None), a) ->
                if subs.ContainsKey (ty, None) then
                    subs.[(ty, None)] <-  a::subs.[(ty, None)]
                else
                    subs.Add((ty, None), [a])
                return! loop ()
            | Publish(ty, a) ->
                let actions = subs.[ty, None]
                actions |>Seq.iter (fun action -> action.Invoke(a))
                return! loop ()
            
        }
        loop () )
           
    interface IEventAggregator with
        member this.Subscribe<'Event when 'Event : not struct>(f:System.Action<'Event>) =
            let action = fun (item:obj) ->
                let a : 'Event = downcast item
                f.Invoke(a)             
            agent.Post (Subscribe((typeof<'Event>, None), System.Action<_>(action)))
            
        member this.Publish<'Event when 'Event : not struct>(a:'Event) =
            agent.Post(Publish(typeof<'Event>, a))
    
    
    
let subAgent = SubjectAgent() :> IEventAggregator

subAgent.Subscribe<string>(System.Action<string>(fun s -> printfn "ciao one %s" s))
subAgent.Subscribe<string>(System.Action<string>(fun s -> printfn "hello one %s" s))
subAgent.Subscribe<myRt1>(System.Action<myRt1>(fun s -> printfn "ciao two %A" s))
subAgent.Subscribe<myRt2>(System.Action<myRt2>(fun s -> printfn "ciao three %A" s))

subAgent.Publish<string>("Bugghina")        
subAgent.Publish<myRt1>({name="Riccardo"})       
        
        
module RxSubject =
    open System
    open System.Collections.Generic

    type CircularBuffer<'T> (bufferSize:int) =
        let buffer = Array.zeroCreate<'T> bufferSize
        let mutable index = 0
        let mutable total = 0
        member this.Add value =
            if bufferSize > 0 then
                buffer.[index] <- value
                index <- (index + 1) % bufferSize
                total <- min (total + 1) bufferSize
        member this.Iter f =     
            let start = if total = bufferSize then index else 0
            for i = 0 to total - 1 do 
                buffer.[(start + i) % bufferSize] |> f                 

    type message<'T> =
        | Add of IObserver<'T>
        | Remove of IObserver<'T>
        | Next of 'T
        | Completed
        | Error of exn

    let startAgent (bufferSize:int) =
        let subscribers = LinkedList<_>()
        let buffer = CircularBuffer bufferSize               
        MailboxProcessor.Start(fun inbox ->
            let rec loop () = async {
                let! message = inbox.Receive()
                match message with
                | Add observer ->                    
                    subscribers.AddLast observer |> ignore
                    buffer.Iter observer.OnNext
                    return! loop ()
                | Remove observer ->
                    subscribers.Remove observer |> ignore
                    return! loop ()
                | Next value ->                                       
                    for subscriber in subscribers do
                        subscriber.OnNext value
                    buffer.Add value
                    return! loop () 
                | Error e ->
                    for subscriber in subscribers do
                        subscriber.OnError e
                | Completed ->
                    for subscriber in subscribers do
                        subscriber.OnCompleted ()
            }
            loop ()
        )

    type ReplaySubject<'T> (bufferSize:int) =
        let bufferSize = max 0 bufferSize
        let agent = startAgent bufferSize    
        let subscribe observer =
            observer |> Add |> agent.Post
            { new System.IDisposable with
                member this.Dispose () =
                    observer |> Remove |> agent.Post
            }
        member this.Next value = Next value |> agent.Post
        member this.Error error = Error error |> agent.Post
        member this.Completed () = Completed |> agent.Post    
        
        interface System.IObserver<'T> with
            member this.OnNext value = Next value |> agent.Post
            member this.OnError error = Error error |> agent.Post
            member this.OnCompleted () = Completed |> agent.Post
        member this.Subscribe(observer:System.IObserver<'T>) =
            subscribe observer
        
        interface System.IObservable<'T> with
            member this.Subscribe observer = subscribe observer                   
    and Subject<'T>() = inherit ReplaySubject<'T>(0)

    let subject = ReplaySubject<int>(3)            
    let d1 = subject.Subscribe(fun (x:int) -> System.Console.WriteLine x)
    
    subject.Next(10)
    subject.Next(11)
    subject.Next(12)
    subject.Next(13)
    let d2 = subject.Subscribe(fun (x:int) -> System.Console.WriteLine x)


    let subject2 = Subject<int>()            
    let d3 = subject.Subscribe(fun (x:int) -> System.Console.WriteLine x)
    subject.Next(10)
    subject.Next(11)
    subject.Next(12)
    subject.Next(13)
    let d4 = subject.Subscribe(fun (x:int) -> System.Console.WriteLine x)
    
    
    
    open System
    open System.Collections.Generic
 
    type internal AggregateAction = 
     {
         Id: Guid
         Action: Action
     }
 
    type AggregateAgent() =
         let agent = MailboxProcessor<AggregateAction>.Start(fun inbox ->
             let dic = new Dictionary<Guid,MailboxProcessor<Action>>()
             async{                                                                                                  
                     while true do
                         let! msg = inbox.Receive()
                         if dic.ContainsKey(msg.Id) then 
                             dic.Item(msg.Id).Post msg.Action
                         else
                             let aggAgent = MailboxProcessor<Action>.Start( fun inbox ->
                                 async{
                                     while true do
                                         let! action = inbox.Receive()
                                         action.Invoke() |> ignore     
                                 })
                             aggAgent.Post msg.Action
                             dic.Add(msg.Id,aggAgent)
             })
         
         member this.Process id action = 
             agent.Post { Id = id; Action = action}
     


    open System
    
    type TestReg() =
        member this.Reg<'a>(f:'a -> unit) =
            let f = Action<'a>(f)
            //let f = Action<'a>(fun (x:int) -> printf "ciao %d" x)
            Delegate.CreateDelegate(typeof<Action<'a>>, f.Target, f.Method) :?> (Action<'a>)
    

    module ObsAggregator =  //  type AgentObserver =
        type AgentObserver (bufferSize:int) =
            let subscribers = LinkedList<_>()
            let buffer = CircularBuffer bufferSize               
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec loop () = async {
                    let! message = inbox.Receive()
                    match message with
                    | Add observer ->                    
                        subscribers.AddLast observer |> ignore
                        buffer.Iter observer.OnNext
                        return! loop ()
                    | Remove observer ->
                        subscribers.Remove observer |> ignore
                        return! loop ()
                    | Next value ->                                       
                        for subscriber in subscribers do
                            subscriber.OnNext value
                        buffer.Add value
                        return! loop () 
                    | Error e ->
                        for subscriber in subscribers do
                            subscriber.OnError e
                    | Completed ->
                        for subscriber in subscribers do
                            subscriber.OnCompleted ()
                }
                loop () )
       
            member this.Add obs = obs |> Add |> agent.Post
            member this.Remove obs = obs |> Remove |> agent.Post
            member this.Next value = value |> Next |> agent.Post
            member this.Error error = error |> Error |> agent.Post
            member this.Completed () = Completed |> agent.Post
        
            interface System.IObserver<obj> with
                member this.OnNext value = Next value |> agent.Post
                member this.OnError error = Error error |> agent.Post
                member this.OnCompleted () = Completed |> agent.Post
        
        open System
        open System.Collections.Generic
        type SubjectAgentMsg =
            | Subscribe
            | Unsubscribe
            | Push
            

        
//        subAgent.Subscribe<obj>(fun x -> printfn "hello one %A" x)
//        subAgent.Subscribe<string>(fun x -> printfn "hello two %s" x)
//        subAgent.Subscribe<int>(fun x -> printfn "hello two %d" x)
        
        
            // topic ??
        // ISubscription<TMessage> Subscribe<TMessage>(Action<TMessage> action) where TMessage : IMessage
        // void Publish<TMessage>(TMessage message) where TMessage : IMessage
        // UnSubscribe<TMessage>(ISubscription<TMessage> subscription) where TMessage : IMessage
