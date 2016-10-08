using System;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Cops
{
    public static class CopUtil
    {
        public static bool ReportPlayer(Client player, int crimeId)
        {
            var crimeWL = WantedLevelDataProvider.Crimes.Get(crimeId);

            if (API.Public.getLocalEntityData(player, "WantedLevel") >= crimeWL.WantedLevel)
            {
                return false;
            }

            API.Public.setLocalEntityData(player, "WantedLevel", crimeWL.WantedLevel);

            API.Public.sendChatMessageToPlayer(player, "~y~You have been reported for " + WantedLevelDataProvider.Crimes.Get(crimeId).Name);

            if (crimeWL.WantedLevel <= 2)
            {
                BroadcastToCops("~b~TICKET ISSUED FOR ~w~" + player.Name + " ~b~FOR~w~ " + WantedLevelDataProvider.Crimes.Get(crimeId).Name);
            }
            else
            {
                BroadcastToCops("~b~ARREST WARRANT ISSUED FOR ~w~" + player.Name + " ~b~FOR~w~ " + WantedLevelDataProvider.Crimes.Get(crimeId).Name);
            }

            List<int> playerCrimes;

            if ((playerCrimes = API.Public.getLocalEntityData(player, "Crimes")) == null)
            {
                playerCrimes = new List<int>();
            }

            playerCrimes.Add(crimeId);

            API.Public.setLocalEntityData(player, "Crimes", playerCrimes);
            if (crimeWL.WantedLevel > 2)
                API.Public.setPlayerNametagColor(player, 232, 44, 44);
            else
                API.Public.setPlayerNametagColor(player, 240, 160, 55);
            API.Public.setPlayerWantedLevel(player, (int)Math.Ceiling(crimeWL.WantedLevel / 2f));

            return true;
        }


        public static void BroadcastToCops(string message)
        {
            var players = API.Public.getAllPlayers();

            foreach (var player in players)
            {
                if (API.Public.getLocalEntityData(player, "IS_COP") == true)
                {
                    API.Public.sendChatMessageToPlayer(player, message);
                }
            }
        }

        public static int CalculatePlayerFine(Client player)
        {
            List<int> crimes = API.Public.getLocalEntityData(player, "Crimes");
            int totalPrice = 0;

            if (crimes != null)
            foreach (var crime in crimes)
            {
                var crimeData = WantedLevelDataProvider.Crimes.Get(crime);

                totalPrice += crimeData.TicketCost;
            }

            return totalPrice;
        }
    }
}