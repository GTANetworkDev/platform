using GTANetworkServer;

namespace RPGResource.Global
{
    public class GlobalCommands : Script
    {
        [Command("stats", Group = "Global Commands")]
        public void GetStatistics(Client sender)
        {
            API.sendChatMessageToPlayer(sender, "_____STATS_____");

            API.sendChatMessageToPlayer(sender, string.Format("~h~Name:~h~ {0} ~h~Class:~h~ {1} ~h~Level:~h~ {2}", sender.Name,
                API.getLocalEntityData(sender, "IS_COP") == true ? "Cop" : "Citizen",
                (int)API.getLocalEntityData(sender, "Level")));

            // TODO: Skills
            // TODO: Cop ranks, experience
        }
    }
}