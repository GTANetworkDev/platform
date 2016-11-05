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
		API.onPlayerDisconnected += playerleft;
		API.onPlayerConnected += player => 
		{
			if (API.getAllPlayers().Count == 1 && !roundstarted)
			{
				roundstart();
			}
		};
	}

	private Vector3 _quadSpawn = new Vector3(-1564.36f, 4499.71f, 21.37f);
	private NetHandle _quad;

	private List<Vector3> _animalSpawnpoints = new List<Vector3>
	{
		new Vector3(-1488.76f, 4720.44f, 46.22f),
		new Vector3(-1317.07f, 4642.76f, 108.31f),
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
		PedHash.Hunter,
		PedHash.Hillbilly01AMM,
		PedHash.Hillbilly02AMM,
		PedHash.Cletus,
		PedHash.OldMan1aCutscene,
	};

	private List<Vector3> _checkpointPoses = new List<Vector3>
	{
		new Vector3(-1567.13f, 4541.15f, 17.26f),
		new Vector3(-1582.11f, 4654.32f, 46.93f),
		new Vector3(-1607.65f, 4369.2f, 2.45f),
		new Vector3(-1336.23f, 4404.12f, 31.91f),
		new Vector3(-1289.52f, 4498.14f, 14.8f),
		new Vector3(-1268.58f, 4690.92f, 83.74f),
	};

	private List<NetHandle> _checkpoints = new List<NetHandle>();
	private List<NetHandle> _checkpointBlips = new List<NetHandle>();

	public bool roundstarted;
	public Client animal;
	private long roundStart;
	private long lastIdleCheck;
	private Vector3 lastIdlePosition;
	private Client hawk;

	private const int TEAM_ANIMAL = 2;
	private const int TEAM_HUNTER = 1;

	private Random r = new Random();
	public void startGM()
	{
		roundstart();
	}


	public void stopGm()
	{
		var players = API.getAllPlayers();

		foreach (var player in players)
		{
			var pBlip = API.exported.playerblips.getPlayerBlip(player);

			API.setBlipTransparency(pBlip, 255);
			API.setBlipSprite(pBlip, 1);
			API.setBlipColor(pBlip, 0);
			API.setPlayerTeam(player, 0);
			API.setEntityInvincible(player, false);
		}
	}

	public void playerleft(Client player, string reason)
	{
		if (player == animal)
		{
			API.sendChatMessageToAll("The animal has left! Restarting...");
			animal = null;
			roundstarted = false;
			roundstart();
		}
	}

	public void roundstart()
	{
		var players = API.getAllPlayers();

		if (players.Count == 0) return;

		API.deleteEntity(breadcrumb);
		API.deleteEntity(_quad);

		foreach (var c in _checkpoints)
		{
			API.deleteEntity(c);
		}

		foreach (var c in _checkpointBlips)
		{
			API.deleteEntity(c);
		}

		_checkpoints.Clear();
		_checkpointBlips.Clear();

		for(int i = 0; i < _checkpointPoses.Count; i++)
		{
			_checkpoints.Add(API.createMarker(1, _checkpointPoses[i], new Vector3(), new Vector3(), new Vector3(10f, 10f, 3f), 200, 255, 255, 0));
			var b = API.createBlip(_checkpointPoses[i]);
			API.setBlipColor(b, 66);
			_checkpointBlips.Add(b);
		}

		_quad = API.createVehicle(VehicleHash.Blazer, _quadSpawn, new Vector3(0,0,0), 0, 0);

		animal = players[r.Next(players.Count)];
		var aBlip = API.exported.playerblips.getPlayerBlip(animal);
		API.setPlayerSkin(animal, PedHash.Deer);
		API.setPlayerTeam(animal, TEAM_ANIMAL);
		API.setBlipTransparency(aBlip, 0);
		API.setEntityInvincible(animal, false);
		var spawnp = _animalSpawnpoints[r.Next(_animalSpawnpoints.Count)];
		API.setEntityPosition(animal.handle, spawnp);
		API.setBlipSprite(aBlip, 141);
		API.setBlipColor(aBlip, 1);

		API.sendChatMessageToPlayer(animal, "You are the animal! Collect all the checkpoints to win!");		

		roundStart = API.TickCount;
		lastIdleCheck = API.TickCount;
		lastBreadcrumb = API.TickCount;
		roundstarted = true;
		lastIdlePosition = spawnp;

		if (players.Count > 3)
		{
			do
			{
				hawk = players[r.Next(players.Count)];
			} while (hawk == animal);
		}
		foreach(var p in players)
		{
			if (p == animal)
				continue;

			Spawn(p, p == hawk);
		}
	}

	public void Spawn(Client player, bool hawk = false)
	{
		var pBlip = API.exported.playerblips.getPlayerBlip(player);

		if (!hawk)
		{
			var skin = _skinList[r.Next(_skinList.Count)];		
			API.setPlayerSkin(player, skin);
			API.givePlayerWeapon(player, WeaponHash.PumpShotgun, 9999, true, true);
			API.givePlayerWeapon(player, WeaponHash.SniperRifle, 9999, true, true);
			API.setBlipTransparency(pBlip, 0);
			API.setBlipSprite(pBlip, 1);		
		}
		else
		{
			API.setPlayerSkin(player, PedHash.ChickenHawk);
			API.setBlipTransparency(pBlip, 255);
			API.setBlipSprite(pBlip, 422);
		}
		API.setBlipColor(pBlip, 0);
		API.setEntityPosition(player.handle, _hunterSpawnpoints[r.Next(_hunterSpawnpoints.Count)]);
		if (animal != null) API.sendChatMessageToPlayer(player, "~r~" + animal.name + "~w~ is the animal! ~r~Hunt~w~ it!");		
		API.setPlayerTeam(player, TEAM_HUNTER);
		API.setEntityInvincible(player, false);
	}

	public void dead(Client player, NetHandle reason, int weapon)
	{
		if (player == animal)
		{
			var killer = API.getPlayerFromHandle(reason);
			roundstarted = false;			
			API.sendChatMessageToAll("The animal has been killed" + (killer == null ? "!" : " by " + killer.name + "!") + " The hunters win!");
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
			Spawn(player, player == hawk);
	}

	public void update()
	{
		if (!roundstarted) return;
		if (animal != null)
		{
			var pBlip = API.exported.playerblips.getPlayerBlip(animal);

			for(int i = 0; i < _checkpoints.Count; i++)
			{
				var pos = API.getEntityPosition(_checkpoints[i]);
				if (API.getEntityPosition(animal.handle).DistanceToSquared(pos) < 100f)
				{
					API.deleteEntity(_checkpoints[i]);
					API.deleteEntity(_checkpointBlips[i]);
					_checkpointBlips.RemoveAt(i);
					_checkpoints.RemoveAt(i);
					API.sendChatMessageToAll("The animal has picked up a checkpoint!");

					if (_checkpoints.Count == 0)
					{
						roundstarted = false;
						API.sendChatMessageToAll("The animal has collected all checkpoints! " + animal.name + " has won!");
						API.sendChatMessageToAll("Starting next round in 15 seconds...");
						animal = null;
						roundstarted = false;
						API.sleep(15000);
						roundstart();
						break;
					}

					API.setBlipTransparency(pBlip, 255);
					API.sleep(5000);
					API.setBlipTransparency(pBlip, 0);					

					break;
				}
			}
		
			if (API.TickCount - lastIdleCheck > 20000) // 20 secs
			{
				lastIdleCheck = API.TickCount;

				if (API.getEntityPosition(animal.handle).DistanceToSquared(lastIdlePosition) < 5f)
				{
					API.setBlipTransparency(pBlip, 255);
					API.sleep(1000);
					API.setBlipTransparency(pBlip, 0);
				}
				else
				{
					API.setBlipTransparency(pBlip, 0);
				}

				if (!breadcrumbLock) // breadcrumbs are very messy since i was debugging the blips not disappearing
				{
					breadcrumbLock = true;
					breadcrumb = API.createBlip(lastIdlePosition);
					API.setBlipSprite(breadcrumb, 161);
					API.setBlipColor(breadcrumb, 1);
					API.setBlipTransparency(breadcrumb, 200);

					lastBreadcrumb = API.TickCount;					
				}
				if (animal != null)
					lastIdlePosition = API.getEntityPosition(animal.handle);
			}

			if (API.TickCount - lastBreadcrumb > 15000 && breadcrumbLock)
			{
				API.deleteEntity(breadcrumb);
				breadcrumbLock = false;
			}
		}
	}

	private bool breadcrumbLock;
	private NetHandle breadcrumb;
	private long lastBreadcrumb;
}