using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class PennedInMaster : Script
{
	public PennedInMaster()
	{
		API.onPlayerDeath += onPlayerDeath;
		API.onPlayerConnected += playerJoined;
		API.onResourceStart += resourceStart;
		API.onPlayerRespawn += respawn;

		API.onUpdate += update;
	}

	public void respawn(Client player)
	{
		if (roundrestart > 0 ) return;
		API.setPlayerToSpectator(player);
	}

	public void resourceStart()
	{
		API.sendChatMessageToAll("Round starting in 5 seconds!");
		API.sleep(5000);
		StartRound();
	}



	private List<Client> Survivors = new List<Client>();
	private Dictionary<Client, NetHandle> Vehicles = new Dictionary<Client, NetHandle>();
	private Vector3 StartPos = new Vector3(1400.18f, 3170.84f, 40.41f);	
	private Random randgen = new Random();
	private int Countdown = -1;
	private DateTime _lastsecondupdate;

	private int roundrestart = -1;

	private Vector3 CurrentSpherePosition;
	private float CurrentSphereScale;

	private DateTime? LastInterpolationUpdate;
	private int CurrentStep = -1;

	public void StartRound()
	{
		foreach (var pair in Vehicles)
		{
			API.deleteEntity(pair.Value);
		}

		LastInterpolationUpdate = null;
		CurrentStep = -1;

		var clients = API.getAllPlayers();
		Survivors.Clear();
		Vehicles.Clear();

		Survivors.AddRange(clients);

		CurrentSpherePosition = StartPos;
		CurrentSphereScale = 100f;

		foreach (var player in clients)
		{
			API.unspectatePlayer(player);
			var pos = StartPos.Around(20f);

			var availableCars = Enum.GetValues(typeof(VehicleHash)).Cast<VehicleHash>().ToList();

			//var ourCar = availableCars[randgen.Next(availableCars.Count)];
			var ourCar = VehicleHash.Blista2;

			API.setEntityPosition(player.handle, pos);

			var veh = API.createVehicle(ourCar, pos, new Vector3(), randgen.Next(160), randgen.Next(160));

			var start = Environment.TickCount;

			while (!API.doesEntityExistForPlayer(player, veh) && Environment.TickCount - start < 1000) {}

			API.triggerClientEvent(player, "pennedin_roundstart", CurrentSpherePosition, CurrentSphereScale);

			API.setPlayerIntoVehicle(player, veh, -1);

			API.freezePlayer(player, true);			

			Vehicles.Add(player, veh);
		}

		Countdown = 10;
	}

	public void update()
	{
		if (DateTime.Now.Subtract(_lastsecondupdate).TotalMilliseconds > 1000)
		{
			_lastsecondupdate = DateTime.Now;

			if (Countdown > 0)
			{
				Countdown--;

				if (Countdown == 0)
				{
					var clients = API.getAllPlayers();

					foreach (var player in clients)
					{
						API.freezePlayer(player, false);
					}

					API.sendChatMessageToAll("Round start! Stay inside the sphere!");
					LastInterpolationUpdate = DateTime.Now;
				}
			}

			if (roundrestart > 0)
			{
				roundrestart--;

				if (roundrestart == 0)
				{
					StartRound();
				}
			}
		}

		if (roundrestart > 0) return;

		if (LastInterpolationUpdate != null)
		{
			if (CurrentStep == -1 || DateTime.Now.Subtract(LastInterpolationUpdate.Value).TotalMilliseconds > MovementMap.Map[CurrentStep].Interval)
			{
				CurrentStep++;

				if (CurrentStep >= MovementMap.Map.Count)
				{
					if (Survivors.Any())
					{
						API.sendChatMessageToAll("Round has ended! The winners are: " + Survivors.Select(c => c.name).Aggregate((prev, next) => prev + ", " + next));
					}
					API.triggerClientEventForAll("pennedin_roundend");
					API.sendChatMessageToAll("Next round starts in 30 seconds...");
					roundrestart = 30;					
					return;
				}
				else
				{
					if (CurrentStep > 0 && !MovementMap.Map[CurrentStep - 1].Positional)
					{
						CurrentSphereScale = MovementMap.Map[CurrentStep - 1].Range;
					}

					if (CurrentStep > 0 && MovementMap.Map[CurrentStep - 1].Positional)
					{
						CurrentSpherePosition = MovementMap.Map[CurrentStep - 1].Vector;
					}

					if (MovementMap.Map[CurrentStep].Positional)
					{						
						API.triggerClientEventForAll("pennedin_setposdestination", CurrentSpherePosition, MovementMap.Map[CurrentStep].Vector, MovementMap.Map[CurrentStep].Interval);
					}
					else
					{						
						API.triggerClientEventForAll("pennedin_setscaledestination", CurrentSphereScale, MovementMap.Map[CurrentStep].Range, MovementMap.Map[CurrentStep].Interval);
					}

					LastInterpolationUpdate = DateTime.Now;
				}
			}
		}
		
	}

	public void playerJoined(Client player)
	{
		if (roundrestart>0) return;
		API.setPlayerToSpectator(player);
		API.triggerClientEvent(player, "pennedin_roundstart", CurrentSpherePosition, CurrentSphereScale);
		API.triggerClientEventForAll("pennedin_setposdestination", CurrentSpherePosition, MovementMap.Map[CurrentStep == -1 ? 0 : CurrentStep].Vector, MovementMap.Map[CurrentStep == -1 ? 0 : CurrentStep].Interval - (DateTime.Now.Subtract(LastInterpolationUpdate.Value).TotalMilliseconds));
		API.triggerClientEventForAll("pennedin_setscaledestination", CurrentSphereScale, MovementMap.Map[CurrentStep == -1 ? 0 : CurrentStep].Range, MovementMap.Map[CurrentStep == -1 ? 0 : CurrentStep].Interval - (DateTime.Now.Subtract(LastInterpolationUpdate.Value).TotalMilliseconds));
	}

	public void onPlayerDeath(Client player, NetHandle killer, int weapon)
	{
		if (roundrestart > 0) return;
		API.sendNotificationToAll("~b~~h~" + player.name + "~h~~w~ has died!");
		Survivors.Remove(player);		

		if (Survivors.Count == 1)
		{
			API.sendChatMessageToAll(Survivors[0].name + " has won! Restarting round in 30 seconds...");			

			var players = API.getAllPlayers();

			foreach (var c in players)
			{
				API.unspectatePlayer(c);
			}

			roundrestart = 30;
		}

		if (Survivors.Count == 0)
		{
			API.sendChatMessageToAll("Nobody won! Restarting round in 30 seconds...");			

			var players = API.getAllPlayers();

			foreach (var c in players)
			{
				API.unspectatePlayer(c);
			}

			roundrestart = 30;
		}
	}
}