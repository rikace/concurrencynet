﻿module BTree

// Immutable B-tree representation in F#
type Tree<'a> = 
    | Empty     
    | Node of leaf:'a * left:Tree<'a> * right:Tree<'a> 

let tree =      
    Node (20,
        Node (9, Node (4, Node (2, Empty, Empty), Empty),
                 Node (10, Empty, Empty)),
        Empty)

// B-tree helper recursive functions
let rec contains item tree =
    match tree with
    | Empty -> false
    | Node(leaf, left, right) ->
        if leaf = item then true
        elif item < leaf then contains item left
        else contains item right

let rec insert item tree = 
    match tree with
    | Empty -> Node(item, Empty, Empty)
    | Node(leaf, left, right) as node ->
        if leaf = item then node
        elif item < leaf then Node(leaf, insert item left, right)
        else Node(leaf, left, insert item right)

let ``exist 9`` = tree |> contains 9
let ``tree 21`` = tree |> insert 21
let ``exist 21`` = ``tree 21`` |> contains 21

// In-Order navigation function
let rec inorder action tree = 
    seq {
        match tree with
        | Node(leaf, left, right) ->
            yield! inorder action left
            yield action leaf
            yield! inorder action right
        | Empty -> ()
    }

tree |> inorder (fun n -> printfn "%d" n) |> ignore 
