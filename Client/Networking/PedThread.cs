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
        public static bool FPS = true;


        public static int InRangePlayers = 0;
        public static int OutRangePlayers = 0;
        public static bool StreamerDebug = false;
#if DEBUG
        public static bool ToggleNametag = true;
        public static bool ToggleUpdate = true;
        public static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.NumPad1) ToggleUpdate = !ToggleUpdate;
            if (e.KeyCode == Keys.NumPad2) ToggleNametag = !ToggleNametag;
        }
#endif

        private static int position = 400;
        private static int offset = 25;
        private static int n = 1;
        public static void OnTick(object sender, EventArgs e)
        {

            InRangePlayers = 0;
            OutRangePlayers = 0;
            var sw = new Stopwatch();

            if (!Main.IsOnServer()) return;
            if (sender.GetType() != typeof(string) && !Main.Multithreading) return;

            if(StreamerDebug) sw.Start();

            //foreach (SyncPed StreamedInPlayers in StreamerThread.StreamedInPlayers)
            //{
            //    StreamedInPlayers?.DisplayLocally();
            //}

            // For loop is recommended
            for (int i = StreamerThread.StreamedInPlayers.Length - 1; i >= 0; i--)
            {
                StreamerThread.StreamedInPlayers[i]?.DisplayLocally();
            }

            if (StreamerDebug) sw.Stop();


#if DEBUG
            Util.Util.DrawText("Show Nametags: " + ToggleNametag.ToString(), 5, 420, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
            Util.Util.DrawText("Update Players: " + ToggleUpdate.ToString(), 5, 440, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
#endif
            if(StreamerDebug)
            {
                Util.Util.DrawText("Rendering Latency: " + sw.ElapsedMilliseconds + " ms", 5, position, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n++;
                Util.Util.DrawText("Streamed Players: " + (StreamerThread.StreamedInPlayers.Length + 1) + "/" + (StreamerThread.StreamedInPlayers.Length + StreamerThread.StreamedOutPlayers + 1), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Entities: " + StreamerThread.StreamedInItems + "/" + (StreamerThread.StreamedInItems + StreamerThread.StreamedOutItems), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Vehicles: " + StreamerThread.StreamedInVehicles + "/" + (StreamerThread.StreamedInVehicles + StreamerThread.StreamedOutVehicles), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Objects: " + StreamerThread.StreamedInObjects + "/" + (StreamerThread.StreamedInObjects + StreamerThread.StreamedOutObjects), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Labels: " + StreamerThread.StreamedInLabels + "/" + (StreamerThread.StreamedInLabels + StreamerThread.StreamedOutLabels), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Pickups: " + StreamerThread.StreamedInPickups + "/" + (StreamerThread.StreamedInPickups + StreamerThread.StreamedOutPickups), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Blips: " + StreamerThread.StreamedInBlips + "/" + (StreamerThread.StreamedInBlips + StreamerThread.StreamedOutBlips), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed Particles: " + StreamerThread.StreamedInParticles + "/" + (StreamerThread.StreamedInParticles + StreamerThread.StreamedOutParticles), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Streamed API Peds: " + StreamerThread.StreamedInPeds + "/" + (StreamerThread.StreamedInPeds + StreamerThread.StreamedOutPeds), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n++;
                Util.Util.DrawText("Spawned Peds: " + GTA.World.GetAllPeds().Length + "", 5, (position + (offset * n++)), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Spawned Vehicles: " + GTA.World.GetAllVehicles().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Spawned Blips: " + GTA.World.GetAllBlips().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Spawned Props: " + GTA.World.GetAllProps().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.Util.DrawText("Spawned Entities: " + GTA.World.GetAllEntities().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n = 1;
            }

            //Util.Util.DrawText("CurrentStreamedPlayersInRange: " + StreamedPlayersInRange + "", 600, 920, 0.5f, 255, 255, 255, 255, 0, 1, false, true, 0);

            if(FPS) Util.Util.DrawText(Game.FPS.ToString("0"), Screen.PrimaryScreen.WorkingArea.Width - 20, 0, 0.35f, 255, 255, 255, 255, 0, 1, false, true, 0);

        }
    }
}