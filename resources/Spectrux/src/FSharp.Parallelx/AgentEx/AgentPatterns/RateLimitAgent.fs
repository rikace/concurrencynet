namespace FSharp.Parallelx.AgentEx.Patterns

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic

/// Limits the rate of emitted messages to at most one per the specified number of milliseconds
type RateLimitAgent<'T>(milliseconds:int) =
  let event = Event<'T>()
  let error = Event<_>()
  let agent = MailboxProcessor.Start(fun inbox ->
    let rec loop (lastMessageTime:DateTime) = async {
      let! e = inbox.Receive()
      let now = DateTime.UtcNow
      if (now - lastMessageTime).TotalMilliseconds > float milliseconds then
        try event.Trigger(e)
        with e -> error.Trigger(e)
        return! loop now
      else
        return! loop lastMessageTime }
    loop DateTime.MinValue )

  /// Triggered when an event happens (within the specified rate)
  member x.EventOccurred = event.Publish
  /// Send an event to the agent - it will either be ignored or forwarded
  member x.AddEvent(event) = agent.Post(event)
  /// Exception has been thrown when triggering `EventOccurred`
  member x.ErrorOccurred = Event.merge agent.Error error.Publish