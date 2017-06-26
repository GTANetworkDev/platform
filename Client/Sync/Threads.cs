using System;
using GTA;
using GTANetwork.Util;
using GTANetwork.Streamer;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using GTA.Native;

namespace GTANetwork.Sync
{
    public class SyncThread : Script
    {
        public SyncThread() { Tick += OnTick; 
        }

        public static Stopwatch sw;

        private static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected() || !Main.IsOnServer()) return;
            
            sw = new Stopwatch();
            if (DebugInfo.StreamerDebug) sw.Start();

            SyncPed[] myBubble;
            lock (StreamerThread.StreamedInPlayers) { myBubble = StreamerThread.StreamedInPlayers.ToArray(); }
            for (var i = myBubble.Length - 1; i >= 0; i--) { myBubble[i]?.Render(); }

            if (DebugInfo.StreamerDebug) sw.Stop();
        }
    }

    public class NametagThread : Script
    {
        public NametagThread() { Tick += OnTick;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsConnected() || !Main.IsOnServer()) return;

            SyncPed[] myBubble;
            CallCollection nametagCollection = new CallCollection();
            lock (StreamerThread.StreamedInPlayers) { myBubble = StreamerThread.StreamedInPlayers.Take(50).ToArray(); }
            for (var i = myBubble.Length - 1; i >= 0; i--) { myBubble[i]?.DrawNametag(nametagCollection); }
            nametagCollection.Execute();
        }
    }

    
}