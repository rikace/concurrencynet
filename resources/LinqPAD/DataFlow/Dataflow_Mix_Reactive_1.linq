<Query Kind="Statements">
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

IPropagatorBlock<int, string> source = new TransformBlock<int, string>(i => (i + i).ToString());
IObservable<int> observable = source.AsObservable().Select(int.Parse);
IDisposable subscription = observable.Subscribe(i => $"Value {i} - Time {DateTime.Now.ToString("hh:mm:ss.fff")}".Dump());

for (int i = 0; i < 100; i++)
	source.Post(i);

IPropagatorBlock<string, int> target = new TransformBlock<string, int>(s => int.Parse(s));
IDisposable link = target.LinkTo(new ActionBlock<int>(i => $"Value {i} - Time {DateTime.Now.ToString("hh:mm:ss.fff")}".Dump()));

IObserver<string> observer = target.AsObserver();

IObservable<string> observable_2 = Observable.Range(1,20).Select(i => (i *i).ToString());
observable_2.Subscribe(observer);

for (int i = 0; i < 100; i++)
	target.Post(i.ToString());