namespace FSharp.Parallelx.CollectionEx

module StreamQueue =
  open System.Collections
  open System.Collections
  open System.Collections.Generic


  [<Struct>]
  [<CustomEquality>]
  [<CustomComparison>]
  type Stream<'a when 'a : equality and 'a : comparison> =
    | Nil
    | Cons of 'a * 'a Stream Lazy
    with
    member private s.getEnumerator =
      let decap = function
        | Nil -> None
        | Cons(h, t) -> Some(h, t.Value)
      (Seq.unfold decap s).GetEnumerator()
      
    override stream1.Equals stream2 =
      List.ofSeq stream1 = List.ofSeq (unbox stream2)
      
    override stream.GetHashCode() =
      (List.ofSeq stream).GetHashCode()      

    interface IEnumerable with
      member s.GetEnumerator() = (s.getEnumerator :> IEnumerator)

    interface IEnumerable<'a> with
      member s.GetEnumerator() = s.getEnumerator
      
    interface System.IEquatable<Stream<'a>> with
      member stream1.Equals(stream2: Stream<'a>) =
        List.ofSeq stream1 = List.ofSeq stream2      

    interface System.IComparable with
      member stream1.CompareTo(stream2: obj) = 
        Seq.compareWith compare stream1 (unbox stream2)
        
    interface System.IComparable<Stream<'a>> with
      member stream1.CompareTo(stream2: Stream<'a>) =
        Seq.compareWith compare stream1 stream2
    
  module Stream =    
    let force (value : Lazy<_>) = value.Force()
    
    let empty = Nil
    let head (Cons (h, _)) = h
    let tail (Cons (_, t)) = force t

    let rec range i j =
      if i=j then Cons(i, lazy empty) else
        Cons(i, lazy(range (i+1) j))

//  Stream.range 0 10 = Stream.range 0 10
//  Stream.range 0 10000 = Stream.range 0 10000

    
  
  type IQueue<'a> =
    inherit Generic.IEnumerable<'a>
    inherit IEnumerable
    inherit System.IComparable<IQueue<'a>>
    inherit System.IComparable
    abstract IsEmpty : bool
    abstract PushBack : 'a -> IQueue<'a>
    abstract PopFront : unit -> 'a * IQueue<'a>

  exception Empty

  type private BatchedQueue<'a when 'a: comparison>(f, r) =
    let mk = function
      | [], r -> BatchedQueue(List.rev r, [])
      | f, r -> BatchedQueue(f, r)

    interface IQueue<'a> with
      override q.IsEmpty =
        match f with
        | [] -> true
        | _ -> false

      override q.PushBack x =
        (mk(f, x::r) :> IQueue<'a>)

      override q.PopFront() =
        match f with
        | x::f -> x, (mk(f, r) :> IQueue<'a>)
        | [] -> raise Empty

    interface Generic.IEnumerable<'a> with
      member q.GetEnumerator() : Generic.IEnumerator<'a> =
        let decap (q: IQueue<_>) =
          if q.IsEmpty then None else Some(q.PopFront())
        (Seq.unfold decap (q :> IQueue<'a>)).GetEnumerator()

    interface IEnumerable with
      member q.GetEnumerator() =
        ((q :> seq<'a>).GetEnumerator() :> IEnumerator)

    override q1.Equals q2 =
      (q1 :> IQueue<'a>).Equals ((unbox q2) :> IQueue<'a>)

    interface System.IComparable<IQueue<'a>> with
      member q1.CompareTo(q2: IQueue<'a>) =
        Seq.compareWith compare q1 q2

    interface System.IComparable with
      member q1.CompareTo(q2: obj) =
        Seq.compareWith compare q1 (unbox q2)

    override q.GetHashCode() =
      hash(List.ofSeq q)


  type private RealTimeQueue<'a when 'a: comparison>(xs, ys, zs) =
    let rec rotate = function
      | Stream.Nil, y::_, zs -> Stream.Cons(y, lazy zs)
      | Stream.Cons(x, xs), y::ys, zs ->
          Stream.Cons(x, lazy(rotate(xs.Value, ys, Stream.Cons(y, lazy zs))))
      | _, [], _ -> invalidArg "" "Queue.rotate"

    let rec exec(xs, ys, zs) =
      match zs with
      | Stream.Cons(_, zs) -> (RealTimeQueue(xs, ys, zs.Value) :> IQueue<'a>)
      | Stream.Nil ->
          let xs' = rotate (xs, ys, Stream.empty)
          (RealTimeQueue(xs', [], xs') :> IQueue<'a>)

    interface IQueue<'a> with
      override q.IsEmpty =
        match xs with
        | Stream.Nil -> true
        | _ -> false

      override q.PushBack y = exec(xs, y::ys, zs)

      override q.PopFront() =
        match xs with
        | Stream.Cons(x, xs) -> x, exec(xs.Value, ys, zs)
        | Stream.Nil -> raise Empty

    interface Generic.IEnumerable<'a> with
      member q.GetEnumerator() : Generic.IEnumerator<'a> =
        let decap (q: IQueue<_>) =
          if q.IsEmpty then None else Some(q.PopFront())
        (Seq.unfold decap (q :> IQueue<'a>)).GetEnumerator()

    interface IEnumerable with
      member q.GetEnumerator() =
        ((q :> seq<'a>).GetEnumerator() :> IEnumerator)

    override q1.Equals q2 =
      (q1 :> IQueue<'a>).Equals ((unbox q2) :> IQueue<'a>)

    interface System.IComparable<IQueue<'a>> with
      member q1.CompareTo(q2: IQueue<'a>) =
        Seq.compareWith compare q1 q2

    interface System.IComparable with
      member q1.CompareTo(q2: obj) =
        Seq.compareWith compare q1 (unbox q2)

    override q.GetHashCode() =
      hash(List.ofSeq q)

  // Note that the RealTimeQueue class defined above and the RealTimeQueue module defined
  // below must be evaluated at the same time in an F# interactive session or the private
  // constructor will not be available to the RealTimeQueue module.

  module RealTimeQueue =
    let empty<'a when 'a: comparison> =
      (RealTimeQueue<'a>(Stream.empty, [], Stream.empty) :> IQueue<'a>)

    let add (q: IQueue<_>) x = q.PushBack x

    let take (q: IQueue<_>) = q.PopFront()

  Seq.fold RealTimeQueue.add RealTimeQueue.empty [1; 2; 3]
