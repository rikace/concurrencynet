using System;
using System.Collections.Generic;

namespace CoreDiagnostics
{
    public class Product
    {
        string name;
        int id;
        char[] details = new char[10000];

        public Product(int id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }

    public static class MemoryLeak
    {
        public static List<Product> products = new List<Product>();

        public static void Start()
        {
            Console.WriteLine("NOTE! KEEP WATCHING THE GC HEAP SIZE IN COUNTERS");
            string answer = "";
            do
            {
                for (int i = 0; i < 10000; i++)
                {
                    products.Add(new Product(i, "product" + i));
                }

                Console.WriteLine("Leak some more? Y/N");
                answer = Console.ReadLine().ToUpper();
            } while (answer == "Y");
        }
    }
}
