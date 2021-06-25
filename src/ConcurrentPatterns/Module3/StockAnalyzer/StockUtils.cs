using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace StockAnalyzer
{
    public static class StockUtils
    {
        public static void DisplayStockInfos(IEnumerable<Tuple<string, StockData[]>> stockHistories)
        {
            foreach (var stockHistory in stockHistories)
            {
                DisplayStockInfo(stockHistory);
            }
        }

        public static void DisplayStockInfo(Tuple<string, StockData[]> stockHistory)
        {
            var legendText = stockHistory.Item1;
            var highest = stockHistory.Item2.OrderByDescending(f => f.High).First();
            var lowest = stockHistory.Item2.OrderBy(f => f.Low).First();

            PrintWithColor(legendText,
                $"highest price on date {highest.Date} - High ${highest.High} - Low ${highest.Low} - Thread Id# {Thread.CurrentThread.ManagedThreadId}");
            PrintWithColor(legendText,
                $"lowest price on date {lowest.Date} - Low ${lowest.High} - Low ${highest.Low} - Thread Id# {Thread.CurrentThread.ManagedThreadId}");
        }

        static void PrintWithColor(string stock, string text)
        {
            var color = Console.ForegroundColor;
            if (stock == "MSFT")
                Console.ForegroundColor = ConsoleColor.Green;
            else if (stock == "FB")
                Console.ForegroundColor = ConsoleColor.Blue;
            else if (stock == "AAPL")
                Console.ForegroundColor = ConsoleColor.Red;
            else if (stock == "GOOG")
                Console.ForegroundColor = ConsoleColor.Magenta;
            else if (stock == "AMZN")
                Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{stock} - {text} (Thread Id #{Thread.CurrentThread.ManagedThreadId})");
            Console.ForegroundColor = color;
        }

        public static readonly string[] Stocks =
            new[] {"MSFT", "FB", "AAPL", "GOOG", "AMZN"};

        private const string KEY = "W3LUV5WID6C0PV5L";
        //  The Or combinator applies to falls back behavior
        public static Func<string, string> alphavantageSourceUrl = (symbol) =>
            $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol={symbol}&outputsize=full&apikey={KEY}&datatype=csv";

        public static Func<string, string> stooqSourceUrl = (symbol) =>
            $"https://stooq.com/q/d/l/?s={symbol}.US&i=d";

        //  Stock prices history analysis
        public static async Task<StockData[]> ConvertStockHistory(string stockHistory)
        {
            return await Task.Run(() =>
            {
                string[] stockHistoryRows =
                    stockHistory.Split(Environment.NewLine.ToCharArray(),
                        StringSplitOptions.RemoveEmptyEntries);
                return (from row in stockHistoryRows.Skip(1)
                        let cells = row.Split(',')
                        let date = DateTime.Parse(cells[0])
                        let open = double.TryParse(cells[1], out _) ? double.Parse(cells[1]) : 0
                        let high = double.TryParse(cells[2], out _) ? double.Parse(cells[2]) : 0
                        let low = double.TryParse(cells[3], out _) ? double.Parse(cells[3]) : 0
                        let close = double.TryParse(cells[4], out _) ? double.Parse(cells[4]) : 0
                        select new StockData(date, open, high, low, close)
                    ).ToArray();
            });
        }

        public static async Task<string> DownloadStockHistory(string symbol)
        {
            var filePath = Path.Combine("../../../../../Data/Tickers", $"{symbol}.csv"); // OK
            using (var reader = new StreamReader(filePath))
                return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
