using System;
using GTA;
using GTANetwork.Util;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;

namespace GTANetwork.Networking
{
    public class PedThread : Script
    {
        public PedThread()
        {
            Tick += OnTick;
        }

        public static Stopwatch sw;
        public static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;
            //if (sender.GetType() != typeof(string) && !Main.Multithreading) return;
            sw = new Stopwatch();
            if (DebugInfo.StreamerDebug) sw.Start();

            for (int i = StreamerThread.StreamedInPlayers.Length - 1; i >= 0; i--) StreamerThread.StreamedInPlayers[i]?.DisplayLocally();

            if (DebugInfo.StreamerDebug) sw.Stop();
        }
    }
}