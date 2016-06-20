using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace GTANetworkShared
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


        [XmlElement("upnp")]
        public bool UseUPnP { get; set; }

        [XmlElement("announce_lan")]
        public bool AnnounceToLan { get; set; }

        [XmlElement("password")]
        public string Password { get; set; }

        [XmlElement("masterserver")]
        public string MasterServer { get; set; }

        //public bool AllowDisplayNames { get; set; }
        //public string Gamemode { get; set; }

        [XmlElement("resource")]
        public List<SettingsResFilepath> Resources { get; set; }

        [XmlElement("acl_enabled")]
        public bool UseACL { get; set; }

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
            UseACL = true;
            AnnounceToLan = true;
            AutoUpdateMinClientVersion = true;
            //AllowDisplayNames = true;
            MasterServer = "http://148.251.18.67:8080/";
            Resources = new List<SettingsResFilepath>();
        }

        public static ServerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(ServerSettings));

            ServerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (ServerSettings)ser.Deserialize(stream);

                //using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new ServerSettings());
            }

            return settings;
        }

        public static void WriteSettings(string path, ServerSettings sett)
        {
            var ser = new XmlSerializer(typeof(ServerSettings));

            ServerSettings settings = sett;

            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Truncate, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new ServerSettings());
            }
        }
    }
}