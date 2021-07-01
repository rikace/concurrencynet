<Query Kind="Program">
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

async Task Main()
{
	IEnumerable<int> range = Enumerable.Range(0, 100);
	
	await Task.WhenAll(
		Producer(range),
		Consumer(n =>
			Console.WriteLine($"value {n}")));
}

// Simple Producer Consumer using TPL Dataflow BufferBlock
BufferBlock<int> buffer = new BufferBlock<int>(new DataflowBlockOptions { BoundedCapacity = 10});

async Task Producer(IEnumerable<int> values)
{
    foreach (var value in values)
        buffer.Post(value);
    buffer.Complete();
}
async Task Consumer(Action<int> process)
{
    while (await buffer.OutputAvailableAsync())
        process(await buffer.ReceiveAsync());

}
      