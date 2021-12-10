namespace FSharp.Parallelx.State

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
//open FSharp.Parallelx.AsyncEx.AsyncCombinators

module MyState =

    // Lenses 
    // create web crawler 
    // Ses kafunk ?
    
    type State<'state, 'result> = State of ('state -> 'result * 'state)
    
    let run (State r) initstate = r initstate
    
    let bind p f = 
        State(fun initState -> 
            let state1, value1 = 
                match p with
                | State t -> t initState
            let state2, value2 = 
                match f value1 with 
                | State h -> h state1 
            (state2, value2)) 
                    
    
    type StateBuilder() = 
        member __.Bind(result:State<'state, 'a>, cont:'a -> State<'state, 'b>) =
            State(fun initState ->
                let result, updatedState = run result initState
                run (cont result) updatedState)
        
        member __.Combine(a:State<'state, unit>, b:State<'state,'a>) = 
            State(fun initState ->
                let (), updatedState = run a initState
                run b updatedState)
                
        member __.Delay(cont:unit -> State<'state, 'a>) =
            State(fun initState ->
                run (cont()) initState)
                
                
        member __.For(items:seq<'a>, body:'a ->State<'state,unit>) =
            State(fun initState ->
                let state = ref initState
                for e in items do
                    let(), updatedState = run (body e) (!state)
                    state := updatedState
                (), !state)
        
        member __.Return (x:'a) = State(fun initState -> x, initState)
        
        member __.Using<'a, 'state, 'b when 'a :> IDisposable>(x:'a, cont:'a -> State<'state, 'b>) =
            State(fun initState ->
                try
                    run (cont x) initState
                finally 
                    x.Dispose())
                    
        member __.TryFinally(body:State<'state,'a>, finBody:unit -> unit) =
            State(fun initState ->
                try
                    run body initState
                finally 
                    finBody())
                    
        member __.TryWit(body:State<'state,'a>, exnHandler:exn -> State<'state, 'a>) =
            State(fun initState ->
                try
                    run body initState
                with | e -> run (exnHandler e) initState)
                
        member __.While(predicate:unit -> bool, body: State<'state, unit>) =
            State(fun initState ->
                let state = ref initState
                while predicate() = true do
                    let(), updatedState = run body (!state)
                    state := updatedState 
                (), !state)
                
        member __.Zero() = State(fun initState -> (), initState)                                
                                
        
        
    let state = StateBuilder()
    
    let getState = State(fun state -> state,state)
    let setState newState = State(fun _ -> (),newState)
    
    
    
    module Example = 
        let add x = 
            state { 
                let! currentTotal, history = getState
                do! setState (currentTotal + x, (sprintf "Added %d" x) :: history) }                        
                                                    
                        
        let subtract x = 
            state {
                let! currentTotal, history = getState
                do! setState (currentTotal - x, (sprintf "Subtratced %d" x) :: history) }
        
        let mul x =                       
            state {
                let! currentTotal, history = getState
                do! setState (currentTotal * x, (sprintf "Multiplied %d" x) :: history) }

        let div x =                       
            state {
                let! currentTotal, history = getState
                do! setState (currentTotal / x, (sprintf "Divided %d" x) :: history) }    
                

        let calculate = 
            state {
                do! add 2
                do! mul 10
                do! div 5
                do! subtract 8
                return "done" }
                
        let resutl, finalState = run calculate (4,[])
        
                                        

    module SampleFunctions = 
        let wrapText s state = 
            sprintf "## %s ##" s, state  
        let getNumber m state  = 
            let rnd = Random()
            rnd.Next(m) |> string, state 
    

    
    
    
    