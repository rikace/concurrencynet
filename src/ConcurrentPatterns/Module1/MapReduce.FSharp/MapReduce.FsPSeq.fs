namespace MapReduce

module MapReduceFsPSeq =

    open ParallelSeq
    open System.Linq

    //  Implementation of mapF function for the first phase of the MapReduce pattern
    let mapF  M (map:'in_value -> seq<'out_key * 'out_value>)
                (inputs:seq<'in_value>) =
        // TODO
        // Complete the map function "mapF"
        // so that we match the signature
        // int -> ('in_value -> seq<'out_key * 'out_value>) -> seq<'in_value> -> ('out_key * IEnumerable<'out_key * 'out_value>) list
        //
        // with the output as: ('out_key * IEnumerable<'out_key * 'out_value>) list
        // Note: use the PSeq to leverage the underlying PLINQ
        inputs
        // Code missing here
        |> PSeq.toList

    //  Implementation of reduceF function for the second phase of the MapReduce pattern
    let reduceF  R (reduce:'key -> seq<'value> -> 'reducedValues)
                   (inputs:('key * seq<'key * 'value>) seq) =
        // TODO
        // Complete the reduce function "reduceF"
        // so that we match the signature
        // int -> ('key -> seq<'value> -> 'reducedValues) -> seq<'key * seq<'key * 'value>> -> 'reducedValues list
        //
        // with the output as: 'reducedValues list
        // Note: use the PSeq to leverage the underlying PLINQ
        inputs
        // Code missing here
        |> PSeq.toList

    //  Implementation of the MapReduce pattern composing the mapF and reduce functions
    let mapReduce
            (inputs:seq<'in_value>)
            (map:'in_value -> seq<'out_key * 'out_value>)
            (reduce:'out_key -> seq<'out_value> -> 'reducedValues)
            M R =

        // TODO LAB
        // Complete the map reduce composing the function "mapF" and "reduceF"
        // suggestion, use the ">>" composition operator
        // inputs |> // compose map and reduce here
          //        (id) // <= remove this after implementation

        // Code missing here
        []
