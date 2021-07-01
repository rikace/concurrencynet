namespace FSharp.Parallelx

module Transaction =
    open System.Threading

    type Transaction<'State,'Result> = T of ('State -> 'State * 'Result)

    type Atom<'S when 'S : not struct>(value : 'S) =
        let refCell = ref value

        let rec swap (f : 'S -> 'S) =
            let currentValue = !refCell
            let result = Interlocked.CompareExchange<'S>(refCell, f currentValue, currentValue)
            if obj.ReferenceEquals(result, currentValue) then ()
            else Thread.SpinWait 20; swap f

        let transact (f : 'S -> 'S * 'R) =
            let output = ref Unchecked.defaultof<'R>
            let f' x = let t,s = f x in output := s ; t
            swap f' ; output.Value

        static member Create<'S> (x : 'S) = new Atom<'S>(x)

        member self.Value with get() : 'S = !refCell
        member self.Swap (f : 'S -> 'S) : unit = swap f
        member self.Commit<'R> (f : Transaction<'S,'R>) : 'R =
            match f with T f0 -> transact f0

        static member get : Transaction<'S,'S> = T (fun t -> t,t)
        static member set : 'S -> Transaction<'S,unit> = fun t -> T (fun _ -> t,())

    type TransactionBuilder() =
        let (!) = function T f -> f

        member __.Return (x : 'R) : Transaction<'S,'R> = T (fun t -> t,x)
        member __.ReturnFrom (f : Transaction<'S,'R>) = f
        member __.Bind (f : Transaction<'S,'T> ,
                        g : 'T -> Transaction<'S,'R>) : Transaction<'S,'R> =
            T (fun t -> let t',x = !f t in !(g x) t')

    let transact = new TransactionBuilder()

    // example : thread safe stack
    type Stack<'T> () =
        let container : Atom<'T list> = Atom.Create []

        member __.Push (x : 'T) =
            transact {
                let! contents = Atom.get

                return! Atom.set <| x :: contents
            } |> container.Commit

        member __.Pop () =
            transact {
                let! contents = Atom.get

                match contents with
                | [] -> return failwith "stack is empty!"
                | head :: tail ->
                    do! Atom.set tail
                    return head
            } |> container.Commit

        member __.Flush () =
            transact {
                let! contents = Atom.get

                do! Atom.set []

                return contents
            } |> container.Commit
