namespace FSharp.Parallelx.AsyncEx

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open System.Runtime.ExceptionServices
open System.Diagnostics

module AsyncDependecies =
    open System
    open System.Collections.Generic
    open System.Threading

    type IDependencyManager =
        abstract AddOperation : int -> (unit -> unit) -> int [] // Params
        abstract Execute : unit -> unit

    type OperationData =
        { Context : System.Threading.ExecutionContext option
          Dependencies : int array
          Id : int
          Operation : unit -> unit
          NumRemainingDependencies : int option
          Start : DateTimeOffset option
          End : DateTimeOffset option }

    type OperationMessage =
        | AddOperation of int * OperationData
        | Execute

    type DependencyMessage =
        | AddOperation of int * OperationData
        | QueueOperation of OperationData
        | Execute

    type DependencyManager() =
        let onOperationComplete = new Event<OperationData>()

        let rec getDependentOperation (dep : int list) (ops : Dictionary<int, OperationData>) acc =
            match dep with
            | [] -> acc
            | h :: t ->
                let op = ops.[h]
                let nrd =
                    function
                    | Some(n) -> Some(n - 1)
                    | None -> None

                let op' = { op with NumRemainingDependencies = nrd op.NumRemainingDependencies }
                ops.[h] <- op'
                if op'.NumRemainingDependencies.Value = 0 then getDependentOperation t ops (op' :: acc)
                else getDependentOperation t ops acc

        let operationManager =
            MailboxProcessor.Start(fun inbox ->
                let rec loop (operations : Dictionary<int, OperationData>) (dependencies : Dictionary<int, int list>) =
                    async {
                        let! msg = inbox.Receive()
                        match msg with
                        | Execute ->
                            let dependenciesFromTo = new Dictionary<int, int list>()
                            let operations' = new Dictionary<int, OperationData>()

                            for KeyValue(key, value) in operations do
                                let operation' = { value with NumRemainingDependencies = Some(value.Dependencies.Length) }
                                for from in operation'.Dependencies do
                                    let exists, lstDependencies = dependenciesFromTo.TryGetValue(from)
                                    if not(exists) then
                                        dependenciesFromTo.Add(from, [ operation'.Id ])
                                    else
                                        let lst = operation'.Id :: lstDependencies
                                        dependenciesFromTo.[from] <- lst
                                operations'.Add(key, operation')

//                                 operations'
//                                   |> Seq.filter (fun kv -> match kv.Value.NumRemainingDependencies with
//                                                         | Some(n) when n = 0 -> true
//                                                         | _ -> false)
                                //|> Seq.iter (fun op -> inbox.Post(QueueOperation(op.Value)))
//                            let filteredOps' = operations'
//                                                |> Seq.filter (fun kv -> match kv.Value.NumRemainingDependencies with
//                                                                         | Some(n) when n = 0 -> true
//                                                                         | _ -> false)
//                                                //|> Seq.toList
//                            filteredOps' |> Seq.iter (fun op -> inbox.Post(QueueOperation(op.Value)))
                            let filteredOps' = operations'
                                                |> Seq.filter (fun kv -> match kv.Value.NumRemainingDependencies with
                                                                       //  | None -> true
                                                                         | Some(n) when n = 0 -> true
                                                                         | _ -> false)
                            filteredOps' |> Seq.iter (fun op -> inbox.Post(QueueOperation(op.Value)))
                            return! loop operations' dependenciesFromTo
                        | QueueOperation(op) ->
                            async {
                                let start' = DateTimeOffset.Now
                                match op.Context with
                                | Some(ctx) ->
                                    ExecutionContext.Run(ctx.CreateCopy(),
                                                         (fun op ->
                                                         let opCtx = (op :?> OperationData)
                                                         (opCtx.Operation())), op)
                                | None -> op.Operation()
                                let end' = DateTimeOffset.Now

                                let op' =
                                    { op with Start = Some(start')
                                              End = Some(end') }
                                onOperationComplete.Trigger op'

                                let exists, lstDependencies = dependencies.TryGetValue(op.Id)
                                if exists && lstDependencies.Length > 0 then
                                    let dependentOperation' = getDependentOperation lstDependencies operations []
                                    dependencies.Remove(op.Id) |> ignore
                                    dependentOperation'
                                    |> Seq.iter (fun nestedOp -> inbox.Post(QueueOperation(nestedOp)))
                            }
                            |> Async.Start
                            return! loop operations dependencies
                        | AddOperation(id, op) ->
                            operations.Add(id, op)
                            return! loop operations dependencies
                        return! loop operations dependencies
                    }
                loop (new Dictionary<int, OperationData>()) (new Dictionary<int, int list>()))

        [<CLIEventAttribute>]
        member this.OnOperationComplete = onOperationComplete.Publish

        member this.Execute() = operationManager.Post(Execute)
        member this.AddOperation(id, operation, [<ParamArrayAttribute>] dependencies : int array) = // params

            let data =
                { Context = Some(ExecutionContext.Capture())
                  Dependencies = dependencies // (defaultArg dependencies [||])
                  Id = id
                  Operation = operation
                  NumRemainingDependencies = None
                  Start = None
                  End = None }
            operationManager.Post(AddOperation(id, data))

//[<EntryPoint>]
//let main argv =
//    let dependencyManager = AsyncDependecies.DependencyManager()
//    let acc1() = printfn "action 1"
//    let acc2() = printfn "action 2"
//    let acc3() = printfn "action 3"
//    let acc4() = printfn "action 4"
//    let acc5() = printfn "action 5"
//    let acc6() = printfn "action 6"
//    let acc7() = printfn "action 7"
//    let acc8() = printfn "action 8"
//    let acc9() = printfn "action 9"
//  //  dependencyManager.OnOperationComplete.Add(fun op -> printfn "Completed %A" op.Id)
//    dependencyManager.AddOperation(1, acc1, 3)
//    dependencyManager.AddOperation(2, acc2, 1)
//    dependencyManager.AddOperation(3, acc3)
//    dependencyManager.AddOperation(4, acc4, 2, 3)
//    dependencyManager.AddOperation(5, acc5, 1, 2, 3, 4)
//    dependencyManager.AddOperation(6, acc6, 1, 3)
//    dependencyManager.AddOperation(7, acc7, 1)
//    dependencyManager.AddOperation(8, acc8, 2, 3)
//    dependencyManager.AddOperation(9, acc9, 1, 4, 7)
//
//    dependencyManager.Execute()
//    System.Console.ReadLine() |> ignore
//    0 // return an integer exit code
