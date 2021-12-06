namespace Functional

module Helpers =

    open System
    open System.Threading.Tasks
    open System.Collections.Generic
    open System.Collections.Concurrent

    module Memoize =

        let memoize func =
            let table = Dictionary<_,_>()
            fun x ->   if table.ContainsKey(x) then table.[x]
                        else
                            let result = func x
                            table.[x] <- result
                            result


        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior
        let memoizeThreadSafe (func: 'a -> 'b) =
            // Add missing code
            Unchecked.defaultof<'a -> 'b>


        let memoizeWithEnviction cacheTimeSeconds (caller:string) (f: ('a -> 'b)) =
            let cacheTimes = ConcurrentDictionary<string,DateTime>()
            let cache = ConcurrentDictionary<'a, 'b>()
            fun x ->
                match cacheTimes.TryGetValue caller with
                | true, time when time < DateTime.UtcNow.AddSeconds(-cacheTimeSeconds)
                    -> cache.TryRemove(x) |> ignore
                | _ -> ()
                cache.GetOrAdd(x, fun x ->
                    cacheTimes.AddOrUpdate(caller, DateTime.UtcNow, fun _ _ ->DateTime.UtcNow)|> ignore
                    f(x)
                    )

        let memoizeWithEnvictionAsync cacheTimeSeconds (caller:string) (f: ('a -> Async<'b>)) =
            let cacheTimes = ConcurrentDictionary<string,DateTime>()
            let cache = ConcurrentDictionary<'a, System.Threading.Tasks.Task<'b>>()
            fun x ->
                match cacheTimes.TryGetValue caller with
                | true, time when time < DateTime.UtcNow.AddSeconds(-cacheTimeSeconds)
                    -> cache.TryRemove(x) |> ignore
                | _ -> ()
                cache.GetOrAdd(x, fun x ->
                    cacheTimes.AddOrUpdate(caller, DateTime.UtcNow, fun _ _ ->DateTime.UtcNow)|> ignore
                    f(x) |> Async.StartAsTask
                    ) |> Async.AwaitTask
