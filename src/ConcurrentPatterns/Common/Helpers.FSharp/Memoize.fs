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
        let memoize2 f =
            let f = (fun (a,b) -> f a b) |> memoize
            fun a b -> f (a,b)

        let memoize3 f =
            let f = (fun (a,b,c) -> f a b c) |> memoize
            fun a b c -> f (a,b,c)

        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior

        // Thread-safe memoization function
        let memoizeThreadSafe (func: 'a -> 'b) =
            // Unchecked.defaultof<'a -> 'b>
            let table = ConcurrentDictionary<'a,'b>()
            fun x ->   table.GetOrAdd(x, func)

        // Thread-Safe Memoization function with safe lazy evaluation
        // (1) Implement Thread-safe memoization function
        // (2) Optionally, implement memoization with Lazy behavior
        let memoizeThreadSafeTask (func: 'a -> 'b) =
            // Unchecked.defaultof<'a -> Task<'b>>
            let table = ConcurrentDictionary<'a,'b>()
            fun x ->   table.GetOrAdd(x, func)

        let memoizeWithEnviction cacheTimeSeconds (caller:string) (f: ('a -> 'b)) =
            let cacheTimes = ConcurrentDictionary<string,DateTime>()
            let cache = ConcurrentDictionary<'a, 'b>()
            fun (x: 'a) ->
                match cacheTimes.TryGetValue caller with
                | true, time when time < DateTime.UtcNow.AddSeconds(-cacheTimeSeconds)
                    -> cache.TryRemove(x) |> ignore
                | _ -> ()
                cache.GetOrAdd(x, Func<'a, 'b>(fun x ->
                    cacheTimes.AddOrUpdate(caller, DateTime.UtcNow, fun _ _ ->DateTime.UtcNow)|> ignore
                    f(x)
                    ))

        let memoizeWithEnvictionAsync cacheTimeSeconds (caller:string) (f: ('a -> Async<'b>)) =
            let cacheTimes = ConcurrentDictionary<string,DateTime>()
            let cache = ConcurrentDictionary<'a, System.Threading.Tasks.Task<'b>>()
            fun (x: 'a) ->
                match cacheTimes.TryGetValue caller with
                | true, time when time < DateTime.UtcNow.AddSeconds(-cacheTimeSeconds)
                    -> cache.TryRemove(x) |> ignore
                | _ -> ()
                cache.GetOrAdd(x, fun x ->
                    cacheTimes.AddOrUpdate(caller, DateTime.UtcNow, fun _ _ ->DateTime.UtcNow)|> ignore
                    f(x) |> Async.StartAsTask
                    ) |> Async.AwaitTask
