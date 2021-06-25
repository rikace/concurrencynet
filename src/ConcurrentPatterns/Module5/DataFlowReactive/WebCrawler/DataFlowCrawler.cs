namespace DataflowReactive
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Reactive.Disposables;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Text.RegularExpressions;
    using System.Collections.Concurrent;
    using HtmlAgilityPack;
    using System.Linq;
    using Memoization = ConcurrentPatterns.Memoization;

    // TODO RTZ
    public static class DataFlowCrawler
    {
        static int index = 0;
        static ConcurrentDictionary<int, ConsoleColor> mapColors = new ConcurrentDictionary<int, ConsoleColor>();

        private static ConsoleColor ColorByInt(int id)
            => mapColors.GetOrAdd(id, _ => colors[Interlocked.Increment(ref index) % (colors.Length - 1)]);

        private static void WriteLineInColor(string message, ConsoleColor foregroundColor)
        {
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        // private const string LINK_REGEX_HREF = "\\shref=('|\\\")?(?<LINK>http\\://.*?(?=\\1)).*>";
        // private static readonly Regex _linkRegexHRef = new Regex(LINK_REGEX_HREF);
        private const string IMG_REGEX = "<\\s*img [^\\>]*src=('|\")?(?<IMG>http\\://.*?(?=\\1)).*>\\s*([^<]+|.*?)?\\s*</a>";

        private static readonly Regex _imgRegex = new Regex(IMG_REGEX);

        // TODO 4
        // Use httpRgx to validate and correct url
        // How can we make this value ThreadSafe and provide better performance?
        private static Regex httpRgx = new Regex(@"^(http|https|www)://.*$");

        public static IDisposable Start(List<string> urls, Func<string, byte[], Task> compute)
        {
            // TODO 2
            // try to increase the level of parallelism,
            // and then implement a way to cancel the operations.
            // Maybe first try with a time expiration, then add cancellation semantic...
            var downloaderOptions = new ExecutionDataflowBlockOptions()
            {
                // TODO should we pass the MaxDegreeOfParallelism as input/argument in function
                MaxDegreeOfParallelism = 1
            };

            // TODO 1
            // replace the "downloadUrl" function body with an implement that
            // downloads the web page of the given URL
            // Can you avoid to re-compute the same page?
            // Look into the "Memoize.cs" file for ideas
            Func<string, Task<string>> downloadUrl = default; // add the missing code implementation

            var downloader = new TransformBlock<string, string>(downloadUrl
                // TODO 1
                // implement logic to download and return the web page (from url)
                , downloaderOptions);


            // TODO
            // implement a "printer block" to display the message
            // $"Message {DateTime.UtcNow.ToString()} - Thread ID {Thread.CurrentThread.ManagedThreadId} : {msg}"

            // TODO Test only the success of the previous step
            // this lines should be commented or removed after the test
            // UNCOMMENT: downloader.LinkTo(printer);
            foreach (var url in urls)
                downloader.Post(url);

            // TODO
            // initialize a Broadcast block to link the "downloader" output
            // to both the "linkParser" and "imhParser" blocks

            // TODO 3
            // implement a linkParser block that has as input the content of the "web page"
            // sent by "downloader" block, it parses the content to extract all the anchors "a" tags
            // and the related "href"s, and then send out the result.
            // keep in mind that the output can be a list of strings.
            // We need block that map an input to an output.
            Func<string, IEnumerable<string>> linkParserFunc = (html) =>
            {
                var output = new List<string>();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var links =
                    from link in doc.DocumentNode.Descendants("a")
                    where link.Attributes.Contains("href")
                    select link.GetAttributeValue("href", "");

                var linksValidated =
                    from link in links
                    where httpRgx.IsMatch(link)
                    select link;

                foreach (var link in linksValidated)
                {
                    Console.WriteLine($"Link {link} ready to be crawled");
                    output.Add(link);
                }

                return output;
            };


            // here sample code to pare the HTML content to get all the "hrer"s
            Func<string, IEnumerable<string>> parseHtml = html =>
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var links =
                    from link in doc.DocumentNode.Descendants("a")
                    where link.Attributes.Contains("href")
                    select link.GetAttributeValue("href", "");
                return links;
            };

            // TODO 4
            // implement a imgParser block that has as input the content of the "web page"
            // sent by "downloader" block, parses the content to extract all the images "img" tags
            // and the related "src", and then send out the result.
            // keep in mind that the output can be a list of strings
            // We need block that map an input to an output.
            Func<string, IEnumerable<string>> imgParserFunc =
                (html) =>
                {
                    var output = new List<string>();
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var images =
                        from img in doc.DocumentNode.Descendants("img")
                        where img.Attributes.Contains("src")
                        select img.GetAttributeValue("src", "");

                    var imagesValidated =
                        from img in images
                        where httpRgx.IsMatch(img)
                        select img;

                    foreach (var img in imagesValidated)
                    {
                        Console.WriteLine($"image {img} ready to be downloaded");
                        output.Add(img);
                    }

                    return output;
                };

            // TODO
            // initialize a Broadcast block to link the "linkParser" output
            // back to the "downloader" block  and the "writer" block to save the image file

            var printer = new ActionBlock<string>(msg =>
            {
                Console.WriteLine(
                    $"Message {DateTime.UtcNow.ToString()} - Thread ID {Thread.CurrentThread.ManagedThreadId} : {msg}");
            });


            // TODO 5
            // Download the image and run the "compute" function (async is the way)
            // What will happen in case of error?
            // Can you write a defensive code to handle errors?
            var writer = new ActionBlock<string>(async url =>
            {
                url = url.StartsWith("http") ? url : "http:" + url;
                using (WebClient wc = new WebClient())
                {
                    // using IOCP the thread pool worker thread does return to the pool
                    byte[] buffer = default; // Load the image here
                    Console.WriteLine($"Downloading {url}..");
                    // do something with the buffer (use the "compute" function)
                }
            });


            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase;
            Predicate<string> linkFilter = link =>
                link.IndexOf(".aspx", comparison) != -1 ||
                link.IndexOf(".php", comparison) != -1 ||
                link.IndexOf(".htm", comparison) != -1 ||
                link.IndexOf(".html", comparison) != -1;

            Predicate<string> imgFilter = url =>
                url.EndsWith(".jpg", comparison) ||
                url.EndsWith(".png", comparison) ||
                url.EndsWith(".gif", comparison);

            // TODO 6
            // link the blocks implemented to create the WebCrawler mash
            // follow the "from/to" suggestion
            IDisposable disposeAll = new CompositeDisposable(
                // from [downloader] to [Broadcaster block 1]
                // from [Broadcaster block 1] to [imgParser]
                // from [Broadcaster block 1] to [linkParser]
                // from [Broadcaster block 1] tp [printer]
                // from [linkParser] to [Broadcaster block 2]
                // from [Broadcaster block 2] tp [downloader] apply predicate linkFilter
                // from [Broadcaster block 2] tp [imgParser] apply predicate imgFilter
                // from [Broadcaster block 2] tp [printer]
                // from [imgParser] tp [writer]
            );

            // TODO 7
            // Use Reactive Extension to output some colorful logging

            // TODO 8
            // Can you memoize the blocks to avoid to re-parse duplicates URLs ??
            // An option is to use the Observable Scan to keep a state of the URLs visited,
            // or use a memoize function

            // TODO 9
            // Write a batch block to parse group of 2 images per operation
            // Where should you link this block?

            foreach (var url in urls)
                // TODO: improve the Throttle of the pipeline here
                downloader.Post(url);

            // TODO RT
            // add cancellation token to the pipeline.
            // This code here (uncomment) ensure that all the blocks are unregistered in case of cancellation
            // cts.Token.Register(disposeAll.Dispose);

            return disposeAll;
        }

        private static ConsoleColor[] colors = new ConsoleColor[]
        {
            ConsoleColor.Black,
            ConsoleColor.DarkBlue,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkRed,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkYellow,
            ConsoleColor.Gray,
            ConsoleColor.DarkGray,
            ConsoleColor.Blue,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Red,
            ConsoleColor.Magenta,
            ConsoleColor.Yellow,
            ConsoleColor.White
        };
    }
}
