namespace GTANetworkShared
{
    public class MasterServerAnnounce
    {
        public int Port { get; set; }
        public int MaxPlayers { get; set; }
        public string ServerName { get; set; }
        public int CurrentPlayers { get; set; }
        public string Gamemode { get; set; }
        public string Map { get; set; }
    }
}