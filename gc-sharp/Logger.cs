using System;
using System.Collections.Generic;
using System.Text;

namespace gc
{
    internal class Logger
    {
        internal static string GetFormattedDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static void Debug(string message)
        {
            Console.WriteLine($"[{GetFormattedDate()}] [DEBUG] " + message);
        }
        public static void Info(string message, bool newLine = true)
        {
            if (newLine)
            {
                Console.WriteLine($"[{GetFormattedDate()}] " + message);
            }
            else
            {
                Console.Write($"[{GetFormattedDate()}] " + message);
            }
        }
        public static void Info(string message)
        {
            Info(message, true);
        }

        public static void Progress(string message)
        {
            Console.Write($"\r[{GetFormattedDate()}] " + message);
        }
    }
}
