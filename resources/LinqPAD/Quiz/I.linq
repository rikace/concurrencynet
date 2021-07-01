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

async Task DelayAsync()
{
	"DelayAsync start".Dump();
	await Task.Delay(1000);
	"DelayAsync end".Dump();
}

// This method causes a deadlock when called in a GUI or ASP.NET context.
void Test()
{
	"Test start".Dump();
	// Start the delay.
	var delayTask = DelayAsync();
	// Wait for the delay to complete.
	delayTask.Wait(); // Noncompliant
	"Test end".Dump();
}

async Task TestAsync()
{
	"TestAsync start".Dump();
	// Start the delay.
	var delayTask = DelayAsync();
	// Wait for the delay to complete.
	await delayTask;
	"TestAsync end".Dump();
}


Test();
// Solution TestAsync().Wait();

// Some synchronization contexts are non-reentrant and single-threaded. This means only one unit of work can be executed in the context at a given time. 
// An example of this is the Windows UI thread or the ASP.NET request context. In these single-threaded synchronization contexts, 
// it’s easy to deadlock yourself. If you spawn off a task from a single-threaded context, then wait for that task in the context, 
// your waiting code may be blocking the background task.


// The “async” and “await” keywords do not create any additional threads.Async methods are intended to be non-blocking operations. 
// The method runs on the current “synchronization context” and uses time on the thread only when the method is active. 
// You should use “Task.Run” to execute CPU-bound work in a background thread, but you don’t need any background thread to wait for results, e.g.a http-request.It’s like time-sharing a single thread.
// Simply put, a “synchronization context” represents a location “where” code might be executed. Every thread can have a “synchronization context” instance associated with it.
// The problem occurs when you have a single “synchronization context”, like in our Winforms example above. But the same would apply to any ASP.NET application
// The solution is simple, use async all the way down. Never block on tasks yourself.