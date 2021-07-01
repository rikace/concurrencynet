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

async Task SendEmailAsync(string email) 
{
	$"Sending email for {email}".Dump();
	await Task.Delay(500);
	$"Email send for {email}".Dump();
}

// The List<T> class has a "handy" method called ForEach that performs an Action<T> on every element in the list.
// If you've seen any of my LINQ talks you'll know my misgivings about this method as encourages a variety of bad practices (read this for some of the reasons to avoid ForEach). But one common threading-related misuse I see, is using ForEach to call an asynchronous method.
// For example, let's say we want to send an email to all the emails 

List<string> emails = new List<string>{ "email1@domain.com",  "email2@domain.com",  "email3@domain.com",  "email4@domain.com" };
emails.ForEach(email => SendEmailAsync(email));


// What's the problem here? Well, what we've done is exactly the same as if we'd written the following foreach loop:
foreach (var email in emails)
{
	SendEmailAsync(email); // the return task is ignored
}

// We've generated one Task per email but haven't waited for any of them to complete.
// Sometimes I'll see try to fix this by adding in async and await keywords to the lambda:

emails.ForEach(async c => await SendEmailAsync(c));

// But this makes no difference. The ForEach method accepts an Action<T>, which returns void. 
// So you've essentially created an async void method, which of course was one of our previous antipatterns as the caller has no way of awaiting it.

// So what's the fix to this? Well, personally I usually prefer to just replace this with the more explicit foreach loop:
foreach(var email in emails)
{
	await SendEmailAsync(email);
}

