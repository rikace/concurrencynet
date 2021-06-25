module RequestGate

open System.Threading

type RequestGate(n: int) =
    let semaphore = new SemaphoreSlim(n, n)

    member x.Acquire(?timeout) =
        // TODO
        // implement the logic to coordinate the access to resources
        // using "semaphore". Keep async semantic for the "acquire" and "release" of the handle
        // throw new Exception("No implemented");
        //
        // Note: the "SemaphoreSlim" class could help with the implementation

        async {
                return
                    // Add missing implementation here
                    { new System.IDisposable with
                        member x.Dispose() = () }

        }
