<Query Kind="Statements">
  <NuGetReference>Microsoft.Tpl.Dataflow</NuGetReference>
  <NuGetReference>System.Collections.Immutable</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Collections.Immutable</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks.Dataflow</Namespace>
</Query>

var rand = new Random(DateTime.Now.Millisecond);

var broadcastBlock = new BroadcastBlock<int>(x => x);

var transformPositive = new TransformBlock<int, int>(x =>
{
	Thread.Sleep(2000);
	return x;
});

var transformNegative = new TransformBlock<int, int>(x =>
{
	Thread.Sleep(1000);
	return x * -1;
});

var join = new JoinBlock<int, int>();

var batchBlock = new BatchBlock<Tuple<int, int>>(5);

var sumBlock = new ActionBlock<Tuple<int, int>[]>(tuples =>
{
	foreach (var tuple in tuples)
		Console.WriteLine($"{tuple.Item1}+({tuple.Item2})={tuple.Item1 + tuple.Item2}");
});


broadcastBlock.LinkTo(transformPositive, new DataflowLinkOptions { PropagateCompletion = true });
broadcastBlock.LinkTo(transformNegative, new DataflowLinkOptions { PropagateCompletion = true });

transformPositive.LinkTo(@join.Target1, new DataflowLinkOptions { PropagateCompletion = true });
transformNegative.LinkTo(@join.Target2, new DataflowLinkOptions { PropagateCompletion = true });

@join.LinkTo(batchBlock, new DataflowLinkOptions { PropagateCompletion = true });
batchBlock.LinkTo(sumBlock, new DataflowLinkOptions { PropagateCompletion = true });

for (int i = 0; i < 30; i++)
{
	broadcastBlock.Post(rand.Next(100));
	Thread.Sleep(1000);
}

broadcastBlock.Complete();