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

IObservable<int> originalInts = Observable.Range(1, 20);

IPropagatorBlock<int, int[]> batch = new BatchBlock<int>(2);
IObservable<int[]> batched = batch.AsObservable();
originalInts.Subscribe(batch.AsObserver());

IObservable<int> added = batched.Timeout(TimeSpan.FromMilliseconds(250)).Select(a => a.Sum());

IPropagatorBlock<int, string> toString = new TransformBlock<int, string>(i => i.ToString());
added.Subscribe(toString.AsObserver());

JoinBlock<string, int> join = new JoinBlock<string, int>();
toString.LinkTo(join.Target1);

IObserver<int> joinIn2 = join.Target2.AsObserver();
originalInts.Subscribe(joinIn2);

IObservable<Tuple<string, int>> joined = join.AsObservable();

joined.Subscribe(t => Debug.WriteLine("{0};{1}", t.Item1, t.Item2));