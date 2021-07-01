namespace FSharp.Parallelx


open System
open System
open System.Collections.Generic

[<Sealed>]
type ThreadSafeRandom() =
    static let mutable seed = System.Security.Cryptography.RandomNumberGenerator.Create()

    [<ThreadStatic; DefaultValue>]
    static val mutable private current: Random
    static member Current
        with get () =
            if isNull ThreadSafeRandom.current
            then
                let span = Array.zeroCreate 4
                seed.GetBytes span
                ThreadSafeRandom.current <- Random(BitConverter.ToInt32(span, 0))

            ThreadSafeRandom.current

/// A module which allows to produce a random data in thread safe manner.
[<RequireQualifiedAccess>]
module Random =

    type internal R = ThreadSafeRandom

    /// Mutates provided `array` by filling it with random bytes
    let fill (array: byte[]) = R.Current.NextBytes(array)

    /// Returns an array of randomized bytes of a given `size`.
    let bytes (size: int) =
        let buf = Array.zeroCreate size
        R.Current.NextBytes buf
        buf

    /// Returns a random 32bit integer. This is a thread safe operation.
    let int32 (): int = R.Current.Next()

    let int64 (): int64 =
        let hi = R.Current.Next()
        let lo = R.Current.Next()
        ((int64 hi) <<< 32) ||| (int64 lo)

    /// Returns a random 32bit integer in [min, max) range. This is a thread safe operation.
    let between (min: int) (max: int): int = R.Current.Next(min, max)

    /// Returns a random TimeSpan fitting in between [min, max) range. This is a thread safe operation.
    let time (min: TimeSpan) (max: TimeSpan): TimeSpan =
        let value = abs (int64())
        TimeSpan ((value + min.Ticks) % max.Ticks)

    /// Picks a random element from given list. This is a thread safe operation.
    let pick (items: #IReadOnlyList<_>) =
        let length = items.Count
        items.[between 0 length]

    /// Returns items from a given sequence shuffled in a random order. This is a thread safe operation.
    let shuffle<'t> = Seq.sortWith<'t> (fun _ _ -> sign (int32 ()))
