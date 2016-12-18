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
		API.onResourceStop += resourceStop;
		API.onResourceStart += () => 
		{
			foreach (var player in API.getAllPlayers())
			{
				PlayerJoin(player);
			}
		};
	}

	private void resourceStop()
	{
		foreach (var player in API.getAllPlayers())
		{
			API.resetEntitySyncedData(player, "PLAYERBLIPS_HAS_BLIP_RECEIVED");
			API.resetEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP");
		}
	}

	private void PlayerJoin(Client player)
	{
		var pBlip = API.createBlip(API.getEntityPosition(player));
		API.attachEntityToEntity(pBlip, player, null, new Vector3(), new Vector3());

		API.setBlipName(pBlip, player.name);
		API.setBlipScale(pBlip, 0.8f);

		API.setEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP", pBlip);
	}

	private void PlayerJavascriptDownloadComplete(Client player)
	{
		if (API.getEntitySyncedData(player, "PLAYERBLIPS_HAS_BLIP_RECEIVED") != true)
		{
			API.triggerClientEvent(player, "SET_PLAYER_BLIP", getPlayerBlip(player));
			API.setEntitySyncedData(player, "PLAYERBLIPS_HAS_BLIP_RECEIVED", true);
		}
	}

	private void PlayerLeave(Client player, string reason)
	{
		var ourBlip = API.getEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP");

		if (ourBlip != null)
		{
			API.deleteEntity(ourBlip);
		}
	}

	// EXPORTED METHODS

	public NetHandle getPlayerBlip(Client player)
	{
		if (!API.hasEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP")) return new NetHandle(0);

		var data = API.getEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP");
		return (object)data == null ? new NetHandle(0) : data;
	}

	public void setPlayerBlip(Client player, NetHandle blip)
	{
		API.setEntitySyncedData(player, "PLAYERBLIPS_MAIN_BLIP", blip);
	}
}