using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

namespace Votemanager
{
    public static class cr
    {
        public static API CrossReference;

        public static API c
        {
            get
            {
                return CrossReference;
            }
        }
    }

    public class VotemanagerEntryPoint : Script
    {
        public VotemanagerEntryPoint()
        {
            API.onClientEventTrigger += eventReceived;
            API.onPlayerFinishedDownload += playerJoin;
            API.onResourceStop += () => 
            {
                if (CurrentVote != null) CurrentVote.Dispose();
            };

            API.onResourceStart += () =>
            {
                foreach (var p in API.getAllPlayers())
                {
                    API.setEntitySyncedData(p, "VOTEMANAGER_PLAYER_JOINED", true);
                }
            };

            cr.CrossReference = API;
        }

        private void playerJoin(Client player)
        {
            if (CurrentVote != null && API.getEntitySyncedData(player, "VOTEMANAGER_PLAYER_JOINED") != true)
            {
                CurrentVote.SendVoteToClient(CurrentVote.Type, player);                
            }

            API.setEntitySyncedData(player, "VOTEMANAGER_PLAYER_JOINED", true);
        }

        private void eventReceived(Client sender, string name, object[] args)
        {
            if (name == "cast_vote" && CurrentVote != null)
            {
                CurrentVote.CastVote(sender, (int) args[0]);
            }
        }

        public Vote CurrentVote { get; set; }
        public long TimeSinceLastVote { get; set; }

        private Random rng = new Random();

        [Command("votemap")]
        public void votemapcmd(Client sender)
        {
            if (!API.getSetting<bool>("enablevotemap"))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ /votemap has been disabled on this server!");
                return;
            }

            if (CurrentVote != null)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ A vote is already in progress!");
                return;
            }

            if (API.TickCount - TimeSinceLastVote > API.getSetting<int>("votecooldown"))
            {
                API.sendChatMessageToAll("~b~~h~" + sender.name + "~h~~w~ has started a vote map!");
                var result = startMapVote(null);
            }
            else
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ Wait some time before starting another vote!");
            }
        }

        [Command("votekick")]
        public void votekickcmd(Client sender, Client target)
        {
            if (!API.getSetting<bool>("enablevotekick"))
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ /votekick has been disabled on this server!");
            }

            if (CurrentVote != null)
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ A vote is already in progress!");
            }

            if (API.TickCount - TimeSinceLastVote > API.getSetting<int>("votecooldown"))
            {
                API.sendChatMessageToAll("~b~~h~" + sender.name + "~h~~w~ has started a vote to kick ~r~~h~" + target.name + "~h~~w~!");
                startKickVote(target);
            }
            else
            {
                API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ Wait some time before starting another vote!");
            }
        }

        // EXPORTED
        public bool startMapVote(string gamemode)
        {
            if (string.IsNullOrEmpty(gamemode))
            {
                gamemode = API.getCurrentGamemode();
            }

            if (string.IsNullOrEmpty(gamemode)) return false;
            if (CurrentVote != null) return false;

            var maps = API.getMapsForGamemode(gamemode).ToList();

            maps.Shuffle();
            var resultMaps = maps.Take(10).ToList();

            if (resultMaps.Count <= 0) 
            {
                return false;
            }

            var desc = new MapVote();
            desc.Options = resultMaps.ToArray();

            API.consoleOutput("Votemap started at " + DateTime.Now);

            API.sendChatMessageToAll("Vote for the next map!");
            CurrentVote = new Vote(desc, API.getSetting<int>("votelength"));

            CurrentVote.OnFinished += (id, txt) => 
            {
                API.sendChatMessageToAll("Option ~b~~h~" + txt + "~h~~w~ has won the vote!");
                TimeSinceLastVote = API.TickCount;
                CurrentVote = null;
            };

            CurrentVote.Start();

            return true;
        }

        public bool startKickVote(Client target)
        {
            var desc = new VoteKick(target);

            API.consoleOutput("Votekick started at " + DateTime.Now);

            CurrentVote = new Vote(desc, API.getSetting<int>("votelength"));

            CurrentVote.OnFinished += (id, txt) => 
            {
                API.sendChatMessageToAll("Option ~b~~h~" + txt + "~h~~w~ has won the vote!");
                TimeSinceLastVote = API.TickCount;
                CurrentVote = null;
            };

            CurrentVote.Start();

            return true;
        }
    }

    public static class Extensions
    {
        private static Random rng = new Random();  

        public static void Shuffle<T>(this IList<T> list)  
        {  
            int n = list.Count;  
            while (n > 1) {  
                n--;  
                int k = rng.Next(n + 1);  
                T value = list[k];  
                list[k] = list[n];  
                list[n] = value;  
            }  
        }
    }
}