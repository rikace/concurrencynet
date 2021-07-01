namespace FSharp.Parallelx

module Atom =
    open System.Threading
    
    let eq a b = obj.ReferenceEquals(a,b)
    let neq a b = eq a b |> not
    
    type Atom<'T when 'T : not struct>(value : 'T) =
        let cell = ref value
        let spinner = lazy (new SpinWait())
    
        let rec swap f =
            let tempValue = !cell
            if Interlocked.CompareExchange<'T>(cell, f tempValue, tempValue) |> neq tempValue then
                spinner.Value.SpinOnce()
                swap f
    
    
        member x.Value with get() = !cell
        member x.Swap (f : 'T -> 'T) = swap f
    
    [<RequireQualifiedAccess>]
    module Atom =
        let atom value = new Atom<_>(value)
    
        let swap (atom : Atom<_>) (f : _ -> _) = atom.Swap f

module AtomTrans =
    open System.Threading
    
    type Transaction<'state, 'result> = T of ('state -> 'state * 'result)
    
    type Atom<'s when 's : not struct>(value : 's) =
        let refCell = ref value
        
        let rec swap (f : 's -> 's) =
            let currentValue = !refCell
            let result = Interlocked.CompareExchange<'s>(refCell, f currentValue, currentValue)
            if obj.ReferenceEquals(result, currentValue) |> not then
                Thread.SpinWait 20
                swap f
            
        let transact (f : 's -> 's * 'r) =
            let output = ref Unchecked.defaultof<'r>
            let f' x =
                let t,s = f x in output := s
                t
            swap f'
            output.Value
        
        static member create<'s> (x : 's) = Atom<'s>(x)
        
        member this.Value wit get () : 's = !refCell
        member this.Swap (f : 's -> 's) : unit = swap f
        member __.Transact (f : 's -> 's * 'result) : 'result = transact f
        
        member this.Commit<'r>(f : Transaction<'s, 'r>) : 'r =
            match f with            
            | T f0 -> transact f0
        
        static member get : Transaction<'s, 's> = T (fun t -> t,t)
        static member set : 's -> Transaction<'s, unit> = fun t -> T (fun _ -> t, ())
        
    type AtomBuilder () =
        let (!) = function T f -> f
        member this.Return (x : 'r) : Transaction<'s, 'r> = T (fun t -> t,x)
        member this.ReturnFrom (f : Transaction<'s, 'r>) = f
        member this.Bind(f : Transaction<'s, 't>, g : 't -> Transaction<'s, 'r>) : Transaction<'s, 'r> =
            T (fun t -> let t', x = !(f) t in !(g x) t')
            
    let atom = AtomBuilder()
    
    module ConcurrentStack =
        type Stack<'T> () = 
            let container : Atom<'T list> = Atom.create []
            
            member this.Push item =
                atom {
                    let! content = Atom.get
                    return! Atom.set <| item :: content
                } |> container.Commit
                
            member this.Pop () =
                atom {
                    let! content = Atom.get
                    match content with
                    | [] -> return failwith "Stack is empty"
                    | h::t ->
                        do! Atom.set t
                        return h
                } |> container.Commit
        
        
            
                

namespace FSharp.Core

open System
open System.Threading
open System.Runtime.CompilerServices

[<Interface>]
type IAtomic<'a> =
    abstract Value: unit -> 'a
    abstract Swap: 'a -> 'a
    abstract CompareAndSwap: 'a * 'a -> bool
    abstract Update: ('a -> 'a) -> 'a
        
[<Sealed>]
type AtomicBool(initialValue: bool) =
    let mutable value: int = if initialValue then 1 else 0
    interface IAtomic<bool> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value) = 1

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: bool): bool = Interlocked.Exchange(&value, if nval then 1 else 0) = 1

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: bool, nval: bool): bool = 
            let v = if compared then 1 else 0
            Interlocked.CompareExchange(&value, (if nval then 1 else 0), v) = v
            
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = if modify (old = 1) then 1 else 0
                if Interlocked.CompareExchange(&value, nval, old) = old
                then nval = 1
                else loop modify
            loop modify

[<Sealed>]
type AtomicInt(initialValue: int) =
    let mutable value: int = initialValue
    
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member __.Increment() = Interlocked.Increment(&value)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member __.Decrement() = Interlocked.Decrement(&value)

    interface IAtomic<int> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: int): int = Interlocked.Exchange(&value, nval)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: int, nval: int): bool = Interlocked.CompareExchange(&value, nval, compared) = compared

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = modify old
                if Interlocked.CompareExchange(&value, nval, old) = old
                then nval
                else loop modify
            loop modify
            
[<Sealed>]
type AtomicInt64(initialValue: int64) =
    let mutable value: int64 = initialValue

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member __.Increment() = Interlocked.Increment(&value)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member __.Decrement() = Interlocked.Decrement(&value)
    
    interface IAtomic<int64> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: int64): int64 = Interlocked.Exchange(&value, nval)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: int64, nval: int64): bool = Interlocked.CompareExchange(&value, nval, compared) = compared
        
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = modify old
                if Interlocked.CompareExchange(&value, nval, old) = old
                then nval
                else loop modify
            loop modify
            
[<Sealed>]
type AtomicFloat(initialValue: float) =
    let mutable value: float = initialValue
    interface IAtomic<float> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: float): float = Interlocked.Exchange(&value, nval)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: float, nval: float): bool = Interlocked.CompareExchange(&value, nval, compared) = compared
        
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = modify old
                if Interlocked.CompareExchange(&value, nval, old) = old
                then nval
                else loop modify
            loop modify
[<Sealed>]
type AtomicFloat32(initialValue: float32) =
    let mutable value: float32 = initialValue
    interface IAtomic<float32> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: float32): float32 = Interlocked.Exchange(&value, nval)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: float32, nval: float32): bool = Interlocked.CompareExchange(&value, nval, compared) = compared

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = modify old
                if Interlocked.CompareExchange(&value, nval, old) = old
                then nval
                else loop modify
            loop modify
[<Sealed>]
type AtomicRef<'a when 'a: not struct>(initialValue: 'a) =
    let mutable value: 'a = initialValue
    interface IAtomic<'a> with
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Value () = Volatile.Read(&value)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Swap (nval: 'a): 'a = Interlocked.Exchange<'a>(&value, nval)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.CompareAndSwap (compared: 'a, nval: 'a): bool = Object.ReferenceEquals(Interlocked.CompareExchange<'a>(&value, nval, compared), compared)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member __.Update (modify) =
            let rec loop modify=
                let old = Volatile.Read(&value)
                let nval = modify old
                if obj.ReferenceEquals(Interlocked.CompareExchange(&value, nval, old), old)
                then nval
                else loop modify
            loop modify
            
[<Struct>]
type Atom =
    static member inline ($) (_: Atom, value: bool) = AtomicBool value
    static member inline ($) (_: Atom, value: int) = AtomicInt value
    static member inline ($) (_: Atom, value: int64) = AtomicInt64 value
    static member inline ($) (_: Atom, value: float) = AtomicFloat value
    static member inline ($) (_: Atom, value: float32) = AtomicFloat32 value
    static member inline ($) (_: Atom, value: 'a) = AtomicRef value

[<AutoOpen>]
module Atom =

    /// Create a new reference cell with atomic access semantics.
    let inline atom value = Unchecked.defaultof<Atom> $ value

/// Atomic module can be used to work with atomic reference cells. They are 
/// expected to look and work like standard F# ref cells with the difference 
/// that they work using thread-safe atomic operations for reads and updates.
[<RequireQualifiedAccess>]
module Atomic =

    /// Atomically replaces old value stored inside an atom with a new one,
    /// but only if previously stored value is (referentially) equal to the
    /// expected value. Returns true if managed to successfully replace the
    /// stored value, false otherwise.
    let inline cas (expected: 'a) (nval: 'a) (atom: #IAtomic<'a>) =
        atom.CompareAndSwap(expected, nval)

    /// Atomically tries to update value stored inside an atom, by passing
    /// current atom's value to modify function to get new result, which will
    /// be stored instead. Returns an updated value.
    let inline update (modify: 'a -> 'a) (atom: #IAtomic<'a>): 'a =
        atom.Update modify

    /// Atomically increments counter stored internally inside of an atom.
    /// Returns an incremented value.
    let inline inc (atom: ^a ): ^b when ^a : (member Increment: unit -> ^b) =
        ( ^a : (member Increment: unit -> ^b) (atom))
        
    /// Atomically decrements counter stored internally inside of an atom.
    /// Returns an incremented value.
    let inline dec (atom: ^a ): ^b when ^a : (member Decrement: unit -> ^b) =
        ( ^a : (member Decrement: unit -> ^b) (atom))

    module Operators =

        /// Unwraps the value stored inside of an atom.
        let inline (!) (atom: #IAtomic<'a>): 'a = atom.Value ()

        /// Atomically swaps the value stored inside of an atom with provided one.
        /// Returns previously stored value.
        let inline (:=) (atom: #IAtomic<'a>) (value: 'a): 'a = atom.Swap value                