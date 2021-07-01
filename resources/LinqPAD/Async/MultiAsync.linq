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
  <Namespace>System.Net</Namespace>
</Query>

async Task Main()
{
	//ServicePointManager.DefaultConnectionLimit = 3;
	"Started downloading books...\n".Dump();
	await GetMultipleWordCount();
	//await GetWordCount();
	"Finished downloading books...\n".Dump();
}

public async Task GetWordCount()
{
	var client = new HttpClient();
	var urlList = GetBookUrls();
	var wordCountQuery = from book in urlList select ProcessBook(book, client);
	Task<KeyValuePair<string, int>>[] wordCountTasks = wordCountQuery.ToArray();
	
//	KeyValuePair<string, int>[] wordCounts = await Task.WhenAll(wordCountTasks);
//	foreach (var book in wordCounts)
//	{
//		String.Format("Finished processing {0} : Word count {1} \n", book.Key, book.Value).Dump();
//	}

	await Task.WhenAll(wordCountTasks).ContinueWith(task =>
	{
		var wordCounts = task.Result;
		foreach (var book in wordCounts)
		{
			String.Format("Finished processing {0} : Word count {1} \n", book.Key, book.Value).Dump();
		}

	});
}

public async Task GetMultipleWordCount()
{
	var client = new HttpClient();	
	var urlList = GetBookUrls();
	var bookQuery = from book in urlList select ProcessBook(book, client);
	var bookTasks = bookQuery.ToList();
	
	while (bookTasks.Count > 0)
	{
		var firstFinished = await Task.WhenAny(bookTasks);
		bookTasks.Remove(firstFinished);
		var thisBook = await firstFinished;
		String.Format("Finished downloading {0}. Word count: {1}\n",
			thisBook.Key,
			thisBook.Value).Dump();
	}
}

async Task<KeyValuePair<string, int>> ProcessBook(KeyValuePair<string, string> book, HttpClient client)
{
	$"Downloading book {book.Key}...".Dump();
	var bookContents = await client.GetStringAsync(book.Value);
	var wordArray = bookContents.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
	return new KeyValuePair<string, int>(book.Key, wordArray.Count());
}

private List<KeyValuePair<string, string>> GetBookUrls()
{
	var urlList = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string,string>("Pride and Prejudice",
							"https://www.gutenberg.org/files/1342/1342-0.txt"),
				new KeyValuePair<string,string>("Frankenstein",
							"https://www.gutenberg.org/files/84/84-0.txt"),
				new KeyValuePair<string,string>("Alice Adventures in Wonderland",							
							"https://www.gutenberg.org/files/11/11-0.txt")
			};
	return urlList;
}


char[] delimiters = { ' ', ',', '.', ';', ':', '-', '_', '/', '\u000A' };