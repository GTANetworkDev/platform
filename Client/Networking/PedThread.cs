using System;
using GTA;
using GTANetwork.Util;

namespace GTANetwork.Networking
{
    public class PedThread : Script
    {
        public PedThread()
        {
            Tick += OnTick;
        }
        
        public static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;
            if (sender.GetType() != typeof(string) && !Main.Multithreading) return;

            //var sw = new Stopwatch();
            //sw.Start();

            LogManager.DebugLog("START LOOP");

            for (int i = 0; i < StreamerThread.MAX_PLAYERS; i++)
            {
                if (i >= StreamerThread.StreamedInPlayers.Length) break;
                StreamerThread.StreamedInPlayers[i]?.DisplayLocally();
            }

            //sw.Stop();

            LogManager.DebugLog("END LOOP");
        }
    }
}