<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

var cts = new CancellationTokenSource();
var token = cts.Token;

async Task Child()
{
	Console.WriteLine("start child");
	await Task.Delay(1000);
	Console.WriteLine("end child");
}


async Task Parent(CancellationToken cancellationToken)
{
	Console.WriteLine("start parent");
	await Child();
	await Task.Delay(1000, cancellationToken);
	Console.WriteLine("end parent");
}

var p =  Parent(token);
// cts.Cancel();
// "cancel".Dump();
await p;