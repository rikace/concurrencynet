<Query Kind="Statements">
  <NuGetReference>System.Reactive</NuGetReference>
  <NuGetReference>System.Threading.Tasks.Dataflow</NuGetReference>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks.Dataflow</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

var blockOptions = new ExecutionDataflowBlockOptions
{
	MaxDegreeOfParallelism = 2,
	BoundedCapacity = 5
};

ActionBlock<int> warmupBlock = new ActionBlock<int>(async i =>
	{
		await Task.Delay(1000);
		Console.WriteLine($"value {i} \t- Thread Id# {Thread.CurrentThread.ManagedThreadId}");
	}, blockOptions);

ActionBlock<int> postBlock = new ActionBlock<int>(async i =>
	{
		await Task.Delay(1000);
		Console.WriteLine($"value {i} \t- Thread Id# {Thread.CurrentThread.ManagedThreadId}");
	}, blockOptions);

IObservable<int> warmUpSource = Observable.Range(1, 100).TakeUntil(DateTimeOffset.UtcNow.AddSeconds(5));
warmUpSource.Subscribe(warmupBlock.AsObserver());

IObservable<int> testSource = Observable.Range(1000, 1000).TakeUntil(DateTimeOffset.UtcNow.AddSeconds(10));
testSource.Subscribe(postBlock.AsObserver());