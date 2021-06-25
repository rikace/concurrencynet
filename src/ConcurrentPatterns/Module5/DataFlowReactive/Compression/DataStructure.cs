namespace DataFlowPipeline.Compression
{
    public struct CompressingDetails
    {
        public ChunkBytes Data { get; set; }
        public int Sequence { get; set; }
        public ChunkBytes ChunkSize { get; set; }
    }

    public struct CompressedDetails
    {
        public ChunkBytes Data { get; set; }
        public int Sequence { get; set; }
        public ChunkBytes ChunkSize { get; set; }
        public ChunkBytes CompressedDataSize { get; set; }
        public bool IsProcessed { get; set; }
    }
    public struct EncryptDetails
    {
        public ChunkBytes Data { get; set; }
        public int Sequence { get; set; }
        public ChunkBytes EncryptedDataSize { get; set; }
        public bool IsProcessed { get; set; }
    }

    public struct DecompressionDetails
    {

        public ChunkBytes Data { get; set; }
        public int Sequence { get; set; }
        public long ChunkSize { get; set; }
        public bool IsProcessed { get; set; }
    }
    public struct DecryptDetails
    {
        public ChunkBytes Data { get; set; }
        public int Sequence { get; set; }
        public ChunkBytes EncryptedDataSize { get; set; }
    }
}
