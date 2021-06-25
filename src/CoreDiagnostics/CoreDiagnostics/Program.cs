using System;
using System.Diagnostics;

namespace CoreDiagnostics
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Process ID # {Process.GetCurrentProcess().Id}");
            MemoryLeak.Start();

            Console.WriteLine("Test Completed!");
            Console.ReadLine();
        }
    }
}
