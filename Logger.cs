using System;

namespace WC.SARS
{
    class Logger
    {
        public static void Header(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void Success(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void Failure(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void Warn(string txt)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void Basic(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void DebugServer(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void testmsg(string txt)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
        public static void missingHandle(string txt)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(txt);
            Console.ResetColor();
        }
    }
}
