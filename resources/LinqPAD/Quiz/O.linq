<Query Kind="Expression">
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
</Query>

// ConfigureAwait is an important concept, and if you find yourself working on a codebase that uses.Result and.Wait it can be critical to use correctly.
// Essentially the meaning of ConfigureAwait(true) is that I would like my code to continue on the same "synchronization context" after the await has completed. 
// For example, in a WPF application, the "synchronization context" is the UI thread, and I can only make updates to UI components from that thread. So I almost always want ConfigureAwait(true) in my UI code.

private async void OnButtonClicked()
{
	var data = await File.ReadAllBytesAsync().ConfigureAwait(false);
	this.textBoxFileSize.Text = $"The file is ${data.Length} bytes long"; // needs to be on the UI thread
}

// ConfigureAwait(true) is actually the default, so we could safely leave it out of the above example and everything would still work.
// But why might we want to use ConfigureAwait(false)? Well, for performance reasons. Not everything needs to run on the "synchronization context" and so its better if we don't make one thread do all the work.
// So ConfigureAwait(false) should be used whenever we don't care what thread the continuation runs on, which is actually a lot of the time, especially in low - level code that is dealing with files and network calls.
// However, when we start combining code that has synchronization contexts, ConfigureAwait(false) and calls to.Result, there is a real danger of deadlocks.
// It is recommended to avoid this is to remember to call ConfigureAwait(false) everywhere that you don't explicitly need to stay on the synchronization context.

// For example, if you make a general purpose NuGet library, then it is highly recommended to put ConfigureAwait(false) on every single await call, since you can't be sure of the context in which it will be used.
// There is some good news on the horizon.In ASP.NET Core there is no longer a synchronization context, which means that you no longer need to put calls to ConfigureAwait(false) in. 
// But if you are working on projects that run the risk of a deadlock, you need to be very vigilant about adding the ConfigureAwait(false) calls in everywhere.