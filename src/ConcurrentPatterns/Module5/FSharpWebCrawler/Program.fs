open System

open FSharpWebCrawler
open AgentWebCrawler
open AsyncCombinators
open ParallelAgentWebCrawler

[<EntryPoint>]
let main argv =


    let agent = new ParallelWebCrawler.WebCrawler(4)

    agent.Submit "https://www.google.com"

    Console.ReadLine() |> ignore

    agent.Dispose()

    0



