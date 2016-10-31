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
	public void Glue(Client sender)
	{
		if(API.isEntityAttachedToAnything(sender.handle))
		{
			API.detachEntity(sender.handle, false);
			API.sendChatMessageToPlayer(sender, "~g~Unglued!");
			return;
		}

		var vehicles = API.getAllVehicles();
		var playerPos = API.getEntityPosition(sender.handle);

		if (vehicles.Count == 0)
		{
			API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~No nearby vehicles!");
			return;
		}

		var vOrd = vehicles.OrderBy(v => API.getEntityPosition(v).DistanceToSquared(playerPos));
		var targetVehicle = vOrd.First();

		if (API.fetchNativeFromPlayer<bool>(sender, 0x17FFC1B2BA35A494, sender.handle, targetVehicle))
		{
			var positionOffset = API.fetchNativeFromPlayer<Vector3>(sender, 0x2274BC1C4885E333, targetVehicle, playerPos.X, playerPos.Y, playerPos.Z);
			var rotOffset = API.getEntityRotation(targetVehicle) - API.getEntityRotation(sender.handle);

			rotOffset = new Vector3(rotOffset.X, rotOffset.Y, rotOffset.Z * -1f);

			API.attachEntityToEntity(sender.handle, targetVehicle, null, positionOffset, rotOffset);

			API.sendChatMessageToPlayer(sender, "~g~Glued!");
		}
	}
}