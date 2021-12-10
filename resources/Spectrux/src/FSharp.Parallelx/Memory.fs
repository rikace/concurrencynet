namespace FSharp.Parallelx

module Memory =


    open System
    open System.Buffers

    #nowarn "9"

    [<RequireQualifiedAccess>]
    module Span =

        open FSharp.NativeInterop

        let pool = MemoryPool<byte>.Shared

        let procMemory f bufferSize =
            use owner = pool.Rent(minBufferSize = bufferSize)
            f owner.Memory

        /// Returns an empty span.
        let empty<'a> : Span<'a> = Span<'a>.Empty

        /// Returns a Span build from memory allocated on stack.
//        let inline stackalloc<'a when 'a: unmanaged> (length: int): Span<'a> =
//            let p = NativePtr.stackalloc<'a> length |> FSharp.NativeInterop.NativePtr.toVoidPtr
//            Span<'a>(p, length)

        /// Wraps provided array into a Span.
        let inline ofArray (a: 'a[]): Span<'a> = Span(a)

        /// Creates a Span out of (*void) pointer with a given byte length.
        //let inline ofPtr length (ptr: voidptr): Span<'a> = Span(ptr, length)

        /// Changes Span into ReadOnlySpan.
        let inline readOnly (span: Span<_>): ReadOnlySpan<_> = Span.op_Implicit span

        /// Slices current span, returning a narrowed window.
        let inline slice offset length (span: Span<_>) = span.Slice(offset, length)

        /// Returns an `n`-th element of a span, 0-based.
        let inline nth n (span: Span<_>) = span.[n];

        /// Copies contents of current span into an array, which is then returned.
        let inline toArray (span: Span<_>) = span.ToArray()

        /// Returns a length of a current `span`.
        let inline length (span: Span<_>) = span.Length

        /// Checks if current span is `empty`.
        let inline isEmpty (span: Span<_>) = span.IsEmpty

        let inline copyTo (dst: Span<_>) (span: Span<_>) = span.CopyTo(dst)

        let inline tryCopyTo (dst: Span<_>) (span: Span<_>) = span.TryCopyTo(dst)

    [<RequireQualifiedAccess>]
    module ReadOnlySpan =

        /// Returns an empty span.
        let empty<'a> : ReadOnlySpan<'a> = ReadOnlySpan<'a>.Empty

        /// Wraps provided array into a Span.
        let inline ofArray (a: 'a[]): ReadOnlySpan<'a> = ReadOnlySpan(a)

        /// Creates a Span out of (*void) pointer with a given byte length.
     //  let inline ofPtr length (ptr: voidptr): ReadOnlySpan<'a> = ReadOnlySpan(ptr, length)

        /// Converts current string into a read-only span of UTF-16 characters.
        let inline ofString (str: string): ReadOnlySpan<char> = str.AsSpan()

        /// Slices current span, returning a narrowed window.
        let inline slice offset length (span: ReadOnlySpan<_>) = span.Slice(offset, length)

        /// Returns an `n`-th element of a span, 0-based.
        let inline nth n (span: ReadOnlySpan<_>) = span.[n];

        /// Copies contents of current span into an array, which is then returned.
        let inline toArray (span: ReadOnlySpan<_>) = span.ToArray()

        /// Returns a length of a current `span`.
        let inline length (span: ReadOnlySpan<_>) = span.Length

        /// Checks if current span is `empty`.
        let inline isEmpty (span: ReadOnlySpan<_>) = span.IsEmpty

        let inline copyTo (dst: Span<_>) (span: ReadOnlySpan<_>) = span.CopyTo(dst)

        let inline tryCopyTo (dst: Span<_>) (span: ReadOnlySpan<_>) = span.TryCopyTo(dst)

        /// Checks, if contents of both readonly spans are the same.
        let inline eq (a: ReadOnlySpan<_>) (b: ReadOnlySpan<_>) = MemoryExtensions.SequenceEqual(a, b)

        /// Compares contents of first readonly span with a second one.
        let inline cmp (a: ReadOnlySpan<_>) (b: ReadOnlySpan<_>) = MemoryExtensions.SequenceCompareTo(a, b)

    [<RequireQualifiedAccess>]
    module Memory =

        /// Returns an empty span.
        let empty<'a> : Memory<'a> = Memory<'a>.Empty

        /// Rents a Memory segment from a shared memory pool. Memory pool will respect
        /// lower bound, therefore always returning memory having at least `minCapacity`.
        ///
        /// However upper bound depends on the pool implementation, so eg. `Memory.rent(12)`
        /// can possibly return a Memory segment of 4096 bytes.
        ///
        /// Returned object is a disposable resource.
        let inline rent(minCapacity: int): IMemoryOwner<_> = MemoryPool.Shared.Rent(minCapacity)

        /// Wraps provided array into a memory.
        let inline ofArray (a: 'a[]): Memory<'a> = Memory(a)

        /// Creates a memory out of (*void) pointer with a given byte length.
        let inline ofArrayBounded offset length (a: 'a[]): Memory<'a> = Memory(a, offset, length)

        /// Changes memory into ReadOnlymemory.
        let inline readOnly (memory: Memory<_>): ReadOnlyMemory<_> = Memory.op_Implicit memory

        /// Slices current memory, returning a narrowed window.
        let inline slice offset length (memory: Memory<_>) = memory.Slice(offset, length)

        /// Returns span of a current memory.
        let inline span (memory: Memory<_>) = memory.Span

        /// Pins current memory segment, returning a handler to it.
        let inline pin (memory: Memory<_>) = memory.Pin()

        /// Copies contents of current memory into an array, which is then returned.
        let inline toArray (memory: Memory<_>) = memory.ToArray()

        /// Returns a length of a current `memory`.
        let inline length (memory: Memory<_>) = memory.Length

        /// Checks if current memory is `empty`.
        let inline isEmpty (memory: Memory<_>) = memory.IsEmpty

        let inline copyTo (dst: Memory<_>) (span: Memory<_>) = span.CopyTo(dst)

        let inline tryCopyTo (dst: Memory<_>) (span: Memory<_>) = span.TryCopyTo(dst)

    [<RequireQualifiedAccess>]
    module ReadOnlyMemory =

        /// Returns an empty span.
        let empty<'a> : ReadOnlyMemory<'a> = ReadOnlyMemory<'a>.Empty

        /// Wraps provided array into a memory.
        let inline ofArray (a: 'a[]): ReadOnlyMemory<'a> = ReadOnlyMemory(a)

        /// Creates a memory out of (*void) pointer with a given byte length.
        let inline ofArrayBounded offset length (a: 'a[]): ReadOnlyMemory<'a> = ReadOnlyMemory(a, offset, length)

        /// Slices current memory, returning a narrowed window.
        let inline slice offset length (memory: ReadOnlyMemory<_>) = memory.Slice(offset, length)

        /// Returns span of a current `memory`.
        let inline span (memory: ReadOnlyMemory<_>) = memory.Span

        /// Pins current memory segment, returning a handler to it.
        let inline pin (memory: ReadOnlyMemory<_>) = memory.Pin()

        /// Copies contents of current memory into an array, which is then returned.
        let inline toArray (memory: ReadOnlyMemory<_>) = memory.ToArray()

        /// Returns a length of a current `memory`.
        let inline length (memory: ReadOnlyMemory<_>) = memory.Length

        /// Checks if current memory is `empty`.
        let inline isEmpty (memory: ReadOnlyMemory<_>) = memory.IsEmpty

        let inline copyTo (dst: Memory<_>) (memory: ReadOnlyMemory<_>) = memory.CopyTo(dst)

        let inline tryCopyTo (dst: Memory<_>) (memory: ReadOnlyMemory<_>) = memory.TryCopyTo(dst)

    [<RequireQualifiedAccess>]
    module ReadOnlySequence =

        /// Returns an empty readonly sequence.
        let empty<'a> : ReadOnlySequence<'a> = ReadOnlySequence<'a>.Empty

        /// Returns length of a readonly `sequence`.
        let inline length (sequence: ReadOnlySequence<'a>) = sequence.Length

        /// Returns true is current readonly `sequence` is empty. False otherwise.
        let inline isEmpty (sequence: ReadOnlySequence<'a>) = sequence.IsEmpty

        /// Returns true if current readonly `sequence` consists only of a single continuous block of memory.
        let inline isSingleSegment (sequence: ReadOnlySequence<'a>) = sequence.IsSingleSegment

        let inline start (sequence: ReadOnlySequence<'a>) = sequence.Start

        let inline finish (sequence: ReadOnlySequence<'a>) = sequence.End

        let inline slice (offset: int64) (length: int64) (sequence: ReadOnlySequence<'a>) = sequence.Slice(offset, length)

        let inline between (start: SequencePosition) (finish: SequencePosition) (sequence: ReadOnlySequence<'a>) =
            sequence.Slice(start, finish)

        /// Returns first memory segment of a current readonly `sequence`.
        let inline head (sequence: ReadOnlySequence<'a>) = sequence.First

        /// Returns first memory segment of a current readonly `sequence` or None if sequence is empty.

        /// Returns a sequence position of a provided `offset` within current `sequence`.
        let inline positionOf (offset: int64) (sequence: ReadOnlySequence<'a>) = sequence.GetPosition(offset)

        /// Returns a sequence position of a provided `offset` within current `sequence`.
        let inline positionFrom (offset: int64) (start: SequencePosition) (sequence: ReadOnlySequence<'a>) =
            sequence.GetPosition(offset, start)

        let inline tryAdvance (position) (memory) (sequence: ReadOnlySequence<'a>) =
            sequence.TryGet(position, memory, true)

        let inline tryGet (position) (memory) (sequence: ReadOnlySequence<'a>) =
            sequence.TryGet(position, memory, false)

