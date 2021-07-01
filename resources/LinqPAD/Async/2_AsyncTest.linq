<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net</Namespace>
</Query>

async Task<Tuple<string, int>> DownloadPage(string url)
{
	// Create web request to download a web site
	var request = HttpWebRequest.Create(url);
	// Asynchronously obtain the HTTP response
	using (var response = await request.GetResponseAsync())
	{
		// Start reading the response stream
		using (var stream = response.GetResponseStream())
		{
			// Regular expression for extracting page title
			Regex regTitle = new Regex(@"\<title\>([^\<]+)\</title\>");

			// Create a buffer and stream to copy data to
			var buffer = new byte[1024];
			var temp = new MemoryStream();
			int count;
			do
			{
				// Asynchronously read next 1kB of data
				count = await stream.ReadAsync(buffer, 0, buffer.Length);
				// Write data to memory stream
				await temp.WriteAsync(buffer, 0, count);
			} while (count > 0);

			// Read data as string and find page title
			temp.Seek(0, SeekOrigin.Begin);
			var html = new StreamReader(temp).ReadToEnd();
			var title = regTitle.Match(html).Groups[1].Value;
			return Tuple.Create(title, html.Length);
		}
	}
}


List<string> urlList = new List<string>
			{
				"http://www.google.com",
				"http://www.amazon.com",
				"http://microsoft.com",
				"https://github.com",
				"https://www.cnn.com",
				"http://www.bing.com",
				"http://www.excella.com"
			};

// How to execute all the downloads in parallel?
 var downloads = urlList.Select(DownloadPage).ToArray();

//var downloads = urlList.Select(DownloadPage);

foreach (var result in downloads)
{
	var item = await result;
	Console.WriteLine("{0} (length {1})", item.Item1, item.Item2);
}