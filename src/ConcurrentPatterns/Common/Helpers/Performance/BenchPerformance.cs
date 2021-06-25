using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Helpers
{
    public sealed class BenchPerformance : IDisposable
    {
        private static Int64[] m_arr;
        private readonly Int32 m_gen0Start;
        private readonly Int32 m_gen2Start;
        private readonly Int32 m_get1Start;
        private readonly Int64 m_startTime;
        private readonly String m_text;

        private BenchPerformance(Boolean startFresh,
            String format,
            params Object[] args)
        {
            if (startFresh)
            {
                PrepareForOperation();
            }

            m_text = String.Format(format, args);

            m_gen0Start = GC.CollectionCount(0);
            m_get1Start = GC.CollectionCount(1);
            m_gen2Start = GC.CollectionCount(2);

            m_startTime = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            Int64 elapsedTime = Stopwatch.GetTimestamp() - m_startTime;
            Int64 milliseconds = (elapsedTime * 1000) / Stopwatch.Frequency;

            if (false == String.IsNullOrEmpty(m_text))
            {
                ConsoleColor defColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                String title = String.Format("\tOperation > {0} <", m_text);
                String gcInfo =
                    String.Format("\tGC(G0={1,4}, G1={2,4}, G2={3,4})\n\tTotal Time  {0,7:N0}ms \n",
                        milliseconds,
                        GC.CollectionCount(0) - m_gen0Start,
                        GC.CollectionCount(1) - m_get1Start,
                        GC.CollectionCount(2) - m_gen2Start);

                Console.WriteLine(new String('*', gcInfo.Length));
                Console.WriteLine();
                Console.WriteLine(title);
                Console.WriteLine();
                Console.ForegroundColor = defColor;
                if (m_arr.Length > 1)
                {
                    Console.WriteLine(String.Format("\tRepeat times {0}", m_arr.Length));
                    Console.WriteLine(String.Format("\tBest Time {0} ms", m_arr.Min()));
                    Console.WriteLine(String.Format("\tWorst Time {0} ms", m_arr.Max()));
                    Console.WriteLine(String.Format("\tAvarage Time {0} ms", m_arr.Average()));
                }

                Console.WriteLine();
                Console.WriteLine(gcInfo);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(new String('*', gcInfo.Length));
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(new String('*', gcInfo.Length));
                Console.ForegroundColor = defColor;
            }
        }

        public static void Time(String text,
            Action operation,
            Int32 iterations = 1, Boolean startFresh = true)
        {
            using (new BenchPerformance(startFresh, text))
            {
                m_arr = new long[iterations];
                var watch = new Stopwatch();
                for (int i = 0; i < iterations; i++)
                {
                    watch.Start();
                    operation();
                    watch.Stop();
                    m_arr[i] = watch.ElapsedMilliseconds;
                    watch.Reset();
                }
            }
        }

        private static void PrepareForOperation()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Time(String.Empty, () => Thread.Sleep(0), 1, false);
        }
    }
}
