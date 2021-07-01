<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
</Query>


GC.Collect();

double[] resultData = new double[100000000];

var sw = System.Diagnostics.Stopwatch.StartNew();
Parallel.For(0, resultData.Length, (int index) =>
{
	// compuute the result for the current index
	resultData[index] = Math.Pow(index, 2);
});
sw.ElapsedMilliseconds.Dump();
resultData.Sum().Dump();



Partitioner<Tuple<int, int>> chunkPart = Partitioner.Create(0, resultData.Length, 100000000 / Environment.ProcessorCount);

sw.Restart();

Parallel.ForEach(chunkPart, chunkRange =>
{
	// iterate through all of the values in the chunk range
	for (int i = chunkRange.Item1; i < chunkRange.Item2; i++)
	{
		resultData[i] = Math.Pow(i, 2);
	}
});

sw.ElapsedMilliseconds.Dump();
resultData.Sum().Dump();