<Query Kind="FSharpProgram" />

// Reduce / Aggregate / Fold 
// Usual way makes deep recursion and trusts tail-opt: 
// (a,b,c,d,e,f,g,h) => (((((((a + b) + c) + d) + e) + f) + g) + h)
// This more is quicksort-style parallel: 
// (a,b,c,d,e,f,g,h) => (((a + b) + (c + d)) + ((e + f) + (g + h)))

// No Haskell Kinds support for F# so List and Array are easiest to make as separate methods.
open System.Threading.Tasks
module List = 
  let reduceParallel<'a> f (ie :'a list) =
    let rec reduceRec f (ie :'a list) = function
      | 1 -> ie.[0]
      | 2 -> f ie.[0] ie.[1]
      | len -> 
        let h = len / 2
        let o1 = Task.Run(fun _ -> reduceRec f (ie |> List.take h) h)
        let o2 = Task.Run(fun _ -> reduceRec f (ie |> List.skip h) (len-h))
        Task.WaitAll(o1, o2)
        f o1.Result o2.Result
    match ie.Length with
    | 0 -> failwith "Sequence contains no elements"
    | c -> reduceRec f ie c

module Array = 
  let reduceParallel<'a> f (ie :'a array) =
    let rec reduceRec f (ie :'a array) = function
      | 1 -> ie.[0]
      | 2 -> f ie.[0] ie.[1]
      | len -> 
        let h = len / 2
        let o1 = Task.Run(fun _ -> reduceRec f (ie |> Array.take h) h)
        let o2 = Task.Run(fun _ -> reduceRec f (ie |> Array.skip h) (len-h))
        Task.WaitAll(o1, o2)
        f o1.Result o2.Result
    match ie.Length with
    | 0 -> failwith "Sequence contains no elements"
    | c -> reduceRec f ie c

(*
Show case in F#-interactive with #time

> [1 .. 500] |> List.fold(fun a -> fun x -> System.Threading.Thread.Sleep(30);x+a) 0;;
Real: 00:00:15.654, CPU: 00:00:00.015, GC gen0: 0, gen1: 0, gen2: 0
val it : int = 125250
> [1 .. 500] |> List.reduceParallel(fun a -> fun x -> System.Threading.Thread.Sleep(30);x+a);;
Real: 00:00:02.710, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0
val it : int = 125250
> [|1 .. 500|] |> Array.fold(fun a -> fun x -> System.Threading.Thread.Sleep(30);x+a) 0;;
Real: 00:00:15.639, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0
val it : int = 125250
> [|1 .. 500|] |> Array.reduceParallel(fun a -> fun x -> System.Threading.Thread.Sleep(30);x+a);;
Real: 00:00:02.537, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0
val it : int = 125250
*)