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
using Utils = StockAnalyzer.StockUtils;

namespace StockAnalyzer
{
    public partial class StockAnalyzer
    {
        //  The Bind operator in action
        // TODO LAB
        // implement the bind operator respecting the top signature
        // the implementation should be full async (no blocking)
        // take a look at the Bind operator
        // replace using SelectMany and then use the Linq expression semantic (from ** in)
        static async Task<Tuple<string, StockData[]>> ProcessStockHistoryBind(string symbol, CancellationToken cTok)
        {
            // TODO use bind and Map
            return await ProcessStockHistoryConditional(symbol, cTok)
                // TODO complete the function Bind Map
                .Bind(stockHistory => StockUtils.ConvertStockHistory(stockHistory))
                .Map(stockData => Tuple.Create(symbol, stockData));
        }

        // TODO LAB
        // Process the Stock-History analysis for all the stocks in parallel
        public static async Task ProcessStockHistoryThrottle(IEnumerable<string> stockSymbols, CancellationToken cTok)
        {
            // TODO LAB
            // (1) Process the stock analysis in parallel
            // When all the computation complete, then output the stock details
            // Than control the level of parallelism processing max 2 stocks at a given time
            // Suggestion, use the RequestGate class (and/or ExecuteInWithDegreeOfParallelism class)

            // TODO LAB
            List<Tuple<string, StockData[]>> stockHistoryTasks = new List<Tuple<string, StockData[]>>();

            // TODO: execute in parallel to generate the "stockHistoryTasks" using the function
            // - ProcessStockHistoryBind(symbol, cTok)
            // NOTE: control the level of parallelism processing max 2 stocks at a given time using the "ExecuteInWithDegreeOfParallelism.ExecuteInParallel" function

            foreach (var symbol in stockSymbols)
            {
                // NOTE: control the level of parallelism processing max 2 stocks at a given time
                // Suggestion, use the RequestGate class

                var stockHistory = await ProcessStockHistoryBind(symbol, cTok);
                stockHistoryTasks.Add(stockHistory);
            }

            DisplayStockInfos(stockHistoryTasks);

            // (2) display the stock info
            //      DisplayStockInfo

            // (3) process each Task as they complete
            // replace point (1)
            // update the code to process the stocks in parallel and update the console (DisplayStockInfo)
            // as the results arrive
        }
    }
}
