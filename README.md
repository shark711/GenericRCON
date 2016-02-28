# GenericRCON
Source RCON Protocol

# Example Command Line App
using System;

namespace RCONTest
{
    class Program
    {
        static void Main(string[] args)
        {
            GenericRCON.RCONClient RCON = new GenericRCON.RCONClient("password", "127.0.0.1", 27015);
            Console.WriteLine("Trying to connect and authenticate");
            Console.WriteLine("Authenticated: {0} ", RCON.Authenticate());
            if (RCON.Authenticated)
            {
                Console.WriteLine("-Send Command list Players:\n{0}", RCON.SendCommand("Status"));
                Console.WriteLine("-Send Command list Players:\n{0}", RCON.SendCommand("ListPlayers"));
                Console.WriteLine("-Send Command GetChat:\n{0}", RCON.SendCommand("GetChat"));
            }

            // Keep the console window open in debug mode.
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
