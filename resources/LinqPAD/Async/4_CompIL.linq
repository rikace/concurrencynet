<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

//Task DelayAsync()
//{
//	return Task.FromResult(true);
//}

async Task DelayAsync()
{

}

// This method causes a deadlock when called in a GUI or ASP.NET context.
void Test()
{
	// Start the delay.
	var delayTask = DelayAsync();
	// Wait for the delay to complete.
	delayTask.Wait();
}



Test();
"Completed".Dump();