using System;
using GTA;
using GTANetwork.Util;
using System.Diagnostics;
using System.Windows.Forms;

namespace GTANetwork.Networking
{
    public class PedThread : Script
    {
        public PedThread()
        {
            Tick += OnTick;
#if DEBUG
            KeyDown += OnKeyDown;
#endif
        }

#if DEBUG
        public static int StreamedPlayers = 0;
        public static int StreamedPlayersInRange = 0;
        public static int StreamedPlayersInRangeCanBeUpdated = 0;

        public static bool DisableUpdateAndNametag = true;
        public static bool DisableUpdate = true;
        public static bool DisableUCreatedPlayer = true;
        public static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.NumPad3) DisableUpdateAndNametag = !DisableUpdateAndNametag;
            if (e.KeyCode == Keys.NumPad0) DisableUpdate = !DisableUpdate;
            if (e.KeyCode == Keys.NumPad1) DisableUCreatedPlayer = !DisableUCreatedPlayer;
        }
#endif
        public static void OnTick(object sender, EventArgs e)
        {
#if DEBUG
            StreamedPlayers = 0;
            StreamedPlayersInRange = 0;
            StreamedPlayersInRangeCanBeUpdated = 0;
            var sw = new Stopwatch();
            sw.Start();
#endif
            if (!Main.IsOnServer()) return;
            if (sender.GetType() != typeof(string) && !Main.Multithreading) return;

            foreach (SyncPed StreamedInPlayers in StreamerThread.StreamedInPlayers)
            {
                StreamedInPlayers?.DisplayLocally();
            }

#if DEBUG
            sw.Stop();

            Util.Util.DrawText("ms: " + sw.ElapsedMilliseconds + "", 600, 480, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("Disable Update Pos and Nametag: " + DisableUpdateAndNametag.ToString() + "", 600, 600, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("Total VEH: " + GTA.World.GetAllVehicles().Length + "", 600, 760, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("Total PEDS: " + GTA.World.GetAllPeds().Length + "", 600, 800, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("CurrentStreamedPlayers: " + StreamedPlayers + "", 600, 840, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("StreamedPlayersInRangeCanBeUpdated: " + StreamedPlayersInRangeCanBeUpdated + "", 600, 880, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("CurrentStreamedPlayersInRange: " + StreamedPlayersInRange + "", 600, 920, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
            Util.Util.DrawText("Game.FPS: " + Game.FPS.ToString("0.0") + "", 600, 960, 0.7f, 255, 255, 255, 255, 0, 1, false, true, 0);
#endif
                LogManager.DebugLog("END LOOP");
        }
    }
}