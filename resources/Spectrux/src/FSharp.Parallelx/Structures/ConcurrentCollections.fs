namespace FSharp.Parallelx.CollectionEx

open System.Collections.Concurrent 
open System.Collections
open System.Collections.Generic

/// A type that spans effectively limitless memory while keeping each individual chunk away from the LOH threshold size.
/// This type is not threadsafe in any way, so callers must ensure single-threaded access to all members
type HeapAwareResizeArray<'T> () =
    let heapsize = 84_900 // deliberately off from the actual heapsize of 85000 bytes
    let elementsPerArray = heapsize / sizeof<'T>

    let newChild () = ResizeArray<_>(elementsPerArray)
    let innerArrays =
        // initialize here so that the counting logic in Add isn't off
        let holder = ResizeArray<ResizeArray<'T>>()
        holder.Add(newChild())
        holder

    let mutable length = 0

    member x.Length = length

    member x.Add item =
        let arrayToInsert = 
            let last = innerArrays.[innerArrays.Count - 1]
            if last.Count = elementsPerArray 
            then
                let newLast = newChild ()
                innerArrays.Add newLast
                newLast
            else last
        arrayToInsert.Add item
        length <- length + 1

    //TODO: we have to do compaction as part of this, because logic elsewhere expects 'add's to be in sequence
    member x.RemoveAll pred =
        let mutable removed = 0
        let mutable arrayIndex = 0
        let mutable arrayElementIndex = 0
        for innerArray in innerArrays do
            for item in innerArray do
                if pred item 
                then removed <- removed + 1
                else
                    if removed > 0 then
                        innerArrays.[arrayIndex].[arrayElementIndex] <- item
                        arrayElementIndex <- arrayElementIndex + 1
                        if arrayElementIndex = elementsPerArray then
                            arrayIndex <- arrayIndex + 1
                            arrayElementIndex <- 0
        while innerArrays.Count > arrayIndex + 1 do
            innerArrays.RemoveAt(arrayIndex + 1)

        // let child = innerArrays.[arrayIndex]
        // while child.Count > arrayElementIndex do
        //     child.RemoveAt(child.Count - 1)

        length <- length - removed
        removed

    interface IEnumerable with
        member x.GetEnumerator() = (x :> IEnumerable<'T>).GetEnumerator() :> _

    interface IEnumerable<'T> with
        member x.GetEnumerator() : IEnumerator<'T> =
             (seq {
                 for innerArray in innerArrays do
                    for item in innerArray do
                        yield item
             }).GetEnumerator()
             
             

type ConcurrentMap<'key,'value> = ConcurrentDictionary<'key,'value>

module ConcurrentMap =
 
    let empty<'key,'value> = ConcurrentMap<'key,'value>()

    let inline addOrUpdate key value (map: ConcurrentMap<_,_>) =
        map.AddOrUpdate(key, value, fun _ _ -> value)

    let inline addOrUpdateWith key value computeValue (map: ConcurrentMap<_,_>) =
        map.AddOrUpdate(key, value, fun k v -> computeValue k v)

    let inline clear (map: ConcurrentMap<_,_>) =
        map.Clear()
        map

    let inline containsKey key (map: ConcurrentMap<_,_>) =
        map.ContainsKey(key)

    let inline count (map: ConcurrentMap<_,_>) =
        map.Count
    
    let inline getOrAdd key (value: 'b) (map: ConcurrentMap<_,_>) =
        map.GetOrAdd(key, value)

    let inline isEmpty (map: ConcurrentMap<_,_>) =
        map.IsEmpty

    let inline keys (map: ConcurrentMap<_,_>) =
        map.Keys

    let inline values (map: ConcurrentMap<_,_>) =
        map.Values

    let inline tryGetValue key (map: ConcurrentMap<_,_>) =
        match map.TryGetValue(key) with
        | (true, value) -> Some value
        | _ -> None

    let inline tryRemove key (map: ConcurrentMap<_,_>) =
        match map.TryRemove(key) with
        | (true, value) -> Some value
        | _ -> None

    let inline tryUpdate key newValue oldValue (map: ConcurrentMap<_,_>) =
        map.TryUpdate(key, newValue, oldValue)

    let inline toSeq (map: ConcurrentMap<_,_>) =
        seq { for kvp in map -> kvp.Key, kvp.Value }
    
    let toList<'key,'value> = toSeq >> Seq.toList<'key*'value>

    let toArray<'key,'value> = toSeq >> Seq.toArray<'key*'value>

    let inline ofSeq items =
        ConcurrentDictionary(items |> Seq.map (fun (key, value) -> KeyValuePair(key, value)))
    
    let ofList<'key,'value> : List<'key*'value> -> ConcurrentDictionary<'key,'value> = ofSeq

    let ofArray<'key,'value> : ('key*'value) [] -> ConcurrentDictionary<'key,'value> = ofSeq


module ConcurrentBag =
    let empty<'item> = ConcurrentBag<'item>()
    
    let inline add item (bag: ConcurrentBag<_>) =
        bag.Add(item)
        bag

    let inline count (bag: ConcurrentBag<_>) =
        bag.Count

    let inline isEmpty (bag: ConcurrentBag<_>) =
        bag.IsEmpty

    let inline tryTake (bag: ConcurrentBag<_>) =
        match bag.TryTake() with
        | (true, item) -> Some item
        | _ -> None

    let inline tryPeek (bag: ConcurrentBag<_>) =
        match bag.TryPeek() with
        | (true, item) -> Some item
        | _ -> None

    let inline toSeq (bag: ConcurrentBag<_>) =
        seq { for element in bag -> element }
    
    let toList<'item> = toSeq >> Seq.toList<'item>

    let toArray<'item> = toSeq >> Seq.toArray<'item>

    let inline ofSeq items =
        ConcurrentBag(items)
    
    let ofList<'item> : List<'item> -> ConcurrentBag<'item> = ofSeq

    let ofArray<'item> : 'item [] -> ConcurrentBag<'item> = ofSeq


module ConcurrentQueue =
    let empty<'item> = ConcurrentQueue<'item>()

    let inline enqueue item (queue: ConcurrentQueue<_>) =
        queue.Enqueue(item)
        queue

    let inline count (queue: ConcurrentQueue<_>) =
        queue.Count

    let inline isEmpty (queue: ConcurrentQueue<_>) =
        queue.IsEmpty

    let inline tryDequeue (queue: ConcurrentQueue<_>) =
        match queue.TryDequeue() with
        | (true, item) -> Some item
        | _ -> None

    let inline tryPeek (queue: ConcurrentQueue<_>) =
        match queue.TryPeek() with
        | (true, item) -> Some item
        | _ -> None

    let inline toSeq (queue: ConcurrentQueue<_>) =
        seq { for element in queue -> element }
    
    let toList<'item> = toSeq >> Seq.toList<'item>

    let toArray<'item> = toSeq >> Seq.toArray<'item>

    let inline ofSeq items =
        ConcurrentQueue(items)
    
    let ofList<'item> : List<'item> -> ConcurrentQueue<'item> = ofSeq

    let ofArray<'item> : 'item [] -> ConcurrentQueue<'item> = ofSeq

module ConcurrentStack =
    let empty<'item> = ConcurrentStack<'item>()
    
    let inline push item (stack: ConcurrentStack<_>) =
        stack.Push(item)
        stack

    let inline pushRange items (stack: ConcurrentStack<_>) =
        stack.PushRange(items)
        stack

    let inline clear (stack: ConcurrentStack<_>) =
        stack.Clear()
        stack

    let inline count (stack: ConcurrentStack<_>) =
        stack.Count

    let inline isEmpty (stack: ConcurrentStack<_>) =
        stack.IsEmpty

    let inline tryPop (stack: ConcurrentStack<_>) =
        match stack.TryPop() with
        | (true, item) -> Some item
        | _ -> None

    let inline tryPopRange items (stack: ConcurrentStack<_>) =
        stack.TryPopRange(items)

    let inline tryPeek (stack: ConcurrentStack<_>) =
        match stack.TryPeek() with
        | (true, item) -> Some item
        | _ -> None

    let inline toSeq (stack: ConcurrentStack<_>) =
        seq { for element in stack -> element }
    
    let toList<'item> = toSeq >> Seq.toList<'item>

    let toArray<'item> = toSeq >> Seq.toArray<'item>

    let inline ofSeq items =
        ConcurrentStack(items)
    
    let ofList<'item> : List<'item> -> ConcurrentStack<'item> = ofSeq

    let ofArray<'item> : 'item [] -> ConcurrentStack<'item> = ofSeq