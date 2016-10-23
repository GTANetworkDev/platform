using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using GTANetworkServer;
using GTANetworkShared;

/*

	Gamemode Premise

	You have 2 teams, one attacks one defends.

	There's a time limit and a couple of objectives. Objectives are simple checkpoints
	with a countdown attached. An attacker must 'capture' the objective to get it. Once an objective
	has been captured, the spawnpoints are changed (optionally), and the attackers must capture the next one.
	If the time runs out before the attackers capture all of the objectives, defenders win.

*/

public class Assault : Script
{
	public Assault()
	{
		API.onResourceStart += Start;
	    API.onResourceStop += Stop;
		API.onMapChange += MapChange;

	    API.onPlayerFinishedDownload += SpawnPlayer;
	    API.onPlayerRespawn += SpawnPlayer;
	    API.onUpdate += Update;

        _rand = new Random();
	}

    /* EXPORTED */
    public event ExportedEvent onRoundStart;
    public event ExportedEvent onRoundEnd;
    public event ExportedEvent onObjectiveCaptured;

    public void invokeStart()
    {
        if (onRoundStart != null) onRoundStart.Invoke();
    }

    public void invokeEnd()
    {
        if (onRoundEnd != null) onRoundStart.Invoke();
    }

    public void invokeObjCaptured(int objective, List<Client> capturers)
    {
        if (onObjectiveCaptured != null) onObjectiveCaptured.Invoke(objective, capturers);
    }

    public Round CurrentRound;

    public const int ATTACKER_TEAM = 1;
    public const int DEFENDER_TEAM = 2;

    private Random _rand;

	public void Start()
	{

	}

    public void Stop()
    {
        foreach (var player in API.getAllPlayers())
        {
            player.team = 0;
        }
    }

	public void MapChange(string name, XmlGroup map)
	{
		EndRound();

		var round = new Round();

        #region Objectives
        foreach (var element in map.getElementsByType("objective"))
	    {
	        var obj = new Objective();

	        if (element.hasElementData("id"))
	            obj.Id = element.getElementData<int>("id");

            obj.Position = new Vector3(element.getElementData<float>("posX"),
                element.getElementData<float>("posY"),
                element.getElementData<float>("posZ"));

	        if (element.hasElementData("range"))
	            obj.Range = element.getElementData<float>("range");

	        obj.Name = element.getElementData<string>("name");

	        if (element.hasElementData("timer"))
	            obj.Timer = element.getElementData<int>("timer");

	        if (element.hasElementData("required"))
	        {
	            var listStr = element.getElementData<string>("required");
	            obj.RequiredObjectives =
	                listStr.Split(',').Select(id => int.Parse(id, CultureInfo.InvariantCulture)).ToList();
	        }

            round.Objectives.Add(obj);
	    }
        #endregion
        
        #region Spawnpoints

	    foreach (var element in map.getElementsByType("spawnpoint"))
	    {
	        var sp = new Spawnpoint();

	        sp.Team = element.getElementData<int>("team");
	        sp.Heading = element.getElementData<float>("heading");

            sp.Position = new Vector3(element.getElementData<float>("posX"),
                element.getElementData<float>("posY"),
                element.getElementData<float>("posZ"));

	        sp.Skins =
	            element.getElementData<string>("skins")
	                .Split(',')
	                .Select(s => API.pedNameToModel(s))
	                .ToArray();

	        var guns =
	            element.getElementData<string>("weapons")
	                .Split(',')
	                .Select(w => API.weaponNameToModel(w));
	        var ammos =
	            element.getElementData<string>("ammo")
                .Split(',')
                .Select(w => int.Parse(w, CultureInfo.InvariantCulture));

	        sp.Weapons = guns.ToArray();
	        sp.Ammo = ammos.ToArray();

	        round.Spawnpoints.Add(sp);
	    }
        #endregion

        StartRound(round);
    }

    public void SpawnPlayer(Client player)
    {
        if (CurrentRound == null) return;

        // Assume team is assigned

        var availablePoints = CurrentRound.Spawnpoints.Where(sp => sp.Team == player.team).ToArray();

        API.consoleOutput("Available Points: " + availablePoints.Length);
        API.consoleOutput("Team: " + player.team);

        var spawnpoint = availablePoints[_rand.Next(availablePoints.Length)];

        API.consoleOutput("Available Skins: " + spawnpoint.Skins.Length);
        player.removeAllWeapons();
        player.setSkin(spawnpoint.Skins[_rand.Next(spawnpoint.Skins.Length)]);

        API.consoleOutput("Available Guns: " + spawnpoint.Weapons.Length);
        API.consoleOutput("Available Ammo: " + spawnpoint.Ammo.Length);

        for (int i = 0; i < spawnpoint.Weapons.Length; i++)
        {
            player.giveWeapon(spawnpoint.Weapons[i], spawnpoint.Ammo[i], true, true);
        }

        player.team = spawnpoint.Team;
        player.position = spawnpoint.Position;
        player.rotation = new Vector3(0, 0, spawnpoint.Heading);

        if (player.team == ATTACKER_TEAM)
        {
            API.triggerClientEvent(player, "display_subtitle", "Attack the ~r~objectives!", 120000);
        }
        else
        {
            API.triggerClientEvent(player, "display_subtitle", "Defend the ~b~objectives!", 120000);
        }
    }

    public void EndRound()
	{
		if (CurrentRound != null)
		{
		    foreach (var ent in CurrentRound.Cleanup)
		    {
		        ent.delete();
		    }

            invokeEnd();
		}

		CurrentRound = null;
	}

	public void StartRound(Round round)
	{
		if (CurrentRound != null) EndRound();
        CurrentRound = round;

        var players = API.getAllPlayers();

	    for (int i = players.Count - 1; i >= 0; i--)
	    {
	        players[i].team = _rand.Next(1, 3);

            SpawnPlayer(players[i]);
	    }

	    foreach (var objective in round.Objectives)
	    {
	        if (objective.RequiredObjectives.Count > 0) continue;

	        CreateObjective(objective);
        }

        invokeStart();

	    API.consoleOutput("Starting new round!");
	}

    public void CreateObjective(Objective objective)
    {
        objective.Marker = API.createMarker(1, objective.Position, new Vector3(), new Vector3(),
                    new Vector3(objective.Range, objective.Range, 5f), 100, 255, 255, 255);

        objective.Blip = API.createBlip(objective.Position);
        objective.Blip.color = 1;
        /*
        foreach (var player in API.getAllPlayers())
        {
            if (player.team == ATTACKER_TEAM)
            {
                API.triggerClientEvent(player, "set_blip_color", objective.Blip.handle, 1);
                API.triggerClientEvent(player, "set_marker_color", objective.Marker.handle, 240, 10, 10);
            }
            else
            {
                API.triggerClientEvent(player, "set_blip_color", objective.Blip.handle, 67);
                API.triggerClientEvent(player, "set_marker_color", objective.Marker.handle, 10, 10, 200);
            }
        }
        */
        objective.Spawned = true;

        CurrentRound.Cleanup.Add(objective.Marker);
        CurrentRound.Cleanup.Add(objective.Blip);
    }

    public void SpawnRequiredCheckpoints()
    {
        foreach (var objective in CurrentRound.Objectives)
        {
            if (objective.Spawned) continue;
            if (objective.RequiredObjectives.All(
                obj =>
                    CurrentRound.Objectives.Any(o => o.Id == obj) &&
                    CurrentRound.Objectives.First(o => o.Id == obj).Captured))
            {
                CreateObjective(objective);
            }
        }
    }

    public void Update()
    {
        if (CurrentRound == null) return;

        foreach (var objective in CurrentRound.Objectives)
        {
            if (objective.Captured || !objective.Spawned) continue;

            if (objective.RequiredObjectives.Count > 0 &&
                objective.RequiredObjectives.Any(
                    obj =>
                        CurrentRound.Objectives.Any(o => o.Id == obj) &&
                        !CurrentRound.Objectives.First(o => o.Id == obj).Captured))
            {
                continue;
            }

            var players = API.getAllPlayers();

            int attackersOnObjective = 0;
            int defendersOnObjective = 0;

            foreach (var player in players)
            {
                if (player.position.DistanceToSquared(objective.Position) > objective.Range*objective.Range)
                    continue;

                if (player.team == ATTACKER_TEAM) attackersOnObjective++;
                else defendersOnObjective++;
            }

            if (attackersOnObjective > 0 && defendersOnObjective == 0)
            {
                if (!objective.Active)
                {
                    objective.Active = true;
                    objective.TimeLeft = objective.Timer;
                    objective.LastActiveUpdate = API.TickCount;

                    API.triggerClientEventForAll("display_subtitle", "Objective ~y~" + objective.Name + "~w~ is being captured!", objective.Timer);
                }

                objective.TimeLeft -= (int) (API.TickCount - objective.LastActiveUpdate);
                objective.LastActiveUpdate = API.TickCount;

                // TODO: Progress bar

                if (objective.TimeLeft < 0)
                {
                    objective.Captured = true;

                    objective.Blip.delete();
                    objective.Marker.delete();

                    API.triggerClientEventForAll("display_subtitle", "Objective ~y~" + objective.Name + "~w~ captured!", 5000);

                    SpawnRequiredCheckpoints();

                    if (CurrentRound.Objectives.All(o => o.Captured))
                    {
                        API.sendChatMessageToAll("The attackers have won!");
                        API.exported.mapcycler.endRound();
                    }

                    invokeObjCaptured(objective.Id,
                        players.Where(
                            p => p.position.DistanceToSquared(objective.Position) < objective.Range*objective.Range)
                            .ToList());
                }
            }
            else if (attackersOnObjective > 0 && defendersOnObjective > 0)
            {
                objective.LastActiveUpdate = API.TickCount;
            }
            else
            {
                objective.Active = false;
                objective.LastActiveUpdate = 0;
            }
        }
    }

}