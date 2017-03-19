using System;
using GTA;
using NativeUI;
using GTA.UI;
using GTANetwork.Streamer;
using GTANetwork.Sync;
using System.Drawing;
using System.Linq;

namespace GTANetwork.Util
{
    public class DebugInfo : Script
    {
        public static bool ShowFps = true;
        public static bool StreamerDebug = false;
        public static bool PlayerDebug = false;

        private const int Position = 400;

        private int PlayerIndex { get; set; }
        private DateTime _last;
        private float _fps;
        private SizeF _screenRatio;

        public DebugInfo()
        {
            _screenRatio = UIMenu.GetScreenResolutionMantainRatio();
            Tick += Draw;
        }

        private void Draw(object sender, EventArgs e)
        {
            if (!Main.IsConnected()) return;

            if (DateTime.Now.Subtract(_last).TotalMilliseconds >= 500)
            {
                _fps = Game.FPS;
                _last = DateTime.Now;
            }

            if (ShowFps)
            {
                Util.DrawText(_fps.ToString("0"), _screenRatio.Width - 20, 0, 0.35f, 255, 255, 255, 255, 0, 1, false, true, 0);
            }

            if (StreamerDebug)
            {
                //Util.DrawText("Sync: " + Sync.SyncThread.sw?.ElapsedMilliseconds + " ms", 5, position, 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                //Util.DrawText("OnTick: " + Main.oTsw?.ElapsedMilliseconds + " ms", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);
                //Util.DrawText("Streamer: " + Streamer.StreamerThread.sw?.ElapsedMilliseconds + "ms", 5, position + (offset * n++), 0.35f, 255, 255, 255, 255, 0, 0, false, true, 0);

                string output = "=======STREAMER INFO=======\n";
                output += "Streamer Tick: " + StreamerThread.sw?.ElapsedMilliseconds + "ms\n\n";
                output += "Spawned Peds: " + World.GetAllPeds().Length + "\n";
                output += "Spawned Vehicles: " + World.GetAllVehicles().Length + "\n";
                output += "Spawned Blips: " + World.GetAllBlips().Length + "\n";
                output += "Spawned Props: " + World.GetAllProps().Length + "\n";
                output += "Spawned Entities: " + World.GetAllEntities().Length + "\n";

                new UIResText(output, new Point(5, Position), 0.35f) { Outline = true }.Draw(new Size());

            }

            if (PlayerDebug)
            {
                if (Game.IsControlJustPressed(0, Control.FrontendLeft))
                {
                    PlayerIndex--;
                    Screen.ShowSubtitle("NewIndex: " + PlayerIndex);
                }

                else if (Game.IsControlJustPressed(0, Control.FrontendRight))
                {
                    PlayerIndex++;
                    Screen.ShowSubtitle("NewIndex: " + PlayerIndex);
                }

                if (PlayerIndex >= Main.NetEntityHandler.ClientMap.OfType<SyncPed>().Count() || PlayerIndex < 0)
                {
                    // wrong index
                    return;
                }

                var player = Main.NetEntityHandler.ClientMap.OfType<SyncPed>().ElementAt(PlayerIndex);
                string output = "=======PLAYER #" + PlayerIndex + " INFO=======\n";
                output += "Name: " + player.Name + "\n";
                output += "IsInVehicle: " + player.IsInVehicle + "\n";
                output += "Position: " + player.Position + "\n";
                output += "VehiclePosition: " + player.Position + "\n";
                output += "Character Pos: " + player.Character?.Position + "\n";
                output += "BlipPos: " + player.Character?.AttachedBlip?.Position + "\n";
                output += "AL: " + player.AverageLatency + "\n";
                output += "TSU: " + player.TicksSinceLastUpdate + "\n";
                output += "Latency: " + (((player.Latency * 1000) / 2) + ((Main.Latency * 1000) / 2)) + "\n";
                if (player.MainVehicle != null)
                {
                    output += "CharacterIsInVeh: " + player.Character?.IsInVehicle() + "\n";
                    output += "ActualCarPos: " + player.MainVehicle?.Position + "\n";
                    output += "SeatIndex: " + player.VehicleSeat + "\n";
                }

                new UIResText(output, new Point(5, Position), 0.35f) { Outline = true }.Draw(new Size());
            }
        }
    }     
}