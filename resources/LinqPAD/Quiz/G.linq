<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// does the following code wait 1 second between the two prints?


Console.WriteLine("Before");

await Task.Factory.StartNew(
  	async () => { await Task.Delay(1000); }).Unwrap();
	
Console.WriteLine("After");



















// The StartNew method takes a delegate and returns a Task<T> where T is the type returned by the delegate. 
// In the above case, the delegate returns Task, so we get Task<Task> as the result. 
// Using await waits only for the completion of the outer task (which immediately returns the inner task) and the inner task is then ignored.

// you can fix this by using Task.Run instead of StartNew 
// or by dropping the async and await in the lambda function.