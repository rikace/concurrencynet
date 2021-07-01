module FSharp.Parallelx.Courotine


type [<Struct>] Coroutine<'T> = Co of (('T -> unit) -> unit)

module Coroutine =
  open FSharp.Core.Printf

  open System
  open System.Diagnostics
  open System.Threading
  open System.Threading.Tasks

  module Details =
    let inline refEq<'T when 'T : not struct> (a : 'T) (b : 'T) = Object.ReferenceEquals (a, b)

    module Loops =
      let rec cas (rs : byref<'T>) (cs : 'T) u : 'U =
        let v, ns   = u cs
        let acs     = Interlocked.CompareExchange(&rs, ns, cs)
        if refEq acs cs then v
        else cas &rs acs u

    let sw =
      let sw = Stopwatch ()
      sw.Start ()
      sw

    let cas (rs : byref<_>) u =
      let cs = rs
      Interlocked.MemoryBarrier ()
      Loops.cas &rs cs u

    type ChildState<'T> =
      | Initial
      | HasReceiver of ('T -> unit)
      | HasValue    of 'T
      | Done

  open Details

  let result v =
    Co <| fun r ->
      r v

  let bind (Co c) f =
    Co <|
      fun r ->
        let cr v =
          let (Co d) = f v
          d r
        c cr

  let combine (Co c) (Co d) =
    Co <|
      fun r ->
        let cr _ =
          d r
        c cr

  let apply (Co c) (Co d) =
    Co <|
      fun r ->
        let cr f =
          let dr v = r (f v)
          d dr
        c cr

  let map m (Co c) =
    Co <| fun r ->
      let cr v = r (m v)
      c cr

  let unfold uf z =
    Co <| fun r ->
      let ra = ResizeArray 16
      let rec cr p =
        match p with
        | None        -> r (ra.ToArray ())
        | Some (s, v) ->
          ra.Add v
          let (Co c) = uf s
          c cr
      let (Co c) = uf z
      c cr

  let debug nm (Co c) =
    Co <| fun r ->
      printfn "DEBUG - %s - INVOKE" nm
      let cr v =
        printfn "DEBUG - %s - RESULT: %A" nm v
        r v
      c cr

  let debugf fmt = kprintf debug fmt

  let time (Co c) =
    Co <| fun r ->
      let before = sw.ElapsedMilliseconds
      let cr v =
        let after = sw.ElapsedMilliseconds
        r (after - before, v)
      c cr

  let switchToNewThread =
    Co <| fun r ->
      let ts () =  r ()
      let t = Thread (ThreadStart ts)
      t.IsBackground  <- true
      t.Name          <- "Coroutine thread"
      t.Start ()

  let switchToThreadPool =
    Co <| fun r ->
      let wc _ = r ()
      ThreadPool.QueueUserWorkItem (WaitCallback wc) |> ignore

  let join (Co c) =
    Co <| fun r ->
      let cr (Co d) = d r
      c cr

  let runInParallel (Co c) =
    Co <| fun r ->
      let mutable state = Initial

      let cr v =
        let update s =
          match s with
          | Initial       -> None    , HasValue v
          | HasReceiver r -> Some r  , Done
          | HasValue    _ -> None    , HasValue v
          | Done          -> None    , Done
        match cas &state update with
        | Some r -> r v
        | None   -> ()

      c <| cr

      let cco cr =
        let update s =
          match s with
            | Initial       -> None   , HasReceiver cr
            | HasReceiver _ -> None   , HasReceiver cr
            | HasValue    v -> Some v , Done
            | Done          -> None   , Done
        match cas &state update with
        | Some v -> cr v
        | None   -> ()

      r <| Co cco

  let runAllInParallel parallelism (cos : _ array) =
    let chunks = cos |> Array.indexed |> Array.chunkBySize parallelism
    Co <| fun r ->
      let result = Array.zeroCreate cos.Length
      let rec loop i =
        if i < chunks.Length then
          let chunk = chunks.[i]
          let children = chunk |> Array.map (fun (j, co) -> runInParallel co |> join |> map (fun v -> j, v))
          let mutable remaining = children.Length
          let cr (j, v) =
            result.[j] <- v
            if Interlocked.Decrement &remaining = 0 then loop (i + 1)
          for (Co c) in children do
            c cr
        else
          r result
      loop 0

  let ofTask (t : Task<_>) =
    Co <| fun r ->
      let cw (t : Task<_>) = r t.Result
      t.ContinueWith (Action<Task<'T>> cw) |> ignore

  let ofUnitTask (t : Task) =
    Co <| fun r ->
      let cw (_ : Task) = r ()
      t.ContinueWith (Action<Task> cw) |> ignore

  let invoke (Co c) r =
    let wc _ = c r
    ThreadPool.QueueUserWorkItem (WaitCallback wc) |> ignore

  type Builder () =
    class
      member x.Bind       (c, f)  : Coroutine<_> = bind     c               f
      member x.Bind       (t, f)  : Coroutine<_> = bind     (ofTask t)      f
      member x.Bind       (t, f)  : Coroutine<_> = bind     (ofUnitTask t)  f
      member x.Combine    (c, d)  : Coroutine<_> = combine  c               d
      member x.Combine    (t, d)  : Coroutine<_> = combine  (ofUnitTask t)  d
      member x.Return     v       : Coroutine<_> = result   v
      member x.ReturnFrom c       : Coroutine<_> = c
      member x.ReturnFrom t       : Coroutine<_> = ofTask   t
      member x.Zero       ()      : Coroutine<_> = result   ()
    end
let coroutine = Coroutine.Builder ()

type Coroutine<'T> with
  static member (>>=) (c, f) = Coroutine.bind     f c
  static member (>>.) (c, d) = Coroutine.combine  c d
  static member (<*>) (c, d) = Coroutine.apply    c d
  static member (|>>) (c, m) = Coroutine.map      m c


module courotine_tests =
    
  open System
  open System.IO
  open System.Net
  open System.Threading.Tasks

  open Coroutine

  let downloadFromUri uri =
    coroutine {
      let wc    = new WebClient ()
      let! txt  = wc.DownloadStringTaskAsync (Uri uri)
      wc.Dispose ()
      return txt
    }

  let exampleDownloadFromGoogle =
    coroutine {
      let! txt = downloadFromUri "https://www.google.com/"
      return txt.Length
    }

  let exampleDownloadFromGoogleAndBing =
    coroutine {
      let! google = downloadFromUri "https://www.google.com/"
      let! bing   = downloadFromUri "https://www.bing.com/"
      return google.Length, bing.Length
    }

  let exampleDownloadFromGoogleAndBingInParallel =
    coroutine {
      // Start the download coroutines in parallel
      let! cgoogle = runInParallel <| downloadFromUri "https://www.google.com/"
      let! cbing   = runInParallel <| downloadFromUri "https://www.bing.com/"

      // Wait for the coroutines to complete
      let! google  = cgoogle
      let! bing    = cbing

      return google.Length, bing.Length
    }

  let exampleDownloadManyPagesInParallel =
    let download uri =
      coroutine {
        let! text = downloadFromUri uri
        return text.Length
      } |> debugf "Download: %s" (string uri)
    coroutine {
      let uris        = Array.init 10 (fun i -> sprintf "https://gist.github.com/mrange?page=%d" (i + 1))
      let! allLengths = uris |> Array.map download |> runAllInParallel 3
      return allLengths
    } |> time

  let exampleCPUBoundProblem =
    let d = 2048
    let r = d >>> 3
    let m = 250
    let s = 2.0 / float d

    let rec mandelbrot re im cre cim i =
      if i > 0 then
        let re2   = re*re
        let im2   = im*im
        let reim  = re*im
        if re2 + im2 > 4.0 then
          i
        else
          mandelbrot (re2 - im2 + cre) (reim + reim + cim) cre cim (i - 1)
      else
        i

    let lines (pixels : byte array) f t =
      for y = f to (t - 1) do
        let yo  = y*r
        let cim = float y*s - 1.0
        for xo = 0 to (r - 1)  do
          let x0            = xo <<< 3
          let mutable pixel = 0uy
          for p = 0 to 7 do
            let x = x0 + p
            let cre = float x*s - 1.5
            let i   = mandelbrot cre cim cre cim m
            if i = 0 then
              pixel   <-pixel ||| (1uy <<< (7 - p))
          pixels.[yo + xo] <- pixel

    let colines (pixels : byte array) f t =
      coroutine {
        do! switchToThreadPool
        lines pixels f t
        return ()
      }
    coroutine {
      let pixels = Array.zeroCreate (r*d)

      let  s  = d / 4
      let! s0 = runInParallel <| colines pixels (0*s) (1*s)
      let! s1 = runInParallel <| colines pixels (1*s) (2*s)
      let! s2 = runInParallel <| colines pixels (2*s) (3*s)
      let! s3 = runInParallel <| colines pixels (3*s) (4*s)

      do! s0
      do! s1
      do! s2
      do! s3

      let img     = File.Create "mandelbrot.pbm"
      let sw      = new StreamWriter (img)

      do! sw.WriteAsync  (sprintf "P4\n%d %d\n" d d)
      do! sw.FlushAsync  ()
      do! img.WriteAsync (pixels, 0, pixels.Length)

      sw.Dispose ()
      img.Dispose ()

      return ()
    } |> time

//  [<EntryPoint>]
//  let main argv =
//    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory
//
//    invoke exampleDownloadFromGoogleAndBingInParallel <| printfn "Result: %A"
//
//    printfn "Press any key to exit"
//    Console.ReadKey () |> ignore
//
//    0