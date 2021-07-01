<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// Guess what happens when you run the following asynchronous method

async Task Handler()
{
	Console.WriteLine("Before");
	Task.Delay(1000);
	Console.WriteLine("After");
}


Handler().Wait();




























// Were you expecting that it prints "Before", waits 1 second and then prints "After"? Wrong! 
// It prints both messages immediately without any waiting in between. 
// The problem is that Task.Delay returns a Task and we forgot to await until it completes using await.

// Whenever you call a method that returns a Task or Task<T> you should not ignore its return value.In most cases, 
// that means awaiting it, although there are occasions where you might keep hold of the Task to be awaited later.
// In this example, we call Task.Delay but because we don't await it, the "After" message will get immediately written,
// because Task.Delay(1000) simply returns a task that will complete in one second, but nothing is waiting for that task to finish.