<Query Kind="FSharpProgram" />

let ParallelThrottledIgnore (startOnCallingThread:bool) (parallelism:int) (xs:seq<Async<_>>) = async {
  let! ct = Async.CancellationToken
  let sm = new SemaphoreSlim(parallelism)
  let count = ref 1
  let res = TaskCompletionSource<_>()
  let tryWait () =
    try sm.Wait () ; true
    with _ -> false
  let tryComplete () =
    if Interlocked.Decrement count = 0 then
      res.TrySetResult() |> ignore
      false
    else
      not res.Task.IsCompleted
  let ok _ =
    if tryComplete () then
      try sm.Release () |> ignore with _ -> ()
  let err (ex:exn) = res.TrySetException ex |> ignore
  let cnc (_:OperationCanceledException) = res.TryCancel() |> ignore
  let start = async {
    use en = xs.GetEnumerator()
    while not (res.Task.IsCompleted) && en.MoveNext() do
      if tryWait () then
        Interlocked.Increment count |> ignore
        if startOnCallingThread then Async.StartWithContinuations (en.Current, ok, err, cnc, ct)
        else startThreadPoolWithContinuations (en.Current, ok, err, cnc, ct)
    tryComplete () |> ignore }
  Async.Start (tryWith (err >> async.Return) start, ct)
  return! res.Task |> Async.AwaitTask }