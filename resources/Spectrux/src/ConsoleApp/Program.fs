// Learn more about F# at http://fsharp.org

open System
open FSharp.Parallelx

[<EntryPoint>]
let main argv =
    Network.printIpAddressInfo "ww.mkp.com"
    Network.printIpAddressInfo "169.1.1.1"
    Console.ReadLine() |> ignore
    0 // return an integer exit code
