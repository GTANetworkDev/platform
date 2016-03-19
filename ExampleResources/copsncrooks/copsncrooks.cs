using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class CopsNCrooks : Script
{
	public CopsNCrooks()
	{
		Cops = new List<Client>();
		Crooks = new List<Client>();
		Vehicles = new List<NetHandle>();

		API.onPlayerRespawn += onDeath;
		API.onPlayerConnected += OnPlayerConnected;
		API.onUpdate += onUpdate;
		API.onResourceStart += onResStart;
		API.onPlayerDisconnected += onPlayerDisconnected;
	}

	public List<Client> Cops;
	public List<Client> Crooks;

	public int CopTeam = 2;
	public int CrookTeam = 3;

	public List<NetHandle> Vehicles;

	private Vector3 _copRespawn = new Vector3(444.12f, -983.73f, 30.69f);
	private Vector3 _crookRespawn = new Vector3(617.16f, 608.95f, 128.91f);
	private Vector3 _targetPos = new Vector3(-1079.34f, -3001.1f, 13.96f);

	public Client CrookBoss;
	public NetHandle EscapeVehicle;
	public bool isRoundOngoing;
	private Random rngProvider = new Random();

	private void SpawnCars()
	{
		Vehicles.Clear();

		// Crooks
		Vehicles.Add(API.createVehicle(1777363799, new Vector3(634.32f, 622.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));
		Vehicles.Add(API.createVehicle(1777363799, new Vector3(636.32f, 635.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));
		Vehicles.Add(API.createVehicle(1777363799, new Vector3(631.32f, 613.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));

		Vehicles.Add(API.createVehicle(1777363799, new Vector3(611.32f, 637.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));
		Vehicles.Add(API.createVehicle(1777363799, new Vector3(607.32f, 626.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));
		Vehicles.Add(API.createVehicle(1777363799, new Vector3(593.32f, 618.54f, 128.44f), new Vector3(0, 0, 250.89f), 0, 0));

		Vehicles.Add(API.createVehicle(-114627507, new Vector3(631.5f, 642.54f, 128.44f), new Vector3(0, 0, 327.15f), 0, 0));
		Vehicles.Add(API.createVehicle(-1660661558, new Vector3(644.09f, 599.09f, 129.01f), new Vector3(0, 0, 0.15f), 0, 0));

		// Cops
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(407.37f, -979.13f, 28.88f), new Vector3(0, 0, 52.84f), 111, 0));		
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(407.37f, -983.99f, 28.88f), new Vector3(0, 0, 52.84f), 111, 0));		
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(407.37f, -988.8f, 28.88f), new Vector3(0, 0, 52.84f), 111, 0));		
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(407.37f, -992.76f, 28.88f), new Vector3(0, 0, 52.84f), 111, 0));		
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(407.37f, -997.72f, 28.88f), new Vector3(0, 0, 52.84f), 111, 0));		
		Vehicles.Add(API.createVehicle(-1627000575, new Vector3(393.51f, -981.3f, 28.96f), new Vector3(0, 0, 356.26f), 111, 0));		

		Vehicles.Add(API.createVehicle(-1860900134, new Vector3(428.9f, -960.98f, 29.11f), new Vector3(0, 0, 90.12f), 111, 0));		
	}

	private void onResStart(object e, EventArgs ob)
	{
		var blip = API.createBlip(_targetPos);
		API.setBlipColor(blip, 66);

		API.createMarker(28, _targetPos, new Vector3(), new Vector3(), new Vector3(20f, 20f, 20f), 80, 255, 255, 255);
	}

	public void onPlayerDisconnected(Client player, string reason)
	{
		if (Crooks.Contains(player))
			Crooks.Remove(player);
		if (Cops.Contains(player))
			Cops.Remove(player);

		if (CrookBoss == player)
		{
			API.sendNotificationToAll("The boss has left the game. Restarting the round.");
			isRoundOngoing = false;
		}

		if (Crooks.Count == 0 || Cops.Count == 0)
		{
			API.sendNotificationToAll("One of the teams is empty. Restarting...");
			isRoundOngoing = false;
		}
	}

	private bool IsInRangeOf(Vector3 playerPos, Vector3 target, float range)
	{
		var direct = new Vector3(target.X - playerPos.X, target.Y - playerPos.Y, target.Z - playerPos.Z);
		var len = direct.X * direct.X + direct.Y * direct.Y + direct.Z * direct.Z;
		return range * range > len;
	}

	public void onUpdate(object sender, EventArgs e)
	{
		if (!isRoundOngoing)
		{
			var players = API.getAllPlayers();
			if (players.Count < 2)
			{
				return;
			}
			API.sendNotificationToAll("Starting new round in 5 seconds!");
			API.sleep(5000);
			StartRound();
		}
		else
		{
			if (IsInRangeOf(CrookBoss.Position, _targetPos, 10f))
			{
				API.sendNotificationToAll("The boss has arrived to the destination. The ~r~Crooks~w~ win!");
				isRoundOngoing = false;
			}
		}
	}

	public void StartRound()
	{
		Cops.Clear();
		Crooks.Clear();

		API.triggerClientEventForAll("clearAllBlips");

		isRoundOngoing = true;

		foreach (var car in Vehicles)
		{
			API.deleteEntity(car);
		}

		// spawn vehicles here

		SpawnCars();

		var players = API.getAllPlayers();
		players.Shuffle();
		var firstHalfCount = players.Count / 2;
		var secondHalfCount = players.Count - players.Count / 2;

		Crooks = new List<Client>(players.GetRange(0, firstHalfCount));
		Cops = new List<Client>(players.GetRange(firstHalfCount, secondHalfCount));
		CrookBoss = Crooks[rngProvider.Next(Crooks.Count)];

		foreach (var c in Cops)
		{
			Respawn(c);
			API.consoleOutput(c.Name + " is a cop!");
		}

		foreach (var c in Crooks)
		{
			if (c == CrookBoss)
			{
				API.setPlayerSkin(c, 1226102803); // MexBoss02GMM
				API.sendNotificationToPlayer(c, "You are ~r~the boss~w~! Get to the ~y~extraction point~w~ alive!~");				
			}					
			else
			{
				API.sendNotificationToPlayer(c, "Don't let the ~r~cops~w~ kill your boss!");
				API.setPlayerSkin(c, -1773333796); // MexGoon03GMY
			}
			CrookRespawn(c);
			API.setPlayerTeam(c, CrookTeam);			
		}

		API.sendNotificationToAll("The crook boss is " + CrookBoss.Name + "!");
		
		
	}

	public void Respawn(Client player)
	{
		if (!isRoundOngoing) return;

		if (!Cops.Contains(player))
		{
			Cops.Add(player);
		}

		API.setPlayerSkin(player, 1581098148); // Cop01SMY

		API.givePlayerWeapon(player, 1737195953, 1, true, true);
		API.givePlayerWeapon(player, 1593441988, 300, true, true);
		API.givePlayerWeapon(player, 487013001, 300, true, true);


		API.setPlayerHealth(player, 100);

		API.sendNotificationToPlayer(player, "Apprehend the ~r~crooks!");
		API.setEntityPosition(player.CharacterHandle, _copRespawn);			

		API.setPlayerTeam(player, CopTeam);
	}

	public void CrookRespawn(Client player)
	{		
		if (!isRoundOngoing) return;
		API.givePlayerWeapon(player, 615608432, 20, true, true);		
		API.givePlayerWeapon(player, 137902532, 100, true, true);
		API.givePlayerWeapon(player, 1627465347, 700, true, true);
		API.setPlayerHealth(player, 100);
		API.setEntityPosition(player.CharacterHandle, _crookRespawn);
	}

	public void onDeath(Client player)
	{
		if (player == CrookBoss)
		{
			API.sendNotificationToAll("The boss has died! ~b~Cops~w~ win!");
			isRoundOngoing = false;
			return;
		}
		else if (Cops.Contains(player))
		{
			Respawn(player);
		}
		else if (Crooks.Contains(player))
		{
			CrookRespawn(player);
		}
	}	

	public void OnPlayerConnected(Client player)
    {    	
        Respawn(player);        
    }

}


public static class Extensions
{
	public static Random rng = new Random();
	public static void Shuffle<T>(this IList<T> list)  
	{  
	    int n = list.Count;  
	    while (n > 1) {  
	        n--;  
	        int k = rng.Next(n + 1);  
	        T value = list[k];  
	        list[k] = list[n];  
	        list[n] = value;  
	    }  
	}
}