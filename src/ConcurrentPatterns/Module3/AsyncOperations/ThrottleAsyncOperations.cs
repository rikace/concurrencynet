using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Functional.Async;

namespace AsyncOperations
{
    public static class ThrottleAsyncOperations
    {
        // Download an image(icon) from the network asynchronously
        private static async Task DownloadSiteIconAsync(string url, string fileDestination)
        {
            // TODO check the implementations of Bind Map Tap
            using (FileStream stream = new FileStream(fileDestination,
                FileMode.Create, FileAccess.Write,
                FileShare.Write, 0x1000, FileOptions.Asynchronous))
                await new HttpClient()
                    .GetAsync($"{url}/favicon.ico")
                    .Bind(async content => await
                        content.Content.ReadAsByteArrayAsync())
                    .Map(bytes => new MemoryStream(bytes))
                    .Tap(memStream => memStream.CopyToAsync(stream));
        }

        public static async Task DownloadSiteIconsAsync(List<string> urls)
        {
            // TODO LAB
            // control the degree of parallelism
            // use either (or both) "RequestGate" and/or "ExecuteInWithDegreeOfParallelism" class(s)
            // to be implemented
            var tasks = (from url in urls
                select DownloadSiteIconAsync(url,
                    $"./Images/Output/{Guid.NewGuid().ToString("N")}.jpg"));

            // TODO ensure that the tasks run in parallel
            // TODO wait for all the tasks to complete

            Console.WriteLine("All icons downloaded!");
        }


    }
}
