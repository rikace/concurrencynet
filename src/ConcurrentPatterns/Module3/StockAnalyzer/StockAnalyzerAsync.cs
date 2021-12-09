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

        /// Download Stock info from "alphavantage" website
        /// go to the https://www.alphavantage.co website to get a Free Key, then update the
        /// value KEY in the function "alphavantageSourceUrl"
        private static async Task<string> HttpDownloadStockHistory(string symbol, CancellationToken token)
        {
            string stockUrl = Utils.alphavantageSourceUrl(symbol);
            var request = await new HttpClient().GetAsync(stockUrl, token);
            return await request.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }

        // Fetch Stock info from local file system
        private static async Task<string> FetchStockHistory(string symbol, CancellationToken token)
        {
            var filePath = Path.Combine("../../../../../Data/Tickers", $"{symbol}.csv");
            using (var reader = new StreamReader(filePath))
                return await reader.ReadToEndAsync().ConfigureAwait(false);

            // const string tickerPath = "../../../../../Data/Tickers";
            // return await File.ReadAllTextAsync($"{tickerPath}/{symbol}.csv", token);
        }

        // TODO LAB
        static async Task<string> ProcessStockHistoryConditional(string symbol, CancellationToken token)
        {
            Func<string, Task<string>> downloadStock = stock => HttpDownloadStockHistory(stock, token);
            Func<string, Task<string>> fetchStock = stock => FetchStockHistory(stock, token);

            // TODO LAB
            // Take a look at these operators (in \AsyncOperation\AsyncEx)
            //  - AsyncEx.Retry
            //  - AsyncEx.Otherwise
            //
            // Implement a reliable way to retrieve the stocks (methods FetchStockHistory and HttpDownloadStockHistory in case of error/fallback)
            // using these operators. Ideally, you should use both Retry and Otherwise

            // BONUS : try to control the degree of parallelism using "RequestGate.cs"

            // TODO LAB
            // add here the Data transformation into "Tuple<string, StockData[]>" rather then into the function ProcessStockHistory.
            // This data transformation should use a continuation style (look for example into LINQ / SelectMany style, for example the Bind and Map function into the Async module)
            // .Map(prices => Tuple.Create(symbol, prices));

            #region Solution

            return await AsyncEx.Retry(
                () => downloadStock(symbol).Otherwise(() => fetchStock(symbol)),
                5, TimeSpan.FromSeconds(2));


                 // .Bind(data => StockUtils.ConvertStockHistory(data))
                 // .Map(prices => Tuple.Create(symbol, prices));

            #endregion

            return null;
        }

        /// Process Stock downloading the stock info from "alphavantage" website
        private static async Task<Tuple<string, StockData[]>> ProcessStockHistory(string symbol, CancellationToken cTok)
        {
            string stockHistory = await ProcessStockHistoryConditional(symbol, cTok);
            StockData[] stockData = await Utils.ConvertStockHistory(stockHistory);
            return Tuple.Create(symbol, stockData);
        }

        /// Process a set of Stocks downloading the stocks info from "alphavantage" website
        public static async Task ProcessStockHistoryParallel(IEnumerable<string> stockSymbols, CancellationToken cTok)
        {
            IEnumerable<Task<Tuple<string, StockData[]>>> stockHistoryTasks =
                // TODO LAB add ToList to run in parallel
                stockSymbols.Select(stock => ProcessStockHistory(stock, cTok));

            // TODO LAB replace the foreach loop using
            // (1) Control the degree of parallelism using "RequestGate.cs"
            // (2) Try to use Async Stream to consume the data
            // (3) Use Task Continuation to execute the final step "DisplayStockInfos"
            //     Example: await Task.WhenAll(stockHistoryTasks).ContinueWith(stockData =>


            var stockHistories = new List<Tuple<string, StockData[]>>();
            foreach (var stockTask in stockHistoryTasks)
                stockHistories.Add(await stockTask);

            DisplayStockInfos(stockHistories);
        }
    }
}
