using System;
using Evolution;



namespace Evolution
{
    class Colour
    {
        public void Write(string flag, string text)
        {
            switch(flag)
            {
                case "g":
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;

                case "r":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case "b":
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                    
                case "y":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error - {flag} is not available");
                    Console.ResetColor();
                    break;
            }
            
            Console.Write(text);
            Console.ResetColor();
        }


        public void WriteLine(string flag, string text)
        {
            switch(flag)
            {
                case "g":
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;

                case "r":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case "b":
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                    
                case "y":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error - {flag} is not available");
                    Console.ResetColor();
                    break;
            }
            
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}