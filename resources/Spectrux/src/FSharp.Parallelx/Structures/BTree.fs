namespace FSharp.Parallelx.CollectionEx

module Tree =

  type BTree<'T> = Node of 'T * list<BTree<'T>>

  [<RequireQualifiedAccess>]
  module BTree =
    /// Takes all elements at the specified level and turns them into nodes
    let rec private takeAtLevel indent tail =
      match tail with
      | (i, value)::tail when i >= indent ->  // >= instead of = to handle odd cases
        let nested, tail = takeDeeperThan i tail
        let following, tail = takeAtLevel indent tail
        Node(value, nested) :: following, tail
      | tail -> [], tail

    /// Takes elements that are deeper (children) and turns them into nodes
    and private takeDeeperThan indent tail =
      match tail with
      | (i, value)::tail when i > indent ->
        let nested, tail = takeDeeperThan i tail
        let following, tail = takeAtLevel i tail
        Node(value, nested) :: following, tail
      | tail -> [], tail

    /// Turns a list of items with an indentation specified by an integer
    /// into a tree where indented items are children.
    let ofIndentedList input =
      let res, tail = takeAtLevel 0 input
      if tail <> [] then failwith "Wrong indentation"
      res

  type Tree<'a> =
  | Empty
  | Node of 'a * Tree<'a> * Tree<'a>
  
  [<RequireQualifiedAccess>]
  module Tree =
    
      let printTree t =
          let rec print (t:Tree<'a>) level  =
              //let indent = String.replicate (level*4) " "
              let indent = System.String(' ',  (level * 4)) |> string
              match t with
              | Empty -> printfn "%s %s" indent "<>"
              | Node (a,l,r) ->
                  printfn "%sNode: %d" indent a
                  print l (level+1)
                  print r (level+1)
          print t 0
    
      let rec fold f accu = function
          | Empty -> accu
          | Node(l, v, r) -> fold f (f (fold f accu v) l) r
    
    
          // let rec map f = function
          //     | EmptyStack -> EmptyStack
          //     | StackNode(hd, tl) -> StackNode(f hd, map f tl)
//      let tree = Node(4, Node(2, Node(1, Empty,
//                                         Empty),
//                                 Node(3, Empty,
//                                         Empty)),
//                         Node(6, Node(5, Empty,
//                                         Empty),
//                                 Node(7, Empty,
//                                         Empty)))
//      let ctree = fold (fun a b -> b + a) 0 tree
    
      // Inserting elements without balancing
      let rec mapT f tree =
          match tree with
          | Empty -> Empty
          | Node(v, l, r) -> Node(f v, mapT f l, mapT f r)
    
      let rec collectV tree acc =
          match tree with
          | Empty -> acc
          | Node(v, l, r) -> collectV r (collectV l (v::acc))
    
      // collectV tree []
    
  type CompositeNode<'T> =
  | Node of 'T
  | Tree of 'T * CompositeNode<'T> * CompositeNode<'T>
    with
        member this.InOrder f =
            match this with
            | Tree(n, left, right) ->
                left.InOrder(f)
                f(n)
                right.InOrder(f)
            | Node(n) -> f(n)
        member this.PreOrder f =
            match this with
            | Tree(n, left, right) ->
                f(n)
                left.PreOrder(f)
                right.PreOrder(f)
            | Node(n) -> f(n)
        member this.PostOrder f =
            match this with
            | Tree(n, left, right) ->
                left.PostOrder(f)
                right.PostOrder(f)
                f(n)
            | Node(n) -> f(n)