module FSharp.Parallelx.AgentEx.AgentPatterns.BulkAgent


open System
open System.Collections.Generic
open System.Threading


(*  Aggregating messages into bulks
    Bulk specified number of messages
    Emit bulk after timeout

    Uses of bulking agent
    Grouping data for further processing
    Writing live data to a database *)

//  new BulkingAgent    : int -> int -> BulkingAgent
//  member Enqueue      : 'T -> unit
//  member BulkProduced : Event<'T[]>

type Agent<'T> = MailboxProcessor<'T>

/// Agent that implements bulking of incomming messages
/// A bulk is produced (using an event) either after enough incomming
/// mesages are collected or after the specified time (whichever occurs first)
type BulkingAgent<'T>(bulkSize, timeout, ?eventContext:SynchronizationContext) = 
  // Used to report the aggregated bulks to the user
  let bulkEvent = new Event<'T[]>()
  let cts = new CancellationTokenSource()

  let agent : Agent<'T> = Agent.Start((fun agent -> 
    

    let reportBatch batch =
        match eventContext with 
        | None ->  async { bulkEvent.Trigger(batch) } |> Async.Start // Reporting by ThreadPool (extra overhead)
        | Some ctx -> ctx.Post((fun _ -> bulkEvent.Trigger(batch)), null)

    /// Triggers event using the specified synchronization context
    /// (or directly, if no synchronization context is specified)
    let reportBatch' batch =
        match eventContext with 
        // No synchronization context - trigger as in the first case
        | None -> bulkEvent.Trigger(batch)
            // Use the 'Post' method of the context to trigger the event
        | Some ctx -> ctx.Post((fun _ -> bulkEvent.Trigger(batch)), null)


    // Represents the control loop of the agent
    // - start  The time when last bulk was processed
    // - list   List of items in current bulk
    let rec loop (start:DateTime) (list:_ list) = async {
      if (DateTime.Now - start).TotalMilliseconds > float timeout then
        // Timed-out - report bulk if there is some message & reset time
        if list.Length > 1 then 
          bulkEvent.Trigger(list |> Array.ofList)
        return! loop DateTime.Now []
      else
        // Try waiting for a message
        let! msg = agent.TryReceive(timeout = timeout / 25)
        match msg with 
        | Some(msg) when list.Length + 1 = bulkSize ->
            // Bulk is full - report it to the user
            bulkEvent.Trigger(msg :: list |> Array.ofList)
            return! loop DateTime.Now []
        | Some(msg) ->
            // Continue collecting more mesages
            return! loop start (msg::list)
        | None -> 
            // Nothing received - check time & retry
            return! loop start list }
    loop DateTime.Now [] ), cts.Token)

  [<CLIEventAttribute>]
  member x.BulkProduced = bulkEvent.Publish
  
  member x.Enqueue v = agent.Post(v)

  interface IDisposable with
         member x.Dispose() = cts.Cancel()



/// Agent that implements batch processing
type BatchProcessor<'T>(count) =
  // Event used to report aggregated batches to the user
  let batchEvent = new Event<'T[]>()
  // Trigger event on the thread where the agent is running
  let reportBatch batch =
    try
      // If the handler throws, we need to handle the exception
      batchEvent.Trigger(batch)
    with e ->
      printfn "Event handler failed: %A" e

  // Start an agent that implements the batching
  let agent = Agent<'T>.Start(fun inbox -> async {
    while true do
      // Repeatedly allocate a new queue 
      let queue = new ResizeArray<_>()
      // Add specified number of messages to the queue
      for i in 1 .. count do
        let! msg = inbox.Receive()
        queue.Add(msg)
      // Report the batch as an array
      reportBatch (queue.ToArray()) })

  /// Event that is triggered when a batch is collected
  member x.BatchProduced = batchEvent.Publish
  /// The method adds one object to the agent
  member x.Post(value) = agent.Post(value)
