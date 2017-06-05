using System.Collections.Generic;
using System.Windows.Forms;

namespace GTANetworkShared
{
    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public string MasterServerAddress { get; set; }
        public List<string> FavoriteServers { get; set; }
        public List<string> RecentServers { get; set; }
        public bool ScaleChatWithSafezone { get; set; }
        public string UpdateChannel { get; set; }
        public bool DisableRockstarEditor { get; set; }
        public Keys ScreenshotKey { get; set; }
        public bool ShowFPS { get; set; }
        public bool DisableCEF { get; set; }
        public bool Timestamp { get; set; }
        public bool Militarytime { get; set; }
        //public bool AutosetBorderlessWindowed { get; set; }
        public bool UseClassicChat { get; set; }
        public bool OfflineMode { get; set; }
        public bool MediaStream { get; set; }
        public bool CEFDevtool { get; set; }
        public bool DebugMode { get; set; }

        public int ChatboxXOffset { get; set; }
        public int ChatboxYOffset { get; set; }

        public string GamePath { get; set; }

        public PlayerSettings()
        {
            MasterServerAddress = "https://master.gtanet.work/";
            FavoriteServers = new List<string>();
            RecentServers = new List<string>();
            ScaleChatWithSafezone = true;
            UpdateChannel = "stable";
            DisableRockstarEditor = true;
            //AutosetBorderlessWindowed = false;
            ScreenshotKey = Keys.F8;
            UseClassicChat = false;
            ShowFPS = true;
            DisableCEF = false;
            Timestamp = false;
            Militarytime = true;
            OfflineMode = false;
            MediaStream = false;
            CEFDevtool = false;
            DebugMode = false;
            GamePath = "";
        }
    }
}
