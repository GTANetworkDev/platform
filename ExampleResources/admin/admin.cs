using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class AdminScript : Script
{
	public AdminScript()
	{
		API.onPlayerRespawn += onDeath;
		API.onPlayerConnected += OnPlayerConnected;
		API.onUpdate += onUpdate;
		API.onResourceStart += onResStart;
		API.onPlayerDisconnected += onPlayerDisconnected;
		API.onChatCommand += onPlayerCommand;
	}

	private void onPlayerCommand(Client sender, string cmd, CancelEventArgs cancel)
	{
		if (!API.isAclEnabled()) return;

		var arguments = cmd.Split();

		if (arguments[0] == "/login")
		{
			if (cmd.Length < 7)
			{
				API.sendChatMessageToPlayer(sender, "~r~USAGE:~w~ /login [password]");
			}
			else
			{
				var password = cmd.Substring(7);
				var logResult = API.loginPlayer(sender, password);
				switch (logResult)
				{
					case 0:
						API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ No account found with your name.");
						break;
					case 3:
					case 1:
						API.sendChatMessageToPlayer(sender, "~g~Login successful!~w~ Logged in as ~b~" + API.getPlayerAclGroup(sender) + "~w~.");
						break;
					case 2:
						API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ Wrong password!");
						break;
					case 4:
						API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ You're already logged in!");
						break;
					case 5:
						API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ ACL has been disabled on this server.");
						break;
				}
			}
		}

		if (cmd == "/logout")
		{
			API.logoutPlayer(sender);
		}

		if (arguments[0] == "/start")
		{
			if (cmd.Length > 7)
			{
				var resource = cmd.Substring(7);
				if (API.doesResourceExist(resource))
				{
					API.startResource(resource);
					API.sendChatMessageToPlayer(sender, "~g~Started resource \"" + resource + "\"");
				}
				else
				{
					API.sendChatMessageToPlayer(sender, "~r~No such resource found: \"" + resource + "\"");
				}
			}	
			else
			{
				API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /start [resource]");
			}
		}

		if (arguments[0] == "/stop")
		{
			if (cmd.Length > 6)
			{
				var resource = cmd.Substring(6);
				if (API.doesResourceExist(resource))
				{
					API.stopResource(resource);
					API.sendChatMessageToPlayer(sender, "~g~Stopped resource \"" + resource + "\"");
				}
				else
				{
					API.sendChatMessageToPlayer(sender, "~r~No such resource found: \"" + resource + "\"");
				}
			}	
			else
			{
				API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /stop [resource]");
			}
		}

		if (arguments[0] == "/restart")
		{
			if (cmd.Length > 9)
			{				
				var resource = cmd.Substring(9);
				if (API.doesResourceExist(resource))
				{
					API.stopResource(resource);
					API.startResource(resource);

					API.sendChatMessageToPlayer(sender, "~g~Restarted resource \"" + resource + "\"");
				}
				else
				{
					API.sendChatMessageToPlayer(sender, "~r~No such resource found: \"" + resource + "\"");
				}
			}	
			else
			{
				API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /restart [resource]");
			}
		}

		if (arguments[0] == "/kick")
		{
			var split = cmd.Split();

			if (split.Length >= 3)
			{
				var player = API.getPlayerFromName(split[1]);

				if (player == null)
				{
					API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /kick [player] [reason]");
				}
				else
				{
					API.kickPlayer(player, split[2]);
				}
			}	
			else
			{
				API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /kick [player] [reason]");
			}
		}

		if (arguments[0] == "/kill")
		{
			var split = cmd.Split();

			if (split.Length >= 2)
			{
				var player = API.getPlayerFromName(split[1]);

				if (player == null)
				{
					API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /kill [player]");
				}
				else
				{
					API.setPlayerHealth(player, -1);
				}
			}	
			else
			{
				API.sendChatMessageToPlayer(sender, "~y~USAGE:~w~ /kill [player]");
			}
		}
	}

	private void onResStart(object e, EventArgs ob)
	{
		
	}

	public void onPlayerDisconnected(Client player, string reason)
	{
		API.logoutPlayer(player);
	}

	public void onUpdate(object sender, EventArgs e)
	{
		
	}

	public void onDeath(Client player)
	{
		
	}	

	public void OnPlayerConnected(Client player)
    {    	
        var log = API.loginPlayer(player, "");
        if (log == 1)
        {
        	API.sendChatMessageToPlayer(player, "Logged in as ~b~" + API.getPlayerAclGroup(player) + "~w~.");
        }
        else if (log == 2)
        {
			API.sendChatMessageToPlayer(player, "Please log in with ~b~/login [password]")        	;
        }
    }

}
