using System;
using System.Dynamic;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;
using Microsoft.CSharp;

class dmhelper : Script
{
	dmhelper()
	{
		API.onChatCommand += onCommand;
	}

	void onCommand(Client sender, string cmd, CancelEventArgs e)
	{
		if (cmd == "/respawn")
		{
			API.exported.deathmatch.Respawn(sender);
		}
	}
}