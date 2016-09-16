using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class PlayerBlips : Script
{
	public PlayerBlips()
	{
		API.onPlayerConnected += PlayerJoin;
		API.onPlayerDisconnected += PlayerLeave;
		API.onPlayerFinishedDownload += PlayerJavascriptDownloadComplete;
	}

	private void PlayerJoin(Client player)
	{
		var pBlip = API.createBlip(player);
		API.attachEntityToEntity(pBlip, player, null, new Vector3(), new Vector3());

		API.setBlipName(pBlip, player.Name);
		API.setBlipScale(pBlip, 0.8f);

		API.setEntityData(player, "PLAYERBLIPS_MAIN_BLIP", pBlip);
	}

	private void PlayerJavascriptDownloadComplete(Client player)
	{
		if (API.getEntityData(player, "PLAYERBLIPS_HAS_BLIP_RECEIVED") != true)
		{
			API.triggerClientEvent(player, "SET_PLAYER_BLIP", getPlayerBlip(player));
			API.setEntityData(player, "PLAYERBLIPS_HAS_BLIP_RECEIVED", true);
		}
	}

	private void PlayerLeave(Client player, string reason)
	{
		var ourBlip = API.getEntityData(player, "PLAYERBLIPS_MAIN_BLIP");

		if (ourBlip != null)
		{
			API.deleteEntity(ourBlip);
		}
	}

	// EXPORTED METHODS

	public NetHandle getPlayerBlip(Client player)
	{
		return API.getEntityData(player, "PLAYERBLIPS_MAIN_BLIP");
	}

	public void setPlayerBlip(Client player, NetHandle blip)
	{
		API.setEntityData(player, "PLAYERBLIPS_MAIN_BLIP", blip);
	}
}