namespace FSharp.Parallelx.CollectionEx

module Unrolled =
    
  type 'a node = {
    mutable next : 'a node option
    mutable count : int
    mutable items : 'a array
  }

  type unrolled_linked_list<'a> = {
    node_capacity : int
    mutable length : int
    mutable first : 'a node option
    mutable last : 'a node option
  }
    
  type 'a t = 'a unrolled_linked_list

  let make<'a> node_capacity =
    {   node_capacity = node_capacity 
        length = 0
        first = Option<'a node>.None
        last = Option<'a node>.None
    }

  let length (xs:'a t) = xs.length
    
  let iter (f:'a -> unit) (xs:'a t) =
    let rec next = function
      | Some node ->
        for i = 0 to node.count-1 do
          f node.items.[i]
        next node.next
      | None -> ()
    next xs.first

  let findi (f:'a -> bool) (xs:'a t) =
    let rec next count = function
      | Some node ->
        let i = ref 0
        while !i < node.count && not (f node.items.[!i]) do
          incr i
        if !i = node.count then next (count+node.count) node.next
        else count + !i
      | None -> -1
    next 0 xs.first
      
  let get (xs:'a t) (index:int) =
    let rec find count = function
      | None -> failwith "Outside bounds"
      | Some node ->
        if index < count + node.count then node.items.[count-index]
        else find (count + node.count) node.next
    find 0 xs.first      
      
  let set (xs:'a t) (index:int) (value:'a) =
    let rec find count = function
      | None -> failwith "Outside bounds"
      | Some node ->
        if index < count + node.count then node.items.[count-index] <- value
        else find (count+node.count) node.next
    find 0 xs.first

  let to_array (xs:'a t) =
    let len = length xs
    if len = 0 then [||]
    else (
      let ar = Array.create len (get xs 0)
      let rec copy index = function
        | None -> ()
        | Some node ->
          Array.blit node.items 0 ar index node.count
          copy (index + node.count) node.next
      copy 0 xs.first
      ar
    )
      
  let add (xs:'a t) (x:'a) =
    let new_node (x:'a) =
      let items = Array.create xs.node_capacity x
      { next = None
        count = 1
        items = items }

    match xs.last with
    | None ->
      let node = new_node x
      xs.first <- Some node
      xs.last <- Some node
    | Some node ->
      if node.count = xs.node_capacity then
          let next = new_node x
          node.next <- Some next
          xs.last <- Some next
      else
          node.items.[node.count] <- x
          node.count <- node.count + 1
      
    xs.length <- xs.length + 1
    
  let split (xs:'a t) node offset =
    let split = xs.node_capacity / 2
    match node.next with
    | Some next when next.count = split && offset < split ->
      (* push excess to next node *)
      Array.blit next.items 0 next.items next.count split
      Array.blit node.items split next.items 0 split
      next.count <- xs.node_capacity
      node.count <- node.count - split
      node, offset
    | Some _ | None ->
      (* insert new node to right *)
      let items = Array.create xs.node_capacity node.items.[0]
      let new_node =
        { next = node.next
          count = split
          items = items }
      Array.blit node.items split new_node.items 0 split
      node.next <- Some new_node
      node.count <- split
      match new_node.next with
      | None -> xs.last <- Some new_node
      | Some _ -> ()      
      if offset < node.count then node, offset
      else new_node, offset - node.count    
      
  let insert_left (xs:'a t) (index:int) (x:'a) =
    let rec find count = function
      | None -> failwith "Outside bounds"
      | Some node ->
        if (index - count) <= node.count then node, index - count
        else find (count+node.count) node.next
    let node, offset = find 0 xs.first
    let node, offset =
      if node.count = xs.node_capacity then
        split xs node offset
      else
        node, offset
    (* insert item in node *)
    Array.blit node.items offset node.items (offset+1) (node.count-offset)
    node.items.[offset] <- x
    node.count <- node.count + 1
    xs.length <- xs.length + 1

  let insert (xs:'a t) (index:int) (x:'a) =
    if index = xs.length then add xs x
    else insert_left xs index x

  let delete (xs:'a t) (index:int) =
    let rec find count previous node' =
      match node' with
      | None -> failwith "Outside bounds"
      | Some node ->
        if index - count < node.count then previous, node, index - count
        else find (count+node.count) node' node.next
    let previous, node, offset = find 0 None xs.first
    Array.blit node.items (offset+1) node.items offset (node.count-offset-1)
    node.count <- node.count - 1
    xs.length <- xs.length - 1
    let split = xs.node_capacity / 2
    (* Pull from right *)
    if node.count < split then
      match node.next with
      | None ->
        if node.count = 0 then
          match previous with
          | None ->
              xs.first <- None
              xs.last <- None
          | Some previous' ->
              previous'.next <- None
              xs.last <- previous
      | Some next ->
        if next.count > split then
          let excess = next.count - split
          Array.blit next.items 0 node.items node.count excess
          Array.blit next.items excess next.items 0 excess
          node.count <- node.count + excess
          next.count <- next.count - excess
        else
          Array.blit next.items 0 node.items node.count next.count
          node.count <- node.count + next.count
          node.next <- next.next
          match node.next with
          | None -> xs.last <- Some node
          | Some _ -> ()
