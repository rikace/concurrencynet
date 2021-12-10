namespace FSharp.Parallelx

module NetStream =

    open System
    open System.Net
    open System.Net.Sockets
    open System.IO
    open System.Threading
    open System.Threading.Tasks

    open Microsoft.FSharp.Control

    type AsyncBuilder with
        member __.Bind(f : Task<'T>, g : 'T -> Async<'S>) = __.Bind(Async.AwaitTask f, g)
        member __.Bind(f : Task, g : unit -> Async<'S>) = __.Bind(f.ContinueWith ignore, g)

    type Stream with
        member s.AsyncWriteBytes (bytes : byte []) =
            async {
                do! s.WriteAsync(BitConverter.GetBytes bytes.Length, 0, 4)
                do! s.WriteAsync(bytes, 0, bytes.Length)
                do! s.FlushAsync()
            }

        member s.AsyncReadBytes(length : int) =
            let rec readSegment buf offset remaining =
                async {
                    let! read = s.ReadAsync(buf, offset, remaining)
                    if read < remaining then
                        return! readSegment buf (offset + read) (remaining - read)
                    else
                        return ()
                }

            async {
                let bytes = Array.zeroCreate<byte> length
                do! readSegment bytes 0 length
                return bytes
            }

        member s.AsyncReadBytes() =
            async {
                let! lengthArr = s.AsyncReadBytes 4
                let length = BitConverter.ToInt32(lengthArr, 0)
                return! s.AsyncReadBytes length
            }

    // existentially pack reply channels

    type private IReplyChannelContainer<'T> =
        abstract PostWithReply : MailboxProcessor<'T> -> Async<obj>

    and private ReplyChannelContainer<'T, 'R>(msgB : AsyncReplyChannel<'R> -> 'T) =
        interface IReplyChannelContainer<'T> with
            member __.PostWithReply (mb : MailboxProcessor<'T>) = async {
                let! r = mb.PostAndAsyncReply msgB
                return r :> obj
            }
