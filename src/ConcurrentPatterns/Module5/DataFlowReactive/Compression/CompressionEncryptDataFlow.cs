using ReactiveAgent.CS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Runtime;
using ReactiveAgent.Agents;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using DataFlowPipeline.Compression;
using CompressedDetails =DataFlowPipeline.Compression.CompressedDetails;
using CompressingDetails = DataFlowPipeline.Compression.CompressingDetails;
using DecompressionDetails = DataFlowPipeline.Compression.DecompressionDetails;
using DecryptDetails = DataFlowPipeline.Compression.DecryptDetails;
using EncryptDetails = DataFlowPipeline.Compression.EncryptDetails;

namespace ParallelCompressionCS
{
    public static class CompressionAndEncryptDataFlow
    {
        //   Producer/consumer using TPL Dataflow
        public static async Task CompressAndEncrypt(
                                    Stream streamSource, Stream streamDestination,
                                    int maxDegreeOfParallelism = 4,
                                    long chunkSize = 1048576, CancellationTokenSource cts = null)
        {
            cts = cts ?? new CancellationTokenSource();

            var compressorOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                BoundedCapacity = 20,
                CancellationToken = cts.Token
            };

            var inputBuffer = new BufferBlock<CompressingDetails>(
                                    new DataflowBlockOptions
                                    {
                                        CancellationToken = cts.Token,
                                        BoundedCapacity = 20
                                    });

            var compressor = new TransformBlock<CompressingDetails, CompressedDetails>(
                async details => {
                    var compressedData = await IOUtils.Compress(details.Data);
                    return details.ToCompressedDetails(compressedData);
                }, compressorOptions);

            var encryptor = new TransformBlock<CompressedDetails, EncryptDetails>(
                async details => {
                    var data = IOUtils.CombineByteArrays(details.CompressedDataSize, details.ChunkSize, details.Data);
                    var encryptedData = await IOUtils.Encrypt(data);
                    return details.ToEncryptDetails(encryptedData);
                }, compressorOptions);

            var asOrderedAgent = Agent.Start((new Dictionary<int, EncryptDetails>(), 0),
                async ((Dictionary<int, EncryptDetails>, int) state, EncryptDetails msg) => {
                    (Dictionary<int, EncryptDetails> details, int lastIndexProc) = state;
                    details.Add(msg.Sequence, msg);
                    while (details.ContainsKey(lastIndexProc+1))
                    {
                        msg = details[lastIndexProc + 1];
                        await streamDestination.WriteAsync(msg.EncryptedDataSize.Bytes, 0, msg.EncryptedDataSize.Length);
                        await streamDestination.WriteAsync(msg.Data.Bytes, 0, msg.Data.Length);
                        lastIndexProc = msg.Sequence;
                        details.Remove(lastIndexProc);
                    }
                    return (details, lastIndexProc);
                }, cts);

            // TODO 5.7
            // create write DataFlow block that push to the asOrderedAgent agent
            // the messages EncryptDetails received from the encryptor block

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            // TODO 5.7
            // Link the DataFlow block to compress, encrypt and the safe the data file.

            long sourceLength = streamSource.Length;
            byte[] size = BitConverter.GetBytes(sourceLength);
            await streamDestination.WriteAsync(size, 0, size.Length);

            chunkSize = Math.Min(chunkSize, sourceLength);
            int indexSequence = 0;
            while (sourceLength > 0)
            {
                byte[] data = new byte[chunkSize];
                int readCount = await streamSource.ReadAsync(data, 0, data.Length);
                byte[] bytes = new byte[readCount];
                Buffer.BlockCopy(data, 0, bytes, 0, readCount);
                var compressingDetails = new CompressingDetails
                {
                    Data =new ChunkBytes(bytes),
                    ChunkSize = new ChunkBytes(readCount),
                    Sequence = ++indexSequence
                };
                await inputBuffer.SendAsync(compressingDetails);
                sourceLength -= readCount;
                if (sourceLength < chunkSize)
                    chunkSize = sourceLength;
                if (sourceLength == 0)
                    inputBuffer.Complete();
            }
            await inputBuffer.Completion.ContinueWith(task => compressor.Complete());
            await compressor.Completion.ContinueWith(task => encryptor.Complete());

            await streamDestination.FlushAsync();
        }



        public static async Task DecryptAndDecompress(
                                    Stream streamSource, Stream streamDestination,
                                    int maxDegreeOfParallelism = 4,
                                    int bufferSize = 1048576, CancellationTokenSource cts = null)
        {
            cts = cts ?? new CancellationTokenSource();

            var compressorOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cts.Token,
                BoundedCapacity = 20
            };

            var inputBuffer = new BufferBlock<DecryptDetails>(
                                    new DataflowBlockOptions {
                                        CancellationToken = cts.Token,
                                        BoundedCapacity = 20
                                    });

            var decryptor = new TransformBlock<DecryptDetails, DecompressionDetails>(
                async details =>
                {
                    var decryptedData = await IOUtils.Decrypt(details.Data);
                    return new DecompressionDetails
                    {
                        Data = decryptedData,
                        Sequence = details.Sequence,
                    };
                }, compressorOptions);

            var decompressor = new TransformBlock<DecompressionDetails, DecompressionDetails>(
                async details =>
                {
                    byte[] compressedDataSize = details.Data.Bytes.Take(sizeof(int)).ToArray();
                    byte[] chunkSize = details.Data.Bytes.Skip(sizeof(int)).Take(sizeof(int)).ToArray();
                    int chunkLong = BitConverter.ToInt32(chunkSize, 0);
                    byte[] decompressedData = await IOUtils.Decompress(details.Data.Bytes.Skip(sizeof(int) + sizeof(int)).ToArray());
                    return new DecompressionDetails
                    {
                        Data = new ChunkBytes(decompressedData),
                        Sequence = details.Sequence
                    };
                }, compressorOptions);

            var asOrderedAgent = Agent.Start((new Dictionary<int, DecompressionDetails>(), 0),
                async ((Dictionary<int, DecompressionDetails>, int) state, DecompressionDetails msg) => {
                    (Dictionary<int, DecompressionDetails> details, int lastIndexProc) = state;
                    details.Add(msg.Sequence, msg);
                    while (details.ContainsKey(lastIndexProc + 1))
                    {
                        msg = details[lastIndexProc + 1];
                        await streamDestination.WriteAsync(msg.Data.Bytes, 0, msg.Data.Length);
                        lastIndexProc = msg.Sequence;
                        details.Remove(lastIndexProc);
                    }
                    return (details, lastIndexProc);
                }, cts);

            var writer = new ActionBlock<DecompressionDetails>(async details =>
                await asOrderedAgent.Send(details), compressorOptions);


            var linkOptions = new DataflowLinkOptions() { PropagateCompletion = true };
            inputBuffer.LinkTo(decryptor, linkOptions);
            decryptor.LinkTo(decompressor, linkOptions);
            decompressor.LinkTo(writer, linkOptions);

            GCSettings.LargeObjectHeapCompactionMode =
                GCLargeObjectHeapCompactionMode.CompactOnce;

            byte[] size = new byte[sizeof(long)];
            await streamSource.ReadAsync(size, 0, size.Length);
            // convert the size to number
            long destinationLength = BitConverter.ToInt64(size, 0);
            streamDestination.SetLength(destinationLength);
            long sourceLength = streamSource.Length - sizeof(long);

            int index = 0;
            while (sourceLength > 0)
            {
                // read the encrypted chunk size
                size = new byte[sizeof(int)];
                int sizeReadCount = await streamSource.ReadAsync(size, 0, size.Length);

                // convert the size back to number
                int storedSize = BitConverter.ToInt32(size, 0);

                byte[] encryptedData = new byte[storedSize];
                int readCount = await streamSource.ReadAsync(encryptedData, 0, encryptedData.Length);

                var decryptDetails = new DecryptDetails
                {
                    Data = new ChunkBytes(encryptedData),
                    EncryptedDataSize = new ChunkBytes(size),
                    Sequence = ++index
                };

                await inputBuffer.SendAsync(decryptDetails);

                sourceLength -= (sizeReadCount + readCount);
                if (sourceLength == 0)
                    inputBuffer.Complete();
            }
            await inputBuffer.Completion.ContinueWith(task => decryptor.Complete());
            await decryptor.Completion.ContinueWith(task => decompressor.Complete());
            await decompressor.Completion.ContinueWith(task => writer.Complete());
            await writer.Completion;

            await streamDestination.FlushAsync();
        }

        public static async Task CompressAndEncryptObs(
                                    Stream streamSource, Stream streamDestination,
                                    int bufferSize = 1048576, long chunkSize = 1048576, CancellationTokenSource cts = null)
        {
            cts = cts ?? new CancellationTokenSource();

            var compressorOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = 20,
                CancellationToken = cts.Token
            };

            var inputBuffer = new BufferBlock<CompressingDetails>(
                new DataflowBlockOptions { BoundedCapacity = 20, CancellationToken = cts.Token });

            var compressor = new TransformBlock<CompressingDetails, CompressedDetails>(async details =>
            {
                // get the bytes for the compressed chunk
                var compressedData = await IOUtils.Compress(details.Data);
                return details.ToCompressedDetails(compressedData);

            }, compressorOptions);

            var encryptor = new TransformBlock<CompressedDetails, EncryptDetails>(async details =>
            {
                // copy out the chunk size, the compressed size and the compressed chunk
                var data = IOUtils.CombineByteArrays(details.CompressedDataSize, details.ChunkSize, details.Data);
                var encryptedData = await IOUtils.Encrypt(data);
                return details.ToEncryptDetails(encryptedData);
            }, compressorOptions);

            var linkOptions = new DataflowLinkOptions() { PropagateCompletion = true };

            //   Producer / consumer using TPL Dataflow
            inputBuffer.LinkTo(compressor, linkOptions);
            compressor.LinkTo(encryptor, linkOptions);

            var obs = encryptor
                .AsObservable()
                .Scan((new Dictionary<int, EncryptDetails>(), 0),
                (state, msg) => System.Reactive.Linq.Observable.FromAsync(async () =>
                {
                    (Dictionary<int, EncryptDetails> details, int lastIndexProc) = state;
                    details.Add(msg.Sequence, msg);
                    while (details.ContainsKey(lastIndexProc + 1))
                    {
                        msg = details[lastIndexProc + 1];
                        await streamDestination.WriteAsync(msg.EncryptedDataSize.Bytes, 0, msg.EncryptedDataSize.Length);
                        await streamDestination.WriteAsync(msg.Data.Bytes, 0, msg.Data.Length);
                        lastIndexProc = msg.Sequence;
                        details.Remove(lastIndexProc);
                    }
                    return (details, lastIndexProc);
                }).SingleAsync().Wait());
            obs
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe();


            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            long sourceLength = streamSource.Length;
            // Write total size to destination
            byte[] size = BitConverter.GetBytes(sourceLength);
            await streamDestination.WriteAsync(size, 0, size.Length);

            // read 1MB chunks and compress them
            chunkSize = Math.Min(chunkSize, sourceLength); // 1 MB
            int indexSequence = 0; // index to track the sequence
            while (sourceLength > 0)
            {
                // read the chunk
                byte[] data = new byte[chunkSize];
                int readCount = await streamSource.ReadAsync(data, 0, data.Length);
                byte[] bytes = new byte[readCount];
                Buffer.BlockCopy(data, 0, bytes, 0, readCount);
                var compressingDetails = new CompressingDetails
                {
                    Data = new ChunkBytes(bytes),
                    ChunkSize = new ChunkBytes(readCount),
                    Sequence = ++indexSequence
                };

                await inputBuffer.SendAsync(compressingDetails);

                // subtract the chunk size from the file size
                sourceLength -= chunkSize;
                // if chunk is less than remaining file use
                // remaining file
                if (sourceLength < chunkSize)
                    chunkSize = sourceLength;
                if (sourceLength == 0)
                    inputBuffer.Complete();
            }

            await obs;

            // create a continuation task that marks the next block in the pipeline as completed.
            await inputBuffer.Completion.ContinueWith(task => compressor.Complete());
            await compressor.Completion.ContinueWith(task => encryptor.Complete());
            await encryptor.Completion;

            await streamDestination.FlushAsync();
        }
    }
}
