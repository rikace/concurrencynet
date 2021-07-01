namespace FSharp.Parallelx.ChannelEx

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
module TaskChannelEx =

    /// Returns decomposed writer/reader pair components of n-capacity bounded MPSC
    /// (multi-producer/single-consumer) channel.
    let inline boundedMpsc<'a> (n: int) : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateBounded(BoundedChannelOptions(n, SingleWriter=false, SingleReader=true))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of unbounded MPSC
    /// (multi-producer/single-consumer) channel.
    let inline unboundedMpsc<'a> () : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateUnbounded(UnboundedChannelOptions(SingleWriter=false, SingleReader=true))
        (ch.Writer, ch.Reader)


    /// Returns decomposed writer/reader pair components of n-capacity bounded MPMC
    /// (multi-producer/multi-consumer) channel.
    let inline boundedMpmc<'a> (n: int) : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateBounded(BoundedChannelOptions(n, SingleWriter=false, SingleReader=false))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of unbounded MPMC
    /// (multi-producer/multi-consumer) channel.
    let inline unboundedMpmc<'a> () : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateUnbounded(UnboundedChannelOptions(SingleWriter=false, SingleReader=false))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of n-capacity bounded SPSC
    /// (single-producer/single-consumer) channel.
    let inline boundedSpsc<'a> (n: int) : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateBounded(BoundedChannelOptions(n, SingleWriter=true, SingleReader=true))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of unbounded SPSC
    /// (single-producer/single-consumer) channel.
    let inline unboundedSpsc<'a> () : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateUnbounded(UnboundedChannelOptions(SingleWriter=true, SingleReader=true))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of n-capacity bounded SPMC
    /// (single-producer/multi-consumer) channel.
    let inline boundedSpmc<'a> (n: int) : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateBounded(BoundedChannelOptions(n, SingleWriter=true, SingleReader=false))
        (ch.Writer, ch.Reader)

    /// Returns decomposed writer/reader pair components of unbounded SPMC
    /// (multi-producer/multi-consumer) channel.
    let inline unboundedSpmc<'a> () : (ChannelWriter<'a> * ChannelReader<'a>) =
        let ch = Channel.CreateUnbounded(UnboundedChannelOptions(SingleWriter=true, SingleReader=false))
        (ch.Writer, ch.Reader)

    /// Tries to read as much elements as possible from a given reader to fill provided span
    /// without blocking.
    let readTo (span: Span<'a>) (reader: ChannelReader<'a>) : int =
        let mutable i = 0
        let mutable item = Unchecked.defaultof<_>
        while i < span.Length && reader.TryRead(&item) do
            span.[i] <- item
            i <- i+1
        i

    /// Wraps one channel reader into another one, returning a value mapped from the source.
    let map (f: 'a -> 'b) (reader: ChannelReader<'a>) : ChannelReader<'b> =
        { new ChannelReader<'b>() with

            [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
            member _.TryRead(ref) =
                let ok, value = reader.TryRead()
                if not ok then false
                else
                    ref <- f value
                    true

            [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
            member _.WaitToReadAsync(cancellationToken) = reader.WaitToReadAsync(cancellationToken) }

