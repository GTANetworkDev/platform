using GTANetworkServer;

namespace RPGResource.Global
{
    public class RespawnManager : Script
    {
        public RespawnManager()
        {
            API.onPlayerRespawn += RespawnPlayer;
        }

        public void RespawnPlayer(Client player)
        {
            if (API.getLocalEntityData(player, "IS_COP") == true)
            {
                API.call("SpawnManager", "SpawnCop", player);
            }
        }
    }
}