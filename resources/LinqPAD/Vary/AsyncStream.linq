<Query Kind="FSharpExpression" />

 
/// Represents a sequence of values 'T where items 
/// are generated asynchronously on-demand
type AsyncStream = Async<AsyncStreamCont> 
and AsyncStreamCont =
  | Ended
  | Nuffer of byte[] * AsyncStream


let readInBlocks fn size = async {
  let stream = File.OpenRead(fn)
  let buffer = Array.zeroCreate size
  
  /// Returns next block as 'Item' of async seq
  let rec nextBlock() = async {
    let! count = stream.AsyncRead(buffer, 0, size)
    if count = 0 then return Ended
    else 
      // Create buffer with the right size
      let res = 
        if count = size then buffer
        else buffer |> Seq.take count |> Array.ofSeq
      return Item(res, nextBlock()) }
 
  return! nextBlock() }
 
/// Asynchronous function that compares two asynchronous sequences
/// item by item. If an item doesn't match, 'false' is returned
/// immediately without generating the rest of the sequence. If the
/// lengths don't match, exception is thrown.
let rec compareAsyncSeqs seq1 seq2 = async {
  let! item1 = seq1
  let! item2 = seq2
  match item1, item2 with 
  | Item(b1, ns1), Item(b2, ns2) when b1 <> b2 -> return false
  | Item(b1, ns1), Item(b2, ns2) -> return! compareAsyncSeqs ns1 ns2
  | Ended, Ended -> return true
  | _ -> return failwith "Size doesn't match" }
 
/// Compare two files using 1k blocks
let s1 = readInBlocks "f1" 1000
let s2 = readInBlocks "f2" 1000
compareAsyncSeqs s1 s2
 