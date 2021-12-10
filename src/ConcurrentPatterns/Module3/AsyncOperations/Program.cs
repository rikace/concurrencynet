using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Functional.Async;

namespace AsyncOperations
{
    class Program
    {
        static List<string> urls = new List<string>
        {
            @"https://www.google.com",
            @"https://www.amazon.com",
            @"https://www.bing.com",
            @"https://www.google.com",
            @"https://www.facebook.com"
        };


        void CooperativeCancellation()
        {
            //  Cooperative cancellation token
            CancellationTokenSource ctsOne = new CancellationTokenSource();
            CancellationTokenSource ctsTwo = new CancellationTokenSource();
            CancellationTokenSource ctsComposite =
                CancellationTokenSource.CreateLinkedTokenSource(ctsOne.Token, ctsTwo.Token);

            CancellationToken ctsCompositeToken = ctsComposite.Token;
            Task.Factory.StartNew(async () =>
            {
                var webClient = new WebClient();
                ctsCompositeToken.Register(() => webClient.CancelAsync());

                await webClient.DownloadDataTaskAsync("http://www.manning.com");
            }, ctsComposite.Token);
        }

        static async Task DownloadSiteIconAsyncExample()
        {
            // TODO LAB
            // control the degree of parallelism
            // use either (or both) "RequestGate" and/or "ExecuteInWithDegreeOfParallelism" class(s)
            // to be implemented

            var tasks = (from url in urls
                select ThrottleAsyncOperations.DownloadSiteIconAsync(url, $"./Images/Output/{Guid.NewGuid().ToString("N")}.jpg"));

            // ensure that the tasks run in parallel
            // wait for all the tasks to complete

            Console.WriteLine("All icons downloaded!");
        }

        static void WebCrawlerExample()
        {
            WebCrawlerAsync.RunDemo(urls);
        }

        static async Task Main(string[] args)
        {
            var destination = "./Images/Output";
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            // TODO LAB 1
            WebCrawlerAsync.RunDemo(urls);

            Console.WriteLine("Completed Step 1");
            Console.ReadLine();

            // TODO LAB 2
            await ThrottleAsyncOperations.DownloadSiteIconsAsync(urls);


            Console.WriteLine("Completed Step 2");
            Console.ReadLine();
        }
    }
}
