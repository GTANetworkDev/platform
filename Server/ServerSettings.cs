using System.Collections.Generic;
using System.Xml.Serialization;
using MultiTheftAutoShared;

namespace GTANetworkServer
{
    [XmlRoot("config")]
    public class ServerSettings
    {
        [XmlElement("servername")]
        public string Name { get; set; }

        [XmlElement("serverport")]
        public int Port { get; set; }

        [XmlElement("maxplayers")]
        public int MaxPlayers { get; set; }

        [XmlElement("minclientversion")]
        public ScriptVersion MinimumClientVersion { get; set; }

        [XmlElement("minclientversion_auto_update")]
        public bool AutoUpdateMinClientVersion { get; set; }

        [XmlElement("announce")]
        public bool Announce { get; set; }

        //[XmlElement("announce_lan")]
        //public bool AnnounceToLan { get; set; }

        [XmlElement("password")]
        public string Password { get; set; }

        [XmlElement("masterserver")]
        public string MasterServer { get; set; }

        //public bool AllowDisplayNames { get; set; }
        //public string Gamemode { get; set; }

        [XmlElement("resource")]
        public List<SettingsResFilepath> Resources { get; set; }

        [XmlRoot("resource")]
        public class SettingsResFilepath
        {
            [XmlAttribute("src")]
            public string Path { get; set; }
        }

        public ServerSettings()
        {
            Port = 4499;
            MaxPlayers = 16;
            Name = "Simple GTA Server";
            MinimumClientVersion = ScriptVersion.VERSION_0_6;
            Password = "";
            //Gamemode = "freeroam";
            Announce = true;
            //AnnounceToLan = true;
            AutoUpdateMinClientVersion = true;
            //AllowDisplayNames = true;
            MasterServer = "http://46.101.1.92/";
            Resources = new List<SettingsResFilepath>();
        }
    }
}