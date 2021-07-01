<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// Take a look at the following example and figure out in what order will the strings be printed

async Task WorkThenWait()
{
	await Task.Yield();
	Thread.Sleep(1000);	
	Console.WriteLine("work");
	await Task.Delay(1000);
}

void Demo()
{
	var child = WorkThenWait();
	Console.WriteLine("started");
	child.Wait();
	Console.WriteLine("completed");
}

Demo();


























// If you guessed that it prints "started", "work" and "completed" then you're wrong. 
// The code prints "work", "started" and "completed"

// the work starts by calling WorkThenWait and then await for the task later. 
// The problem is that WorkThenWait starts by doing some heavy computations (here, Thread.Sleep) 
// and only after that uses await.
// You could fix that, for example, by adding await Task.Yield() at the beginning or use await Task.Delay(1000); in place of Thread sleep