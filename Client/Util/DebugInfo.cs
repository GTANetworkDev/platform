using System;
using GTA;
using System.Windows.Forms;

namespace GTANetwork.Util
{
    public class DebugInfo : Script
    {
        public static bool FPS = true;
        public static bool StreamerDebug = false;

        private static int position = 400;
        private static int offset = 25;
        private static int n = 1;

        public static void Draw()
        {
            if (FPS) Util.DrawText(Game.FPS.ToString("0"), Screen.PrimaryScreen.WorkingArea.Width - 20, 0, 0.35f, 255, 255, 255, 255, 0, 1, false, true, 0);

            if (StreamerDebug)
            {
                Util.DrawText("Sync Latency: " + Networking.PedThread.sw.ElapsedMilliseconds + " ms", 5, position, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamer Latency: " + Networking.StreamerThread.sw.ElapsedMilliseconds + "ms", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n++;
                Util.DrawText("Streamed Players: " + (Networking.StreamerThread.StreamedInPlayers.Length + 1) + "/" + (Networking.StreamerThread.StreamedInPlayers.Length + Networking.StreamerThread.StreamedOutPlayers + 1), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Vehicles: " + Networking.StreamerThread.StreamedInVehicles + "/" + (Networking.StreamerThread.StreamedInVehicles + Networking.StreamerThread.StreamedOutVehicles), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Blips: " + Networking.StreamerThread.StreamedInBlips + "/" + (Networking.StreamerThread.StreamedInBlips + Networking.StreamerThread.StreamedOutBlips), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Objects: " + Networking.StreamerThread.StreamedInObjects + "/" + (Networking.StreamerThread.StreamedInObjects + Networking.StreamerThread.StreamedOutObjects), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Labels: " + Networking.StreamerThread.StreamedInLabels + "/" + (Networking.StreamerThread.StreamedInLabels + Networking.StreamerThread.StreamedOutLabels), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Pickups: " + Networking.StreamerThread.StreamedInPickups + "/" + (Networking.StreamerThread.StreamedInPickups + Networking.StreamerThread.StreamedOutPickups), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed Particles: " + Networking.StreamerThread.StreamedInParticles + "/" + (Networking.StreamerThread.StreamedInParticles + Networking.StreamerThread.StreamedOutParticles), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed API Peds: " + Networking.StreamerThread.StreamedInPeds + "/" + (Networking.StreamerThread.StreamedInPeds + Networking.StreamerThread.StreamedOutPeds), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Streamed GR Entities: " + (Networking.StreamerThread.StreamedInItems + 1) + "/" + (Networking.StreamerThread.StreamedInItems + Networking.StreamerThread.StreamedOutItems + 1), 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n++;
                Util.DrawText("Spawned Peds: " + World.GetAllPeds().Length + "", 5, (position + (offset * n++)), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Spawned Vehicles: " + World.GetAllVehicles().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Spawned Blips: " + World.GetAllBlips().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Spawned Props: " + World.GetAllProps().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                Util.DrawText("Spawned Entities: " + World.GetAllEntities().Length + "", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                n = 1;
            }
        }

    }
}
