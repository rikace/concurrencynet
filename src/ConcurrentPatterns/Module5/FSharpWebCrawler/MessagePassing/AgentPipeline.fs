module FSharpWebCrawler.MessagePassing.AgentPipeline

#if INTERACTIVE
#load "../Common/Helpers.fs"
#load "../Asynchronous/Async.fs"
#endif

open System
open System.Threading
open System.Net
open System.IO
open System.Drawing
open FSharpWebCrawler.Async.AsyncOperators
open FSharpWebCrawler
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

// Step (1) implement a structured agent that returns
//          the result of the computation "computation" over the message received
//
//          try to handle messages that could run a computation either "sync" or "async"
//          TIP: you could have a DU to handle a different type of message (to run either Sync or Async)
//               or load the computation at runtime. In this last case, the Agent body should keep an
//               internal state of the function

let agent computation = Agent<'a * AsyncReplyChannel<'b>>.Start(fun inbox ->
    let rec loop () = async {
        let! msg, replyChannel = inbox.Receive()
        let res = computation msg
        replyChannel.Reply res
        return! loop() }
    loop() )

// Step (2) compose agents implementing the "pipeline" function.
//          The idea of this function is to use the previously implemented
//          well structured agent (in step 1), to pass a message and return the result of the
//          agent computation.
//          - Try also to implement a function that handle Async computation

let pipelineAgent (f:'a -> 'b) (m: 'a) : Async<'b> =
    let a = agent f
    a.PostAndAsyncReply(fun replyChannel -> m, replyChannel)


// Step (3) compose pipeline
// given two agents (below), compose them into a pipeline
// in a way that calling (or sending a message) to the pipeline,
// the message is passed across all the agents in the pipelinw

// Testing
let agent1 = pipelineAgent (sprintf "Pipeline 1 processing message : %s")
let agent2 = pipelineAgent (sprintf "Pipeline 2 processing message : %s")

let message i = sprintf "Message %d sent to the pipeline" i

// TIP: Remember the async bind operator?
//      the signature of the Async.bind operator fits quite well,
//      because the return type of the "pipelineAgent" function is an Async<_>
// TIP: It is useful to use an infix operator to simplify the composition between Agents
// BONUS: after have composed the agents, try to use (and implement) the Kliesli operator

// (‘a -> Async<’b>) -> Async<’a> -> Async<’b>
let agentBind f xAsync = async {
    let! x = xAsync
    return! f x }

let agentRetn x = async { return x }

let (>>=) x f = agentBind f x
let pipeline x = agentRetn x >>= agent1 >>= agent2

for i in [1..10] do
    pipeline (string i)
    |> AsyncEx.run (fun res -> printfn $"Thread #id: %d{Thread.CurrentThread.ManagedThreadId} - Msg: %s{res}")

module PipelineKliesli =
    let (>=>) f1 f2 x = f1 x >>= f2
    let pipeline = agent1 >=> agent2

    let operation i = pipeline <| message i



// Step (4) Each agent in the pipeline handles one message at a give time
//    How can you make these agents running in parallel?
//    This is important in the case of async computations, so you can reach great throughput
//
//    Implement an Agent which underlying body computes and distributes the messages in a Round-Robin fashion
//    between a set of (intern and pre-instantiated) Agent children

let parallelAgent (parallelism: int) (computation: 'a -> Async<'b>) =
    // MISSING CODE HERE
    let behavior = (fun (inbox: MailboxProcessor<'a * AsyncReplyChannel<'b>>) ->
        let rec loop () = async {
            let! msg, replyChannel = inbox.Receive()
            let! res = computation msg
            replyChannel.Reply res
            return! loop() }
        loop() )
    let agents = Array.init parallelism (fun _ -> MailboxProcessor.Start(behavior))
    Agent<'a * AsyncReplyChannel<'b>>.Start(fun inbox ->
        let rec loop index = async {
            let! (msg, ch) = inbox.Receive()
            agents.[index].Post(msg, ch)
            return! loop((index+1) % parallelism)
         }
        loop 0)

module AgentComposition =

    open System.IO
    open System
    open System.Drawing

    [<AutoOpen>]
    module HelperType =
        type ImageInfo = { Path:string; Name:string; Image:Image<Rgba32> }

    module ImageHelpers =
        let convertImageTo3D (image:Image<Rgba32>) =
            let bitmap = image.Clone()
            let w,h = bitmap.Width, bitmap.Height
            for x in 20 .. (w-1) do
                for y in 0 .. (h-1) do
                    let c1 = bitmap.[x,y]
                    let c2 = bitmap.[x - 20,y]
                    let color3D = Rgba32(c1.R, c2.G, c2.B)
                    bitmap.[x - 20 ,y] <- color3D
            bitmap


        let loadImage = (fun (imagePath:string) -> async {
            printfn "loading image %s" (Path.GetFileName(imagePath))
            let bitmap = Image.Load<Rgba32>(imagePath)
            return { Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                     Name = Path.GetFileName(imagePath)
                     Image = bitmap } })

        let apply3D = (fun (imageInfo:ImageInfo) -> async {
            printfn "destination image %s >> %s" imageInfo.Name imageInfo.Path
            let bitmap = convertImageTo3D imageInfo.Image
            return { imageInfo with Image = bitmap } })

        let saveImage = (fun (imageInfo:ImageInfo) -> async {
            printfn "Saving image %s" imageInfo.Name
            let destination = Path.Combine(imageInfo.Path, imageInfo.Name)
            imageInfo.Image.Save(destination)
            return imageInfo.Name})

    open ImageHelpers

    // Step (6) apply the parallelAgent to run the below function "loadandApply3dImageAgent"
    // place holder, this function was implemented in step 3
    let (>>=) (x: Async<'a>) (f: 'a -> Async<'b>) = Unchecked.defaultof<Async<'b>> // agentBind f x

    let loadandApply3dImage imagePath = agentRetn imagePath >>= loadImage >>= apply3D >>= saveImage
    let loadandApply3dImageAgent = parallelAgent 2 loadandApply3dImage

    // Step (7) use the "pipeline" function created in step (2), and replace the basic "agent""
    //          with the "parallelAgent". keep in mind of the extra parameter "limit" to indicate
    //          the level of parallelism
    let parallelPipe (limit:int) (operation:'a -> Async<'b>)  =
        let agent = parallelAgent limit operation
        fun (job:'a) ->
            agent.PostAndAsyncReply(fun replyChannel -> job, replyChannel)

    let loadImageAgent = parallelPipe 2 loadImage
    let apply3DEffectAgent = parallelPipe 2 apply3D
    let saveImageAgent = parallelPipe 2 saveImage

    let parallelPipeline = loadImageAgent >=> apply3DEffectAgent >=> saveImageAgent

    let parallelTransformImages() =
       let images = Directory.GetFiles(Environment.CurrentDirectory + @"/src/FSharpWebCrawler/Images")
       for image in images do
            parallelPipeline image |> AsyncEx.run (fun imageName -> printfn "Saved image %s" imageName)

    parallelTransformImages()

module AgentModule =

    type Replyable<'a, 'b> = | Reply of 'a * AsyncReplyChannel<'b>

    let pipelined (agent:MailboxProcessor<_>) previous =
        async {
            let! result = agent.PostAndTryAsyncReply(fun rc -> Reply(previous,rc))
            match result with
            | Some(result) ->
                match result with
                | Choice1Of2(result) -> return result
                | Choice2Of2(err) -> return raise(err)
            | None -> return failwithf "Stage failled"
        }

