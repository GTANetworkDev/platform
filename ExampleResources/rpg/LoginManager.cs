using GTANetworkServer;

namespace RPGResource
{
    public class LoginManager : Script
    {
        public LoginManager()
        {
            Database.Init();

            API.onResourceStop += onResourceStop;
        }

        [Command]
        public void Login(Client sender, string password)
        {
            if (Database.IsPlayerLoggedIn(sender))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~You're already logged in!");
                return;
            }

            if (!Database.TryLoginPlayer(sender, password))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ Wrong password, or account doesnt exist!");
            }
            else
            {
                Database.LoadPlayerAccount(sender);
                API.sendChatMessageToPlayer(sender, "~g~Logged in successfully!");

                // Spawn the player
                API.call("SpawnManager", "CreateSkinSelection", sender);

                int money = API.getEntityData(sender, "Money");
                API.triggerClientEvent(sender, "update_money_display", money);
            }
        }

        [Command]
        public void Register(Client sender, string password)
        {
            if (Database.IsPlayerLoggedIn(sender))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~You're already logged in!");
                return;
            }

            if (Database.DoesAccountExist(sender.socialClubName))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~An account linked to this Social Club handle already exists!");
                return;
            }

            Database.CreatePlayerAccount(sender, password);
            API.sendChatMessageToPlayer(sender, "~g~Account created! ~w~Now log in with ~y~/login [password]");
        }

        public void onResourceStop()
        {
            foreach (var client in API.getAllPlayers())
            {
                foreach (var data in API.getAllEntityData(client))
                {
                    API.resetEntityData(client, data);
                }
            }
        }
    }
}