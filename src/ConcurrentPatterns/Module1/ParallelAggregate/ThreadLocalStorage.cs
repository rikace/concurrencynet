using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Helpers.Helpers;

namespace DataParallelism
{
   public class ThreadLocalStorage
    {
         public static void Start(string text)
        {
            var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            //word count total
            Int32 total = 0;

            // Sting is type of source elements
            // int32 is type of thread-local count variable
            // wordlist is the source collection
            // ()=>0 initializes local variable
            Parallel.ForEach<String, Int32>(lines,
                localInit:() => 0,
                body:(line, loopstate, count) =>  // method invoked on each iteration of loop
                    {
                        var words = line.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();
                        if (words.Any(word => word.Equals("species")))
                        {
                            count++; // increment the count
                        }
                        return count;
                    },
                localFinally:(result) => Interlocked.Add(ref total, result)); // executed when all loops have completed

            Console.WriteLine("The word specied occured {0} times.", total);
            Console.ReadLine();
        }
    }
}
