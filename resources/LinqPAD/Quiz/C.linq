<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// does the folling code run asynchronously?

async Task FooAsync()
{
	"FooAsync start".Dump();
	await Task.Delay(1000);
	"FooAsync end".Dump();
}
void Handler()
{
	"Handler start".Dump();
	FooAsync().Wait();
	"Handler end".Dump();
}

Handler();





















// Another common way that developers work around the difficulty of calling asynchronous methods from synchronous methods
// is by using the .Result property or .Wait method on the Task.
// But there are some problems here. The first is that using blocking calls like Resultties up a thread that could be doing other useful work.
// More seriously, mixing async code with calls to .Result (or .Wait()) opens the door to some really nasty deadlock problems.
// Usually, whenever you need to call an asynchronous method, you should just make the method you are in asynchronous. 
// Yes, its a bit of work and sometimes results in a lot of cascading changes which can be annoying in a large legacy codebase, but that is still usually preferable to the risk of introducing deadlocks.

// we are calling FooAsync().Wait(). This means that we create a task and then, using Wait, block until it completes. 
// Simply removing Wait fixes the problem, because we just want to start the task.