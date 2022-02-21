using System;

namespace WC.SARS
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.Basic("<< Super Animal Royale Server  >>");
            Logger.Header("Super Animal Royale Version: 0.90.2\n");
            if (args.Length > 0)
            {
                Match m = new Match(int.Parse(args[1]), args[0], false, false);
            }
            else
            {
                bool runSetup = true;

                Logger.Warn("If you know, you know. ['Y' OR 'N' key]");
                while (runSetup)
                {
                    switch (Console.ReadKey().Key)
                    {
                        case ConsoleKey.Y:
                            Logger.Basic("attempting to start a server! (port: 4206; local address: 192.168.1.15)");
                            Match match2 = new Match(4206, "192.168.1.15", false, false);
                            break;
                        case ConsoleKey.N:
                            Logger.Basic("attempting to start a server! (port: 42896; local address: 192.168.1.198)");
                            Match match1 = new Match(42896, "192.168.1.198", false, false);
                            //Match match1 = new Match(7642, "10.0.0.4", false, false);
                            break;
                        default:
                            Logger.Failure("invalid key... please try again");
                            Logger.Warn("If you know, you know. ['Y' OR 'N' key]");
                            break;
                    }
                }
            }
        }
    }
}