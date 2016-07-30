using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class GlueScript : Script
{
	[Command]
	public void Command_Glue(Client sender)
	{
		var vehicles = API.getAllVehicles();

		if (vehicles.Count == 0)
		{
			API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~No nearby vehicles!");
			return;
		}
	}
}