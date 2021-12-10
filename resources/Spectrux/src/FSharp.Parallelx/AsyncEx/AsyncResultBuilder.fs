namespace FSharp.Parallelx.AsyncEx

open System
open System.Threading.Tasks

module AsyncResultBuilder =

  
  let private asyncMap f asnc =
    async {
      let! x = asnc
      return f x
    }
  
  
  module Result =
  
    /// Returns the specified error if the value is false.
    let requireTrue error value =
      if value then Ok () else Error error
  
    /// Returns the specified error if the value is true.
    let requireFalse error value =
      if not value then Ok () else Error error
  
    /// Converts an Option to a Result, using the given error if None.
    let requireSome error option =
      match option with
      | Some x -> Ok x
      | None -> Error error
  
    /// Converts an Option to a Result, using the given error if Some.
    let requireNone error option =
      match option with
      | Some _ -> Error error
      | None -> Ok ()
  
    /// Returns Ok if the two values are equal, or the specified error if not.
    /// Same as requireEqual, but with a signature that fits piping better than
    /// normal function application.
    let requireEqualTo other err this =
      if this = other then Ok () else Error err
  
    /// Returns Ok if the two values are equal, or the specified error if not.
    /// Same as requireEqualTo, but with a signature that fits normal function
    /// application better than piping.
    let requireEqual x1 x2 error =
      if x1 = x2 then Ok () else Error error
  
    /// Returns Ok if the sequence is empty, or the specified error if not.
    let requireEmpty error xs =
      if Seq.isEmpty xs then Ok () else Error error
  
    /// Returns the specified error if the sequence is empty, or Ok if not.
    let requireNotEmpty error xs =
      if Seq.isEmpty xs then Error error else Ok ()
  
    /// Returns the first item of the sequence if it exists, or the specified
    /// error if the sequence is empty
    let requireHead error xs =
      match Seq.tryHead xs with
      | Some x -> Ok x
      | None -> Error error
  
    /// Replaces a unit error value with a custom error value. Safer than setError
    /// since you're not losing any information.
    let withError error result =
      result |> Result.mapError (fun () -> error)
  
    /// Replaces an error value with a custom error value.
    let setError error result =
      result |> Result.mapError (fun _ -> error)
  
    /// Returns the contained value if Ok, otherwise returns ifError.
    let defaultValue ifError result =
      match result with
      | Ok x -> x
      | Error _ -> ifError
  
    /// Returns the contained value if Ok, otherwise evaluates ifErrorThunk and
    /// returns the result.
    let defaultWith ifErrorThunk result =
      match result with
      | Ok x -> x
      | Error _ -> ifErrorThunk ()
  
    /// Same as defaultValue for a result where the Ok value is unit. The name
    /// describes better what is actually happening in this case.
    let ignoreError result =
      defaultValue () result
  
    /// If the result is Ok and the predicate returns true, executes the function
    /// on the Ok value. Passes through the input value.
    let teeIf predicate f result =
      match result with
      | Ok x ->
          if predicate x then f x
      | Error _ -> ()
      result
  
    /// If the result is Ok, executes the function on the Ok value. Passes through
    /// the input value.
    let tee f result =
      teeIf (fun _ -> true) f result
  
    /// If the result is Error and the predicate returns true, executes the
    /// function on the Error value. Passes through the input value.
    let teeErrorIf predicate f result =
      match result with
      | Ok _ -> ()
      | Error x ->
          if predicate x then f x
      result
  
    /// If the result is Error, executes the function on the Error value. Passes
    /// through the input value.
    let teeError f result =
      teeErrorIf (fun _ -> true) f result
  
    
  type ResultBuilder() =
  
    member __.Return (value: 'T) : Result<'T, 'TError> =
      Ok value
  
    member __.ReturnFrom (result: Result<'T, 'TError>) : Result<'T, 'TError> =
      result
  
    member this.Zero () : Result<unit, 'TError> =
      this.Return ()
  
    member __.Bind
        (result: Result<'T, 'TError>, binder: 'T -> Result<'U, 'TError>)
        : Result<'U, 'TError> =
      Result.bind binder result
  
    member __.Delay
        (generator: unit -> Result<'T, 'TError>)
        : unit -> Result<'T, 'TError> =
      generator
  
    member __.Run
        (generator: unit -> Result<'T, 'TError>)
        : Result<'T, 'TError> =
      generator ()
  
    member this.Combine
        (result: Result<unit, 'TError>, binder: unit -> Result<'T, 'TError>)
        : Result<'T, 'TError> =
      this.Bind(result, binder)
  
    member this.TryWith
        (generator: unit -> Result<'T, 'TError>,
         handler: exn -> Result<'T, 'TError>)
        : Result<'T, 'TError> =
      try this.Run generator with | e -> handler e
  
    member this.TryFinally
        (generator: unit -> Result<'T, 'TError>, compensation: unit -> unit)
        : Result<'T, 'TError> =
      try this.Run generator finally compensation ()
  
    member this.Using
        (resource: 'T when 'T :> IDisposable, binder: 'T -> Result<'U, 'TError>)
        : Result<'U, 'TError> =
      this.TryFinally (
        (fun () -> binder resource),
        (fun () -> if not <| obj.ReferenceEquals(resource, null) then resource.Dispose ())
      )
  
    member this.While
        (guard: unit -> bool, generator: unit -> Result<unit, 'TError>)
        : Result<unit, 'TError> =
      if not <| guard () then this.Zero ()
      else this.Bind(this.Run generator, fun () -> this.While (guard, generator))
  
    member this.For
        (sequence: #seq<'T>, binder: 'T -> Result<unit, 'TError>)
        : Result<unit, 'TError> =
      this.Using(sequence.GetEnumerator (), fun enum ->
        this.While(enum.MoveNext,
          this.Delay(fun () -> binder enum.Current)))
  
  
  
  module AsyncResult =

  
    /// Maps the Ok value of an async-wrapped result.
    let map mapper asyncResult =
      asyncResult |> asyncMap (Result.map mapper)
  
    /// Maps the Error value of an async-wrapped result.
    let mapError mapper asyncResult =
      asyncResult |> asyncMap (Result.mapError mapper)
  
    /// Returns the specified error if the async-wrapped value is false.
    let requireTrue error value =
      value |> asyncMap (Result.requireTrue error)
  
    /// Returns the specified error if the async-wrapped value is true.
    let requireFalse error value =
      value |> asyncMap (Result.requireFalse error)
  
    /// Converts an async-wrapped Option to a Result, using the given error if None.
    let requireSome error option =
      option |> asyncMap (Result.requireSome error)
  
    /// Converts an Option to a Result, using the given error if None.
    let requireNone error option =
      option |> asyncMap (Result.requireNone error)
  
    /// Returns Ok if the two values are equal, or the specified error if not.
    let requireEqualTo other error this =
      this |> asyncMap (Result.requireEqualTo other error)
  
    /// Returns Ok if the sequence is empty, or the specified error if not.
    let requireEmpty error xs =
      xs |> asyncMap (Result.requireEmpty error)
  
    /// Returns the specified error if the sequence is empty, or Ok if not.
    let requireNotEmpty error xs =
      xs |> asyncMap (Result.requireNotEmpty error)
  
    /// Returns the first item of the sequence if it exists, or the specified
    /// error if the sequence is empty
    let requireHead error xs =
      xs |> asyncMap (Result.requireHead error)
  
    /// Replaces a unit error value of an async-wrapped result with a custom
    /// error value. Safer than setError since you're not losing any information.
    let withError error asyncResult =
      asyncResult |> asyncMap (Result.withError error)
  
    /// Replaces an error value of an async-wrapped result with a custom error
    /// value.
    let setError error asyncResult =
      asyncResult |> asyncMap (Result.setError error)
  
    /// Extracts the contained value of an async-wrapped result if Ok, otherwise
    /// uses ifError.
    let defaultValue ifError asyncResult =
      asyncResult |> asyncMap (Result.defaultValue ifError)
  
    /// Extracts the contained value of an async-wrapped result if Ok, otherwise
    /// evaluates ifErrorThunk and uses the result.
    let defaultWith ifErrorThunk asyncResult =
      asyncResult |> asyncMap (Result.defaultWith ifErrorThunk)
  
    /// Same as defaultValue for a result where the Ok value is unit. The name
    /// describes better what is actually happening in this case.
    let ignoreError result =
      defaultValue () result
  
    /// If the async-wrapped result is Ok and the predicate returns true, executes
    /// the function on the Ok value. Passes through the input value.
    let teeIf predicate f asyncResult =
      asyncResult |> asyncMap (Result.teeIf predicate f)
  
    /// If the async-wrapped result is Ok, executes the function on the Ok value.
    /// Passes through the input value.
    let tee f asyncResult =
      asyncResult |> asyncMap (Result.tee f)
  
    /// If the async-wrapped result is Error and the predicate returns true,
    /// executes the function on the Error value. Passes through the input value.
    let teeErrorIf predicate f asyncResult =
      asyncResult |> asyncMap (Result.teeErrorIf predicate f)
  
    /// If the async-wrapped result is Error, executes the function on the Error
    /// value. Passes through the input value.
    let teeError f asyncResult =
      asyncResult |> asyncMap (Result.teeError f)
      
        
  
  type AsyncResultBuilder() =

    /// A computation expression to build a Result<'T, 'TError> value.
    let result = ResultBuilder()
  
    member __.Return (value: 'T) : Async<Result<'T, 'TError>> =
      async.Return <| result.Return value
  
    member __.ReturnFrom
        (asyncResult: Async<Result<'T, 'TError>>)
        : Async<Result<'T, 'TError>> =
      asyncResult
  
    member __.ReturnFrom
        (taskResult: Task<Result<'T, 'TError>>)
        : Async<Result<'T, 'TError>> =
      Async.AwaitTask taskResult
  
    member __.ReturnFrom
        (result: Result<'T, 'TError>)
        : Async<Result<'T, 'TError>> =
      async.Return result
  
    member __.Zero () : Async<Result<unit, 'TError>> =
      async.Return <| result.Zero ()
  
    member __.Bind
        (asyncResult: Async<Result<'T, 'TError>>,
         binder: 'T -> Async<Result<'U, 'TError>>)
        : Async<Result<'U, 'TError>> =
      async {
        match! asyncResult with
        | Ok x -> return! binder x
        | Error x -> return Error x
      }
  
    member this.Bind
        (taskResult: Task<Result<'T, 'TError>>,
         binder: 'T -> Async<Result<'U, 'TError>>)
        : Async<Result<'U, 'TError>> =
      this.Bind(Async.AwaitTask taskResult, binder)
  
    member this.Bind
        (result: Result<'T, 'TError>, binder: 'T -> Async<Result<'U, 'TError>>)
        : Async<Result<'U, 'TError>> =
      this.Bind(this.ReturnFrom result, binder)
  
    member __.Delay
        (generator: unit -> Async<Result<'T, 'TError>>)
        : Async<Result<'T, 'TError>> =
      async.Delay generator
  
    member this.Combine
        (computation1: Async<Result<unit, 'TError>>,
         computation2: Async<Result<'U, 'TError>>)
        : Async<Result<'U, 'TError>> =
      this.Bind(computation1, fun () -> computation2)
  
    member __.TryWith
        (computation: Async<Result<'T, 'TError>>,
         handler: System.Exception -> Async<Result<'T, 'TError>>)
        : Async<Result<'T, 'TError>> =
      async.TryWith(computation, handler)
  
    member __.TryFinally
        (computation: Async<Result<'T, 'TError>>,
         compensation: unit -> unit)
        : Async<Result<'T, 'TError>> =
      async.TryFinally(computation, compensation)
  
    member __.Using
        (resource: 'T when 'T :> IDisposable,
         binder: 'T -> Async<Result<'U, 'TError>>)
        : Async<Result<'U, 'TError>> =
      async.Using(resource, binder)
  
    member this.While
        (guard: unit -> bool, computation: Async<Result<unit, 'TError>>)
        : Async<Result<unit, 'TError>> =
      if not <| guard () then this.Zero ()
      else this.Bind(computation, fun () -> this.While (guard, computation))
  
    member this.For
        (sequence: #seq<'T>, binder: 'T -> Async<Result<unit, 'TError>>)
        : Async<Result<unit, 'TError>> =
      this.Using(sequence.GetEnumerator (), fun enum ->
        this.While(enum.MoveNext,
          this.Delay(fun () -> binder enum.Current)))
  
  
  [<AutoOpen>]
  module Extensions =
    let result = ResultBuilder()
    
    // Having Async<_> members as extensions gives them lower priority in
    // overload resolution between Async<_> and Async<Result<_,_>>.
    type AsyncResultBuilder with
      
      member __.ReturnFrom (async': Async<'T>) : Async<Result<'T, 'TError>> =
        async {
          let! x = async'
          return Ok x
        }
  
      member __.ReturnFrom (task: Task<'T>) : Async<Result<'T, 'TError>> =
        async {
          let! x = Async.AwaitTask task
          return Ok x
        }
  
      member __.ReturnFrom (task: Task) : Async<Result<unit, 'TError>> =
        async {
          do! Async.AwaitTask task
          return result.Zero ()
        }
  
      member this.Bind
          (async': Async<'T>, binder: 'T -> Async<Result<'U, 'TError>>)
          : Async<Result<'U, 'TError>> =
        let asyncResult = async {
          let! x = async'
          return Ok x
        }
        this.Bind(asyncResult, binder)
  
      member this.Bind
          (task: Task<'T>, binder: 'T -> Async<Result<'U, 'TError>>)
          : Async<Result<'U, 'TError>> =
        this.Bind(Async.AwaitTask task, binder)
  
      member this.Bind
          (task: Task, binder: unit -> Async<Result<'T, 'TError>>)
          : Async<Result<'T, 'TError>> =
        this.Bind(Async.AwaitTask task, binder)
  
  
  /// A computation expression to build an Async<Result<'T, 'TError>> value.
  let asyncResult = AsyncResultBuilder()
        