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

var bcBlock = new BroadcastBlock<int>(n => n);

var actionBlock1 = new ActionBlock<int>(n =>
{
	Thread.Sleep(100);
	Console.WriteLine($"Message Action block 1: {n} - Thread Id#{Thread.CurrentThread.ManagedThreadId}");
});
var actionBlock2 = new ActionBlock<int>(n =>
{
	Thread.Sleep(100);
	Console.WriteLine($"Message Action block 2: {n} - Thread Id#{Thread.CurrentThread.ManagedThreadId}");
});

bcBlock.LinkTo(actionBlock1);
bcBlock.LinkTo(actionBlock2);

for (int i = 0; i < 10; i++)
{
	bcBlock.Post(i);
}