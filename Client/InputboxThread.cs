using System;
using System.Collections.Generic;
using GTA;

namespace GTANetwork
{
    public class InputboxThread : Script
    {
        public InputboxThread()
        {
            Tick += (sender, args) =>
            {
                if (ThreadJumper.Count > 0)
                {
                    ThreadJumper.Dequeue().Invoke();
                }
            };
        }

        public static string GetUserInput(string defaultText, int maxLen, Action spinner)
        {
            string output = null;

            ThreadJumper.Enqueue(delegate
            {
                output = Game.GetUserInput(defaultText, maxLen);
            });

            Main.BlockControls = true;

            Script.Yield();

            while (output == null)
            {
                spinner.Invoke();
                Script.Yield();
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

        public static Queue<Action> ThreadJumper = new Queue<Action>();
    }
}