<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net</Namespace>
</Query>


// Begin - End 

IAsyncResult DownloadPage(string url)
{
	var request = HttpWebRequest.Create(url);
	return request.BeginGetResponse(ResponseCallback, request);
}

void ResponseCallback(IAsyncResult iar)
{
	var request = iar.AsyncState as WebRequest;	
 	using (var response = request.EndGetResponse(iar))
	{
		using (var stream = response.GetResponseStream())
		{
			Regex regTitle = new Regex(@"\<title\>([^\<]+)\</title\>");

			var buffer = new byte[1024];
			var temp = new MemoryStream();
			int count = 0;
			do
			{
				var wait = stream.BeginRead(buffer, 0, buffer.Length, iar2 => 
				{
					count = stream.EndRead(iar2);
					temp.Write(buffer, 0, count);
				}, null);
				
				wait.AsyncWaitHandle.WaitOne();				
			} while (count > 0);

			temp.Seek(0, SeekOrigin.Begin);
			var html = new StreamReader(temp).ReadToEnd();
			var title = regTitle.Match(html).Groups[1].Value;
			// ?? return Tuple.Create(title, html.Length);
		}
	}
}

