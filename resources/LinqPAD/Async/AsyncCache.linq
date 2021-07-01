<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
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
  <Namespace>System.Net.Http</Namespace>
</Query>

async Task Main()
{

	var client = new HttpClient();

	var getData = MemoizeLazyTaskdSafe<string, string>(url =>
	{
		Console.WriteLine($"Get String Async {url} - {DateTime.Now.ToString("hh:mm:ss.fff")}");
		return client.GetStringAsync(url);
	});

	var result_1 = await getData("http://www.google.com");
	result_1.Length.Dump();

	var result_2 = await getData("http://www.microsoft.com");
	result_2.Length.Dump();

	var result_3 = await getData("http://www.google.com");
	result_3.Length.Dump();

	var result_4 = await getData("http://www.microsoft.com");
	result_4.Length.Dump();
}

public static Func<T, Task<R>> MemoizeLazyTaskdSafe<T, R>(Func<T, Task<R>> func) where T : IComparable
{
	ConcurrentDictionary<T, Lazy<Task<R>>> cache = new ConcurrentDictionary<T, Lazy<Task<R>>>();
	return arg => cache.GetOrAdd(arg, a => new Lazy<Task<R>>(() => func(a))).Value;
}

public static Func<T, Task<R>> MemoizeLazy<T, R>(Func<T, Task<R>> func) where T : IComparable
{
	ConcurrentDictionary<T, Task<R>> cache = new ConcurrentDictionary<T, Task<R>>();
	return arg => cache.GetOrAdd(arg, a => Task<R>.Run(() => func(a)));
}