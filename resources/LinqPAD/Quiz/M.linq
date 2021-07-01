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

// when we identify a series of tasks that are performed sequentially as being a performance bottleneck .For example, here's some code that processes some orders sequentially:

List<int> orders = new List<int> { 1,2,3,4,5,6,7,8,9,10};

async Task ProcessOrderAsync(int id)
{
	$"ProcessOrderAsync start for {id}".Dump();
	await Task.Delay(500);
	$"ProcessOrderAsync end for {id}".Dump();
}

foreach (var o in orders)
{
	await ProcessOrderAsync(o);
}


// the attempt to speed this up with something like this:
var tasks = orders.Select(o => ProcessOrderAsync(o)).ToList();  // List<Task>
await Task.WhenAll(tasks);


// What we're doing here is calling the ProcessOrderAsync method for every order id, and storing each resulting Task in a list. 
// Then we wait for all the tasks to complete. Now, this does "work", but what if there were 10,000 order ids? 
// We've flooded the thread pool with thousands of tasks, potentially preventing other useful work from completing. 
// If ProcessOrderAsync makes downstream calls to another service like a database or a microservice, we'll potentially overload that with too high a volume of calls.
// What's the right approach here? Well at the very least we could consider constraining the number of concurrent threads that can be calling ProcessOrderAsync at a time. I've written about a few different ways to achieve that here.
// If I see code like this in a distributed cloud application, it's often a sign that we should introducing some messaging so that the workload can be split into batches and handled by more than one server.

// RequestGate !! (for Example)
