<Query Kind="Program">
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
  <Namespace>System.Net</Namespace>
</Query>

async Task Main()
{
	var urls = new List<string>();

	urls.Add("https://www.cnn.com");
	urls.Add("https://www.bbc.com");
	urls.Add("https://www.amazon.com");
	urls.Add("https://www.cnn.com");
	urls.Add("https://www.bbc.com");


	var downloadUrl = MemoizeLazyThreadSafe<string, string>(async (url) =>
		  {
			  $"processing url {url}".Dump();
			  using (var wc = new WebClient())
				  return await wc.DownloadStringTaskAsync(url);
		  });

	foreach (var url in urls)
	{
		var result = await downloadUrl(url);
		Console.WriteLine($"Url {url} length {result.Length}");
	}
}

Func<T, Task<R>> MemoizeLazyThreadSafe<T, R>(Func<T, Task<R>> func) where T : IComparable
{
	ConcurrentDictionary<T, Lazy<Task<R>>> cache = new ConcurrentDictionary<T, Lazy<Task<R>>>();
	return arg => cache.GetOrAdd(arg, a => new Lazy<Task<R>>(() => func(a))).Value;
}

