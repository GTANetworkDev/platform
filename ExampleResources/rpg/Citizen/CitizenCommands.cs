using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;
using RPGResource.Cops;

namespace RPGResource.Citizen
{
    public class CitizenCommands : Script
    {
        [Command("acceptfine", Group = "Citizen Commands")]
        public void AcceptCopFine(Client sender)
        {
            var ticketOffered = API.getEntityData(sender, "FINE_OFFERED");
            var cop = API.getEntityData(sender, "FINE_OFFERED_BY");

            API.resetEntityData(sender, "FINE_OFFERED");
            API.resetEntityData(sender, "FINE_OFFERED_BY");

            if (API.getEntityData(sender, "IS_COP") == true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're a cop, you can't be fined!");
                return;
            }

            if (API.getEntityData(sender, "WantedLevel") == 0 ||
                API.getEntityData(sender, "WantedLevel") > 2)
            {
                API.sendChatMessageToPlayer(sender, "~r~You cant accept the ticket with your wanted level!");
                return;
            }

            if (ticketOffered != true)
            {
                API.sendChatMessageToPlayer(sender, "~r~Nobody offered you to pay your fine!");
                return;
            }

            if (cop == null || !API.isPlayerConnected(cop) || API.getEntityPosition(cop).DistanceToSquared(API.getEntityPosition(sender)) > 100f)
            {
                API.sendChatMessageToPlayer(sender, "~r~The cop has left!");
                return;
            }

            List<int> crimes = API.getEntityData(sender, "Crimes");
            int totalPrice = 0;

            foreach (var crime in crimes)
            {
                var crimeData = WantedLevelDataProvider.Crimes.Get(crime);

                totalPrice += crimeData.TicketCost;
            }

            var playerMoney = API.getEntityData(sender, "Money");

            if (playerMoney >= totalPrice)
            {
                API.setEntityData(sender, "Money", playerMoney - totalPrice);

                API.triggerClientEvent(sender, "update_money_display", API.getEntityData(sender, "Money"));

                API.sendChatMessageToPlayer(sender, "You have paid your fine!");
                API.sendChatMessageToPlayer(cop, sender.name + " has paid his fine!");

                API.setEntityData(sender, "WantedLevel", 0);
                API.resetEntityData(sender, "Crimes");
                API.setPlayerWantedLevel(sender, 0);
            }
            else
            {
                API.sendChatMessageToPlayer(sender, "~r~You dont have enough money!");
            }
        }

        [Command("payfine", Group = "Citizen Commands")]
        public void PayOwnFine(Client sender)
        {
            if (API.getEntityData(sender, "IS_COP") == true)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're a cop, you can't be fined!");
                return;
            }

            if (!(bool) API.call("PoliceStation", "IsInPoliceStation", (NetHandle)sender.handle))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: You're not in a police station!");
                return;
            }

            if (API.getEntityData(sender, "WantedLevel") == 0 ||
                API.getEntityData(sender, "WantedLevel") > 2)
            {
                API.sendChatMessageToPlayer(sender, "~r~You can't pay your fine with that wanted level!");
                return;
            }

            
            List<int> crimes = API.getEntityData(sender, "Crimes");
            int totalPrice = 0;

            foreach (var crime in crimes)
            {
                var crimeData = WantedLevelDataProvider.Crimes.Get(crime);

                totalPrice += crimeData.TicketCost;
            }

            var playerMoney = API.getEntityData(sender, "Money");

            if (playerMoney >= totalPrice)
            {
                API.setEntityData(sender, "Money", playerMoney - totalPrice);

                API.triggerClientEvent(sender, "update_money_display", API.getEntityData(sender, "Money"));

                API.sendChatMessageToPlayer(sender, "You have paid your fine!");

                API.setEntityData(sender, "WantedLevel", 0);
                API.resetEntityData(sender, "Crimes");
                API.setPlayerWantedLevel(sender, 0);
            }
            else
            {
                API.sendChatMessageToPlayer(sender, "~r~You dont have enough money!");
            }
        }

    }
}