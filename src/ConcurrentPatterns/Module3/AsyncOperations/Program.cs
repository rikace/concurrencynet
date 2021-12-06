using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
