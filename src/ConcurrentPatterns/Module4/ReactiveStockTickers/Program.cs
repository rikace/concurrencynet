using System;

namespace RxConcurrentStockTickers
{
    class Program
    {
        static void Main(string[] args)
        {
            ObservableDataStreams.RxStream();

            Console.WriteLine("Press Enter to EXIT");
            Console.ReadLine();
        }
    }
}
