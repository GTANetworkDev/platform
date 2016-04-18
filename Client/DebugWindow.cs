using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NativeUI;
using Rage;

namespace GTANetwork
{
    public class DebugWindow
    {
        public bool Visible { get; set; }
        public int PlayerIndex { get; set; }

        public void Draw()
        {
            if (!Visible) return;

            if (Game.IsControlJustPressed(0, GameControl.FrontendLeft))
            {
                PlayerIndex--;
                UI.ShowSubtitle("NewIndex: " + PlayerIndex);
            }

            else if (Game.IsControlJustPressed(0, GameControl.FrontendRight))
            {
                PlayerIndex++;
                UI.ShowSubtitle("NewIndex: " + PlayerIndex);
            }

            if (PlayerIndex >= Main.Opponents.Count || PlayerIndex < 0)
            {
                // wrong index
                return;
            }

            var player = Main.Opponents.ElementAt(PlayerIndex);
            string output = "=======PLAYER #" + PlayerIndex + " INFO=======\n";
            output += "Name: " + player.Value.Name + "\n";
            output += "IsInVehicle: " + player.Value.IsInVehicle + "\n";
            output += "Position: " + player.Value.Position + "\n";
            output += "VehiclePosition: " + player.Value.VehiclePosition + "\n";
            output += "Last Updated: " + player.Value.LastUpdateReceived + "\n";
            output += "Character Pos: " + player.Value.Character?.Position + "\n";
            output += "BlipPos: " + player.Value.Character?.CurrentBlip?.Position + "\n";
            if (player.Value.MainVehicle != null)
            {
                output += "CharacterIsInVeh: " + player.Value.Character?.IsInVehicle() + "\n";
                output += "ActualCarPos: " + player.Value.MainVehicle?.Position + "\n";
            }
            
            new UIResText(output, new Point(500, 10), 0.5f) {Outline = true}.Draw();
        }
    }
}