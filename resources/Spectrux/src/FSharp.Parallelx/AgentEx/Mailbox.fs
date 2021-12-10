namespace FSharp.Parallelx.AgentEx

module Mailbox =

    open System
    open System.Threading
    open System.Collections.Concurrent


    type IMailbox<'a> =
        inherit IDisposable
        abstract Post : 'a -> unit
        abstract TryScan : int * ('a -> Async<'b> option) -> Async<Option<'b>>
        abstract Scan : ('a -> Async<'b> option) -> Async<'b>
        abstract TryReceive : int -> Async<Option<'a>>
        abstract Receive : unit -> Async<'a>

    type DefaultMailbox<'a>(id : string, ?metricContext, ?boundingCapacity:int) =
        let mutable disposed = false
        let mutable inbox : ResizeArray<_> = new ResizeArray<_>()
        let mutable arrivals =
            match boundingCapacity with
            | None -> new BlockingCollection<_>()
            | Some(cap) -> new BlockingCollection<_>(cap)
        let awaitMsg = new AutoResetEvent(false)

        let rec scanInbox(f,n) =
            match inbox with
            | null -> None
            | inbox ->
                if n >= inbox.Count
                then None
                else
                    let msg = inbox.[n]
                    match f msg with
                    | None -> scanInbox (f,n+1)
                    | res -> inbox.RemoveAt(n); res

        let rec scanArrivals(f) =
            if arrivals.Count = 0 then None
            else
                 match arrivals.TryTake() with
                 | true, msg ->
                     match f msg with
                     | None ->
                         inbox.Add(msg);
                         scanArrivals(f)
                     | res -> res
                 | false, _ -> None

        let receiveFromArrivals() =
            if arrivals.Count = 0
            then None
            else
                match arrivals.TryTake() with
                | true, msg -> Some msg
                | false, _ -> None

        let receiveFromInbox() =
            match inbox with
            | null -> None
            | inbox ->
                if inbox.Count = 0
                then None
                else
                    let x = inbox.[0]
                    inbox.RemoveAt(0);
                    Some(x)

        interface IMailbox<'a> with
            member __.TryReceive(timeout) =
                  let rec await() =
                      async { match receiveFromArrivals() with
                              | None ->
                                  let! gotArrival = Async.AwaitWaitHandle(awaitMsg, timeout)
                                  if gotArrival
                                  then
                                    return! await()
                                  else
                                    return None
                              | Some res ->
                                return Some(res) }
                  async { match receiveFromInbox() with
                          | None -> return! await()
                          | Some res ->
                            return Some(res) }

            member this.Receive() =
                async {
                let! msg = (this :> IMailbox<'a>).TryReceive(Timeout.Infinite)
                match msg with
                | Some(res) -> return res
                | None -> return raise(TimeoutException("Failed to receive message"))
                }

            member __.TryScan(timeout, f) =
                  let rec await() =
                      async { match scanArrivals(f) with
                              | None ->
                                  let! gotArrival = Async.AwaitWaitHandle(awaitMsg, timeout)
                                  if gotArrival
                                  then return! await()
                                  else return None
                              | Some res ->
                                let! msg = res
                                return Some(msg) }
                  async { match scanInbox(f, 0) with
                          | None -> return! await()
                          | Some res ->
                            let! msg = res
                            return Some(msg) }

            member this.Scan(f) =
                async {
                let! msg = (this :> IMailbox<'a>).TryScan(Timeout.Infinite, f)
                match msg with
                | Some(res) -> return res
                | None -> return raise(TimeoutException("Failed to receive message"))
                }

            member __.Post(msg) =
                if disposed
                then ()
                else
                    arrivals.Add(msg)
                    awaitMsg.Set() |> ignore

            member __.Dispose() =
                inbox <- null
                disposed <- true


    module MailboxUnfold =

          open System
          open System.Threading
          open System.Threading.Tasks
          open FSharp.Parallelx.AsyncEx
          open AsyncExtensions
          type IAsyncEnumerator<'T> =
            abstract MoveNext : unit -> Async<'T option>
            inherit IDisposable

          type IAsyncEnumerable<'T> = 
            abstract GetEnumerator : unit -> IAsyncEnumerator<'T>
            
          type AsyncSeq<'T> = IAsyncEnumerable<'T>

          type AsyncSeqSrc<'a> = private { tail : AsyncSeqSrcNode<'a> ref }

          and private AsyncSeqSrcNode<'a> =
            val tcs : TaskCompletionSource<('a * AsyncSeqSrcNode<'a>) option>
            new (tcs) = { tcs = tcs }
            
          [<AbstractClass>]
          type AsyncSeqOp<'T> () =
              abstract member ChooseAsync : ('T -> Async<'U option>) -> AsyncSeq<'U>
              abstract member FoldAsync : ('S -> 'T -> Async<'S>) -> 'S -> Async<'S>
              abstract member MapAsync : ('T -> Async<'U>) -> AsyncSeq<'U>
              abstract member IterAsync : ('T -> Async<unit>) -> Async<unit>
              default x.MapAsync (f:'T -> Async<'U>) : AsyncSeq<'U> =
                x.ChooseAsync (f >> Async.map Some)
              default x.IterAsync (f:'T -> Async<unit>) : Async<unit> =
                x.FoldAsync (fun () t -> f t) ()    
          
          type UnfoldAsyncEnumerator<'S, 'T> (f:'S -> Async<('T * 'S) option>, init:'S) =
            inherit AsyncSeqOp<'T> ()
            override x.IterAsync g = async {
              let rec go s = async {
                let! next = f s
                match next with
                | None -> return ()
                | Some (t,s') ->
                  do! g t
                  return! go s' }
              return! go init }
            override __.FoldAsync (g:'S2 -> 'T -> Async<'S2>) (init2:'S2) = async {
              let rec go s s2 = async {
                let! next = f s
                match next with
                | None -> return s2
                | Some (t,s') ->
                  let! s2' = g s2 t
                  return! go s' s2' }
              return! go init init2 }
            override __.ChooseAsync (g:'T -> Async<'U option>) : AsyncSeq<'U> =
              let rec h s = async {
                let! res = f s
                match res with
                | None -> 
                  return None
                | Some (t,s) ->
                  let! res' = g t
                  match res' with
                  | Some u ->
                    return Some (u, s)
                  | None ->
                    return! h s }
              new UnfoldAsyncEnumerator<'S, 'U> (h, init) :> _
            override __.MapAsync (g:'T -> Async<'U>) : AsyncSeq<'U> =
              let h s = async {
                let! r = f s
                match r with
                | Some (t,s) ->
                  let! u = g t
                  return Some (u,s)
                | None ->
                  return None }
              new UnfoldAsyncEnumerator<'S, 'U> (h, init) :> _
            interface IAsyncEnumerable<'T> with
              member __.GetEnumerator () =
                let s = ref init
                { new IAsyncEnumerator<'T> with
                    member __.MoveNext () : Async<'T option> = async {
                      let! next = f !s 
                      match next with
                      | None -> 
                        return None 
                      | Some (a,s') ->
                        s := s'
                        return Some a }
                    member __.Dispose () = () }
          

          let unfoldAsync (f:'State -> Async<('T * 'State) option>) (s:'State) : AsyncSeq<'T> = 
            new UnfoldAsyncEnumerator<_, _>(f, s) :> _  

          let replicateInfiniteAsync (v:Async<'T>) : AsyncSeq<'T> =
            let gen _ = async {
              let! v = v
              return Some (v,0) }
            unfoldAsync gen 0


          // let queuedOffsets = replicateInfiniteAsync (Mb.take queuedOffsetsMb)