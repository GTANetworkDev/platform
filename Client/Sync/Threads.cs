using System;
using GTA;
using GTANetwork.Util;
using GTANetwork.Streamer;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;

namespace GTANetwork.Sync
{
    public class SyncThread : Script
    {
        public SyncThread() { Tick += OnTick; }

        public static Stopwatch sw;

        private static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected() || !Main.IsOnServer()) return;
            
            sw = new Stopwatch();
            if (DebugInfo.StreamerDebug) sw.Start();

            SyncPed[] myBubble;
            lock (StreamerThread.StreamedInPlayers) { myBubble = StreamerThread.StreamedInPlayers.ToArray(); }
            var length = myBubble.Length;
            for (var i = length - 1; i >= 0; i--) { myBubble[i]?.DisplayLocally(); }


            if (DebugInfo.StreamerDebug) sw.Stop();

        }
    }

    public class NametagThread : Script
    {
        public NametagThread() { Tick += OnTick; }

        private static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected() || !Main.IsOnServer()) return;

            SyncPed[] myBubble;
            lock (StreamerThread.StreamedInPlayers) { myBubble = StreamerThread.StreamedInPlayers.ToArray(); }
            var length = myBubble.Length;
            for (var i = length - 1; i >= 0; i--) { myBubble[i]?.DrawNametag(); }
        }
    }

    
}