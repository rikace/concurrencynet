namespace FSharp.Parallelx.EventEx


module GlobalEvents =

    type Mode = Once | Always

    type private ActionContainer<'T> () =
        // this implementation does not account for thread safety
        // a good way to go would be to wrap the container map
        // in an atom structure like the one in http://fssnip.net/bw
        static let container : Map<string option, ('T -> unit) list> ref = ref Map.empty

        static let morph mode (f : 'T -> unit) =
            match mode with
            | Once ->
                let untriggered = ref true
                fun t -> if !untriggered then f t ; untriggered := false
            | Always -> f

        static let getActions param = defaultArg ((!container).TryFind param) []

        static member Subscribe (mode, action : 'T -> unit, param) =
            container := (!container).Add(param, morph mode action :: getActions param)

        static member Trigger (input, param) =
            getActions param
            |> Seq.map (fun action -> async { do action input })
            |> Async.Parallel
            |> Async.Ignore
            |> Async.Start


    type GlobalEvent =
        static member Subscribe<'T> (mode, action, ?param) = ActionContainer<'T>.Subscribe(mode, action, param)
        static member Trigger<'T> (input, ?param) = ActionContainer<'T>.Trigger (input, param)


    // example
//
//    GlobalEvent.Subscribe (Always, printfn "%d")
//    GlobalEvent.Subscribe (Always, printfn "other %d", "other")
//    GlobalEvent.Subscribe (Once, (fun i -> printfn "other %d" <| i + 1), "other")
//
//    GlobalEvent.Trigger (2, "other")
