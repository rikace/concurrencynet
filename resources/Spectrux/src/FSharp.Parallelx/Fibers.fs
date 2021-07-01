namespace FSharp.Parallelx

module Fibers =
      
  open System
  open System.Threading

  type FiberResult<'a> = Result<'a, exn> option

  [<Sealed;AllowNullLiteral>]
  type Cancel(parent: Cancel) =
    let mutable flag: int = 0
    let mutable children: Cancel list = []
    new() = Cancel(null)
    /// Check if token was cancelled
    member __.Cancelled = flag = 1
    /// Remove child token
    member private __.RemoveChild(child) = 
      let rec loop child =
        let children' = children
        let nval = children' |> List.filter ((<>) child)
        if not (obj.ReferenceEquals(children', Interlocked.CompareExchange(&children, nval, children')))
        then loop child
      if not (List.isEmpty children) then loop child
    /// Create a new child token and return it.
    member this.AddChild () =
      let rec loop child =
        let children' = children
        if (obj.ReferenceEquals(children', Interlocked.CompareExchange(&children, child::children', children')))
        then child
        else loop child
      loop (Cancel this)
    /// Cancel a token
    member this.Cancel() =
      if Interlocked.Exchange(&flag, 1) = 0 then
        for child in Interlocked.Exchange(&children, []) do child.Cancel()
        if not (isNull parent) then parent.RemoveChild(this)

  [<Interface>]
  type IScheduler = 
    abstract Schedule: (unit -> unit) -> unit
    abstract Delay: TimeSpan * (unit -> unit) -> unit

  type Fiber<'a> = Fiber of (IScheduler * Cancel -> (FiberResult<'a> -> unit) -> unit)

  [<RequireQualifiedAccess>]
  module Fiber =

    /// Wraps value into fiber.
    let success r = Fiber <| fun (_, c) next -> if c.Cancelled then next None else next (Some (Ok r))

    /// Wraps exception into fiber.
    let fail e = Fiber <| fun (_, c) next -> if c.Cancelled then next None else next (Some (Error e))

    /// Returns a cancelled fiber.
    let cancelled<'a> = Fiber <| fun _ next -> next None

    /// Returns a fiber, which will delay continuation execution after a given timeout.
    let delay timeout =
      Fiber <| fun (s, c) next ->
        if c.Cancelled then next None
        else s.Delay(timeout, fun () -> 
          if c.Cancelled 
          then next None 
          else next (Some (Ok ())))
          
    /// Maps result of Fiber execution to another value and returns new Fiber with mapped value.
    let mapResult fn (Fiber call) = Fiber <| fun (s, c) next ->
      if c.Cancelled then next None
      else 
        try 
          call (s, c) (fun result ->
          if c.Cancelled then next None
          else next (Option.map fn result))
        with e -> next (Some (Error e))

    /// Maps successful result of Fiber execution to another value and returns new Fiber with mapped value.
    let map fn fiber = mapResult (Result.map fn) fiber

    /// Allows to recover from exception (if `fn` returns Ok) or recast it (if `fn` returns Error).
    let catch fn fiber = mapResult (function Error e -> fn e | other -> other) fiber

    let bind fn (Fiber call) = Fiber <| fun (s, c) next ->
      if c.Cancelled then next None 
      else 
        try
          call (s, c) (fun result ->
            if c.Cancelled then next None
            else match result with
               | Some (Ok r) ->
                let (Fiber call2) = fn r
                call2 (s, c) next
               | None -> next None
               | Some (Error e) -> next (Some(Error e))
          )
        with e -> next (Some(Error e))
    
    /// Starts both fibers running in parallel, returning the result from the winner 
    /// (the one which completed first) while cancelling the other.
    let race (Fiber left) (Fiber right): Fiber<Choice<'a, 'b>> =
      Fiber <| fun (s, c) next ->
        if c.Cancelled then next None
        else 
          let mutable flag = 0
          let child = c.AddChild()
          let run fiber choice =
            s.Schedule (fun () ->
              fiber (s, child) (fun result ->
                if Interlocked.Exchange(&flag, 1) = 0 then
                  child.Cancel()
                  if c.Cancelled then next None
                  else match result with
                       | None -> next None
                       | Some(Ok v) -> next (Some(Ok(choice v)))
                       | Some(Error e) -> next (Some(Error e))))
          run left Choice1Of2
          run right Choice2Of2

    let timeout (t: TimeSpan) fiber =
      Fiber <| fun (s, c) next ->
        let (Fiber call) = race (delay t) fiber
        call (s, c) (fun result ->
          if c.Cancelled then next None
          else match result with
               | None -> next None
               | Some(Ok (Choice1Of2 _)) -> next None // timeout won
               | Some(Ok (Choice2Of2 v)) -> next (Some(Ok v))
               | Some(Error e) -> next (Some(Error e))
        )
      
      
    /// Executes a bunch of Fiber operations in parallel, returning an Fiber which may contain
    /// a gathered set of results or (potential) failures that have happened during the execution.
    let parallel fibs =
      Fiber <| fun (s, c) next ->
        if c.Cancelled then next None
        else 
          let mutable remaining = Array.length fibs
          let successes = Array.zeroCreate remaining
          let childCancel = c.AddChild()
          fibs |> Array.iteri (fun i (Fiber call) ->
            s.Schedule (fun () ->
              call (s, childCancel) (fun result -> 
              match result with
              | Some (Ok success) ->
                successes.[i] <- success
                if c.Cancelled && Interlocked.Exchange(&remaining, -1) > 0 then
                  next None 
                elif Interlocked.Decrement(&remaining) = 0 then
                  if c.Cancelled then next None
                  else next (Some (Ok successes))
              | Some (Error fail) ->
                if Interlocked.Exchange(&remaining, -1) > 0 then 
                  childCancel.Cancel()
                  if c.Cancelled then next None
                  else next (Some (Error fail))
              | None ->
                if Interlocked.Exchange(&remaining, -1) > 0 then 
                  next None))
          )
        
    /// Blocks current execution thread, executing given Fiber, and returning result of execution.
    let blocking (s: IScheduler) (cancel: Cancel) (Fiber fn) =
      use waiter = new ManualResetEventSlim(false)
      let mutable res = None
      s.Schedule(fun () -> fn (s, cancel) (fun result ->
        if not cancel.Cancelled then
          Interlocked.Exchange(&res, Some result) |> ignore
        waiter.Set()))
      waiter.Wait()
      res.Value
      
    /// Converts given Fiber into F# Async.
    let toAsync s (Fiber call) = Async.FromContinuations <| fun (onSuccess, onError, onCancel) ->
      call (s, Cancel()) <| fun result ->
        match result with
        | None -> onCancel (OperationCanceledException "")
        | Some (Ok value) -> onSuccess value
        | Some (Error e)  -> onError e
      
  [<RequireQualifiedAccess>]
  module Scheduler =

    /// Default environment, which is backed by .NET Thread pool.
    let shared = 
      { new IScheduler with
        member __.Schedule fn = System.Threading.ThreadPool.QueueUserWorkItem(WaitCallback (ignore>>fn)) |> ignore
        member __.Delay (timeout: TimeSpan, fn) = 
          let mutable t = Unchecked.defaultof<Timer>
          let callback = fun _ -> 
            t.Dispose()
            fn()
            ()
          t <- new Timer(callback, null, int timeout.TotalMilliseconds, Timeout.Infinite)
      }
    type TestScheduler(now: DateTime) =
      let mutable running = false
      let mutable currentTime = now.Ticks
      let mutable timeline = Map.empty
      let schedule delay fn = 
        let at = currentTime + delay
        timeline <-
          match Map.tryFind at timeline with
          | None -> Map.add at [fn] timeline 
          | Some fns -> Map.add at (fn::fns) timeline
      let rec run () =
        match Seq.tryHead timeline with
        | None -> running <- false
        | Some (KeyValue(time, bucket)) ->
          timeline <- Map.remove time timeline
          currentTime <- time
          for fn in List.rev bucket do 
            fn ()          
          run ()
      member __.UtcNow () = DateTime(currentTime)
      interface IScheduler with
        member this.Schedule fn = 
          schedule 0L fn
          if not running then
            running <- true
            run ()
        member this.Delay (timeout: TimeSpan, fn) = schedule timeout.Ticks fn

    let test(cancel, fiber) = 
      let s = TestScheduler(DateTime.UtcNow)
      Fiber.blocking s cancel fiber


  [<Struct>]
  type FiberBuilder =
    member inline __.Zero = Fiber.success (Unchecked.defaultof<_>)
    member inline __.ReturnFrom fib = fib
    member inline __.Return value = Fiber.success value
    member inline __.Bind(fib, fn) = Fiber.bind fn fib


  module FiberBuilder =

    let fib = FiberBuilder()

  //---------------------
  // run some actual code
  //---------------------

  module TestFibers=
    open System
    let inline millis n = TimeSpan.FromMilliseconds (float n)

    let program = FiberBuilder.fib {
      let a = FiberBuilder.fib {
        do! Fiber.delay (millis 5000)
        return 3
      }
      let! b = a |> Fiber.timeout (millis 3000)
      return b }

    let cancel = Cancel ()
    let result = Scheduler.test(cancel, program)
    printfn "Result: %A" result
    