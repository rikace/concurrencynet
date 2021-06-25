using System;
using System.IO;
using System.Threading.Tasks;
using Dataflow.WebCrawler;
using Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ProducersConsumers
{
    class Program
    {


        static async Task Main(string[] args)
        {
            string sourcePaintings = "../../../../../Data/paintings";
            string sourceImages = "../../../../../Data/Images";

            //var images = Directory.GetFiles(sourcePaintings, "*.jpg");

            var destination = "./Images/Output";
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            // TODO 1
            // var pc = new ProducerConsumer.BlockingCollectionProdCons();
            // pc.Run(source, destination);

            // TODO bonus complete the code missing in
            // await ProducerConsumerWebCrawler.Run();

            // TODO 2
            // var pc = new ProducersConsumers.ChannelProdsCons();
            // pc.Run(source, destination);

            // TODO 3
            //pc.Run(new[]{sourceImages,sourcePaintings}, destination);

            // Example how to Async/Stream Channel
            //     await foreach (var receivedItem in channel.ReadAllAsync())
            //     {
            //         var outputItem = await imageProcessingHelpers.SaveImage_Step4(receivedItem);
            //         // do something with outputItem
            //     }

            Console.WriteLine("Complete!");
            Console.ReadLine();
        }
    }
}
