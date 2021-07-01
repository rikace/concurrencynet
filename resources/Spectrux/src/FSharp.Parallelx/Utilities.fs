namespace FSharp.Parallelx

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Text.RegularExpressions

[<AutoOpen>]
module Utilities =
    module Console =
        open System
        let log =
            let lockObj = obj()
            fun color s ->
                lock lockObj (fun _ ->
                    Console.ForegroundColor <- color
                    printfn "%s" s
                    Console.ResetColor())

        let complete = log ConsoleColor.Magenta
        let ok = log ConsoleColor.Green
        let info = log ConsoleColor.Cyan
        let warn = log ConsoleColor.Yellow
        let error = log ConsoleColor.Red

    let diffStrings (s1 : string) (s2 : string) =
       let s1', s2' = s1.PadRight(s2.Length), s2.PadRight(s1.Length)

       let d1, d2 =
          (s1', s2')
          ||> Seq.zip
          |> Seq.map (fun (c1, c2) -> if c1 = c2 then '-','-' else c1, c2)
          |> Seq.fold (fun (d1, d2) (c1, c2) -> (sprintf "%s%c" d1 c1), (sprintf "%s%c" d2 c2) ) ("","")
       d1, d2

    let notNullOrEmpty = not << System.String.IsNullOrEmpty

    let inline force (lz: 'a Lazy)  = lz.Force()

    let internal removeInvalidChars (str : string) = Regex.Replace(str, "[:@\,]", "_")

    let (|Null|NotNull|) (x : 'T when 'T : not struct) =
        if obj.ReferenceEquals(x, null) then Null else NotNull x

    let inline tryGet (key:^k) this =
        let mutable v = Unchecked.defaultof<'v>
        let scc = ( ^a : (member TryGetValue : 'k * ('v byref) -> bool) this, key, &v)
        if scc then Some v else None

    let charDelimiters = [0..256] |> Seq.map(char)|> Seq.filter(fun c -> Char.IsWhiteSpace(c) || Char.IsPunctuation(c)) |> Seq.toArray

    /// Transforms a function by flipping the order of its arguments.
    let inline flip f a b = f b a

    let inline diag a = a,a

    /// Given a value, apply a function to it, ignore the result, then return the original value.
    let inline tap fn x = fn x |> ignore; x

    /// Sequencing operator like Haskell's ($). Has better precedence than (<|) due to the
    /// first character used in the symbol.
    let (^) = (<|)

    /// Given a value, apply a function to it, ignore the result, then return the original value.
    let inline tee fn x = fn x |> ignore; x

    /// Custom operator for `tee`: Given a value, apply a function to it, ignore the result, then return the original value.
    let inline (|>!) x fn = tee fn x

    /// Safely invokes `.Dispose()` on instances of `IDisposable`
    let inline dispose (d :#IDisposable) = match box d with null -> () | _ -> d.Dispose()

    let is<'T> (x: obj) = x :? 'T

    let delimiters =
            [0..256]
            |> List.map(char)
            |> List.filter(fun c -> Char.IsWhiteSpace(c) || Char.IsPunctuation(c))
            |> List.toArray

    let con<'a> (o:obj) =
        match Convert.ChangeType(o, typeof<'a>) with
        | :? 'a as k -> Some k
        | _ -> None

    let conty (ty:Type) (o:obj) : 'a option=
        match Convert.ChangeType(o, ty) with
        | :? 'a as k when typeof<'a> = ty -> Some k
        | _ -> None


    [<Sealed>]
    type private ReferenceEqualityComparer<'T when 'T : not struct and 'T : equality>() =
        interface IEqualityComparer<'T> with
            member __.Equals(x, y) = obj.ReferenceEquals(x, y)
            member __.GetHashCode(x) = x.GetHashCode()

    [<Sealed>]
    type private EquatableEqualityComparer<'T when 'T :> IEquatable<'T> and 'T : struct and 'T : equality>() =
        interface IEqualityComparer<'T> with
            member __.Equals(x, y) = x.Equals(y)
            member __.GetHashCode(x) = x.GetHashCode()

    [<Sealed>]
    type private AnyEqualityComparer<'T when 'T : equality>() =
        interface IEqualityComparer<'T> with
            member __.Equals(x, y) = x.Equals(y)
            member __.GetHashCode(x) = x.GetHashCode()

    let inline (|??) (a: 'a Nullable) (b: 'a) = if a.HasValue then a.Value else b

    let private consoleColor (color : ConsoleColor) =
        let current = Console.ForegroundColor
        Console.ForegroundColor <- color
        { new IDisposable with
          member x.Dispose() = Console.ForegroundColor <- current }

    let cprintf color str =
        Printf.kprintf (fun s -> use c = consoleColor color in printf "%s" s) str

    module Log =

        let report =
            let lockObj = obj()
            fun (color : ConsoleColor) (message : string) ->
                lock lockObj (fun _ ->
                    Console.ForegroundColor <- color
                    printfn "%s (thread ID: %i)"
                        message Thread.CurrentThread.ManagedThreadId
                    Console.ResetColor())

        let red = report ConsoleColor.Red
        let green = report ConsoleColor.Green
        let yellow = report ConsoleColor.Yellow
        let cyan = report ConsoleColor.Cyan


    type String with
        member x.IsEmpty
            with get() = String.IsNullOrEmpty(x) || String.IsNullOrWhiteSpace(x)

    module GC =
        let clean () =
            for i=1 to 2 do
                GC.Collect ()
                GC.WaitForPendingFinalizers ()
            Thread.Sleep 10


    let rec foldk f (acc:'State) xs =
        match xs with
        | []    -> acc
        | x::xs -> f acc x (fun lacc -> foldk f lacc xs)

    let synchronize f =
        let ctx = System.Threading.SynchronizationContext.Current
        f (fun g arg ->
            let nctx = System.Threading.SynchronizationContext.Current
            if ctx <> null && ctx <> nctx then ctx.Post((fun _ -> g(arg)), null)
            else g(arg) )

    module StreamHelpers =
        open System.IO
        open System.IO.Compression
        open FSharp.Parallelx

        type System.IO.Stream with
            member x.WriteBytesAsync (bytes : byte []) =
                task {
                    do! x.WriteAsync(BitConverter.GetBytes bytes.Length, 0, sizeof<int>)
                    do! x.WriteAsync(bytes, 0, bytes.Length)
                    do! x.FlushAsync()
                }

            member x.ReadBytesAsync(length : int) =
                let rec readSegment buf offset remaining =
                    task {
                        let! read = x.ReadAsync(buf, offset, remaining)
                        if read < remaining then
                            return! readSegment buf (offset + read) (remaining - read)
                        else
                            return ()
                    }

                task {
                    let bytes = Array.zeroCreate<byte> length
                    do! readSegment bytes 0 length
                    return bytes
                }

            member x.ReadBytesAsync() =
                task {
                    let! lengthArr = x.ReadBytesAsync sizeof<int>
                    let length = BitConverter.ToInt32(lengthArr, 0)
                    return! x.ReadBytesAsync length
                }

        let compress (f:MemoryStream -> Stream) (data:byte[]) =
            use targetStream = new MemoryStream()
            use sourceStream = new MemoryStream(data)
            use zipStream = f targetStream
            sourceStream.CopyTo(zipStream)
            targetStream.ToArray()

        let decompress (f:MemoryStream -> Stream) (data:byte[]) =
            use targetStream = new MemoryStream()
            use sourceStream = new MemoryStream(data)
            use zipStream = f sourceStream
            zipStream.CopyTo(targetStream)
            targetStream.ToArray()

        let compressGZip data =
            compress (fun memStream -> new GZipStream(memStream, CompressionMode.Compress) :> Stream) data

        let compressDeflate data =
            compress (fun memStream -> new DeflateStream(memStream, CompressionMode.Compress) :> Stream) data

        let decompressGZip data =
            decompress (fun memStream -> new GZipStream(memStream, CompressionMode.Decompress) :> Stream) data

        let decompressDeflate data =
            decompress (fun memStream -> new DeflateStream(memStream, CompressionMode.Decompress) :> Stream) data

module Benchmark =
    /// Do countN repetitions of the function f and print the
    /// time elapsed, number of GCs and change in total memory
    let time countN label f  =

        let stopwatch = System.Diagnostics.Stopwatch()

        // do a full GC at the start but NOT thereafter
        // allow garbage to collect for each iteration
        System.GC.Collect()
        printfn "Started"

        let getGcStats() =
            let gen0 = System.GC.CollectionCount(0)
            let gen1 = System.GC.CollectionCount(1)
            let gen2 = System.GC.CollectionCount(2)
            let mem = System.GC.GetTotalMemory(false)
            gen0,gen1,gen2,mem


        printfn "======================="
        printfn "%s" label
        printfn "======================="
        for iteration in [1..countN] do
            let gen0,gen1,gen2,mem = getGcStats()
            stopwatch.Restart()
            f()
            stopwatch.Stop()
            let gen0',gen1',gen2',mem' = getGcStats()
            // convert memory used to K
            let changeInMem = (mem'-mem) / 1000L
            printfn "#%2i elapsed:%6ims gen0:%3i gen1:%3i gen2:%3i mem:%6iK" iteration stopwatch.ElapsedMilliseconds (gen0'-gen0) (gen1'-gen1) (gen2'-gen2) changeInMem

module Dynamic =
    open System.Reflection

    let (?) (thingey : obj) (propName: string) : 'a =
        let propInfo = thingey.GetType().GetProperty(propName)
        propInfo.GetValue(thingey, null) :?> 'a

    let (?<-) (thingey : obj) (propName : string) (newValue : 'a) =
        let propInfo = thingey.GetType().GetProperty(propName)
        propInfo.SetValue(thingey, newValue, null)


module CPUOperationSimulation =

    open System
    open System.Drawing
    open System.Diagnostics.CodeAnalysis
    open System.Collections.Generic
    open System.Diagnostics
    open System.Globalization
    open System.IO
    open System.Linq
    open System.Threading

    /// <summary>
    /// Simulates a CPU-intensive operation on a single core. The operation will use approximately 100% of a
    /// single CPU for a specified duration.
    /// </summary>
    /// <param name="seconds">The approximate duration of the operation in seconds</param>
    /// <param name="token">A token that may signal a request to cancel the operation.</param>
    /// <param name="throwOnCancel">true if an execption should be thrown in response to a cancellation request.</param>
    /// <returns>true if operation completed normally false if the user canceled the operation</returns>
    let DoCpuIntensiveOperation seconds (token:CancellationToken) throwOnCancel =
        if (token.IsCancellationRequested) then
            if (throwOnCancel) then token.ThrowIfCancellationRequested()
            false
        else
            let ms = int64 (seconds * 1000.0)
            let sw = new Stopwatch()
            sw.Start()
            let checkInterval = Math.Min(20000000, int (20000000.0 * seconds))

            // Loop to simulate a computationally intensive operation
            let rec loop i =
                // Periodically check to see if the user has requested
                // cancellation or if the time limit has passed
                let check = seconds = 0.0 || i % checkInterval = 0
                if check && token.IsCancellationRequested then
                    if throwOnCancel then token.ThrowIfCancellationRequested()
                    false
                elif check && sw.ElapsedMilliseconds > ms then
                    true
                else
                  loop (i + 1)

            // Start the loop with 0 as the first value
            loop 0

    /// <summary>
    /// Simulates a CPU-intensive operation on a single core. The operation will use approximately 100% of a
    /// single CPU for a specified duration.
    /// </summary>
    /// <param name="seconds">The approximate duration of the operation in seconds</param>
    /// <returns>true if operation completed normally false if the user canceled the operation</returns>
    let DoCpuIntensiveOperationSimple seconds =
        DoCpuIntensiveOperation seconds CancellationToken.None false


    // vary to simulate I/O jitter
    let SleepTimeouts =
      [| 65; 165; 110; 110; 185; 160; 40; 125; 275; 110; 80; 190; 70; 165;
         80; 50; 45; 155; 100; 215; 85; 115; 180; 195; 135; 265; 120; 60;
         130; 115; 200; 105; 310; 100; 100; 135; 140; 235; 205; 10; 95; 175;
         170; 90; 145; 230; 365; 340; 160; 190; 95; 125; 240; 145; 75; 105;
         155; 125; 70; 325; 300; 175; 155; 185; 255; 210; 130; 120; 55; 225;
         120; 65; 400; 290; 205; 90; 250; 245; 145; 85; 140; 195; 215; 220;
         130; 60; 140; 150; 90; 35; 230; 180; 200; 165; 170; 75; 280; 150;
         260; 105 |]


    /// <summary>
    /// Simulates an I/O-intensive operation on a single core. The operation will use only a small percent of a
    /// single CPU's cycles however, it will block for the specified number of seconds.
    /// </summary>
    /// <param name="seconds">The approximate duration of the operation in seconds</param>
    /// <param name="token">A token that may signal a request to cancel the operation.</param>
    /// <param name="throwOnCancel">true if an execption should be thrown in response to a cancellation request.</param>
    /// <returns>true if operation completed normally false if the user canceled the operation</returns>
    let DoIoIntensiveOperation seconds (token:CancellationToken) throwOnCancel =
        if token.IsCancellationRequested then false else
        let ms = int (seconds * 1000.0)
        let sw = new Stopwatch()
        let timeoutCount = SleepTimeouts.Length
        sw.Start()

        // Loop to simulate I/O intensive operation
        let mutable i = Math.Abs(sw.GetHashCode()) % timeoutCount
        let mutable result = None
        while result = None do
            let timeout = SleepTimeouts.[i]
            i <- (i + 1) % timeoutCount

            // Simulate I/O latency
            Thread.Sleep(timeout)

            // Has the user requested cancellation?
            if token.IsCancellationRequested then
                if throwOnCancel then token.ThrowIfCancellationRequested()
                result <- Some false

            // Is the computation finished?
            if sw.ElapsedMilliseconds > int64 ms then
                result <- Some true

        result.Value


    /// <summary>
    /// Simulates an I/O-intensive operation on a single core. The operation will use only a small percent of a
    /// single CPU's cycles however, it will block for the specified number of seconds.
    /// </summary>
    /// <param name="seconds">The approximate duration of the operation in seconds</param>
    /// <returns>true if operation completed normally false if the user canceled the operation</returns>
    let DoIoIntensiveOperationSimple seconds =
        DoIoIntensiveOperation seconds CancellationToken.None false


    /// Simulates an I/O-intensive operation on a single core. The operation will
    /// use only a small percent of a single CPU's cycles however, it will block
    /// for the specified number of seconds.
    ///
    /// This is same as 'DoIoIntensiveOperation', but uses F# asyncs to simulate
    /// non-blocking (asynchronous) I/O typical in F# async applications.
    let AsyncDoIoIntensiveOperation seconds (token:CancellationToken) throwOnCancel =
      async { if token.IsCancellationRequested then return false else
              let ms = int (seconds * 1000.0)
              let sw = new Stopwatch()
              let timeoutCount = SleepTimeouts.Length
              sw.Start()

              // Loop to simulate I/O intensive operation
              let i = ref (Math.Abs(sw.GetHashCode()) % timeoutCount)
              let result = ref None
              while !result = None do
                  let timeout = SleepTimeouts.[!i]
                  i := (!i + 1) % timeoutCount

                  // Simulate I/O latency
                  do! Async.Sleep(timeout)

                  // Has the user requested cancellation?
                  if token.IsCancellationRequested then
                      if throwOnCancel then token.ThrowIfCancellationRequested()
                      result := Some false

                  // Is the computation finished?
                  if sw.ElapsedMilliseconds > int64 ms then
                      result := Some true

              return result.Value.Value }


    /// Simulates an I/O-intensive operation on a single core. The operation will
    /// use only a small percent of a single CPU's cycles however, it will block
    /// for the specified number of seconds.
    ///
    /// This is same as 'DoIoIntensiveOperationSimple', but uses F# asyncs to simulate
    /// non-blocking (asynchronous) I/O typical in F# async applications.
    let AsyncDoIoIntensiveOperationSimple seconds =
        AsyncDoIoIntensiveOperation seconds CancellationToken.None false


[<RequireQualifiedAccess>]
module Seq =
    let tryExactlyOne (s:#seq<_>) =
        let mutable i = 0
        let mutable first = Unchecked.defaultof<_>
        use e = s.GetEnumerator()
        while (i < 2 && e.MoveNext()) do
            i <- i + 1
            first <- e.Current
        if i = 1 then Some first
        else None

    /// Unzip a seq by mapping the elements that satisfy the predicate
    /// into the first seq and mapping the elements that fail to satisfy the predicate
    /// into the second seq
    let partitionAndChoose predicate choosefn1 choosefn2 sqs =
        (([],[]),sqs)
        ||> Seq.fold (fun (xs,ys) elem ->
            if predicate elem then
                match choosefn1 elem with
                | Some x ->  (x::xs,ys)
                | None -> xs,ys
            else
                match choosefn2 elem with
                | Some y -> xs,y::ys
                | None -> xs,ys
        ) |> fun (xs,ys) ->
            List.rev xs :> seq<_>, List.rev ys :> seq<_>

    let tryTake n (s:#seq<_>) =
        let mutable i = 0
        seq {
            use e = s.GetEnumerator()
            while (i < n && e.MoveNext()) do
                i <- i + 1
                yield e.Current
        }

[<RequireQualifiedAccess>]
module List =
    // Try to find an element in a list.
    // If found, return the element and the list WITHOUT the element.
    // If not found, return None and the whole original list.
    let tryExtractOne fn values =
        match List.tryFindIndex fn values with
        | Some i ->
            let v = values.[i]
            Some v, (values.[0 .. i - 1 ] @ values.[i + 1 .. ])
        | None -> None, values


[<RequireQualifiedAccess>]
module String =
    open System.IO

    /// Match if 'text' starts with the 'prefix' string case
    let (|StartsWith|_|) (prefix: string) (input: string) =
        if input.StartsWith prefix then Some () else None

    /// Match if 'text' starts with the 'prefix' and return the text with the prefix removed
    let (|RemovePrefix|_|) (prefix: string) (input: string) =
        if input.StartsWith prefix then Some (input.Substring prefix.Length)
        else None

    let getLines (str: string) =
        use reader = new StringReader(str)
        [|  let mutable line = reader.ReadLine()
            while (isNull >> not) line do
                yield line
                line <- reader.ReadLine()
            if str.EndsWith "\n" then   // last trailing space not returned
                yield String.Empty      // http://stackoverflow.com/questions/19365404/stringreader-omits-trailing-linebreak
        |]

    /// Check if the two strings are equal ignoring case
    let inline equalsIgnoreCase str1 str2 =
        String.Compare(str1,str2,StringComparison.OrdinalIgnoreCase) = 0


    /// Check if 'text' includes the 'target' string case insensitive
    let inline containsIgnoreCase (target:string) (text:string) =
        text.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0

    /// Match if 'text' includes the 'target' string case insensitive
    let (|ContainsIC|_|) (target:string) (str2:String) =
        if containsIgnoreCase target str2 then Some () else None

    /// Check if 'text' starts with the 'prefix' string case insensitive
    let inline startsWithIgnoreCase (prefix:string) (text:string) =
        text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) = 0

    /// Match if 'text' starts with the 'prefix' string case insensitive
    let (|StartsWithIC|_|) (prefix:string) (text:String) =
        if startsWithIgnoreCase prefix text then Some () else None

    /// Check if 'text' ends with the 'suffix' string case insensitive
    let inline endsWithIgnoreCase (suffix:string) (text:string) =
        suffix.Length <= text.Length &&
        text.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= text.Length - suffix.Length

    /// Match if 'text' ends with the 'suffix' string case insensitive
    let (|EndsWithIC|_|) (suffix:string) (text:String) =
        if endsWithIgnoreCase suffix text then  Some () else None

    let quoted (text:string) = (if text.Contains(" ") then "\"" + text + "\"" else text)

    let inline trim (text:string) = text.Trim()
    let inline trimChars (chs: char[]) (text:string) = text.Trim chs
    let inline trimStart (pre: char[]) (text:string) = text.TrimStart pre
    let inline split sep (text:string) = text.Split sep


// adapted from MiniRx
// http://minirx.codeplex.com/
[<AutoOpen>]
module ObservableExtensions =

    let private synchronize f =
        let ctx = System.Threading.SynchronizationContext.Current
        f (fun g arg ->
            let nctx = System.Threading.SynchronizationContext.Current
            if ctx <> null && ctx <> nctx then
                ctx.Post((fun _ -> g arg), null)
            else
                g arg)

    type Microsoft.FSharp.Control.Async with
      static member AwaitObservable(ev1:IObservable<'a>) =
        synchronize (fun f ->
          Async.FromContinuations((fun (cont,_econt,_ccont) ->
            let rec callback = (fun value ->
              remover.Dispose()
              f cont value )
            and remover : IDisposable  = ev1.Subscribe callback
            () )))

    [<RequireQualifiedAccess>]
    module Observable =
        open System.Collections.Generic

        /// Creates an observable that calls the specified function after someone
        /// subscribes to it (useful for waiting using 'let!' when we need to start
        /// operation after 'let!' attaches handler)
        let guard f (e:IObservable<'Args>) =
          { new IObservable<'Args> with
              member __.Subscribe observer =
                let rm = e.Subscribe observer in f(); rm }

        let sample milliseconds source =
            let relay (observer:IObserver<'T>) =
                let rec loop () = async {
                    let! value = Async.AwaitObservable source
                    observer.OnNext value
                    do! Async.Sleep milliseconds
                    return! loop()
                }
                loop ()

            { new IObservable<'T> with
                member __.Subscribe(observer:IObserver<'T>) =
                    let cts = new System.Threading.CancellationTokenSource()
                    Async.Start (relay observer, cts.Token)
                    { new IDisposable with
                        member __.Dispose() = cts.Cancel()
                    }
            }

        let ofSeq s =
            let evt = new Event<_>()
            evt.Publish |> guard (fun _ ->
                for n in s do evt.Trigger(n))

        let private oneAndDone (obs : IObserver<_>) value =
            obs.OnNext value
            obs.OnCompleted()

        let ofAsync a : IObservable<'a> =
            { new IObservable<'a> with
                member __.Subscribe obs =
                    let oneAndDone' = oneAndDone obs
                    let token = new CancellationTokenSource()
                    Async.StartWithContinuations (a,oneAndDone',obs.OnError,obs.OnError,token.Token)
                    { new IDisposable with
                        member __.Dispose() =
                            token.Cancel |> ignore
                            token.Dispose() } }

        let ofAsyncWithToken (token : CancellationToken) a : IObservable<'a> =
            { new IObservable<'a> with
                  member __.Subscribe obs =
                      let oneAndDone' = oneAndDone obs
                      Async.StartWithContinuations (a,oneAndDone',obs.OnError,obs.OnError,token)
                      { new IDisposable with
                            member __.Dispose() = () } }

        let flatten (input: IObservable<#seq<'a>>): IObservable<'a> =
            { new IObservable<'a> with
                member __.Subscribe obs =
                    let cts = new CancellationTokenSource()
                    let sub =
                        input.Subscribe
                          ({ new IObserver<#seq<'a>> with
                              member __.OnNext values = values |> Seq.iter obs.OnNext
                              member __.OnCompleted() =
                                cts.Cancel()
                                obs.OnCompleted()
                              member __.OnError e =
                                cts.Cancel()
                                obs.OnError e })

                    { new IDisposable with
                        member __.Dispose() =
                            sub.Dispose()
                            cts.Cancel() }}

        let distinct (a: IObservable<'a>): IObservable<'a> =
            let seen = HashSet()
            Observable.filter seen.Add a


[<RequireQualifiedAccess>]
module Array =

    open System
    open System
    open System.Numerics
    open System.Runtime.Intrinsics

    /// Inserts a `value` at the given `index` of an `array`,
    /// returning new array in the result with all contents copied and expanded by inserted item.
    let insert (index: int) (value: 'a) (array: 'a[]): 'a[] =
        let count = array.Length
        if index < 0 || index > count then raise (IndexOutOfRangeException (sprintf "Cannot insert value at index %i of array of size %i" index count))
        else
            let copy = Array.zeroCreate (count+1)
            Array.blit array 0 copy 0 index
            copy.[index] <- value
            Array.blit array index copy (index+1) (count-index)
            copy

    let removeAt (index: int) (array: 'a[]): 'a[] =
        let count = array.Length
        if index < 0 || index > count then raise (IndexOutOfRangeException (sprintf "Cannot insert value at index %i of array of size %i" index count))
        else
            let copy = Array.zeroCreate (count-1)
            Array.blit array 0 copy 0 (index-1)
            Array.blit array index copy (index+1) (count-index)
            copy

    [<RequireQualifiedAccess>]
    module Simd =

        /// Checks if two arrays are equal to each other using vectorized operations.
        let equal (a: 'a[]) (b: 'a[]): bool =
            if a.Length <> b.Length then false
            else
                let mutable result = true
                let mutable i = 0
                if Vector.IsHardwareAccelerated then
                    let vectorizedLen = a.Length - Vector<'a>.Count
                    while result && i < vectorizedLen do
                        let va = Vector<'a>(a, i)
                        let vb = Vector<'a>(b, i)
                        result <- Vector.EqualsAll(va, vb)
                        i <- i + Vector<'a>.Count
                if result then
                    while i < a.Length do
                        result <- a.[i] = b.[i]
                        i <- i + 1
                result

        /// Checks if given `item` can be found inside a window of an array
        /// (starting at given `offset` for up to `count` items), using vectorized operations.
        let containsWithin offset count (item: 'a) (a: 'a[]): bool =
            if offset + count > a.Length then raise (ArgumentException <| sprintf "Provided range window (offset: %i, count: %i) doesn't fit inside array of length: %i" offset count a.Length)
            let finish = offset + count
            if count < Vector<'a>.Count then
                let mutable i = offset
                let mutable found = false
                while not found && i < finish do
                    found <- a.[i] = item
                found
            else
                let mutable found = true
                let mutable i = offset
                let vi = Vector<'a>(item)
                let vectorizedLen = finish - Vector<'a>.Count
                while not found && i < vectorizedLen do
                    let va = Vector<'a>(a, i)
                    found <- Vector.EqualsAny(vi, va)
                    i <- i + Vector<'a>.Count
                if not found then
                    while i < finish do
                        found <- a.[i] = item
                        i <- i + 1
                found

        /// Checks if given `item` can be found inside a window of an array, using vectorized operations.
        let inline contains item a = containsWithin 0 (Array.length a) item a
