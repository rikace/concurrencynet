namespace FSharp.Parallelx

module Registry =



    open System
    open System.Threading

    type TrieKey = 
        | Key of string
        | Wildcard

    type Trie<'v> = Node of 'v option * Map<TrieKey, Trie<'v>>

    module Trie =

        let isEmpty = function
            | Node (None, m) -> Map.isEmpty m
            | _ -> false

        let empty = Node(None, Map.empty)
        
        let values trie = 
            let rec values' acc = function
                | Node(v, m) -> m |> Map.toSeq |> Seq.map snd |> Seq.fold (values') (v :: acc)
            values' [] trie |> List.choose id |> List.rev

        let rec resolve keys trie = 
            match keys, trie with
            | [], Node(None, _) -> []
            | [], Node(Some(a), _) -> [a]
            | Wildcard::ks, Node(_,m) -> 
                Map.toList m |> List.map snd |> List.collect (resolve ks)
            | k::ks, Node(_, m) -> 
                match Map.tryFind k m with
                | None -> []
                | Some(m) -> resolve ks m

        let rec subtrie keys trie = 
            match keys, trie with
            | [], trie -> trie
            | k::ks, Node(_, m) -> 
                match Map.tryFind k m with
                | None -> empty
                | Some(m) -> subtrie ks m

        let add key value trie = 
            let rec add' = function
                | [], Node(_,m) -> Node (Some(value), m)
                | k::ks, Node(v,m) -> 
                    let t' = Map.tryFind k m |> function | Some(a) -> a | None -> empty
                    let t'' = add' (ks, t')
                    Node(v, Map.add k t'' m)
            add' (key,trie)

        let rec remove key trie = 
            match (key,trie) with
            | [], Node (_,m) -> Node (None,m)
            | k::ks, Node (v,m) -> 
                  let t' = remove ks (Map.find k m) 
                  Node (v, if t' = empty then Map.remove k m else Map.add k t' m)




    type IRegistry<'key, 'ref> =
        inherit IDisposable
        abstract Resolve : 'key -> 'ref list
        abstract ResolveAsync : 'key * TimeSpan option -> Async<'ref list>
        abstract Register : 'ref -> unit
        abstract UnRegister : 'ref -> unit
        abstract All : 'ref list with get

    //type ActorRegistry = IRegistry<ActorPath, ActorRef>
    //
    //type InMemoryActorRegistry() =
    //    let syncObj = new ReaderWriterLockSlim()
    //    let actors : Trie<ActorRef> ref = ref Trie.empty
    //    interface ActorRegistry with
    //        member x.All with get() = !actors |> Trie.values
    //
    //        member x.Resolve(path) = 
    //            try
    //                syncObj.EnterReadLock()
    //                let comps = ActorPath.components path
    //                Trie.resolve comps !actors
    //            finally
    //                syncObj.ExitReadLock()
    //
    //        member x.ResolveAsync(path, _) = async { return (x :> ActorRegistry).Resolve(path) }
    //
    //        member x.Register(actor) =
    //            try
    //                let components = ActorPath.components actor.Path
    //                syncObj.EnterWriteLock()
    //                actors := Trie.add components actor !actors
    //            finally
    //                syncObj.ExitWriteLock()
    //
    //        member x.UnRegister actor =
    //            try
    //                let components = ActorPath.components actor.Path
    //                syncObj.EnterWriteLock()
    //                actors := Trie.remove components !actors
    //            finally
    //                syncObj.ExitWriteLock()
    //
    //        member x.Dispose() =
    //            actors := Trie.empty