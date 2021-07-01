namespace FSharp.Parallelx.CollectionEx

[<RequireQualifiedAccess>]
module Seq =
    let partition condition values =
        let pairs = seq {
            for i in values do
                if condition i then
                    yield Some(i), None
                else
                    yield None, Some(i) }

        pairs |> Seq.choose fst, pairs |> Seq.choose snd

    let fibs take =
        Seq.unfold
            (fun (n0, n1) ->
                Some(n0, (n1, n0 + n1)))
            (1I,1I)
        |> Seq.take take

    let rec cycle s = seq { yield! s; yield! cycle s }

    let circularSeq (lst:'a list) =
        let rec next () =
            seq {
                for element in lst do
                    yield element
                yield! next()
            }
        next()

    /// Returns a sequence that yields chunks of length n.
    /// Each chunk is returned as a list.
    let split length (xs: seq<'T>) =
        let rec loop xs =
            [
                yield Seq.truncate length xs |> Seq.toList
                match Seq.length xs <= length with
                | false -> yield! loop (Seq.skip length xs)
                | true -> ()
            ]
        loop xs


/// Additional operations on Map<'Key, 'Value>
[<RequireQualifiedAccess>]
module Map =
    open System.Collections.Generic
  
    let keys   (source: #IReadOnlyDictionary<_,_>) = Seq.map (fun (KeyValue(k, _)) -> k) source
    let values (source: #IReadOnlyDictionary<_,_>) = Seq.map (fun (KeyValue(_, v)) -> v) source
    
    let tryGetValue k (dct: IDictionary<'Key, 'Value>) =
        match dct.TryGetValue k with
        | true, v -> Some v
        | _       -> None

    let keys'   (source: IDictionary<_,_>) = Seq.map (fun (KeyValue(k, _)) -> k) source
    let values' (source: IDictionary<_,_>) = Seq.map (fun (KeyValue(_, v)) -> v) source
    
    
[<RequireQualifiedAccess>]    
module List = 
  /// Returns a singleton list containing a specified value
  let singleton v = [v]
      
  let rec factorial_aux k = function
    | 0 | 1 -> k 1
    | n -> factorial_aux (fun m -> k (n * m)) (n - 1)
    
  let factorial = factorial_aux (fun n -> n)
    
  let rec map_aux k f = function
    | [] -> k []
    | x::xs -> map_aux (fun ys -> k(f x::ys)) f xs
    
  let map f = map_aux id f
    
  let rec foldk (f : 'State -> 'a -> ('State -> 'State) -> 'State) (acc:'State) (xs :'a list) =
    match xs with
    | []    -> acc
    | x::xs -> f acc x (fun lacc -> foldk f lacc xs)
    
    
  let mapK f lst = foldk f [] lst
    
  //map (fun acc x k -> k ((string x)::acc)) [1..10]
  // [1..5] |> foldk (fun acc x k -> if x < 3 then k (acc + x) else acc) 0 // 3
    
  let tryPick predicate xs =
    xs |> foldk (fun acc x k ->
        if   predicate x
        then Some x
        else k acc
    ) None
    
  let contains y xs =
    xs |> foldk (fun acc x k ->
        if   x = y
        then true
        else k acc
    ) false
    
  let exists predicate xs =
    xs |> foldk (fun acc x k ->
        if   predicate x
        then true
        else k acc
    ) false
    
  let forall predicate xs =
    xs |> foldk (fun acc x k ->
        if   predicate x
        then k acc
        else false
    ) true
    
  let item idx xs =
    xs |> foldk (fun acc x k ->
        if   idx = acc
        then x
        else k (acc + 1)
    ) 0
    
  let take amount xs =
    xs |> foldk (fun (collected,acc) x k ->
        if   collected < amount
        then k (collected+1, x::acc)
        else (collected,acc)
    ) (0,[]) |> snd  |> List.rev
    
    
  let rec insertions x = function
    | []             -> [[x]]
    | (y :: ys) as l -> (x::l)::(List.map (fun x -> y::x) (insertions x ys))
    
  let rec permutations = function
    | []      -> seq [ [] ]
    | x :: xs -> Seq.concat (Seq.map (insertions x) (permutations xs))
  
  /// Skips the specified number of elements. Fails if the list is smaller.
  let rec skip count = function
    | xs when count = 0 -> xs
    | _::xs when count > 0 -> skip (count - 1) xs
    | _ -> invalidArg "" "Insufficient length"

  /// Skips elements while the predicate returns 'true' and then 
  /// returns the rest of the list as a result.
  let rec skipWhile p = function
    | hd::tl when p hd -> skipWhile p tl
    | rest -> rest

  /// Partitions list into an initial sequence (while the 
  /// specified predicate returns true) and a rest of the list.
  let partitionWhile p input = 
    let rec loop acc = function
      | hd::tl when p hd -> loop (hd::acc) tl
      | rest -> List.rev acc, rest
    loop [] input

  /// Partitions list into an initial sequence (while the specified predicate 
  /// returns true) and a rest of the list. The predicate gets the entire 
  /// tail of the list and can perform lookahead.
  let partitionWhileLookahead p input = 
    let rec loop acc = function
      | hd::tl when p (hd::tl) -> loop (hd::acc) tl
      | rest -> List.rev acc, rest
    loop [] input

  /// Partitions list into an initial sequence (while the 
  /// specified predicate returns 'false') and a rest of the list.
  let partitionUntil p input = partitionWhile (p >> not) input

  /// Partitions list into an initial sequence (while the 
  /// specified predicate returns 'false') and a rest of the list.
  let partitionUntilLookahead p input = partitionWhileLookahead (p >> not) input

  /// Iterates over the elements of the list and calls the first function for 
  /// every element. Between each two elements, the second function is called.
  let rec iterInterleaved f g input =
    match input with 
    | x::y::tl -> f x; g (); iterInterleaved f g (y::tl)
    | x::[] -> f x
    | [] -> ()

  /// Tests whether a list starts with the elements of another
  /// list (specified as the first parameter)
  let inline startsWith start (list:'T list) = 
    let rec loop start (list:'T list) = 
      match start, list with
      | x::xs, y::ys when x = y -> loop xs ys
      | [], _ -> true
      | _ -> false
    loop start list

  /// Partitions the input list into two parts - the break is added 
  /// at a point where the list starts with the specified sub-list.
  let partitionUntilEquals endl input = 
    let rec loop acc = function
      | input when startsWith endl input -> Some(List.rev acc, input)
      | x::xs -> loop (x::acc) xs
      | [] -> None
    loop [] input    

  /// A function that nests items of the input sequence 
  /// that do not match a specified predicate under the 
  /// last item that matches the predicate. 
  let nestUnderLastMatching f input = 
    let rec loop input = seq {
      let normal, other = partitionUntil f input
      match List.rev normal with
      | last::prev ->
          for p in List.rev prev do yield p, []
          let other, rest = partitionUntil (f >> not) other
          yield last, other 
          yield! loop rest
      | [] when other = [] -> ()
      | _ -> invalidArg "" "Should start with true" }
    loop input |> List.ofSeq
