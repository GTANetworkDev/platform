using System.Linq;
using GTANetworkServer;
using GTANetworkShared;
using RPGResource.Cops;

namespace RPGResource
{
    public class AutoCrimeReporter : Script
    {
        public AutoCrimeReporter()
        {
            API.onPlayerDeath += onPlayerKilled;
            API.onPlayerDetonateStickies += StickyDetonation;
            API.onPlayerEnterVehicle += PlayerEnterCar;
        }

        public void PlayerEnterCar(Client sender, NetHandle vehicle)
        {
            if (API.getLocalEntityData(sender, "IS_COP") == true) return;

            if (API.getLocalEntityData(vehicle, "COPCAR") == true)
            {
                CopUtil.ReportPlayer(sender, 3); // Copcar Car jacking
            }
        }

        public void StickyDetonation(Client sender)
        {
            if (API.getLocalEntityData(sender, "IS_COP") == true) return;

            CopUtil.ReportPlayer(sender, 1); // Explosion
        }

        public void onPlayerKilled(Client victim, NetHandle killer, int weapon)
        {
            var killerClient = API.getPlayerFromHandle(killer);

            if (killerClient != null)
            {
                if (API.getLocalEntityData(killerClient, "IS_COP") == true)
                {
                    // TODO: Demote cop if the victim didnt have wanted level
                    return;
                }

                if (API.getLocalEntityData(victim, "IS_COP") == true)
                    CopUtil.ReportPlayer(killerClient, 2); // Cop Murder
                else
                    CopUtil.ReportPlayer(killerClient, 0); // Murder
            }

            if (API.getLocalEntityData(victim, "WantedLevel") > 2)
            {
                var allPlayers = API.getPlayersInRadiusOfPlayer(15f, victim);

                if (allPlayers.Any(player => API.getLocalEntityData(player, "IS_COP") == true))
                {
                    API.call("JailController", "jailPlayer", victim,
                        WantedLevelDataProvider.GetTimeFromWantedLevel(API.getLocalEntityData(victim, "WantedLevel")));
                }
            }

        }
    }
}