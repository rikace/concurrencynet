namespace DataFlowPipeline.Compression
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FileEx = Helpers.FileEx;

    public class ExecuteCompression
    {
        public static async Task Start()
        {
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("Select the process count to run the \"Compress and Encrypt\"");
                Console.WriteLine("Insert number");
                var pc = Console.ReadLine();

                if (Int32.TryParse(pc, out var processCount))
                {
                    await RunGcComparison(new[] {0.5, 1, 2}, processCount);
                }
                else
                    break;

                Console.WriteLine();
                Console.WriteLine();
            }
        }

        private static string workDirectory = @"./Data";
        private static string templateFile = @"./Data/TextData.txt";

        private static async Task CompressAndEncryptTest(string srcFile, string dstFile, string rstFile,
            int processorCount, double fileSize)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                Console.WriteLine("CompressAndEncrypt ...");
                var dataflow = new CompressAndEncryptPipeline(processorCount);

                if (File.Exists(dstFile))
                    File.Delete(dstFile);

                using (var streamSource = new FileStream(srcFile, FileMode.OpenOrCreate, FileAccess.Read,
                    FileShare.None, 0x1000, useAsync: true))
                using (var streamDestination = new FileStream(dstFile, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.None, 0x1000, useAsync: true))
                {
                    await BenchMark($"CompressAndEncrypt file size {fileSize}GB", processorCount, async () =>
                    {
                        await dataflow.CompressAndEncrypt(streamSource, streamDestination);
                        streamDestination.Close();
                    });
                }

                Console.WriteLine("Press Enter to continue");
                Console.ReadLine();
                sw.Restart();

                if (File.Exists(rstFile))
                    File.Delete(rstFile);

                Console.WriteLine("DecryptAndDecompress ...");
                using (var streamSource = new FileStream(dstFile, FileMode.OpenOrCreate, FileAccess.Read,
                    FileShare.None, 0x1000, useAsync: true))
                using (var streamDestination = new FileStream(rstFile, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.None, 0x1000, useAsync: true))
                {
                    await BenchMark($"DecryptAndDecompress file size {fileSize}GB", processorCount,
                        async () => { await dataflow.DecryptAndDecompress(streamSource, streamDestination); });
                }

                Console.WriteLine("Press Enter to continue");
                Console.ReadLine();

                Console.WriteLine("Press [a] to run Verification");
                Console.WriteLine("Press [b] to skip Verification");

                var choice = Console.ReadLine();
                if (choice[0] == 'a')
                {
                    using (var f1 = File.OpenRead(srcFile))
                    using (var f2 = File.OpenRead(rstFile))
                    {
                        bool ok = false;
                        if (f1.Length == f2.Length)
                        {
                            ok = true;
                            int count;
                            const int size = 0x1000000;

                            var buffer = new byte[size];
                            var buffer2 = new byte[size];

                            while ((count = f1.Read(buffer, 0, buffer.Length)) > 0 && ok == true)
                            {
                                f2.Read(buffer2, 0, count);
                                ok = buffer2.SequenceEqual(buffer);
                                if (!ok) break;
                            }
                        }

                        Console.WriteLine($"Restored file isCorrect = {ok}");
                    }
                }

                Console.WriteLine("Press Enter to continue");
                Console.ReadLine();
            }
            catch (AggregateException ex)
            {
                var q = new Queue<Exception>(new[] {ex});
                while (q.Count > 0)
                {
                    var e = q.Dequeue();
                    Console.WriteLine($"\t{e.Message}");
                    if (e is AggregateException)
                    {
                        foreach (var x in (e as AggregateException).InnerExceptions)
                            q.Enqueue(x);
                    }
                    else
                    {
                        if (e.InnerException != null)
                            q.Enqueue(e.InnerException);
                    }
                }
            }
        }

        private static void CreateTextFileWithSize(string path, string templateFilePath, long targetSize)
        {
            var bytes = File.ReadAllBytes(templateFilePath);

            using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                var iterations = (targetSize - fs.Length + bytes.Length) / bytes.Length;

                for (var i = 0; i < iterations; i++)
                    fs.Write(bytes, 0, bytes.Length);
            }
        }

        private static async Task RunGcComparison(double[] fileSizesInGb, int processorCount)
        {
            //const long bytesInGb = 1024 * 1024 * 1024;
            //var bytesInGbe = 0.5 * 1024L * 1024 * 1024;

            string inFile = Path.Combine(workDirectory, "inFile.txt");
            string outFile = Path.Combine(workDirectory, "outFile.txt");


            foreach (var size in fileSizesInGb)
            {
                Console.WriteLine($"Creating input file {size} GB ...");
                if (File.Exists(inFile))
                    File.Delete(inFile);

                CreateTextFileWithSize(inFile, templateFile, /* bytesInGb*/ (long) (1024 * 1024 * 1024 * size));

                if (File.Exists(outFile))
                    File.Delete(outFile);
                Console.WriteLine("GC.Collect() ...");
                GC.Collect();
                GC.Collect();
                Console.WriteLine($"Running compression test...");

                await CompressAndEncryptTest(inFile, outFile,
                    Path.Combine(workDirectory, Path.GetFileName(inFile) + "_copy" + Path.GetExtension(inFile)),
                    processorCount, size);
            }
        }

        private static async Task BenchMark(string label, int processorCount, Func<Task> operation)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();

            // do a full GC at the start but NOT thereafter
            // allow garbage to collect for each iteration
            System.GC.Collect();
            Console.WriteLine("Started");

            Func<Tuple<int, int, int, long>> getGcStats = () =>
            {
                var gen0 = System.GC.CollectionCount(0);
                var gen1 = System.GC.CollectionCount(1);
                var gen2 = System.GC.CollectionCount(2);
                var mem = System.GC.GetTotalMemory(false);
                return Tuple.Create(gen0, gen1, gen2, mem);
            };

            Console.WriteLine("=====================================================================");
            Console.WriteLine($"Operation {label} run with processor count #{processorCount}");
            Console.WriteLine("=====================================================================");

            var gcStatsBegin = getGcStats();
            stopwatch.Restart();
            await operation();
            stopwatch.Stop();
            var gcStatsEnd = getGcStats();
            var changeInMem = Math.Abs((gcStatsEnd.Item4 - gcStatsBegin.Item4) / 1000L);
            Console.WriteLine(
                $"Operation {label} run with processor count #{processorCount} - elapsed:{stopwatch.ElapsedMilliseconds}ms gen0:{(gcStatsEnd.Item1 - gcStatsBegin.Item1)} gen1:{(gcStatsEnd.Item2 - gcStatsBegin.Item2)} gen2:{(gcStatsEnd.Item3 - gcStatsBegin.Item3)} mem:{changeInMem}");
        }
    }
}
