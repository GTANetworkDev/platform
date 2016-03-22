using System.Collections.Generic;
using System.Windows.Forms;
using GTA.Math;

namespace GTANetwork
{
    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }
        public List<string> FavoriteServers { get; set; }
        public List<string> RecentServers { get; set; }
        public bool ScaleChatWithSafezone { get; set; }
        

        public PlayerSettings()
        {
            DisplayName = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
            MaxStreamedNpcs = 10;
            MasterServerAddress = "http://148.251.18.67:8888/";
            ActivationKey = Keys.F9;
            FavoriteServers = new List<string>();
            RecentServers = new List<string>();
            ScaleChatWithSafezone = true;
        }
    }
}