using System;
using System.Collections.Generic;
using System.Threading;

namespace CSharp.Parallelx
{
    
    public class Stride
    {
        public static void For(int inclusive, int exclusive, Action<int> action)
        {
            int size = exclusive - inclusive;
            int procs = Environment.ProcessorCount;
            int range = size / procs;
            int done = procs;

            var threads = new List<Thread>(procs);
            using (var mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < procs; i++)
                {
                    int start = i * range + inclusive;
                    int end = (procs == procs - 1) ? exclusive : start + range;
                    threads.Add(new Thread(() =>
                    {
                        for (int j = start; start < end; start++)
                        {
                            action.Invoke(j);
                            if (Interlocked.Decrement(ref done) == 0) mre.Set();
                        }
                    }));
                }
                foreach (var thread in threads) thread.Start();
                mre.WaitOne();
            }
        }

        public static void ForStriping(int inclusive, int exclusive, Action<int> action)
        {
         //   int size = exclusive - inclusive;
            int procs = Environment.ProcessorCount;
            int done = procs;
            int nextIteration = inclusive;
            const int batchSize = 3;

            var threads = new List<Thread>(procs);
            using (var mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < procs; i++)
                {
                    threads.Add(new Thread(() =>
                    {
                        int index;
                        while ((index = Interlocked.Add(ref nextIteration, batchSize) - batchSize) < exclusive)
                        {
                            int end = index + batchSize;
                            if (end >= exclusive)
                                end = exclusive;
                            for (int j = index; j < end; j++)
                                action.Invoke(j);
                        }
                        if (Interlocked.Decrement(ref done) == 0) mre.Set();
                    }));
                }
                foreach (var thread in threads) thread.Start();
                mre.WaitOne();
            }
        }
    }
}