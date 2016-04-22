using System;
using System.Collections.Generic;
using Rage;

namespace GTANetwork
{
    public class InputboxThread 
    {
        public static string GetUserInput(string defaultText, int maxLen, Action spinner)
        {
            string output = null;

            var newFiber = GameFiber.StartNew(delegate
            {
                output = Util.GetUserInput(defaultText, maxLen);
            });
            
            Main.BlockControls = true;

            
            while (output == null)
            {
                spinner.Invoke();
                GameFiber.Yield();
            }

            Main.BlockControls = false;
            return output;
        }

        public static string GetUserInput(int maxLen, Action spinner)
        {
            return GetUserInput("", maxLen, spinner);
        }

        public static string GetUserInput(Action spinner)
        {
            return GetUserInput("", 40, spinner);
        }
    }
}