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

var bb = new BufferBlock<int>(new DataflowBlockOptions() {
	 

BoundedCapacity = 2 });

var a1 = new ActionBlock<int>(
	a =>
	{
		Console.WriteLine("Action A1 executing with value {0}", a);
		Thread.Sleep(100);
	}
	, new ExecutionDataflowBlockOptions() { BoundedCapacity = 1 }
);

var a2 = new ActionBlock<int>(
	a =>
	{
		Console.WriteLine("Action A2 executing with value {0}", a);
		Thread.Sleep(50);
	}
	, new ExecutionDataflowBlockOptions() { BoundedCapacity = 1 }
);
var a3 = new ActionBlock<int>(
	a =>
	{
		Console.WriteLine("Action A3 executing with value {0}", a);
		Thread.Sleep(50);
	}
	, new ExecutionDataflowBlockOptions() { BoundedCapacity = 1 }
);

var tr = new TransformBlock<int, int>(n => n, new ExecutionDataflowBlockOptions { BoundedCapacity = 1});

var broadcast = new BroadcastBlock<int>(n => n);
bb.LinkTo(broadcast);

broadcast.LinkTo(a1, n => n % 2 == 0);
broadcast.LinkTo(a2, n => n % 2 != 0);


broadcast.LinkTo(a3);

bb.LinkTo(tr);


for (int i = 0; i < 10; i++)
{
	Thread.Sleep(10);
	bb
		.SendAsync(i)
		.ContinueWith(a => Console.WriteLine($"Message {i} sent #{a.Result}"));
}