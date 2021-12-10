namespace FSharp.Parallelx.EventEx

module EventAggregator =
    
    open System
    open System.Reactive
    open System.Reactive.Linq
    open System.Reactive.Subjects
    open System.Reactive.Concurrency
    open System.Threading
    open System.Threading.Tasks

    // Event Aggregator using Reactive Extension
    type IEventAggregator =  
        inherit IDisposable
        abstract GetEvent<'Event> : unit -> IObservable<'Event>
        abstract Publish<'Event> : eventToPublish:'Event -> unit

    type EventAggregator() =
        let disposedErrorMessage = "The EventAggregator is already disposed."

        let subject = new Subject<obj>()  

        interface IEventAggregator with
            member this.GetEvent<'Event>(): IObservable<'Event> =
                if (subject.IsDisposed) then
                    failwith disposedErrorMessage
                subject.OfType<'Event>().AsObservable<'Event>()
                    .ObserveOn(TaskPoolScheduler.Default)
                    .SubscribeOn(TaskPoolScheduler.Default)

            member this.Publish(eventToPublish: 'Event): unit =
                if (subject.IsDisposed) then
                    failwith disposedErrorMessage
                subject.OnNext(eventToPublish)  

            member this.Dispose(): unit = subject.Dispose()

        static member Create() = new EventAggregator() :> IEventAggregator
        
        
        
    module TestEventAggregator =
            
        type IncrementEvent = { Value: int }
        type ResetEvent = { ResetTime: DateTime }

        let evtAggregator = EventAggregator.Create()

        let disposeResetEvent =
            evtAggregator.GetEvent<ResetEvent>()
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(fun evt ->
                    printfn "Counter Reset at: %A - Thread Id %d" evt.ResetTime Thread.CurrentThread.ManagedThreadId)

        let disposeIncrementEvent =
            evtAggregator.GetEvent<IncrementEvent>()
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(fun evt ->
                    printfn "Counter Incremented. Value: %d - Thread Id %d" evt.Value Thread.CurrentThread.ManagedThreadId)

        for i in [0..10] do
            evtAggregator.Publish({ Value = i })

        evtAggregator.Publish({ ResetTime = DateTime(2015, 10, 21) })
        
        evtAggregator.Publish("")

module BrokerPubSub =
    open System
    open System.Collections.Generic
    
    type Message =
        | Subscribe of Type * Action<obj>
        | Publish of Type * obj
        
    type IEventAggregator =  
        abstract Subscribe<'Event when 'Event : not struct > : System.Action<'Event> -> unit
        abstract Publish<'Event when 'Event : not struct > : 'Event -> unit
        
    type SubjectAgent() =
        
//        let createSubAgent () =
//            MailboxProcessor.Start(fun inbox ->
//                let rec loop () = async {
//                    return! loop()
//                }
//                loop())   
            
        let agent = MailboxProcessor.Start(fun inbox ->
            let subs = Dictionary<Type, System.Action<obj> list>()
            let rec loop () = async {
                let! msg = inbox.Receive()
                match msg with
                | Subscribe(ty, a) ->
                    if subs.ContainsKey ty then
                        subs.[ty] <-  a::subs.[ty]
                    else
                        subs.Add(ty, [a])
                    return! loop ()
                | Publish(ty, a) ->
                    let actions = subs.[ty]
                    actions |> Seq.iter (fun action -> action.Invoke(a))
                    return! loop ()
                
            }
            loop () )
               
        interface IEventAggregator with
            member this.Subscribe<'Event when 'Event : not struct>(f:System.Action<'Event>) =
                let action = fun (item:Object) ->
                    let a : 'Event = downcast item
                    f.Invoke(a)             
                agent.Post (Subscribe(typeof<'Event>, System.Action<_>(action)))
                
            member this.Publish<'Event when 'Event : not struct>(a:'Event) =
                agent.Post(Publish(typeof<'Event>, a))

