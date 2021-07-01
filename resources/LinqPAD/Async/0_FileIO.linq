<Query Kind="Statements" />

IAsyncResult ReadFileNoBlocking(string filePath, Action<byte[]> process)
{
	using (var fileStream = new FileStream(filePath, FileMode.Open,
							FileAccess.Read, FileShare.Read, 0x1000,
											 FileOptions.Asynchronous))
	{
		byte[] buffer = new byte[fileStream.Length];
		var state = Tuple.Create(buffer, fileStream, process);
		return fileStream.BeginRead(buffer, 0, buffer.Length,
									  EndReadCallback, state);
	}
}

void EndReadCallback(IAsyncResult ar)
{
	var state = ar.AsyncState as Tuple<byte[], FileStream, Action<byte[]>>;
	using (state.Item2) state.Item2.EndRead(ar);
	state.Item3(state.Item1);
}