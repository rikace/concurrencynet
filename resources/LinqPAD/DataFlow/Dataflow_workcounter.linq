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

void Main()
{
	// Create a cancellation token source
	CancellationTokenSource cancellationSource = new CancellationTokenSource();

	var opts = new ExecutionDataflowBlockOptions
	{
		CancellationToken = cancellationSource.Token
		, MaxDegreeOfParallelism = 4
	};

	// Download a book as a string
	var downloadBook = new TransformBlock<string, string>(async uri =>
	{
		Console.WriteLine("Downloading the book...");
		return await new WebClient().DownloadStringTaskAsync(new Uri(uri));
	}, opts);



	// splits text into an array of strings.
	var createWordList = new TransformBlock<string, string[]>(text =>
	{
		Console.WriteLine("Creating list of words...");

				// Remove punctuation
				char[] tokens = text.ToArray();
		for (int i = 0; i < tokens.Length; i++)
		{
			if (!char.IsLetter(tokens[i]))
				tokens[i] = ' ';
		}
		text = new string(tokens);

		return text.Split(new char[] { ' ' },
		   StringSplitOptions.RemoveEmptyEntries);
	}, opts);

	// Remove short words and return the count
	var filterWordList = new TransformBlock<string[], int>(words =>
	{
		Console.WriteLine("Counting words...");

		var wordList = words.Where(word => word.Length > 3).OrderBy(word => word)
		   .Distinct().ToArray();
		return wordList.Count();
	}, opts);

	var printWordCount = new ActionBlock<int>(wordcount =>
	{
		Console.WriteLine("Found {0} words",
		   wordcount);
	}, opts);

	downloadBook.LinkTo(createWordList);
	createWordList.LinkTo(filterWordList);
	filterWordList.LinkTo(printWordCount);

	// create a continuation task that marks the next block in the pipeline as completed.
	downloadBook.Completion.ContinueWith(t =>
	{
		if (t.IsFaulted) ((IDataflowBlock)createWordList).Fault(t.Exception);
		else createWordList.Complete();
	});
	createWordList.Completion.ContinueWith(t =>
	{
		if (t.IsFaulted) ((IDataflowBlock)filterWordList).Fault(t.Exception);
		else filterWordList.Complete();
	});
	filterWordList.Completion.ContinueWith(t =>
	{
		if (t.IsFaulted) ((IDataflowBlock)printWordCount).Fault(t.Exception);
		else printWordCount.Complete();
	});

	try
	{
		Console.WriteLine("Starting...");

		// Download Origin of Species
		downloadBook.Post("https://www.gutenberg.org/files/1342/1342-0.txt");
		downloadBook.Post("https://www.gutenberg.org/files/84/84-0.txt");
		downloadBook.Post("https://www.gutenberg.org/files/11/11-0.txt");
		
		// Mark the head of the pipeline as complete.
		downloadBook.Complete();

		// Cancel the operation
	    cancellationSource.Cancel();

		printWordCount.Completion.Wait();

	}
	catch (AggregateException ae)
	{
		foreach (Exception ex in ae.InnerExceptions)
		{
			Console.WriteLine(ex.Message);
		}
	}
	finally
	{
		Console.WriteLine("Finished. Press any key to exit.");
	}
}