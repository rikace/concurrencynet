<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// Async void lambda functions
// Does the following code finish in 1 second (after all the tasks finish sleeping), or does it finish immediately?

Console.WriteLine("START");

Parallel.For(0, 10, async i =>
{
	await Task.Delay(1000);
});

"END".Dump();






















// when you pass asynchronous lambda function to some method as a delegate. 
// In this case, the C# compiler infers the type of method from the delegate type. 
// If you use the Action delegate, then the compiler produces async void function (which starts the work and returns void). 
// If you use the Func<Task> delegate, the compiler generates a function that returns Task.