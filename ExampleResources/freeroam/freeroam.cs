using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class FreeroamScript : Script
{
	public FreeroamScript()
	{
		API.onPlayerRespawn += onPlayerRespawn;		
		API.onChatCommand += onPlayerCommand;
	}

	private void onPlayerCommand(Client sender, string cmd, CancelEventArgs cancel)
	{
		var args = cmd.Split();

		if (args[0] == "/me")
		{
			API.sendChatMessageToAll("~#CB15EB~", sender.Name + " " + cmd.Substring(4) + ".");
		}

		if (args[0] == "/spec")
		{
			if (args.Length >= 2)
			{
				var targetName = args[1];
				var target = API.getPlayerFromName(targetName);
				if (target != null)
				{
						API.setPlayerToSpectatePlayer(sender, target);
				}
				else
				{
						API.sendNotificationToPlayer(sender, "No such player found.");
				}
			}
			else
			{
				API.sendNotificationToPlayer(sender, "USAGE: /spec [player name]");
			}
    	}

	    if (cmd == "/unspec")
	    {
	    	API.unspectatePlayer(sender);
	    }

	    if (args[0] == "/car")
	    {
	        if (args.Length >= 2)
	        {
	            var vehName = args[1];
	            var vehModel = API.vehicleNameToModel(vehName);
	            if (vehModel == 0)
	            {
	                API.sendNotificationToPlayer(sender, "No such model found!");
	            }
	            else
	            {
	                var veh = API.createVehicle(vehModel, sender.Position, new Vector3(), 0, 0);
	                var c = 0;
	                while (!API.doesEntityExistForPlayer(sender, veh) && c < 40)
	                {
	                	c++;
	                }

	                API.setPlayerIntoVehicle(sender, veh, -1);
	            }
	        }
	        else
	        {
	            API.sendNotificationToPlayer(sender, "USAGE: /car [model name]");
	        }
	   }

	   if (args[0] == "/pic")
	   {
	        if (args.Length >= 2)
	        {
	            var vehName = args[1];
	            var vehModel = API.pickupNameToModel(vehName);
	            if (vehModel == 0)
	            {
	                API.sendNotificationToPlayer(sender, "No such model found!");
	            }
	            else
	            {
	                var veh = API.createPickup(vehModel, new Vector3(sender.Position.X + 10, sender.Position.Y, sender.Position.Z), new Vector3(), 10);                
	            }
	        }
	        else
	        {
	            API.sendNotificationToPlayer(sender, "USAGE: /pic [model name]");
	        }
	   }
	   
	   if (cmd == "/countdown")
	   {
	   		API.triggerClientEventForAll("startCountdown");
	   }
   
   		if (args[0] == "/tp")
   		{
   			if (args.Length >= 2)
			{
				var targetName = args[1];
				var target = API.getPlayerFromName(targetName);

				if (target != null)
				{
					API.consoleOutput("Name: " + target.Name + " ID: " + target.CharacterHandle.Value + " pos: " + (API.getEntityPosition(target.CharacterHandle) == null));
					API.setEntityPosition(sender.CharacterHandle, API.getEntityPosition(target.CharacterHandle));
				}
				else
				{
					API.sendNotificationToPlayer(sender, "No such player found.");
				}
			}
			else
			{
				API.sendNotificationToPlayer(sender, "USAGE: /tp [player name]");
			}       		
   		}
	}

	public void onPlayerRespawn(Client player)
	{
		API.consoleOutput(player.Name + " has respawned.");
	}
}
