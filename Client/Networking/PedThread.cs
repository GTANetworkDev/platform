using System;
using GTA;
using GTANetwork.Util;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;

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
        public static int InRangePlayers = 0;
        public static int OutRangePlayers = 0;
        public static bool ToggleNametag = true;
        public static bool ToggleUpdate = true;
        public static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.NumPad1) ToggleUpdate = !ToggleUpdate;
            if (e.KeyCode == Keys.NumPad2) ToggleNametag = !ToggleNametag;
        }
#endif
        public  static void OnTick(object sender, EventArgs e)
        {
#if DEBUG
            InRangePlayers = 0;
            OutRangePlayers = 0;
            var sw = new Stopwatch();
#endif
            if (!Main.IsOnServer()) return;
            if (sender.GetType() != typeof(string) && !Main.Multithreading) return;
#if DEBUG
            sw.Start();
#endif
            //foreach (SyncPed StreamedInPlayers in StreamerThread.StreamedInPlayers)
            //{
            //    StreamedInPlayers?.DisplayLocally();
            //}

            // For loop is recommended
            for (int i = StreamerThread.StreamedInPlayers.Length - 1; i >= 0; i--)
            {
                StreamerThread.StreamedInPlayers[i]?.DisplayLocally();
            }
#if DEBUG
            sw.Stop();

            Util.Util.DrawText("Stream Loop: " + sw.ElapsedMilliseconds + " ms", 5, 380, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Show Nametags: " + ToggleNametag.ToString(), 5, 420, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Update Players: " + ToggleUpdate.ToString(), 5, 440, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Spawned Vehicles: " + GTA.World.GetAllVehicles().Length + "", 5, 480, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Spawned Peds: " + GTA.World.GetAllPeds().Length + "", 5, 500, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Streamed Players: " + InRangePlayers + "", 5, 540, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            //Util.Util.DrawText("CurrentStreamedPlayersInRange: " + StreamedPlayersInRange + "", 600, 920, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);
#endif
            Util.Util.DrawText(Game.FPS.ToString("0"), Screen.PrimaryScreen.WorkingArea.Width - 20, 0, 0.35f, 255, 255, 255, 255, 0, 1, false, true, 0);

        }
    }
}