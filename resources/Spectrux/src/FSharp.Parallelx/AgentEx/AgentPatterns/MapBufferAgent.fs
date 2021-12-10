namespace FSharp.Parallelx.AgentEx.Patterns

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic


/// A simple ring buffer that starts with specified initial values
type RingBuffer<'T>(size, initial:'T) =
  let buffer = Array.create size initial
  let mutable index = 0
  member x.Add(v) =
    buffer.[index] <- v
    index <- index + 1
    if index = buffer.Length then index <- 0
  member x.Buffer = buffer
  member x.Values = [ for i in 0 .. buffer.Length-1 -> buffer.[(index+i)%buffer.Length] ]


/// Agent that keeps values in a ring buffer of the specified size and allows
/// the caller to calculate aggregates over the buffer
type MapBufferAgent<'T, 'R>(size, f, initial:'T) =
  let event = new Event<'R>()
  let error = new Event<_>()
  let agent = MailboxProcessor.Start(fun inbox -> async {
    let buffer = RingBuffer(size, initial)
    let index = ref 0
    while true do
      let! msg = inbox.Receive()
      buffer.Add(msg)
      try event.Trigger(f buffer.Buffer) with e -> error.Trigger(e)
  })

  /// Add new event to the buffer
  member x.AddEvent(v) = agent.Post(v)
  /// Triggered when the state changes happens
  member x.StateChanged = event.Publish
  /// Exception has been thrown when triggering `StateChanged`
  member x.ErrorOccurred = Event.merge agent.Error error.Publish