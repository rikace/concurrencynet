namespace FSharp.Parallelx.CollectionEx

module HashMultimap =
    open System
    open System.Runtime.InteropServices
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Linq

    /// Functor used to create a HashMultiMap container type.
    [<AbstractClass>]
    type public HashMultiMapFunctor<'K,'V when 'K: equality and 'V:comparison>(dict : IDictionary<'K,HashSet<'V>>) =

        /// Add a value to the multimap under a specified key
        abstract member Add : 'K -> 'V -> unit

        /// Remove a single value under the specified key from the multimap
        abstract member RemoveValue : 'K -> 'V -> bool

        /// Non concurrent implementation of Add
        member x.AddNonConcurrent (key:'K) (value:'V) =
            match dict.TryGetValue key with
            | true, hashset ->
                hashset.Add(value) |> ignore
            | _ ->
                dict.Add(key, HashSet<'V>([value]))

        /// Remove all entries from the map
        member x.Clear() =
            dict.Clear()

        /// Remove all entries from the map and force clearing each value bag
        member x.DeepClear() =
            dict.Values
            |> Seq.iter (fun bag -> bag.Clear())
            x.Clear()

        /// Set the set of values associated with a given key
        member x.SetRange (key:'K) (values:HashSet<'V>) =
            dict.[key] <- values

        /// Remove an entire key entry
        member x.RemoveKey (key:'K) =
            dict.Remove key |> ignore

        /// Gets the sequence of hash keys
        member x.Keys = dict.Keys

        /// Remove a single value from the map (non-concurrent implementation)
        member x.RemoveValueNonConcurrent (key:'K) (value:'V) =
            match dict.TryGetValue key with
            | true, values ->
                if values.Remove value then
                    if not (values.Any()) then
                        dict.Remove key |> ignore
                    true
                else
                    false
            | false, _ ->
                false

        /// Determine if a key exists
        member x.ContainsKey (key:'K) =
            dict.ContainsKey key

        /// Returns the values associated with a specified key as an enumerable
        member x.Item (key:'K) =
            match dict.TryGetValue key with
            | true, values -> values.AsEnumerable()
            | _ -> Seq.empty<'V>

        /// Same as Items but returns a collection instead
        member x.ItemAsCollection (key:'K) =
            match dict.TryGetValue key with
            | true, values -> values :> ICollection<'V>
            | _ -> Set.empty<'V> :> ICollection<'V>

        /// Lookup item
        member x.TryGetValue (key:'K, [<Out>]value:byref<HashSet<'V>>) =
            dict.TryGetValue(key,&value)


    /// A concurrent HashMultiMap container type created from an existing
    /// concurrent dictionary.
    //
    /// WARNING: this implementation only guarantees thread-safety when accessing the
    /// keys. The values are still stored in a thread-*unsafe* HashSet.
    /// This is fine as long as no two threads access and modify the same key's values at the same time.
    /// (which is the case in the context of RetSet)
    type public ConcurrentHashMultiMap<'K,'V when 'K: equality and 'V:comparison>(concurrentDict : ConcurrentDictionary<'K,HashSet<'V>>) =
        inherit HashMultiMapFunctor<'K,'V>(concurrentDict)

        /// Thread-safe implementation of Add.
        override x.Add key value =
            concurrentDict.AddOrUpdate(
                key,
                (fun _ -> new HashSet<'V>([value])),
                (fun _ (existingHashSet:HashSet<'V>) ->
                        lock existingHashSet (fun () -> existingHashSet.Add value |> ignore; existingHashSet)))
            |> ignore

        /// Thread-safe implementation of RemoveValue.
        override x.RemoveValue (key:'K) (value:'V) =             
            // TODO: implement thread-safe value removal: requires either
            // - extending ConcurrentDictionary class with a new RemoveOrUpdate atomic construct
            // - or replacing HashSet values with thread-safe HashSets
            raise (NotImplementedException())