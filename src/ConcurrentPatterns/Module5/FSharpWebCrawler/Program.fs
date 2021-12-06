open System
open FSharpWebCrawler

[<EntryPoint>]
let main argv =

    let agent = new AgentWebCrawler.ParallelWebCrawler.WebCrawler(4)

    agent.Submit "https://www.google.com"

    Console.ReadLine() |> ignore

    agent.Dispose()

    0



