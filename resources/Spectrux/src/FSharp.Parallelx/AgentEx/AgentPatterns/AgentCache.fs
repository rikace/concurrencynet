namespace FSharp.Parallelx.AgentEx

#nowarn "11"
    
module AgentCache =

    open System
    open System.Collections.Generic
    open System.Threading
    open System.Threading.Tasks
    
    type private CacheMessage<'Key> =
        | GetOrSet of 'Key * AsyncReplyChannel<obj>
        | UpdateFactory of Func<'Key,obj>
        | Clear
        
    type Cache<'Key when 'Key : comparison> (factory : Func<'Key, obj>, ?timeToLive : int,
                                             ?synchContext:SynchronizationContext) =
        let timeToLive = defaultArg timeToLive 10
        let expiry = TimeSpan.FromMilliseconds (float timeToLive)
    
        let cacheItemRefreshed = Event<('Key * 'obj)[]>()   
    
        let reportBatch items =    
            match synchContext with 
            | None -> cacheItemRefreshed.Trigger(items)  
            | Some ctx -> 
               ctx.Post((fun _ -> cacheItemRefreshed.Trigger(items)), null)  
    
        let cacheAgent = Agent.Start(fun inbox ->
            let cache = Dictionary<'Key, (obj * DateTime)>(HashIdentity.Structural)
            let rec loop (factory:Func<'Key, obj>) = async {
                let! msg = inbox.TryReceive timeToLive
                match msg with
                | Some (GetOrSet (key, channel)) ->
                    match cache.TryGetValue(key) with
                    | true, (v,dt) when DateTime.Now - dt < expiry ->
                        channel.Reply v
                        return! loop factory
                    | _ ->
                        let value = factory.Invoke(key)
                        channel.Reply value
                        reportBatch ([| (key, value) |])    
                        cache.Add(key, (value, DateTime.Now))
                        return! loop factory
                | Some(UpdateFactory newFactory) ->
                    return! loop (newFactory)
                | Some(Clear) ->
                    cache.Clear()
                    return! loop factory
                | None ->
                    cache 
                    |> Seq.choose(function KeyValue(k,(_, dt)) -> 
                                                if DateTime.Now - dt > expiry then 
                                                    let value, dt = factory.Invoke(k), DateTime.Now
                                                    cache.[k] <- (value,dt)
                                                    Some (k, value)
                                                else None)
                    |> Seq.toArray
                    |> reportBatch   
                }
            loop factory )
            
//        member this.TryGet<'a>(key : 'Key) = async {
//                let! item = cacheAgent.PostAndAsyncReply(fun channel -> GetOrSet(key, channel))
//                match item with
//                | :? 'a as v -> return Some v
//                | _ -> return None  }
        member this.DataRefreshed = cacheItemRefreshed.Publish   
        member this.Clear() = cacheAgent.Post(Clear)
        
    open FSharp.Parallelx.AtomTrans
    
    type CacheAtom<'T> (factory : unit -> 'T, ?timeToLive : int) =
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
        