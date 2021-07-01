namespace FSharp.Parallelx.AgentEx.Patterns

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow
open System.Collections.Generic


/// A wrapper for MailboxProcessor that catches all unhandled
/// exceptions and reports them via the 'OnError' event, repeatedly
/// running the provided function until it returns normally.
type ResilientMailbox<'T> private(f:ResilientMailbox<'T> -> Async<unit>) as self =
    // Create an event for reporting errors
    let event = Event<_>()
    // Start the standard MailboxProcessor
    let inbox = new MailboxProcessor<_>(fun _inbox ->
        // Recursivly run the user-provided function until it returns
        // normally; handle any exceptions it throws
        let rec loop() = async {
            // Run the user-provided function and handle exceptions
            try return! f self
            with e ->
                event.Trigger(e)
                return! loop()
            }
        loop())
    /// Triggered when an unhandled exception occurs
    member __.OnError = event.Publish
    /// Starts the mailbox processor
    member __.Start() = inbox.Start()
    /// Receive a message from the mailbox processor
    member __.Receive() = inbox.Receive()
    /// Post a message to the mailbox processor
    member __.Post(v:'T) = inbox.Post(v)
    /// Start the mailbox processor
    static member Start(f) =
        let mbox = new ResilientMailbox<_>(f)
        mbox.Start()
        mbox