module FSharpWebCrawler.AgentWebCrawler

open System
open System.Threading
open System.Net
open System.IO
open HtmlAgilityPack
open System.Text.RegularExpressions
open FSharpWebCrawler

// Extracts links from HTML.
let extractLinks html =
    let pattern1 = "(?i)href\\s*=\\s*(\"|\')/?((?!#.*|/\B|mailto:|location\.|javascript:)[^\"\']+)(\"|\')"
    let pattern2 = "(?i)^https?"

    let links =
        [
            for x in Regex(pattern1).Matches(html) do
                yield x.Groups.[2].Value
        ] |> List.filter (fun x -> Regex(pattern2).IsMatch(x))
    links

    // Download Html/Page content
let downloadContent (url : string) = async {
    try
        let req = WebRequest.Create(url) :?> HttpWebRequest
        req.UserAgent <- "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)"
        req.Timeout <- 5000
        use! resp = req.GetResponseAsync() |> Async.AwaitTask
        let content = resp.ContentType
        let isHtml = Regex("html").IsMatch(content)
        match isHtml with
        | true -> use stream = resp.GetResponseStream()
                  use reader = new StreamReader(stream)
                  let! html = reader.ReadToEndAsync() |> Async.AwaitTask
                  return Some html
        | false -> return None
    with
    | _ -> return None
}

// TODO LAB
module ParallelWebCrawler =

    type Msg<'a, 'b> =
    | Item of 'a
    | Mailbox of Agent<Msg<'a, 'b>>

    let cts = new CancellationTokenSource()

    let [<Literal>] parallelism = 4 // this can be any arbitrary value

    let httpRgx =
        new ThreadLocal<Regex>(fun () -> new Regex(@"^(http|https|www)://.*$"))

    let sites = [
       "http://cnn.com/";          "http://bbc.com/";
       "http://www.yahoo.com";     "http://www.amazon.com"
       "http://news.yahoo.com";    "http://www.microsoft.com";
       "http://www.google.com";    "http://www.netflix.com";
       "http://www.bing.com";      "http://www.microsoft.com";
       "http://www.yahoo.com";     "http://www.amazon.com"
       "http://news.yahoo.com";    "http://www.microsoft.com"; ]

    // Step (1) create an Agent that prints the messages received
    //    this is important in parallel computations that print some output
    //    to keep the console in a readable state
    let printerAgent =
        Agent<Msg<string, _>>.Start((fun (inbox : Agent<Msg<string, _>>) -> async {

          // MISSING CODE

          return! async.Return ()  // << replace this line with implementation
          }), cancellationToken = cts.Token)


    // Test
    printerAgent.Post (Item "Hello from printerAgent!!")

    // Step (2)
    //     create a "parallelAgent" worker based on the MailboxPorcerssor.
    //     the idea is to have an Agent that handles, computes and distributes the messages in a Round-Robin fashion
    //     between a set of (intern and pre-instantiated) Agent children
    //
    //     This is important in the case of async computations, so you can reach great throughput
    //     If already completed the "Agent Pipeline" lab, then feel free to use the "parallelAgent" already created

    let parallelAgent (degreeOfParallelism : int) (f: MailboxProcessor<Msg<'a, 'b>> -> Async<unit>) =
        let token = cts.Token

        // MISSING CODE HERE
        // 1 - use the "Array" module to initialize an array of Agents
        let agents = Unchecked.defaultof<MailboxProcessor<_> []> // << replace this line with implementation

        // 2 - crete an agent that broadcasts the messages received
        //     in a Round-Robin fashion between the agents created in the  previous point
        let agent = new Agent<Msg<'a, 'b>>((fun inbox ->
            let rec loop index = async {
                let! msg = inbox.Receive()
                // MISSING CODE HERE

                return! loop index
            }
            loop 0), cancellationToken = token)

        token.Register(fun () -> agents |> Seq.iter(fun agent -> (agent :> IDisposable).Dispose())) |> ignore
        agent.Start()
        agent


    // Step (3) complete the "Item(url)" case
    let fetchContentAgent (limit : int option) =
        parallelAgent parallelism (fun (inbox : MailboxProcessor<_>) ->
            let rec loop (urls : Set<string>) (agents : Agent<_> list) = async {
                let! msg = inbox.Receive()

                match msg with
                | Item(url) ->
                    // check if the content of the "url" has been already downloaded.
                    // if not then
                    //     downloaded the content (use the function "downloadContent")
                    //     and print (using the "printerAgent") a message that the "content of url %s hes been downloaded"
                    //
                    //    IMPORTANT: the content is passed (broadcast) as message to all the agents subscribed to this agent.
                    //               the registration is done using the "Mailbox(agent)" message/case.
                    //               The list of agent subscribed is kept as state of the agent loop (agents : Agent<_> list)
                    // else
                    //     do nothing
                    //
                    // verify if the limit of the Urls downloaded is reached, and stop the process accordingly
                    // (keep in mind that the "limit" is an option type (if None then the process is limitless)

                    return! loop urls agents

                // the "Msg<_,_>" case is not completed.
                // finish the code covering the missing "Msg<_,_>" cases.
                // this missing case is responsible to register the Agents (passed as message)
                // into the current Agent body.
            }
            loop Set.empty [])

    // Testing
    let testFetchContentAgent () =
        let agent = fetchContentAgent (Some 5)
        agent.Post (Mailbox(printerAgent))
        for site in sites do agent.Post (Item site)

    testFetchContentAgent()


    // Step (4)  create a broadcast agent, which simply broadcasts
    //           the messages received to all the agent subscribed
    //     Bonus:    would be nice to have a filter in place to select
    //               which agent receives which message (no required)
    let broadcastAgent () =
        parallelAgent parallelism (fun inbox ->
            let rec loop (agents : Agent<_> list) = async {
                let! msg = inbox.Receive()
                // the content is passed (broadcast) as message to all the agents subscribed to this agent.
                // the registration is done using the "Mailbox(agent)" message/case.
                // The list of agent subscribed is kept as state of the agent loop (agents : Agent<_> list)

                // MISSING CODE

                // match msg with

                return! loop agents // << this line should be replaced with correct implementation
            }
            loop [])

    // Testing
    let testBroadcastAgent1() =
        let broadcast = broadcastAgent()
        broadcast.Post (Mailbox(printerAgent))
        for site in sites do broadcast.Post (Item site)

    testBroadcastAgent1()

    let imageParserAgent () =
        parallelAgent parallelism (fun inbox ->
            let rec loop (agents : Agent<Msg<string, string>> list) = async {
                let! msg = inbox.Receive()
                match msg with
                | Item(html) ->
                    let doc = new HtmlDocument()
                    doc.LoadHtml(html)

                    let imageLinks =
                        doc.DocumentNode.Descendants("img")
                        |> Seq.choose(fun n ->
                            if n.Attributes.Contains("src") then
                                n.GetAttributeValue("src", "") |> Some
                            else None)
                        |> Seq.filter(fun url -> httpRgx.Value.IsMatch(url))

                    for imgLink in imageLinks do
                        agents |> Seq.iter(fun agent -> agent.Post (Item(imgLink)))

                    return! loop agents
                | Mailbox(agent) -> return! loop (agent::agents)
            }
            loop [])


    // Step (5)  Implement a "link" agent parser.
    //           Following the same idea from the previous agents,
    //           using the messages "Mailbox(agent)" to subscribe agent(s), and the message
    //           "Item(url)" to deliver an url to process,
    //           implement an agent that extract the "href" tags from a web page
    //           and send the reference (href) to the Agent subscribed  as link
    let linksParserAgent () =
        parallelAgent parallelism (fun inbox ->
            let rec loop (agents : Agent<_> list) = async {
                let! msg = inbox.Receive()
                match msg with
                | Item(html) ->

                    let doc = new HtmlDocument()
                    doc.LoadHtml(html)

                    let links =
                        doc.DocumentNode.Descendants("a")
                        |> Seq.choose(fun n ->
                            if n.Attributes.Contains("href") then
                                n.GetAttributeValue("href", "") |> Some
                            else None)
                        |> Seq.filter(fun url -> httpRgx.Value.IsMatch(url)) // NOTE, IS THIS CORRECT ??

                    // broadcast the links extracted to all the "agents" subscribed
                    // (use the "Item" case to send the "link" extracted
                    // Missing code

                    return! loop agents
                // Add the missing case to register/subscribe Agents
                // | ...
            }
            loop [])

    let comparison = StringComparison.InvariantCultureIgnoreCase
    let linkFilter =
        fun (link : string) ->
            link.IndexOf(".aspx", comparison) <> -1 ||
            link.IndexOf(".php", comparison) <> -1 ||
            link.IndexOf(".htm", comparison) <> -1 ||
            link.IndexOf(".html", comparison) <> -1

    let imageSideEffect (f: string -> byte[] -> Async<unit>) =
        parallelAgent parallelism (fun inbox ->
            let rec loop () = async {
                let! msg = inbox.Receive()
                match msg with
                | Item(url) ->
                    if linkFilter url then
                        let client = new WebClient()
                        let! buffer = client.DownloadDataTaskAsync(url) |> Async.AwaitTask
                        do! f url buffer
                | _ -> failwith "no implemented"
                return! loop ()
            }
            loop ())

    // Step (6)
    // complete the "side effect" function as you wish.
    // For example, you could just print the image name downloaded and/or save it to the file-system
    let saveImageAgent : Agent<Msg<string, string>> =
        imageSideEffect (fun url buffer -> async {
                let fileName = Path.GetFileName(url)
                let name = @"Images\" + fileName

                // Missing code
                ()
            })

    type WebCrawler (?limit) as this =
        let fetchContentAgent = fetchContentAgent limit
        let contentBroadcaster = broadcastAgent ()
        let linkBroadcaster = broadcastAgent ()
        let imageParserAgent = imageParserAgent ()
        let linksParserAgent = linksParserAgent ()

        // Step (6)
        // Register/subscribe the agent to compose and run the Web-Crawler
        do
            fetchContentAgent.Post     (Mailbox(contentBroadcaster))
            contentBroadcaster.Post   (Mailbox(imageParserAgent))
            contentBroadcaster.Post   (Mailbox(linksParserAgent))
            contentBroadcaster.Post   (Mailbox(printerAgent))
            linkBroadcaster.Post      (Mailbox(printerAgent))
            imageParserAgent.Post     (Mailbox(saveImageAgent))
            linksParserAgent.Post     (Mailbox(linkBroadcaster))
            linkBroadcaster.Post      (Mailbox(saveImageAgent))
            linkBroadcaster.Post      (Mailbox(fetchContentAgent))

        member __.Submit(url : string) = fetchContentAgent.Post(Item(url))

        member __.Dispose() = cts.Cancel()

        interface IDisposable with
            member x.Dispose() = this.Dispose()

    // BONUS
    // what happen if an error is thrown ??
    // we could use the build in functionality for Error
    // propagation to do something, like logging or recovering from errors
    //
    // Build a Reactive Agent (Observer/Obseravble) to register the Agent for :
        // 1- subscribe for loging
        // 2- could you re-implement the Web-Crawler using Reatcive Agent?
        // See the "ReactiveAgent.fs" in Module 2
    type MailboxProcessor<'T> with
        member inline this.withSupervisor (supervisor: Agent<exn>, transform) =
            this.Error.Add(fun error -> supervisor.Post(transform(error))); this

        member this.withSupervisor (supervisor: Agent<exn>) =
            this.Error.Add(supervisor.Post); this
