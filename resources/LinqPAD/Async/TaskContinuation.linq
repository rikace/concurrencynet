<Query Kind="Program">
  <Reference>C:\ws\Course_InProg\Common\Functional.cs\bin\Debug\Functional.dll</Reference>
  <Namespace>Functional.Tasks</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

void Main()
{

	var urls = new[] { "http://www.google.com/", "http://microsoft.com/", "http://www.wordpress.com/", "http://www.peta.org" };

	foreach (var url in urls)
	{
		DownloadAsync(url);
		//DownloadTaskAsync(url);
		//DownloadTaskLINQAsync(url);
	}

}

void RunContinuation<T>(Func<T> input, Action<T> rest)
	=> ThreadPool.QueueUserWorkItem(new WaitCallback(o => rest(input())));


void DownloadAsync(string url)
{
	Action<string> printMsg = msg =>
		Console.WriteLine($"ThreadID = {Thread.CurrentThread.ManagedThreadId}, Url = {url}, {msg}");

	RunContinuation(() =>
	{
		printMsg("Creating webclient...");
		return new System.Net.WebClient();
	}, (webclient) =>
		RunContinuation(() =>
		{
			printMsg("Downloading url...");
			return webclient.DownloadString(url);
		}, (html) =>
			RunContinuation(() =>
			{
				printMsg("Extracting urls...");
				return Regex.Matches(html, @"http://\S+");
			}, (matches) =>
					printMsg("Found " + matches.Count.ToString() + " links")
				)));
}


void DownloadTaskAsync(string url)
{
	Action<string> printMsg = msg =>
		Console.WriteLine($"ThreadID = {Thread.CurrentThread.ManagedThreadId}, Url = {url}, {msg}");

	Task.Run(() =>
	{
		printMsg("Creating webclient...");
		return new System.Net.WebClient();
	}).ContinueWith(taskWebClient =>
	{
		var webclient = taskWebClient.Result;
		printMsg("Downloading url...");
		return webclient.DownloadString(url);
	}).ContinueWith(htmlTask =>
	{
		var html = htmlTask.Result;
		printMsg("Extracting urls...");
		return Regex.Matches(html, @"http://\S+");
	}).ContinueWith(rgxTask =>
	{
		var matches = rgxTask.Result;
		printMsg("Found " + matches.Count.ToString() + " links");
	});
}

static void DownloadTaskLINQAsync(string url)
{
	Action<string> printMsg = msg =>
		Console.WriteLine($"ThreadID = {Thread.CurrentThread.ManagedThreadId}, Url = {url}, {msg}");

	var matches = from webclient in Task.Run(() => new System.Net.WebClient())
				  from html in Task.Run(() => webclient.DownloadString(url))
				  from rgx in Task.Run(() => Regex.Matches(html, @"http://\S+"))
				  select rgx;

	Console.WriteLine($"Url = {url}, Found {matches.Result.Count.ToString()} links");
}
