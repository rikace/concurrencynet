module ConsoleApp.Trampoline

[<AutoOpen>]
module Trampoline =
    type Trampoline<'a> =
        | Return of 'a
        | Suspend of (unit -> 'a Trampoline)
        | Flatten of {| m : 'a Trampoline; f : 'a -> 'a Trampoline |}
        with
            static member (<|>) ((this : 'a Trampoline), (f : 'a -> 'a)) =
                Flatten {| m = this; f = (f >> Return) |}

            static member (>>=) ((this : 'a Trampoline), (f : 'a -> 'a Trampoline)) =
                Flatten {| m = this; f = f |}

    let execute (head : 'a Trampoline) : 'a =
        let rec execute' = function
            | Return v -> v
            | Suspend f ->
                f ()
                |> execute'
            | Flatten b ->
                match b.m with
                | Return v ->
                    b.f v
                    |> execute'
                | Suspend f ->
                    Flatten {| m = f (); f = b.f |}
                    |> execute'
                | Flatten f ->
                    let fm = f.m
                    let ff a = Flatten {| m = f.f a ; f= b.f |}
                    Flatten {| m = fm;  f = ff |}
                    |> execute'
        execute' head

module TestTrampoline =
    open System.Numerics

    let fact n =
        let rec fact' n accum =
            if n = 0 then
                Return accum
            else
                Suspend (fun () -> fact' (n-1) (accum * (BigInteger n)))

        if (n < 0) then invalidArg "n" "should be > 0"

        fact' n BigInteger.One
        |> execute

    // The basic BinaryTree structure
    type BinaryTree<'node> =
        {
            Value : 'node
            Left  : BinaryTree<'node> option
            Right : BinaryTree<'node> option
        }
    with
        static member Apply(n, ?l, ?r) = { Value = n; Left = l; Right = r }
        member this.AddLeft(n)  = { this with Left  = (Some << BinaryTree<_>.Apply) n }
        member this.AddRight(n) = { this with Right = (Some << BinaryTree<_>.Apply) n }

    // In-Order Traversal
    let foldInOrder root seed consume =

        // The (stack-unsafe but elegant) recursive version
        let rec foldRecursive node accum =
            match node with
            | None -> accum
            | Some n ->
                foldRecursive n.Left accum
                |> (fun left -> consume left n.Value)
                |> (fun curr -> foldRecursive n.Right curr)

        // The (stack-safe and equally elegant) recursive version
        let rec foldTrampoline node accum =
            match node with
            | None -> Return accum
            | Some n ->
                Suspend (fun () -> foldTrampoline n.Left accum)
                >>= (fun left -> Return (consume left n.Value))
                >>= (fun curr -> Suspend (fun () -> foldTrampoline n.Right curr))

        foldTrampoline root seed
        |> execute
