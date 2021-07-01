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

var actionBlock = new ActionBlock<int>(n =>
{
    Thread.Sleep(100);
    Console.WriteLine($"Message : {n} - Thread Id#{Thread.CurrentThread.ManagedThreadId}");
});

var tfBlock = new TransformBlock<int, int>(
    n =>
    {
        Thread.Sleep(500);
		return n * n;
	}, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 }); // Change DOP

tfBlock.LinkTo(actionBlock);

for (int i = 0; i < 10; i++)
{
	tfBlock.Post(i);
	Console.WriteLine($"Message {i} processed - queue count {actionBlock.InputCount}");
}

for (int i = 0; i < 10; i++)
{
	int result = tfBlock.Receive();
	Console.WriteLine($"Message {i} received - value {result}");
}