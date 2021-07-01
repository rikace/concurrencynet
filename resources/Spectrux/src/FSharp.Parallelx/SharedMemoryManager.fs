namespace FSharp.Parallelx

module SharedMemoryManager =
    open System
    open System.Threading
    open System.IO
    open System.Runtime.InteropServices
    open Microsoft.FSharp.NativeInterop

    module PInvokeHelper =
    
        type HANDLE = nativeint
        type ADDR = nativeint
    
        type PageProtection =
                | NoAccess = 0x01
                | Readonly = 0x02
                | ReadWrite = 0x04
                | WriteCopy = 0x08
                | Execute = 0x10
                | ExecuteRead = 0x20
                | ExecuteReadWrite = 0x40
                | ExecuteWriteCopy = 0x80
                | Guard = 0x100
                | NoCache = 0x200
                | WriteCombine = 0x400
    
        [<DllImport("kernel32", SetLastError = true)>]
        extern bool CloseHandle(HANDLE handler)
    
        [<DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)>]
        extern HANDLE CreateFile(string lpFileName,
                                 int dwDesiredAccess,
                                 int dwShareMode,
                                 HANDLE lpSecurityAttributes,
                                 int dwCreationDisposition,
                                 int dwFlagsAndAttributes,
                                 HANDLE hTemplateFile)
    
    //    [<DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)>]
    //    extern HANDLE CreateFileMapping(HANDLE hFile,
    //                                    HANDLE lpAttributes,
    //                                    int flProtect,
    //                                    int dwMaximumSizeLow,
    //                                    int dwMaximumSizeHigh,
    //                                    string lpName)
    
        [<DllImport("kernel32.dll", SetLastError = true)>]
        extern IntPtr CreateFileMapping(IntPtr hFile,
                IntPtr lpFileMappingAttributes, PageProtection flProtect,
                uint32 dwMaximumSizeHigh,
                uint32 dwMaximumSizeLow, string lpName);
    
    
        [<DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)>]
        extern ADDR MapViewOfFile(HANDLE hFileMappingObject,
                                  int dwDesiredAccess,
                                  int dwFileOffsetLow,
                                  int dwNumBytesToMap,
                                  HANDLE dwNumberOfBytesToMap)
    
        [<DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)>]
        extern HANDLE OpenFileMapping(int dwDesiredAccess,
                                      bool bInheritHandle,
                                      string lpName)
    
        [<DllImport("kernel32", SetLastError = true)>]
        extern bool UnmapViewOfFile(ADDR lpBaseAddress)
    
        let INVALID_HANDLE = new IntPtr(-1)
        let MAP_READ = 0x0004
        let GENERIC_READ = 0x80000000
        let NULL_HANDLE = IntPtr.Zero
        let FILE_SHARE_NONE = 0x0000
        let FILE_SHARE_READ = 0x0001
        let FILE_SHARE_WRITE = 0x0002
        let FILE_SHARE_READ_WRITE = 0x0003
        let CREATE_ALWAYS = 0x0002
        let OPEN_EXISTING = 0x0003
        let OPEN_ALWAYS = 0x0004
        let READONLY = 0x0000000
    
    
    
    module SharedMemoryManager =
    
        #nowarn "9"
        open System
        open System.Threading
        open System.IO
        open System.Runtime.InteropServices
        open Microsoft.FSharp.NativeInterop
        open PInvokeHelper
        open System.ComponentModel
        open System.Runtime.Serialization
        open System.Runtime.Serialization.Formatters.Binary
        open System.ComponentModel
        open System.IO.MemoryMappedFiles
        open System.IO.Pipes
        open System.Net
    
    
        [<InterfaceAttribute>]
        type ISharedMemory<'T> =
            interface
                abstract SharedMemoryBaseSize :int with get
                abstract Close : unit -> unit
                abstract Send : 'T -> unit
                abstract Receive : unit -> 'T
                inherit IDisposable
            end
    
        type SharedMemoryManager<'T>(name, sharedMemorySize) =
            do
                if String.IsNullOrEmpty name then raise <| new Exception("")
                if sharedMemorySize <= 0 then raise <| Exception("Shared memory size is not valid")
    
            let [<LiteralAttribute>] INVALID_HANDLE_VALUE = -1
            let [<LiteralAttribute>] FILE_MAP_WRITE = 0x0002
            let [<LiteralAttribute>] ERROR_ALREADY_EXISTS = 183
    
            let mutable disposed = false
            let memoryRegion = name
            let memRegionSize = uint32 (sharedMemorySize + sizeof<int>)
    
            let (ptrToMemory :ADDR ref, handleFileMapping: HANDLE ref, mtxSharedMutex) =
                let handleFileMapping = PInvokeHelper.CreateFileMapping(PInvokeHelper.INVALID_HANDLE,
                                                                        IntPtr.Zero,
                                                                        PInvokeHelper.PageProtection.ReadWrite,
                                                                        uint32 0,
                                                                        uint32 memRegionSize,
                                                                        memoryRegion)
    
                if handleFileMapping = IntPtr.Zero then raise <| new Win32Exception("Could not create file mapping")
    
                let retVal = Marshal.GetLastWin32Error()
                let mtxSharedMutex =
                    if retVal = ERROR_ALREADY_EXISTS then new Mutex(false, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                    elif retVal = 0 then new Mutex(true, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                    else raise <| new Win32Exception(retVal, "Error creating file mapping")
    
                let ptrToMemory = PInvokeHelper.MapViewOfFile(handleFileMapping, FILE_MAP_WRITE, 0, 0, IntPtr.Zero)
    
                if ptrToMemory = IntPtr.Zero then
                    let retVal = Marshal.GetLastWin32Error()
                    raise <| new Win32Exception(retVal, "Could not map file view")
    
                let retVal = Marshal.GetLastWin32Error()
                if retVal <> 0 && retVal <> ERROR_ALREADY_EXISTS then
                    raise <| new Win32Exception(retVal, "Error mapping file view")
    
                (ref ptrToMemory, ref handleFileMapping, mtxSharedMutex)
    
            let closeSharedMemory() =
                if !ptrToMemory <> IntPtr.Zero then
                    PInvokeHelper.UnmapViewOfFile(!ptrToMemory) |> ignore
                    ptrToMemory := IntPtr.Zero
    
                if !handleFileMapping <> IntPtr.Zero then
                    PInvokeHelper.CloseHandle(!handleFileMapping) |> ignore
                    handleFileMapping := IntPtr.Zero
    
            let dispose disposing =
                if not <| disposed then
                        closeSharedMemory()
                disposed <- true
    
            interface ISharedMemory<'T> with
    
                member x.SharedMemoryBaseSize = sharedMemorySize
                member x.Close() = closeSharedMemory()
    
                member x.Send(item:'T) =
                    use ms = new MemoryStream()
                    let formatter = new BinaryFormatter()
    
                    try
                        formatter.Serialize(ms, item)
                        let bytes = ms.ToArray()
    
                        if bytes.Length + sizeof<int> > int memRegionSize then
                            raise <| new ArgumentException("Object is too large to share in memory")
    
                        Marshal.WriteInt32(!ptrToMemory, bytes.Length)
                        Marshal.Copy(bytes, 0, !ptrToMemory, bytes.Length)
                    finally
                        mtxSharedMutex.ReleaseMutex() |> ignore
                        mtxSharedMutex.WaitOne() |> ignore
    
                member x.Receive() : 'T =
                    mtxSharedMutex.WaitOne() |> ignore
                    let count = Marshal.ReadInt32(!ptrToMemory)
    
                    if count <= 0 then
                        raise <| new InvalidDataException("No object to read")
    
                    let bytes = Array.zeroCreate<byte> count
                    Marshal.Copy(!ptrToMemory, bytes, 0, count)
    
                    use ms = new MemoryStream(bytes)
                    let formatter = new BinaryFormatter()
    
                    try
                        let item = formatter.Deserialize(ms) :?> 'T
                        item
                    finally
                        mtxSharedMutex.ReleaseMutex()
               // interface IDisposable with
    
                member x.Dispose() =
                            dispose(true)
                            GC.SuppressFinalize(x)
    
            override x.Finalize() = dispose (false)
    
        type SharedMemoryMappedManager<'T>(name, sharedMemorySize) =
            do
                if String.IsNullOrEmpty name then raise <| new Exception("")
                if sharedMemorySize <= 0 then raise <| Exception("Shared memory size is not valid")
            let mutable disposed = false
            let memoryRegion = name
            let memRegionSize = uint32 (sharedMemorySize + sizeof<int>)
    
            let log msg = printfn msg
    
            let mmf = MemoryMappedFile.CreateOrOpen(memoryRegion, int64 sharedMemorySize)
            let accessor = mmf.CreateViewAccessor(0L, int64 sharedMemorySize, MemoryMappedFileAccess.ReadWrite)
    
           // let mtxSharedMutex = new Mutex(true, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)//, ref locked)
            let mtxSharedMutex =
                let mutable mtxSharedMutex = new Mutex(true, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                try
                    if Mutex.TryOpenExisting(sprintf "%smtx%s" (string typeof<'T>) memoryRegion,ref mtxSharedMutex) then
                            new Mutex(false, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                    else
                            new Mutex(true, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                finally
                    mtxSharedMutex.Dispose()
    
            let closeSharedMemory() =
                accessor.Dispose()
                mmf.Dispose()
                mtxSharedMutex.Dispose()
    
            let dispose disposing =
                if not <| disposed then
                        closeSharedMemory()
                        log "disposing"
                disposed <- true
    
            interface ISharedMemory<'T> with
    
                member x.SharedMemoryBaseSize = int sharedMemorySize
                member x.Close() = closeSharedMemory()
    
                member x.Send(item:'T) =
                    use ms = new MemoryStream()
                    let formatter = new BinaryFormatter()
    
                    log "Sending.."
    
                    try
                        formatter.Serialize(ms, item)
                        let bytes = ms.ToArray()
    
                        log "bytes array len %d" bytes.Length
    
                        accessor.Write(0L, int64 bytes.Length)
                        accessor.Flush()
                        if bytes.Length + sizeof<int> > int memRegionSize then
                            raise <| new ArgumentException("Object is too large to share in memory")
    
                        log "write item"
                        accessor.WriteArray<byte>(0L, bytes, 0, bytes.Length)
                        accessor.Flush()
                    finally
                        mtxSharedMutex.ReleaseMutex() |> ignore
                        mtxSharedMutex.WaitOne() |> ignore
    
                member x.Receive() : 'T =
                    mtxSharedMutex.WaitOne() |> ignore
                    let count :int = int( accessor.ReadInt32(0L) )
    
                    log "receive count %d" count
    
                    if count <= 0 then
                        raise <| new InvalidDataException("No object to read")
    
                    let bytes = Array.zeroCreate<byte> count
                    let item = accessor.ReadArray<byte>(0L, bytes, 0, count)
                    log "read from array item size %d" item
    
                    use ms = new MemoryStream(bytes)
                    let formatter = new BinaryFormatter()
    
                    try
                        log "deserializing"
                        let item = formatter.Deserialize(ms) :?> 'T
                        item
                    finally
                        mtxSharedMutex.ReleaseMutex()
               // interface IDisposable with
    
                member x.Dispose() =
                            dispose(true)
                            GC.SuppressFinalize(x)
    
            override x.Finalize() = dispose (false)
    
        type PipeType =
        | Client
        | Server
    
        type SharedMemoryPipeManager<'T>(name, pipeType:PipeType) =
            do
                if String.IsNullOrEmpty name then raise <| new Exception("")
    
            let mutable disposed = false
    
            let pipeClient = new NamedPipeClientStream(".",name, PipeDirection.InOut)
            let pipeServer = new NamedPipeServerStream(name, PipeDirection.InOut, 2)
    
            let closeSharedMemory() =
                if pipeClient.IsConnected then
                    pipeClient.Close()
    
                if pipeServer.IsConnected then
                    pipeServer.Disconnect()
                    pipeServer.Close()
                pipeClient.Dispose()
                pipeServer.Dispose()
    
            let dispose disposing =
                if not <| disposed then
                        closeSharedMemory()
                disposed <- true
    
            interface ISharedMemory<'T> with
    
                member x.SharedMemoryBaseSize = 0
                member x.Close() = closeSharedMemory()
    
                member x.Send(item:'T) =
                    let formatter = new BinaryFormatter()
                    match pipeType with
                    | Client ->
                                if not <| pipeClient.IsConnected then
                                    pipeClient.Connect()
    
                                formatter.Serialize(pipeClient, item)
                                pipeClient.Flush()
                                pipeClient.WaitForPipeDrain()
                    | Server -> if not <| pipeServer.IsConnected then
                                    pipeServer.WaitForConnection()
                                formatter.Serialize(pipeServer, item)
                                pipeServer.Flush()
                                pipeServer.WaitForPipeDrain()
    
    
                member x.Receive() : 'T =
                    let formatter = new BinaryFormatter()
                    match pipeType with
                    | Client ->
                                if not <| pipeClient.IsConnected then
                                    pipeClient.Connect()
                               //pipeClient.ReadMode <- PipeTransmissionMode.Byte
                                formatter.Deserialize(pipeClient) :?> 'T
                    | Server -> if not <| pipeServer.IsConnected then
                                    pipeServer.WaitForConnection()
                                formatter.Deserialize(pipeServer) :?> 'T
    
    
                member x.Dispose() =
                            dispose(true)
                            GC.SuppressFinalize(x)
    
            override x.Finalize() = dispose (false)
    
        type SharedUnmanagedMemoryManager<'T>(name, sharedMemorySize) =
            do
                if String.IsNullOrEmpty name then raise <| new Exception("")
                if sharedMemorySize <= 0 then raise <| Exception("Shared memory size is not valid")
    
            let [<LiteralAttribute>] INVALID_HANDLE_VALUE = -1
            let [<LiteralAttribute>] FILE_MAP_WRITE = 0x0002
            let [<LiteralAttribute>] ERROR_ALREADY_EXISTS = 183
    
            let mutable disposed = false
            let memoryRegion = name
            let memRegionSize = uint32 (sharedMemorySize + sizeof<int>)
    
            let mtxSharedMutex =
                let mutable mtxSharedMutex = new Mutex(true, sprintf "%smtx%s" (string typeof<'T>) memoryRegion)
                try
                    if Mutex.TryOpenExisting(memoryRegion,ref mtxSharedMutex) then
                            printfn "mutex false"
                            new Mutex(false, memoryRegion)
                    else
                            printfn "mutex true"
                            new Mutex(true, memoryRegion)
                finally
                    mtxSharedMutex.Dispose()
    
    
            let (ptrToMemory :ADDR ref, handleFileMapping: UnmanagedMemoryStream) =
    
                let ptrToMemory = Marshal.AllocHGlobal(sharedMemorySize)
    
                if ptrToMemory = IntPtr.Zero then
                    let retVal = Marshal.GetLastWin32Error()
                    raise <| new Win32Exception(retVal, "Could not map file view")
    
                let sharedMemorySize = int64 sharedMemorySize
    
    
                let (memBytePtr:nativeptr<byte>) = NativePtr.ofNativeInt( ptrToMemory )
    
                let handleFileMapping = new UnmanagedMemoryStream(memBytePtr, sharedMemorySize, sharedMemorySize, FileAccess.ReadWrite)
    
                (ref ptrToMemory, handleFileMapping)
    
            let closeSharedMemory() =
                if !ptrToMemory <> IntPtr.Zero then
                    PInvokeHelper.UnmapViewOfFile(!ptrToMemory) |> ignore
                    ptrToMemory := IntPtr.Zero
    
                handleFileMapping.Close()
                handleFileMapping.Dispose()
    
            let dispose disposing =
                if not <| disposed then
                        closeSharedMemory()
                disposed <- true
    
            interface ISharedMemory<'T> with
    
                member x.SharedMemoryBaseSize = sharedMemorySize
                member x.Close() = closeSharedMemory()
    
                member x.Send(item:'T) =
                    use ms = new MemoryStream()
                    let formatter = new BinaryFormatter()
    
                    try
                        handleFileMapping.Position <- 0L
                        formatter.Serialize(handleFileMapping, item)
                        handleFileMapping.Flush()
    
    //                    let bytes = ms.ToArray()
    //
    //                    if bytes.Length + sizeof<int> > int memRegionSize then
    //                        raise <| new ArgumentException("Object is too large to share in memory")
    //
    //                    let sizeBuffer = BitConverter.GetBytes(bytes.Length)
    //
    //                    handleFileMapping.Write(sizeBuffer, 0, 4)
    //                    handleFileMapping.Flush()
    //                    handleFileMapping.Write(bytes, 0, bytes.Length)
    //                    handleFileMapping.Flush()
                    finally
                        mtxSharedMutex.ReleaseMutex() |> ignore
                        mtxSharedMutex.WaitOne() |> ignore
    
                member x.Receive() : 'T =
                    mtxSharedMutex.WaitOne() |> ignore
    
    //                let sizeBuffer = Array.zeroCreate<byte> 4
    //                let count = handleFileMapping.Read(sizeBuffer, 0, 4)
    //                let sizeBuffer' = BitConverter.ToInt32(sizeBuffer, 0)
    //
    //                if count <= 0 then
    //                    raise <| new InvalidDataException("No object to read")
    //
    //                let bytes = Array.zeroCreate<byte> sizeBuffer'
    //                let count' = handleFileMapping.Read(bytes, 0, bytes.Length)
    //
    //                use ms = new MemoryStream(bytes)
                    let formatter = new BinaryFormatter()
                    handleFileMapping.Position <- 0L
                    try
                        let item = formatter.Deserialize(handleFileMapping) :?> 'T
                        item
                    finally
                        mtxSharedMutex.ReleaseMutex()
               // interface IDisposable with
    
                member x.Dispose() =
                            dispose(true)
                            GC.SuppressFinalize(x)
    
            override x.Finalize() = dispose (false)
    
