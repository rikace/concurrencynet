<Query Kind="FSharpExpression">
  <Reference>C:\Code\Concurrency\Common\Lib\CommonHelpers.dll</Reference>
  <Namespace>CommonHelpers</Namespace>
</Query>

BenchPerformance.Time("List : map -> map -> reduce", fun () -> 
    let ops f (l: int list) = List.map f (List.map f l) |> List.reduce(+)
    let _ = ops((+)1)[0..10000000]
    ())


BenchPerformance.Time("List : fold >>", fun () -> 
    let opfs f l = l |> List.fold(fun s e -> s + ((f >> f) e)) 0
    let _ = opfs((+)1)[0..10000000]
    ())


//BenchPerformance.Time("Seq : map -> map -> reduce", fun () -> 
//    let ops f (l: int list) = Seq.map f (Seq.map f l) |> Seq.reduce(+)
//    let _ = ops((+)1)[0..10000000]
//    ())
//
//
//BenchPerformance.Time("Seq : fold >>", fun () -> 
//    let opfs f l = l |> Seq.fold(fun s e -> s + ((f >> f) e)) 0
//    let _ = opfs((+)1)[0..10000000]
//    ())