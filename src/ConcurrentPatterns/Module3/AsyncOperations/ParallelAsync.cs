namespace AsyncOperations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Linq;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class ParallelAsync
    {
        List<string> urls = new List<string>
        {
            "http://www.bing.com", "http://www.google.com", "http://www.yahoo.com",
            "http://www.facebook.com", "http://www.youtube.com", "http://www.reddit.com",
            "http://www.digg.com", "http://www.twitter.com", "http://www.gmail.com",
            "http://www.docs.google.com", "http://www.maps.google.com",
            "http://www.microsoft.com", "http://www.netflix.com", "http://www.hulu.com"
        };

        public async Task<string> HttpAsync(string url)
        {
            var req = WebRequest.Create(new Uri(url));
            using (var resp = await req.GetResponseAsync())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var contents = await reader.ReadToEndAsync();
                return contents;
            }
        }

        public string HttpSync(string url)
        {
            var req = WebRequest.Create(new Uri(url));
            using (var resp = req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var contents = reader.ReadToEnd();
                return contents;
            }
        }

        public void TestHttpSync()
        {
            var sw = Stopwatch.StartNew();
            var contents = from url in urls
                select HttpSync(url);

            foreach (var content in contents)
            {
                Console.WriteLine($"Url size {content.Length}");
            }

            Console.WriteLine($"TestHttpSync completed in {sw.ElapsedMilliseconds}ms");
        }

        public async Task TestHttpAsync()
        {
            // TODO
            //      add a way to throttle/limit the number of concurrent operations
            //      The RequestGate is a good approach (but not the only one)
            var sw = Stopwatch.StartNew();
            var contentTasks = from url in urls
                select HttpAsync(url);

            foreach (var contentTask in contentTasks)
            {
                var content = await contentTask;
                Console.WriteLine($"Url size {content.Length}");
            }

            Console.WriteLine($"TestHttpAsync completed in {sw.ElapsedMilliseconds}ms");
        }
    }
}
