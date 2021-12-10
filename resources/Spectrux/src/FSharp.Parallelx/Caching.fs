namespace FSharp.Parallelx

module AtomCache =
    open System.Threading

    type Atom<'T when 'T : not struct> (value : 'T) =
        let cell = ref value

        let rec swap f =
            let currentValue = !cell
            let result = Interlocked.CompareExchange<'T>(cell, f currentValue, currentValue)
            if obj.ReferenceEquals(result, currentValue) then ()
            else Thread.SpinWait 20; swap f

        let transact f =
            let output = ref Unchecked.defaultof<'S>
            let f' x = let t,s = f x in output := s ; t
            swap f' ; !output

        member __.Value = !cell
        member __.Swap (f : 'T -> 'T) : unit = swap f
        member __.Transact (f : 'T -> 'T * 'S) : 'S = transact f

    // implementation of the caching mechanism

    open System

    type Cache<'T> (factory : unit -> 'T, ?timeToLive : int) =
        let ttl = defaultArg timeToLive 1000 |> float |> TimeSpan.FromMilliseconds
        let container = Atom<(Choice<'T,exn> * DateTime) option> None

        member __.Value =
            let update () =
                let value = try factory () |> Choice1Of2 with e -> Choice2Of2 e
                Some (value, DateTime.Now), value

            let result =
                container.Transact(
                    function
                    | None -> update ()
                    | Some(_, time) when DateTime.Now - time > ttl -> update ()
                    | Some(value, _) as state -> state, value)

            match result with
            | Choice1Of2 v -> v
            | Choice2Of2 e -> raise e

    // example

//    let cache = new Cache<_>(fun () -> printfn "computing..."; 42)
//
//    for _ in 1 .. 1000000 do
//        cache.Value |> ignore


module Caching =

    open System
    open System.Threading.Tasks
    open System.Collections.Concurrent

    let inline internal memoizeByExt (getKey : 'a -> 'key) (f: 'a -> 'b) : ('a -> 'b) * ('key * 'b -> unit) =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<'key, 'b>(HashIdentity.Structural)
        (fun (x: 'a) ->
            cache.GetOrAdd(getKey x, fun _ -> f x)),
        (fun (key, c) ->
            cache.TryAdd(key, c) |> ignore)

//    let memoize (f : 'T -> 'U) =
//        let cache = ConcurrentDictionary(HashIdentity.Structural)
//        fun x ->
//            cache.GetOrAdd(x, lazy (f x)).Force()

    let inline internal memoizeBy (getKey : 'a -> 'key) (f: 'a -> 'b) : ('a -> 'b) =
        memoizeByExt getKey f |> fst

    let inline internal memoize (f: 'a -> 'b) : 'a -> 'b = memoizeBy id f

    type MemoizeAsyncExResult<'TResult, 'TCached> =
        | FirstCall of ( 'TCached * 'TResult ) Task
        | SubsequentCall of 'TCached Task

    let internal memoizeAsyncEx (f: 'iext -> 'i -> Async<'o * 'oext>) =
        let cache = ConcurrentDictionary<'i, Task<'o>>()
        let handle (ex:'iext) (x:'i) : MemoizeAsyncExResult<'oext, 'o> =
            let mutable tcs_result = null
            let task_cached = cache.GetOrAdd(x, fun x ->
                tcs_result <- TaskCompletionSource()
                let tcs = TaskCompletionSource()

                Async.Start (async {
                    try
                        let! o, oext = f ex x
                        tcs.SetResult o
                        tcs_result.SetResult (o, oext)
                    with
                      exn ->
                        tcs.SetException exn
                        tcs_result.SetException exn
                })

                tcs.Task)
            // if this was the first call for the key, then tcs_result was set inside 'cache.GetOrAdd'
            if tcs_result <> null then
                FirstCall tcs_result.Task
            else
                SubsequentCall task_cached
        handle


    let internal memoizeAsync f =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<'a, System.Threading.Tasks.Task<'b>>()
        fun (x: 'a) -> // task.Result serialization to sync after done.
            cache.GetOrAdd(x, fun x -> f(x) |> Async.StartAsTask) |> Async.AwaitTask




    let memoize2 (f : 'a -> 'b -> 'c) =
        let f = (fun (a,b) -> f a b) |> memoize
        fun a b -> f (a,b)

    let memoize3 (f : 'a -> 'b -> 'c -> 'd) =
        let f = (fun (a,b,c) -> f a b c) |> memoize
        fun a b c -> f (a,b,c)

    let memoizeEnviction cacheTimeSeconds (caller:string) (f: ('a -> 'b)) =
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

    let memoizeEnvictionAsync cacheTimeSeconds (caller:string) (f: ('a -> Async<'b>)) =
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

//    let rec fibFast =
//        memoize (fun n -> if n <= 2 then 1 else fibFast (n - 1) + fibFast (n - 2))

    type ICache<'TKey, 'TValue> =
      abstract Set : key:'TKey * value:'TValue -> unit
      abstract TryRetrieve : key:'TKey * ?extendCacheExpiration:bool -> 'TValue option
      abstract Remove : key:'TKey -> unit

    /// Creates a cache that uses in-memory collection
    let createInMemoryCache (expiration:TimeSpan) =
        let dict = ConcurrentDictionary<'TKey_,'TValue*DateTime>()
        let rec invalidationFunction key =
            async {
                do! Async.Sleep (int expiration.TotalMilliseconds)
                match dict.TryGetValue(key) with
                | true, (_, timestamp) ->
                    if DateTime.UtcNow - timestamp >= expiration then
                        dict.TryRemove(key) |> ignore
                    else
                        do! invalidationFunction key
                | _ -> ()
            }
        { new ICache<_,_> with
            member __.Set(key, value) =
                dict.[key] <- (value, DateTime.UtcNow)
                invalidationFunction key |> Async.Start
            member x.TryRetrieve(key, ?extendCacheExpiration) =
                match dict.TryGetValue(key) with
                | true, (value, timestamp) when DateTime.UtcNow - timestamp < expiration ->
                    if extendCacheExpiration = Some true then
                        dict.[key] <- (value, DateTime.UtcNow)
                    Some value
                | _ -> None
            member __.Remove(key) = dict.TryRemove(key) |> ignore
        }
