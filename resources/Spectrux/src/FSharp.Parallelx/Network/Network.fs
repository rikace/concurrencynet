namespace FSharp.Parallelx


module Network =
    open System
    open System.IO
    open System.Net
    open System.Net.Sockets

    let resolveHostInfo (host: string) = task {
        try
            let! hostInfo = Dns.GetHostEntryAsync host
            return hostInfo |> Ok
        with
        | ex ->
            printfn "Unable to resolve host for %s" host
            return ex |> Error
    }

    let resolveCanonicalHost (hostInfo: IPHostEntry) =
        printfn "Canonical Name: %s" hostInfo.HostName
        printfn "\tIP Address:"
        for ipAddr in hostInfo.AddressList do
            printfn "\t\t- %s" (string ipAddr)
        hostInfo


    let resolveAliases (hostInfo: IPHostEntry) =
        printfn "Aliases: %s" hostInfo.HostName
        for alias in hostInfo.Aliases do
            printfn "\t- %s" alias
        hostInfo

    let passResult f result =
        match result with
        | Ok r -> f r |> Ok
        | Error err ->
            printfn "Error: %A" err
            err |> Error

    let printIpAddressInfo (host: string) = task {
        let! hostInfo = resolveHostInfo host
        return passResult resolveCanonicalHost hostInfo |> passResult resolveAliases
    }

