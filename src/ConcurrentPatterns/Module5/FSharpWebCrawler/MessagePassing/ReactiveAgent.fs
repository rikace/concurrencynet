module FSharpWebCrawler.MessagePassing.ReactiveAgent

open System
open System.Threading
open System.Net
open System.IO
open FSharpWebCrawler.Async
open FSharpWebCrawler


module ReactiveAgent =

    // Subject state maintained inside of the mailbox loop
    type State<'T> = {
        observers : IObserver<'T> list
        stopped   : bool
    }
    with static member empty() = {observers=[]; stopped=false}

    // Messages required for the mailbox loop
    type Message<'T, 'U> =
    | Add       of IObserver<'U>
    | Remove    of IObserver<'U>
    | Next      of 'T
    | Error     of exn
    | Completed

    type AgentObservable<'T, 'U>(seed:'U, selector:Func<'U, 'T, 'U>) =

        let error() = raise(new System.InvalidOperationException("Subject already completed"))

        let mbox = Agent<_>.Start(fun inbox ->
            let rec loop(t:State<_>) acc = async {
                let! req = inbox.Receive()

                match req with
                | Message.Add(observer) ->
                    if t.stopped then error() else
                    return! loop ({t with observers = t.observers @ [observer]}) acc

                | Message.Remove(observer) ->
                    if t.stopped then error() else
                        let t = {t with observers = t.observers |> List.filter(fun f -> f <> observer)}
                        return! loop t acc

                | Message.Next(value) ->
                     let newAcc = selector.Invoke(acc, value)
                     if t.stopped then error() else
                         t.observers |> List.iter(fun o -> o.OnNext(newAcc))
                         return! loop t newAcc

                | Message.Error(err) ->
                    if t.stopped then error() else
                        t.observers |> List.iter(fun o -> o.OnError(err))
                        return! loop t acc

                | Message.Completed ->
                    if t.stopped then error() else
                        t.observers |> List.iter(fun o -> o.OnCompleted())
                        let t = {t with stopped = true}
                        return! loop t acc
            }

            loop (State<'U>.empty()) seed)

        /// Raises OnNext in all the observers
        member x.Next value  = Message.Next(value)  |> mbox.Post
        /// Raises OnError in all the observers
        member x.Error ex    = Message.Error(ex)    |> mbox.Post
        /// Raises OnCompleted in all the observers
        member x.Completed() = Message.Completed    |> mbox.Post

        // Step (1)
        // Implement both the reactive interfaces IObserver and IObservable, also pay attention to the generic
        // type constarctor for these intefaces, they might not be the same.
        // Then, expose the IObservable interface throughout an instance method called "AsObservable". This methid will be used
        // to register the output after each message is processed.
        interface IObserver<'T> with
            member x.OnNext value   = x.Next(value)
            member x.OnError ex     = x.Error(ex)
            member x.OnCompleted()  = x.Completed()

        interface IObservable<'U> with
            member x.Subscribe(observer:IObserver<'U>) =
                observer |> Message.Add |> mbox.Post
                { new IDisposable with
                    member x.Dispose() =
                        observer |> Message.Remove |> mbox.Post }

        interface IAgent<'T, 'U> with
            member x.Post(msg) = Message.Next(msg)  |> mbox.Post
            member x.Send(msg) = async { Message.Next(msg)  |> mbox.Post } |> Async.startAsPlainTask
            member x.AsObservable() = x.AsObservable()

        member x.AsObservable() = (x :> IObservable<'U>)
