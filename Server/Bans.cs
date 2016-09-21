using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace GTANetworkServer
{
    public class BanCollection
    {
        [XmlElement("ban")]
        public List<BanItem> Bans { get; set; }
    }

    public class BanItem
    {
        [XmlAttribute("schandle")]
        public string SocialClubHandle { get; set; }
        [XmlAttribute("ip")]
        public string IP { get; set; }
        [XmlAttribute("reason")]
        public string Reason { get; set; }
    }

    public class BanManager
    {
        private BanCollection _banCollection = new BanCollection();

        public void Initialize()
        {
            if (File.Exists("bans.xml"))
            {
                var ser = new XmlSerializer(typeof(BanCollection));
                using (var stream = File.OpenRead("bans.xml"))
                    _banCollection = (BanCollection) ser.Deserialize(stream);

                if (_banCollection.Bans == null) _banCollection.Bans = new List<BanItem>();
            }
            else
            {
                _banCollection = new BanCollection();
                _banCollection.Bans = new List<BanItem>();
            }
        }

        public bool IsClientBanned(Client player)
        {
            lock (_banCollection)
            {
                return
                    _banCollection.Bans.Any(
                        ban =>
                            ban.SocialClubHandle == player.SocialClubName ||
                            ban.IP == player.NetConnection.RemoteEndPoint.Address.ToString());
            }
        }

        public void BanPlayer(Client player, string reason = null)
        {
            lock (_banCollection)
            {
                _banCollection.Bans.Add(new BanItem()
                {
                    SocialClubHandle = player.Name,
                    IP = player.NetConnection.RemoteEndPoint.Address.ToString(),
                    Reason = reason,
                });

                Commit();
            }
        }

        public void UnbanPlayer(string socialClubName)
        {
            lock (_banCollection)
            {
                _banCollection.Bans.RemoveAll(ban => ban.SocialClubHandle == socialClubName);

                Commit();
            }
        }

        private void Commit()
        {
            var ser = new XmlSerializer(typeof(BanCollection));

            using (
                var stream = new FileStream("bans.xml", File.Exists("bans.xml") ? FileMode.Truncate : FileMode.Create))
            {
                ser.Serialize(stream, _banCollection);
            }
        }
    }
}