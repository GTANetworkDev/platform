using System;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

public class KillchatUtility : Script
{
	public KillchatUtility()
	{
		API.onPlayerDeath += onPlayerDeath;
	}

	public void onPlayerDeath(Client player, NetHandle reason, int weapon)
	{
		var killer = "";
	    API.consoleOutput("killed: " + player.Name + " reason: " + reason.Value + " weapon: " + weapon);

	    if (!reason.IsNull)	    
	    {
	        var players = API.getAllPlayers();
	        for (var i = 0; i < players.Count; i++)
	        {
	            if (players[i].CharacterHandle == reason) {
	                killer = players[i].Name;
	                break;
	            }            
	        }        
	    }
	    
	    API.triggerClientEventForAll("addKillToKillchat", player.Name, killer, weapon);
	}	    
}
