using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkServer;
using GTANetworkShared;
using RPGResource.Cops;

namespace RPGResource.Global
{
    public class GlobalCommands : Script
    {
        [Command("stats", Group = "Global Commands")]
        public void GetStatistics(Client sender)
        {
            API.sendChatMessageToPlayer(sender, "_____STATS_____");

            API.sendChatMessageToPlayer(sender, string.Format("~h~Name:~h~ {0} ~h~Class:~h~ {1} ~h~Level:~h~ {2}", sender.name,
                API.getEntityData(sender, "IS_COP") == true ? "Cop" : "Citizen",
                (int)API.getEntityData(sender, "Level")));

            if (API.getEntityData(sender, "WantedLevel") > 0)
            {
                var crimes = (List<int>) API.getEntityData(sender, "Crimes");

                string crimeList = string.Join(", ", crimes.Select(i => WantedLevelDataProvider.Crimes.Get(i).Name));

                if (API.getEntityData(sender, "IS_COP") != true)
                {
                    API.sendChatMessageToPlayer(sender,
                        string.Format("~h~Wanted Level:~h~ ~b~{0}~w~~h~Crimes~h~: {1}",
                            Util.Repeat("* ", API.getEntityData(sender, "WantedLevel")),
                            crimeList
                            ));

                    // TODO: Skills
                }
                else
                {
                    // TODO: Cop ranks, experience
                }
            }
        }
    }
}