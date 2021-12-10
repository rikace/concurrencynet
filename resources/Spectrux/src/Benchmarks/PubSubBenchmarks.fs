namespace Benchmarks

open System
open System.Threading.Tasks
open BenchmarkDotNet
open System.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers

open System.Threading
open CSharp.Parallelx.EventEx

module Types =
    type RecOne =
        {
            name : string
            number : int
        }
        interface Aggregator.IMessage
        
    type RecTwo =
        {
            brand : string
            year : int
        }
        interface Aggregator.IMessage  
    
    type RecThree =
        {
            model : string
            price : float
        }
        interface Aggregator.IMessage  
    
    
module PubSubBenchmarks =
    open Types
    
    [<MemoryDiagnoser>]
    [<RPlotExporter; RankColumn>]
    type Benchmark() =

        [<Params(1000, 10000)>]
        [<DefaultValue>]
        val mutable N : int
        
        [<DefaultValue>]
        val mutable iterations : int[]
        
        [<GlobalSetup>]
        member this.Setup() =
            let rand = new Random((int) DateTime.Now.Ticks)
            this.iterations <- Array.init this.N (fun _ -> rand.Next())
            
        [<Benchmark>]    
        member this.CSharp_EventAggregator() =
            let agg = EventAggregator()
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.Subscribe<RecThree>(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()

            
        [<Benchmark>]    
        member this.CSharp_Aggregator() =
            let agg = Aggregator.EventAggregator()
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.Subscribe<RecThree>(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()
                
        [<Benchmark>]    
        member this.CSharp_SubscriptionService() =
            let agg = SubscriptionService()
            
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.Subscribe<RecThree>(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()
                
        [<Benchmark>]    
        member this.CSharp_RxEventAggregator() =
            let agg = new RxEventAggregator()
            
            agg.GetEvent<RecOne>().Subscribe(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.GetEvent<RecOne>().Subscribe(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.GetEvent<RecTwo>().Subscribe(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.GetEvent<RecTwo>().Subscribe(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.GetEvent<RecThree>().Subscribe(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()
                
        [<Benchmark>]    
        member this.CSharp_ChannelsQueuePubSub() =
            let agg = ChannelsQueuePubSub()
            
            agg.RegisterHandler<RecOne>(Action<RecOne>(fun r ->
                {r with number = r.number + 1} |> ignore)) |> ignore
            agg.RegisterHandler<RecOne>(Action<RecOne>(fun r ->
                {r with number = r.number * 2} |> ignore)) |> ignore
            agg.RegisterHandler<RecTwo>(Action<RecTwo>(fun r ->
                {r with year = r.year + 1} |> ignore)) |> ignore
            agg.RegisterHandler<RecTwo>(Action<RecTwo>(fun r ->
                {r with year = r.year * 2} |> ignore)) |> ignore
            agg.RegisterHandler<RecThree>(Action<RecThree>(fun r ->
                {r with price = r.price * 1.2} |> ignore)) |> ignore
            
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () ->             
                    for i in this.iterations do
                        let interTasks = [|
                            agg.Enqueue<RecOne>({name="Bugghina"; number=1+i})
                            agg.Enqueue<RecOne>({name="Stellina"; number=4+i})
                            agg.Enqueue<RecTwo>({brand="Maserati"; year=2014+i})
                            agg.Enqueue<RecTwo>({brand="Porsche"; year=2020+i})
                            agg.Enqueue<RecThree>({model="Carrear"; price= (float i) + 120.})
                            |]
                        let waitAll = interTasks |> Task.WhenAll
                        waitAll.Wait()                    
                    ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()
            agg.Stop()
                
                
                         
        [<Benchmark>] 
        member this.FSharp_EventAggregator() =
            let agg = new FSharp.Parallelx.EventEx.EventAggregator.EventAggregator() :> FSharp.Parallelx.EventEx.EventAggregator.IEventAggregator
            
            agg.GetEvent<RecOne>().Subscribe(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.GetEvent<RecOne>().Subscribe(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.GetEvent<RecTwo>().Subscribe(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.GetEvent<RecTwo>().Subscribe(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.GetEvent<RecThree>().Subscribe(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()
                
        [<Benchmark>]    
        member this.FSharp_SubscriptionService() =
            let agg = FSharp.Parallelx.EventEx.BrokerPubSub.SubjectAgent() :> FSharp.Parallelx.EventEx.BrokerPubSub.IEventAggregator
            
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number + 1} |> ignore)) |> ignore
            agg.Subscribe<RecOne>(Action<RecOne>(fun r -> {r with number = r.number * 2} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year + 1} |> ignore)) |> ignore
            agg.Subscribe<RecTwo>(Action<RecTwo>(fun r -> {r with year = r.year * 2} |> ignore)) |> ignore
            agg.Subscribe<RecThree>(Action<RecThree>(fun r -> {r with price = r.price * 1.2} |> ignore)) |> ignore
        
            let tasks = ResizeArray<_>()
            for k = 0 to 4 do
                Task.Run(fun () -> 
                for i in this.iterations do 
                    agg.Publish<RecOne>({name="Bugghina"; number=1+i})
                    agg.Publish<RecOne>({name="Stellina"; number=4+i})
                    agg.Publish<RecTwo>({brand="Maserati"; year=2014+i})
                    agg.Publish<RecTwo>({brand="Porsche"; year=2020+i})
                    agg.Publish<RecThree>({model="Carrear"; price= (float i) + 120.})
                ) |> tasks.Add
            let allTask = tasks |> Task.WhenAll
            allTask.Wait()           
                                         