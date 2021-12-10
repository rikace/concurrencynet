namespace FSharp.Parallelx.AgentEx.Patterns

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic

/// Agent that emits the specified events at the specified times
type SchedulerAgent<'T>() =
  let event = new Event<'T>()
  let error = new Event<_>()
  let agent = MailboxProcessor<DateTime * 'T>.Start(fun inbox ->

    // We keep a list of events together with the DateTime when they should occur
    let rec loop events = async {

      // Report events that are happening now & forget them
      let events, current =
        events |> List.partition (fun (time, e) -> time > DateTime.UtcNow)
      for _, e in current do
        try event.Trigger(e)
        with e -> error.Trigger(e)

      // Sleep until new events are added or until the first upcoming event
      let timeout =
        if List.isEmpty events then System.Threading.Timeout.Infinite else
          let t = int ((events |> List.map fst |> List.min) - DateTime.UtcNow).TotalMilliseconds
          max 10 t
      let! newEvents = inbox.TryReceive(timeout)
      let newEvents = match newEvents with Some v -> [v] | _ -> []
      return! loop (if List.isEmpty newEvents then events else events @ newEvents) }
    loop [])

  /// Schedule new events to happen in the future
  member x.AddEvent(event) = agent.Post(event)
  /// Triggered when an event happens
  member x.EventOccurred = event.Publish
  /// Exception has been thrown when triggering `EventOccurred`
  member x.ErrorOccurred = Event.merge agent.Error error.Publish