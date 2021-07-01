<Query Kind="Program">
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
</Query>

public static IObservable<string> FetchWebpage(string url)
{
	var hwr = WebRequest.CreateDefault(new Uri(url)) as HttpWebRequest;
	var requestFunc = Observable.FromAsyncPattern<WebResponse>(hwr.BeginGetResponse, hwr.EndGetResponse);

	return requestFunc().Select(x =>
	{
		var ms = new MemoryStream();
		x.GetResponseStream().CopyTo(ms);
		return Encoding.UTF8.GetString(ms.ToArray());
	});
}


void Main()
{
	var inputs = (new[] {
	"http://www.google.com",
	"http://www.duckduckgo.com",
	"http://www.yahoo.com",
	"http://www.bing.com",
		});

	inputs.ToObservable()
	.SelectMany(x => Observable.Defer(() => { ("Requesting page for " + x).Dump(); return FetchWebpage(x);})
		.Timeout(TimeSpan.FromMilliseconds(750))		
		.Retry(3)
		.Catch(Observable.Return(x + " Error")))
		//.OnErrorResumeNext(Observable.Return("Couldn't fetch the Website")))
		.Subscribe(x => x.Substring(0,10).Dump());
}