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


//7.Non - thread - safe side - effects

// the idea of "pure" functions is the idea of a "pure" function that it has no side effects. 
// It takes data in, and it returns data, but it doesn't mutate anything. Pure functions bring many benefitsincluding inherent thread safety.
// Often I see asynchronous methods like this, where we've been passed a list or dictionary and in the method we modify it

async Task<string> GetAsync(Guid id) 
{
	$"GetAsync start for {id}".Dump();
	await Task.Delay(100);
	$"GetAsync end for {id}".Dump();
	return $"User {id}";
}

async Task ProcessUserAsync(Guid id, List<string> users)
{
	var user = await GetAsync(id);
	// do other stuff with the user
	users.Add(user);
}

// The trouble is, this code is risky as it is not thread - safe for the users list to be modified on different threads at the same time. 
// Here's the same method updated so that it no longer has side effects on the users list.
async Task<string> ProcessUserPureAsync(Guid id)
{
	var user = await GetAsync(id);
	// do other stuff with the user
	return user;
}

// Now we've moved the responsibility of adding the user into the list onto the caller of this method, 
// who has a much better chance of ensuring that the list is accessed from one thread only.
