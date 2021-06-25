using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using Functional.Async;
using static StockAnalyzer.StockUtils;

namespace StockAnalyzer
{
    public partial class StockAnalyzer
    {
        public static async Task ProcessStockHistoryAsComplete(IEnumerable<string> stocks, CancellationToken cTok)
        {

            List<Task<Tuple<string, StockData[]>>> stockHistoryTasks =
                stocks.Select(symbol => ProcessStockHistory(symbol, cTok)).ToList();

            // TODO RT
            // Implement an algorithm to execute all the tasks
            // in parallel and then to process each task as soon as one completes (order does not matter)
            // Process the Task using the function "DisplayStockInfo"

            while (stockHistoryTasks.Count > 0)
            {
                // Delay for Demo purpose
                await Task.Delay(500);

                // DisplayStockInfo( < result from the task here > );
            }
        }
    }
}
