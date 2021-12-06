using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace ReactiveStockTickers
{
    class FileLinesStream<T>
    {
        public FileLinesStream(string filePath, Func<string, T> map)
        {
            _filePath = filePath;
            _map = map;
            _data = new List<T>();
        }

        private string _filePath;
        private List<T> _data;
        private Func<string, T> _map;

        // TODO convert to IAsyncEnumerable
        public IEnumerable<T> GetLines()
        {
            const string tickerPath = "../../../../../Data/Tickers";
            // TODO RT use FileStream Async
            using (var stream = File.OpenRead(Path.Combine(tickerPath, _filePath)))
            using (var reader = new StreamReader(stream))
            {
                // TODO : create/convert to async operation
                //        possibly convert the "GetLine" function into IAsyncEnumerable
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var value = _map(line);
                    if (value != null)
                        _data.Add(value);
                }
            }
            _data.Reverse();
            while (true)
                foreach (var item in _data)
                    yield return item;
        }

        // TODO LAB
        // enable Task scheduler to generate the stream of events concurrently
        public IObservable<T> ObserveLines() => GetLines().ToObservable();
    }

}
