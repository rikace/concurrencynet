using System;

namespace RxConcurrentStockTickers
{
    class Program
    {
        static void Main(string[] args)
        {
            ObservableDataStreams.RxStream();

            Console.ReadLine();
        }
    }
}
