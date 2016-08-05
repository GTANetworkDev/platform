using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class HunterScript : Script
{
	public HunterScript()
	{
		API.onResourceStart += startGM;
		API.onResourceStop += stopGm;
		API.onUpdate += update;
		API.onPlayerDeath += dead;
		API.onPlayerRespawn += respawn;
		API.onPlayerConnected += respawn;
		API.onPlayerDisconnect += playerleft;
	}

	private List<Vector3> _animalSpawnpoints = new List<Vector3>
	{
		new Vector3(-1488.76f, 4720.44f, 46.22f),
		new Vector3(-1317.07, 4642.76, 108.31),
		new Vector3(-1291.08f, 4496.54f, 14.7f),
		new Vector3(-1166.29f, 4409.41f, 8.87f),
		new Vector3(-1522.33f, 4418.05f, 12.2f),
		new Vector3(-1597.47f, 4483.42f, 17.4f),
	};

	private List<Vector3> _hunterSpawnpoints = new List<Vector3>
	{
		new Vector3(-1592.35f, 4486.51f, 17.57f),
		new Vector3(-1572.83f, 4471.25f, 13.92f),
		new Vector3(-1559.22f, 4467.11f, 18.74f),
		new Vector3(-1515.67f, 4482.07f, 17.56f),
		new Vector3(-1538.81f, 4443.99f, 11.53f),
	};

	private List<PedHash> _skinList = new List<PedHash>
	{
		PedHash.Hunter, PedHash.Hillbilly01AMM, PedHash.Hillbilly02AMM, PedHash.Cletus,
		PedHash.OldMan1aCutscene,
	};

	public bool roundstarted;
	public Client animal;
	private long roundStart;
	private long lastIdleCheck;
	private Vector3 lastIdlePosition;

	private const int TEAM_ANIMAL = 2;
	private const int TEAM_HUNTER = 1;

	private Random r = new Random();
	public void startGM(object sender, EventArgs e)
	{
		roundstart();
	}


	public void stopGm(object sender, EventArgs e)
	{
		var players = API.getAllPlayers();

		foreach (var player in players)
		{
			API.setPlayerBlipAlpha(player, 255);
			API.setPlayerBlipSprite(player, 1);
			API.setPlayerBlipColor(player, 0);
			API.setPlayerTeam(player, 0);
			API.setPlayerInvincible(player, false);
		}
	}

	public void playerleft(Client player, string reason)
	{
		if (player == animal)
		{
			API.sendChatMessageToAll("The animal has left! Restarting...");
			roundstart();
		}
	}

	public void roundstart()
	{
		var players = API.getAllPlayers();

		animal = players[r.Next(players.Count)];
		API.setPlayerSkin(animal, PedHash.Deer);
		API.setPlayerTeam(animal, TEAM_ANIMAL);
		API.setPlayerBlipAlpha(animal, 0);
		API.setPlayerInvincible(animal, false);
		var spawnp = _animalSpawnpoints[r.Next(_animalSpawnpoints.Count)];
		API.setEntityPosition(animal.CharacterHandle, spawnp);
		API.setPlayerBlipSprite(animal, 141);
		API.setPlayerBlipColor(animal, 1);

		API.sendChatMessageToPlayer(animal, "You are the animal! Survive five minutes to win!");		

		roundStart = API.TickCount;
		lastIdleCheck = API.TickCount;
		lastBreadcrumb = API.TickCount;
		roundstarted = true;
		lastIdlePosition = spawnp;

		foreach(var p in players)
		{
			if (p == animal)
				continue;

			Spawn(p);
		}
	}

	public void Spawn(Client player)
	{
		var skin = _skinList[r.Next(_skinList.Count)];
		API.setPlayerSkin(player, skin);
		API.givePlayerWeapon(player, 487013001, 9999, true, true);
		API.setEntityPosition(player.CharacterHandle, _hunterSpawnpoints[r.Next(_hunterSpawnpoints.Count)]);
		if (animal != null) API.sendChatMessageToPlayer(player, "~r~" + animal.Name + "~w~ is the animal! ~r~Hunt~w~ it!");
		API.setPlayerBlipAlpha(player, 0);
		API.setPlayerBlipSprite(player, 1);
		API.setPlayerBlipColor(player, 0);
		API.setPlayerTeam(player, TEAM_HUNTER);
		API.setPlayerInvincible(player, false);
	}

	public void dead(Client player, NetHandle reason, int weapon)
	{
		if (player == animal)
		{
			var killer = API.getPlayerFromHandle(reason);
			roundstarted = false;			
			API.sendChatMessageToAll("The animal has been killed" + (killer == null ? "!" : " by " + killer.Name + "!") + " The hunters win!");
			API.sendChatMessageToAll("Starting next round in 15 seconds...");
			animal = null;
			roundstarted = false;
			API.sleep(15000);
			roundstart();
		}
	}

	public void respawn (Client player)
	{
		if (roundstarted && player != animal)		
			Spawn(player);
	}

	public void update(object s, EventArgs e)
	{
		if (!roundstarted) return;

		if (API.TickCount - roundStart > 5 * 60 * 1000) // 5 min
		{
			roundstarted = false;
			API.sendChatMessageToAll("The animal has survived 5 minutes! " + animal.Name + " has won!");
			API.sendChatMessageToAll("Starting next round in 15 seconds...");
			animal = null;
			roundstarted = false;
			API.sleep(15000);
			roundstart();
		}

		if (animal != null)
			if (API.TickCount - lastIdleCheck > 20000) // 20 secs
			{
				lastIdleCheck = API.TickCount;

				if (API.getEntityPosition(animal.CharacterHandle).DistanceToSquared(lastIdlePosition) < 5f)
				{
					API.setPlayerBlipAlpha(animal, 255);
					API.sleep(1000);
					API.setPlayerBlipAlpha(animal, 0);
				}
				else
				{
					API.setPlayerBlipAlpha(animal, 0);
				}

				if (!breadcrumbLock)
				{
					API.consoleOutput("Putting a breadcrumb...");
					breadcrumbLock = true;
					breadcrumb = API.createBlip(lastIdlePosition);
					API.setBlipSprite(breadcrumb, 161);
					API.setBlipColor(breadcrumb, 1);
					API.setBlipAlpha(breadcrumb, 30);

					lastBreadcrumb = API.TickCount;					
				}
				if (animal != null)
					lastIdlePosition = API.getEntityPosition(animal.CharacterHandle);
			}

			if (API.TickCount - lastBreadcrumb > 15000 && breadcrumbLock)
			{
				API.deleteEntity(breadcrumb);
				API.consoleOutput("removing breadcrumb...");
				breadcrumbLock = false;
			}
	}

	private bool breadcrumbLock;
	private NetHandle breadcrumb;
	private long lastBreadcrumb;
}