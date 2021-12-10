namespace FSharp.Parallelx.AsyncEx

open System
open System.Threading
open System.Threading.Tasks

module AsyncApplicative =

    type AsyncBuilder with
        member async.MergeSources(left : Async<'T>, right : Async<'S>) : Async<'T * 'S> = async {
            let box f = async { let! x = f in return x :> obj }
            let! results = Async.Parallel [|box left; box right|]
            return (results.[0] :?> 'T, results.[1] :?> 'S)
        }

//    let test = async {
//        let f x = async { let! _ = Async.Sleep 10_000 in return x }
//        let! x = f false
//        and! y = f 42
//        and! z = f (nameof f)
//        return (x, y, z)
//}
