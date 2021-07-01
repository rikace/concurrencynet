module FSharpWebCrawler.Agent

#if INTERACTIVE
#load "../Common/Helpers.fs"
#load "../Asynchronous/Async.fs"
#endif

open System
open System.Threading
open System.Net
open System.IO
open FSharpWebCrawler.Async
open FSharpWebCrawler


let httpAsync (url : string) = async {
    let req = WebRequest.Create(url)
    let! resp = req.AsyncGetResponse()
    use stream = resp.GetResponseStream()
    use reader = new StreamReader(stream)
    return! reader.ReadToEndAsync() |> Async.AwaitTask }

let downloadAgent =
    Agent<string * AsyncReplyChannel<string>>.Start(fun inbox ->
        let rec loop (seenUrls : Map<string, string>) =
            async {
                let! (url, reply) = inbox.Receive()
                match seenUrls.TryFind(url) with
                | Some(result) ->
                    printfn "Site %s is already downloaded" url
                    reply.Reply(result)
                    return! loop seenUrls
                | None ->
                    printfn "Site %s is new. Downloading..." url
                    let! downloaded = httpAsync url
                    reply.Reply(downloaded)
                    return! loop (Map.add url downloaded seenUrls)
            }
        loop Map.empty
    )

let sites = [
   "http://cnn.com/";          "http://bbc.com/";
   "http://www.yahoo.com";     "http://www.amazon.com"
   "http://news.yahoo.com";    "http://www.microsoft.com";
   "http://www.google.com";    "http://www.netflix.com";
   "http://www.bing.com";      "http://www.microsoft.com";
   "http://www.yahoo.com";     "http://www.amazon.com"
   "http://news.yahoo.com";    "http://www.microsoft.com"; ]

//for site in sites do
//    downloadAgent.PostAndReply(fun ch -> site, ch)


type MailboxProcessor<'T> with
    member inline this.withSupervisor (supervisor: Agent<exn>, transform) =
        this.Error.Add(fun error -> supervisor.Post(transform(error))); this

    member this.withSupervisor (supervisor: Agent<exn>) =
        this.Error.Add(supervisor.Post); this





type MailboxProcessor<'a> with
    static member public parallelWorker (workers:int)
            (behavior:MailboxProcessor<'a> -> Async<unit>)
            (?errorHandler:exn -> unit) (?cts:CancellationToken) =

        let cts = defaultArg cts (CancellationToken())
        let errorHandler = defaultArg errorHandler ignore
        let agent = new MailboxProcessor<'a>((fun inbox ->
            let agents = Array.init workers (fun _ ->
                let child = MailboxProcessor.Start(behavior, cts)
                child.Error.Subscribe(errorHandler) |> ignore
                child)
            cts.Register(fun () ->
                agents |> Array.iter(
                    fun a -> (a :> IDisposable).Dispose()))
            |> ignore

            let rec loop i = async {
                let! msg = inbox.Receive()
                agents.[i].Post(msg)
                return! loop((i+1) % workers)
            }
            loop 0), cts)
        agent.Start()


