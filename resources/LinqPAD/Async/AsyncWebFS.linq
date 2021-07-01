<Query Kind="FSharpProgram">
  <Namespace>System.Net</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

let srcMatch = new Regex(@"src\s*=\s*['""](.*?\.(png|gif|png|jpg|js))['""]", RegexOptions.IgnoreCase)

let getSiteSize uri = async {
    let uri = Uri(uri)
    (sprintf "Downloading %A..." uri).Dump()
    let wc = new WebClient()
    let! html = wc.AsyncDownloadString(uri)
    
    let otherFiles =
        srcMatch.Matches(html)
        |> Seq.cast<Match>
        |> Seq.map(fun m -> m.Groups.[1].Value)

    let otherFileLengths =
        otherFiles
        |> Seq.distinct
        |> fun s -> s.Dump("(these are the other URIs)")
                    s
        |> Seq.map(fun n -> let wc = new WebClient()
                            wc.AsyncDownloadString(Uri(uri, n)))
        
    let! fileContents = 
        otherFileLengths
        |> Seq.head
        |> Async.StartChild
    let! fileContents = fileContents
    let firstFileContent = fileContents |> Seq.head
    let res = html.Length + fileContents.Length
    res.Dump()
    return res
}

let  urlList = 
			[	
                "http://www.google.com"
				"http://www.amazon.com"
				"http://msdn.microsoft.com"
				"http://msdn.microsoft.com/library/windows/apps/br211380.aspx"
				"http://msdn.microsoft.com/en-us/library/hh290136.aspx"
				"http://msdn.microsoft.com/en-us/library/dd470362.aspx"
				"http://msdn.microsoft.com/en-us/library/aa578028.aspx"
				"http://msdn.microsoft.com/en-us/library/ms404677.aspx"
				"http://msdn.microsoft.com/en-us/library/ff730837.aspx"
			]

let downloadTasksQuery =
    urlList 
    |> Seq.map getSiteSize
    |> Async.Parallel
    |> Async.RunSynchronously
  