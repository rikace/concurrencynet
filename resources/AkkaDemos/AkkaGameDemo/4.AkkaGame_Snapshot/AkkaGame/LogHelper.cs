namespace AkkaGame
{
    using System;
    using static System.Console;
    
    public class LogHelper
    {
        public static void WriteLine(string message)
        {
            var originalColor = ForegroundColor;
                        
            ForegroundColor = ConsoleColor.Green;

            Console.WriteLine(message);

            ForegroundColor = originalColor;
        }
        
        public static void WriteLineYellow(string message)
        {
            var originalColor = ForegroundColor;
                        
            ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine(message);

            ForegroundColor = originalColor;
        }
        public static void WriteLineRed(string message)
        {
            var originalColor = ForegroundColor;
                        
            ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(message);

            ForegroundColor = originalColor;
        }
        public static void WriteLineCyan(string message)
        {
            var originalColor = ForegroundColor;

            ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine(message);

            ForegroundColor = originalColor;
        }
        public static void WriteLineGreen(string message)
        {
            var originalColor = ForegroundColor;
                        
            ForegroundColor = ConsoleColor.Green;

            Console.WriteLine(message);

            ForegroundColor = originalColor;
        }
    }
}