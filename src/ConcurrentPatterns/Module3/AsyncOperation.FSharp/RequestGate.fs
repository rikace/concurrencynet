module RequestGate

open System
open System.Threading

type RequestGate(n: int) =
    let semaphore = new SemaphoreSlim(n, n)

    member x.Acquire(?timeout: TimeSpan) =
        // TODO LAB
        // implement the logic to coordinate the access to resources
        // using "semaphore". Keep async semantic for the "acquire" and "release" of the handle
        // throw new Exception("No implemented");
        //
        // Note: the "SemaphoreSlim" class could help with the implementation
        async {
            let! ok = semaphore.WaitAsync(timeout=(defaultArg timeout TimeSpan.MaxValue)) |> Async.AwaitTask
            if ok then
               return
                 { new System.IDisposable with
                     member x.Dispose() =
                         semaphore.Release() |> ignore }
            else
               return! failwith "couldn't acquire a semaphore"
        }
