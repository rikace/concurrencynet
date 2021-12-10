open System
open System.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Reports
open Benchmarks

[<EntryPoint>]
let main argv =
    let performanceStats = BenchmarkRunner.Run<PubSubBenchmarks.Benchmark>()    
    //let summary = Benchmark.mapSummary performanceStats
    //Benchmark.drawSummaryReport summary
    
    Console.ReadLine() |> ignore
    0
