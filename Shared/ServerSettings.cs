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
        public string MinimumClientVersion { get; set; }

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

        [XmlElement("resource")]
        public List<SettingsResFilepath> Resources { get; set; }

        [XmlElement("acl_enabled")]
        public bool UseACL { get; set; }

        [XmlElement("log")]
        public bool LogToFile { get; set; }

        [XmlElement("vehicle_lagcomp")]
        public bool VehicleLagCompensation { get; set; }

        [XmlElement("onfoot_lagcomp")]
        public bool OnFootLagCompensation { get; set; }

        [XmlElement("refresh_rate")]
        public int RefreshHz { get; set; }

        [XmlElement("loglevel")]
        public int LogLevel { get; set; }

        [XmlElement("httpserver")]
        public bool UseHTTPServer { get; set; }

        [XmlElement("trust_client_entity_properties")]
        public bool EnableClientsideEntityProperties { get; set; }

        [XmlElement("local_address")]
        public string LocalAddress { get; set; }

        [XmlElement("global_stream_range")]
        public int GlobalStreamingRange { get; set; }

        [XmlElement("player_stream_range")]
        public int PlayerStreamingRange { get; set; }

        [XmlElement("vehicle_stream_range")]
        public int VehicleStreamingRange { get; set; }

        [XmlElement("fqdn")]
        public string fqdn { get; set; }

        [XmlElement("conntimeout")]
        public bool Conntimeout { get; set; }

        [XmlElement("allowcefdevtool")]
        public bool Allowcefdevtool { get; set; }

        public WhitelistCollection whitelist { get; set; }

        [XmlRoot("resource")]
        public class SettingsResFilepath
        {
            [XmlAttribute("src")]
            public string Path { get; set; }
        }

        public ServerSettings()
        {
            Port = 4499;
            MaxPlayers = 50;
            Name = "Simple GTA Network Server";
            MinimumClientVersion = "0.0.0.0";
            Password = "";
            LogToFile = true;
            Announce = true;
            UseACL = true;
            AnnounceToLan = true;
            AutoUpdateMinClientVersion = true;
            MasterServer = "http://master.gtanet.work";
            Resources = new List<SettingsResFilepath>();
            OnFootLagCompensation = true;
            VehicleLagCompensation = true;
            GlobalStreamingRange = 1000;
            PlayerStreamingRange = 250;
            VehicleStreamingRange = 350;
            UseHTTPServer = false;
            LogLevel = 0;
            EnableClientsideEntityProperties = false;
            LocalAddress = "0.0.0.0";
            fqdn = "";
            Conntimeout = true;
            Allowcefdevtool = false;
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

    public class WhitelistCollection
    {
        [XmlElement("mod")]
        public List<WhitelistItem> Items;
    }

    public class WhitelistItem
    {
        [XmlAttribute("hash")]
        public string Hash { get; set; }
    }
}