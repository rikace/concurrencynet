using System;

namespace ConsoleTaskEx
{
    // using ImageDetection;
    using System.IO;
    using System.Threading.Tasks;

    class Program
    {
        static void Main(string[] args)
        {
            var sourceImages = "../../../../../Data/Images";
            var destination = "./Images/Output";
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            ImageProcessing imageProc = new ImageProcessing(sourceImages, destination);

            // TODO :
            //      try different concurrent implementations

            imageProc.RunContinuation().Wait();
            // TODO: imageProc.RunTransformer().Wait();

            Console.WriteLine("Completed");
            Console.ReadLine();
        }
    }
}
