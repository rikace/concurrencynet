using System;
using System.Threading;
using System.Threading.Tasks;

namespace StockAnalyzer.CS
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO
            // control the degree of parallelism
            // use either (or both) "RequestGate" and/or "ExecuteInWithDegreeOfParallelism" class(s)
            // (to be implemented)

            //  Cancellation of Asynchronous Task
            CancellationTokenSource cts = new CancellationTokenSource();

            // TODO (1)
            //Task.Factory.StartNew(
              //  async () => await StockAnalyzer.ProcessStockHistoryParallel(StockUtils.Stocks, cts.Token), cts.Token);

            // TODO (2)
            Task.Factory.StartNew(async () => await StockAnalyzer.ProcessStockHistoryThrottle(StockUtils.Stocks, cts.Token), cts.Token);

            // TODO (3)
            //Task.Factory.StartNew(async () => await StockAnalyzer.ProcessStockHistoryAsComplete(StockUtils.Stocks, cts.Token), cts.Token);

            Console.ReadLine();
            cts.Cancel();
        }
    }
}
