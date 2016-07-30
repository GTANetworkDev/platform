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
		if(API.isEntityAttachedToAnything(sender.CharacterHandle))
		{
			API.detachEntity(sender.CharacterHandle, false);
			API.sendChatMessageToPlayer(sender, "~g~Unglued!");
			return;
		}

		var vehicles = API.getAllVehicles();
		var playerPos = API.getEntityPosition(sender.CharacterHandle);

		if (vehicles.Count == 0)
		{
			API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~No nearby vehicles!");
			return;
		}

		var vOrd = vehicles.OrderBy(v => API.getEntityPosition(v).DistanceToSquared(playerPos));
		var targetVehicle = vOrd.First();

		if (API.fetchNativeFromPlayer<bool>(sender, 0x17FFC1B2BA35A494, sender.CharacterHandle, targetVehicle))
		{
			var positionOffset = API.fetchNativeFromPlayer<Vector3>(sender, 0x2274BC1C4885E333, targetVehicle, playerPos.X, playerPos.Y, playerPos.Z);
			var rotOffset = API.getEntityRotation(targetVehicle) - API.getEntityRotation(sender.CharacterHandle);

			rotOffset = new Vector3(rotOffset.X, rotOffset.Y, rotOffset.Z * -1f);

			API.attachEntityToEntity(sender.CharacterHandle, targetVehicle, null, positionOffset, rotOffset);

			API.sendChatMessageToPlayer(sender, "~g~Glued!");
		}
	}
}