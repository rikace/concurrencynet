module FSharpWebCrawler.AsyncCombinators

///Type representing a future / promise with Result constr
type AsyncRes<'a> = Async<Result<'a, exn>>



///Functions for working with the AsyncRes<'a> type
[<RequireQualifiedAccess>]
module AsyncRes =

    // Convert a choice to an outcome
    let ofChoice = function
        | Choice1Of2 value -> Result.Ok value
        | Choice2Of2 e -> Result.Error e


    // Create a AsyncRes from an async computation
    let wrap computation =
        async {

            let! choice = (Async.Catch computation)

            return (ofChoice choice)
        }

    // Create a AsyncRes from a value
    let retn x =
        wrap (async { return x })

    // Map the success of a future
    let flatMap f future =
        async {

            let! outcome = future

            match outcome with
            | Ok value -> return! (f value)
            | Error e -> return (Error e)
        }

    // Map a success
    let map f =
        let k value = wrap (async { return (f value) })
        flatMap k

    // Rescue a failed
    let fallback f future =
        async {
            let! outcome = future
            match outcome with
            | Ok value -> return (Ok value)
            | Error e -> return! (f e)
        }

    ///Bind two futures returning functions together
    let bind f g = f >> (flatMap g)

    ///Chain a list of futures returning functions together
    let chain futures =
        futures
        |> List.fold bind retn


    let (>>=) f g = flatMap g f

    let kleisli (f:'a -> AsyncRes<'b>) (g:'b -> AsyncRes<'c>) (x:'a) = (f x) >>= g

    let (>=>) (operation1:'a -> AsyncRes<'b>) (operation2:'b -> AsyncRes<'c>) (value:'a) =
                                                                        operation1 value >>= operation2
