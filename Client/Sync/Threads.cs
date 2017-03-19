using System;
using GTA;
using GTANetwork.Util;
using GTANetwork.Streamer;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;

namespace GTANetwork.Sync
{
    public class SyncThread : Script
    {
        public SyncThread() { Tick += OnTick; }

        public static Stopwatch sw;

        public static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected()) return;
            if (!Main.IsOnServer()) return;

            sw = new Stopwatch();
            if (DebugInfo.StreamerDebug) sw.Start();



            int Length = StreamerThread.StreamedInPlayers.Length;
            for (int i = Length - 1; i >= 0; i--) { StreamerThread.StreamedInPlayers[i]?.DisplayLocally(); }


            if (DebugInfo.StreamerDebug) sw.Stop();

        }
    }

    public class NametagThread : Script
    {
        public NametagThread() { Tick += OnTick; }

        public static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected()) return;
            if (!Main.IsOnServer()) return;
            int Length = StreamerThread.StreamedInPlayers.Length;
            for (int i = Length - 1; i >= 0; i--) { StreamerThread.StreamedInPlayers[i]?.DrawNametag(); }
        }
    }

    
}