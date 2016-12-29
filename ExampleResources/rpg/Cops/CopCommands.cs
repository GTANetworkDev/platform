using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Cops
{
    public class CopCommands : Script
    {
        [Command("arrest", Alias = "ar", Group = "Cop Commands")]
        public void ArrestPlayer(Client sender, Client target)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            if (target == sender)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant arrest yourself!");
                return;
            }

            if (API.getEntityData(target, "IS_COP") == true)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant arrest a cop!");
                return;
            }

            if (API.getEntityPosition(sender).DistanceToSquared(API.getEntityPosition(target)) > 16f)
            {
                API.sendChatMessageToPlayer(sender, "~r~You're too far!");
                return;
            }

            if (API.getEntityData(target, "WantedLevel") == null ||
                API.getEntityData(target, "WantedLevel") <= 2)
            {
                API.sendChatMessageToPlayer(sender, "~r~The player doesn't have an arrest warrant!");
                return;
            }

            API.sendChatMessageToPlayer(sender, "~g~You have arrested " + target.name + "!");
            API.sendChatMessageToPlayer(target, "~g~You have been arrested by " + sender.name + "!");
            API.call("JailController", "jailPlayer", target,
                WantedLevelDataProvider.GetTimeFromWantedLevel(API.getEntityData(target, "WantedLevel")));

            CopUtil.BroadcastToCops("~b~Player ~h~" + target.name + "~h~ has been arrested!");
        }

        [Command("wanted", Group = "Cop Commands")]
        public void GetAllWantedPlayers(Client sender)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            var players = API.getAllPlayers();

            int count = 0;

            API.sendChatMessageToPlayer(sender, "_________WANTED________");

            foreach (var player in players)
            {
                if (API.getEntityData(player, "LOGGED_IN") != true || API.getEntityData(player, "IS_COP") == true || API.getEntityData(player, "WantedLevel") <= 2) continue;

                var crimes = (List<int>)API.getEntityData(player, "Crimes");

                string crimeList = string.Join(", ", crimes.Select(i => WantedLevelDataProvider.Crimes.Get(i).Name));

                API.sendChatMessageToPlayer(sender,
                    string.Format("~b~{0}~w~ ~h~{1}~h~ -- {2}",
                    new String('*', API.getEntityData(player, "WantedLevel")),
                    player.name,
                    crimeList
                    ));
            }
        }

        [Command("report", Alias = "re", Group = "Cop Commands")]
        public void ReportPlayer(Client sender, Client criminal, int crimeId)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            if (criminal == sender)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant report yourself!");
                return;
            }

            if (API.getEntityData(criminal, "IS_COP") == true)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant report a cop!");
                return;
            }

            if (!WantedLevelDataProvider.Crimes.ContainsKey(crimeId))
            {
                API.sendChatMessageToPlayer(sender, "~r~No such crime exists. Use /crimelist for a full list of crime IDs.");
                return;
            }

            if (WantedLevelDataProvider.Crimes.Get(crimeId).WantedLevel > 2)
            {
                API.sendChatMessageToPlayer(sender, "~r~You can only report petty crimes!");
                return;
            }

            CopUtil.ReportPlayer(criminal, crimeId);
        }

        [Command("crimelist", Alias = "cl", Group = "Cop Commands")]
        public void CrimeList(Client sender)
        {
            int count = 0;
            string accumulator = "";

            API.sendChatMessageToPlayer(sender, "_____CRIMES_____");

            foreach (
                var crime in
                    WantedLevelDataProvider.Crimes.Select(pair => string.Format("{0}: {1}", pair.Key, pair.Value.Name)))
            {
                accumulator += crime + ", ";

                if (++count%6 == 0)
                {
                    API.sendChatMessageToPlayer(sender, accumulator);
                    accumulator = "";
                }
            }
        }

        [Command("ticket", Group = "Cop Commands")]
        public void TicketPlayer(Client sender, Client criminal)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            if (criminal == sender)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant ticket yourself!");
                return;
            }

            if (API.getEntityData(criminal, "IS_COP") == true)
            {
                API.sendChatMessageToPlayer(criminal, "~r~You cant ticket a cop!");
                return;
            }

            if (API.getEntityData(criminal, "WantedLevel") == 0 ||
                API.getEntityData(criminal, "WantedLevel") > 2)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant ticket this player!");
                return;
            }

            if (API.getEntityPosition(sender).DistanceToSquared(API.getEntityPosition(criminal)) > 25f)
            {
                API.sendChatMessageToPlayer(sender, "~r~You're too far!");
                return;
            }

            List<int> crimes = API.getEntityData(criminal, "Crimes");
            int totalPrice = 0;

            foreach (var crime in crimes)
            {
                var crimeData = WantedLevelDataProvider.Crimes.Get(crime);

                totalPrice += crimeData.TicketCost;
            }

            if (API.getEntityData(criminal, "Money") >= totalPrice)
            {
                API.sendChatMessageToPlayer(criminal, "~b~" + sender.name + "~w~ has fined you for $" + totalPrice + ". Type /acceptfine to pay the fine.");
                API.sendChatMessageToPlayer(sender, "You offered " + criminal.name + " to pay his fine.");

                API.setEntityData(criminal, "FINE_OFFERED", true);
                API.setEntityData(criminal, "FINE_OFFERED_BY", sender);
            }
            else
            {
                API.sendChatMessageToPlayer(sender, "~r~The player can't pay their fine!");
            }
        }

        [Command("faction", Alias = "f,d", Group = "Cop Commands", GreedyArg = true)]
        public void BroadcastToOtherCops(Client sender, string text)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            CopUtil.BroadcastToCops("~b~[RADIO] ~h~" + sender.name + "~h~~w~: " + text);
        }

        [Command("jaillist", Group = "Cop Commands")]
        public void GetAllPlayersInJail(Client sender)
        {
            if (API.getEntityData(sender, "IS_COP") != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not a cop!");
                return;
            }

            var players = API.getAllPlayers();

            int count = 0;
            string accumulator = "";


            API.sendChatMessageToPlayer(sender, "_________JAIL________");

            foreach (var player in players)
            {
                if (API.getEntityData(player, "LOGGED_IN") != true || API.getEntityData(player, "JAILED") != true) continue;

                accumulator += string.Format("{0} ({1}s), ", player.name, API.TickCount - JailController.JailTimes.Get(player));

                if (++count % 6 == 0)
                {
                    API.sendChatMessageToPlayer(sender, accumulator);
                    accumulator = "";
                }
            }
        }


    }
}