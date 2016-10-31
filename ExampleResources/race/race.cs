using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class RaceGamemode : Script
{
    public RaceGamemode()
    {
        API.onUpdate += onUpdate;
        API.onPlayerDisconnected += onDisconnect;        
        API.onPlayerFinishedDownload += onPlayerConnect;
        API.onPlayerRespawn += onPlayerRespawn;
        API.onClientEventTrigger += onClientEvent;
        API.onResourceStop += onResourceStop;
        API.onResourceStart += onResourceStart;

        API.onMapChange += MapChange;
    }

    public bool IsRaceOngoing { get; set; }
    public List<Opponent> Opponents { get; set; }
    public Race CurrentRace { get; set; }
    public List<Race> AvailableRaces { get; set; }
    public List<Vector3> CurrentRaceCheckpoints { get; set; }
    public DateTime RaceStart { get; set; }
    public List<NetHandle> Objects { get; set; }
    public List<Thread> ActiveThreads { get; set; }    
    public int RaceStartCountdown { get; set; }
    public DateTime RaceTimer { get; set; }
    public DateTime LastSecond {get; set;}

    public void onResourceStart()
    {
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        CurrentRaceCheckpoints = new List<Vector3>();
        Objects = new List<NetHandle>();

        API.consoleOutput("Race gamemode started!");

        API.exported.scoreboard.addScoreboardColumn("race_place", "Place", 80);
        API.exported.scoreboard.addScoreboardColumn("race_checkpoints", "Checkpoints", 160);
        API.exported.scoreboard.addScoreboardColumn("race_time", "Time", 120);

        API.exported.mapcycler.endRound();
    }

    private void UpdateScoreboardData(Opponent player, int place)
    {
        if (place != -1)
            API.exported.scoreboard.setPlayerScoreboardData(player.Client, "race_place", place.ToString());
        API.exported.scoreboard.setPlayerScoreboardData(player.Client, "race_checkpoints", player.CheckpointsPassed.ToString());
        API.exported.scoreboard.setPlayerScoreboardData(player.Client, "race_time", player.TimeFinished);
    }

    public void onPlayerRespawn(Client player)
    {
        if (IsRaceOngoing)
        {
            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == player);
            if (curOp == null || curOp.HasFinished || !curOp.HasStarted || curOp.CheckpointsPassed == 0)
            {
                SetUpPlayerForRace(player, CurrentRace, false, 0);
            }
            else
            {
                RespawnPlayer(player, CurrentRace, curOp.CheckpointsPassed - 1);
            }
        }
    }

    public void onResourceStop()
    {
        API.triggerClientEventForAll("resetRace");
        
        API.exported.scoreboard.removeScoreboardColumn("race_place");
        API.exported.scoreboard.removeScoreboardColumn("race_checkpoints");
        API.exported.scoreboard.removeScoreboardColumn("race_time");
    }

    private Race parseRace(string mapName, XmlGroup map)
    {
        var output = new Race();
        output.Name = API.getResourceName(mapName);
        output.Description = API.getResourceDescription(mapName);
        output.Filename = mapName;

        var meta = map.getElementByType("laps");

        if (meta != null)
        {
            output.LapsAvailable = meta.getElementData<bool>("value");
        }

        var checkpoints = new List<Vector3>();

        foreach(var chk in map.getElementsByType("checkpoint"))
        {
            checkpoints.Add(
                new Vector3(chk.getElementData<float>("posX"),
                            chk.getElementData<float>("posY"),
                            chk.getElementData<float>("posZ")));
        }

        output.Checkpoints = checkpoints.ToArray();

        var sp = new List<SpawnPoint>();

        foreach(var chk in map.getElementsByType("spawnpoint"))
        {
            sp.Add(new SpawnPoint() {
                Position = new Vector3(chk.getElementData<float>("posX"),
                            chk.getElementData<float>("posY"),
                            chk.getElementData<float>("posZ")),
                Heading = chk.getElementData<float>("heading"),
            });
        }

        output.SpawnPoints = sp.ToArray();

        var vehs = new List<VehicleHash>();

        foreach(var chk in map.getElementsByType("availablecar"))
        {
            vehs.Add((VehicleHash)chk.getElementData<int>("model"));
        }

        output.AvailableVehicles = vehs.ToArray();

        return output;
    }

    public void MapChange(string mapName, XmlGroup map)
    {
        EndRace();

        API.consoleOutput("Parsing map...");
        var race = new Race(parseRace(mapName, map));
        API.consoleOutput("Map parse done! Race null? " + (race == null));

        CurrentRace = race;

        Opponents.ForEach(op =>
        {
            op.HasFinished = false;
            op.CheckpointsPassed = 0;
            op.TimeFinished = "";
            if (!op.Vehicle.IsNull)
            {
                API.deleteEntity(op.Vehicle);
            }

            API.freezePlayer(op.Client, true);
        });

        if (Objects != null)
        {
            foreach (var ent in Objects)
            {
                API.deleteEntity(ent);
            }

            Objects.Clear();
        }
        else
        {
            Objects = new List<NetHandle>();
        }

        var clients = API.getAllPlayers();

        for (int i = 0; i < clients.Count; i++)
        {
            API.freezePlayer(clients[i], false);

            SetUpPlayerForRace(clients[i], CurrentRace, true, i);
        }

        CurrentRaceCheckpoints = race.Checkpoints.ToList();
        RaceStart = DateTime.UtcNow;

        API.consoleOutput("RACE: Starting race " + race.Name);

        RaceStartCountdown = 13;
    }

    private DateTime _lastPositionCalculation;
    private void CalculatePositions()
    {
        if (DateTime.Now.Subtract(_lastPositionCalculation).TotalMilliseconds < 1000)
            return;

        foreach (var opponent in Opponents)
        {
            if (opponent.HasFinished || !opponent.HasStarted)
            {
            }
            else
            {
                var newPos = CalculatePlayerPositionInRace(opponent);
                if (true)
                {
                    opponent.RacePosition = newPos;
                    API.triggerClientEvent(opponent.Client, "updatePosition", newPos, Opponents.Count, opponent.CheckpointsPassed, CurrentRaceCheckpoints.Count);
                }                
            }
        }

        _lastPositionCalculation = DateTime.Now;
    }

    private DateTime _lastScoreboardUpdate;
    private void UpdateScoreboard()
    {
        if (DateTime.Now.Subtract(_lastScoreboardUpdate).TotalMilliseconds < 5000)
            return;

        foreach (var opponent in Opponents)
        {
            if (opponent.HasFinished || !opponent.HasStarted)
            {
                UpdateScoreboardData(opponent, -1);
            }
            else
            {
                UpdateScoreboardData(opponent, opponent.RacePosition);
            }
        }

        _lastScoreboardUpdate = DateTime.Now;
    }

    private void onClientEvent(Client sender, string eventName, params object[] arguments)
    {
        if (eventName == "race_requestRespawn")
        {
            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == sender);
            if (curOp == null || curOp.HasFinished || !curOp.HasStarted || curOp.CheckpointsPassed == 0) return;
            RespawnPlayer(sender, CurrentRace, curOp.CheckpointsPassed - 1);
        }
    }

    public void onUpdate()
    {
        if (DateTime.Now.Subtract(LastSecond).TotalMilliseconds > 1000)
        {
            LastSecond = DateTime.Now;
            
            if (RaceStartCountdown > 0)
            {
                RaceStartCountdown--;

                if (RaceStartCountdown == 3)
                {
                    API.triggerClientEventForAll("startRaceCountdown");
                }
                else if (RaceStartCountdown == 0)
                {
                    IsRaceOngoing = true;

                    lock (Opponents)
                    foreach (var opponent in Opponents)
                        {
                            API.setEntityPositionFrozen(opponent.Client, opponent.Vehicle, false);
                            opponent.HasStarted = true;
                        }

                    RaceTimer = DateTime.Now;
                }
            }
        }
        

        if (!IsRaceOngoing) return;

        CalculatePositions();
        UpdateScoreboard();

        lock (Opponents)
        {
            lock (CurrentRaceCheckpoints)
            foreach (var opponent in Opponents)
                {
                    if (opponent.HasFinished || !opponent.HasStarted) continue;
                    if (CurrentRaceCheckpoints.Any() && opponent.Client.position.IsInRangeOf(CurrentRaceCheckpoints[opponent.CheckpointsPassed], 10f))
                    {
                        opponent.CheckpointsPassed++;
                        if (opponent.CheckpointsPassed >= CurrentRaceCheckpoints.Count)
                        {
                            if (Opponents.All(op => !op.HasFinished))
                            {
                                API.exported.mapcycler.endRoundEx(60000);
                            }

                            opponent.HasFinished = true;
                            var pos = Opponents.Count(o => o.HasFinished);
                            var suffix = pos.ToString().EndsWith("1")
                                ? "st"
                                : pos.ToString().EndsWith("2") ? "nd" : pos.ToString().EndsWith("3") ? "rd" : "th";
                            var timeElapsed = DateTime.Now.Subtract(RaceTimer);
                            API.sendChatMessageToAll("~h~" + opponent.Client.name + "~h~ has finished " + pos + suffix + " (" + timeElapsed.ToString("mm\\:ss\\.fff") + ")");
                            opponent.TimeFinished = timeElapsed.ToString("mm\\:ss\\.fff");
                            API.triggerClientEvent(opponent.Client, "finishRace");
                            continue;
                        }
                        Vector3 nextPos = CurrentRaceCheckpoints[opponent.CheckpointsPassed];
                        Vector3 nextDir = null;


                        if (CurrentRaceCheckpoints.Count > opponent.CheckpointsPassed + 1)
                        {
                            var nextCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed + 1];
                            var curCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed];

                            if (nextCp != null && curCp != null)
                            {
                                Vector3 dir = nextCp.Subtract(curCp);
                                dir.Normalize();
                                nextDir = dir;
                            }
                        }

                        if (nextDir == null)
                        {
                            API.triggerClientEvent(opponent.Client, "setNextCheckpoint", nextPos, true, true);
                        }
                        else
                        {
                            API.triggerClientEvent(opponent.Client, "setNextCheckpoint", nextPos, false, true, nextDir, CurrentRaceCheckpoints[opponent.CheckpointsPassed + 1]);
                        }
                    }
                }
        }
    }


    public void onDisconnect(Client player, string reason)
    {
        Opponent curOp = Opponents.FirstOrDefault(op => op.Client == player);
        if (curOp == null) return;
        API.deleteEntity(curOp.Vehicle);
        lock (Opponents) Opponents.Remove(curOp);
    }

    [Command("forcemap", ACLRequired = true, GreedyArg = true)]
    public void ForceMapCommand(Client sender, string mapFilename)
    {
        if (!API.doesResourceExist(mapFilename) || API.getResourceType(mapFilename) != ResourceType.map)
        {
            API.sendChatMessageToPlayer(sender, "Map was not found!");
            return;
        }

        EndRace();
        API.sendChatMessageToAll("Starting map ~b~" + mapFilename + "!");
        API.sleep(1000);
        API.startResource(mapFilename);
    }

    public void onPlayerConnect(Client player)
    {
        if (IsRaceOngoing)
        {
            SetUpPlayerForRace(player, CurrentRace, false, 0);
        }

        if (ghostmode)
        {
            API.triggerClientEvent(player, "race_toggleGhostMode", true);
        }
    }

    private void EndRace()
    {
        IsRaceOngoing = false;
        CurrentRace = null;

        foreach (var opponent in Opponents)
        {
            opponent.CheckpointsPassed = 0;
            opponent.HasFinished = true;
            opponent.HasStarted = false;
        }

        API.triggerClientEventForAll("resetRace");

        CurrentRaceCheckpoints.Clear();
    }

    private bool ghostmode;
    [Command("ghostmode")]
    public void ghostmodetoggle(Client sender, bool ghost)
    {
        API.triggerClientEventForAll("race_toggleGhostMode", ghost);
        ghostmode = ghost;
        API.sendChatMessageToAll("Ghost mode has been " + (ghost ? "~g~enabled~" : "~r~disabled!"));
    }

    private Random randGen = new Random();
    private void SetUpPlayerForRace(Client client, Race race, bool freeze, int spawnpoint)
    {
        if (race == null) return;

        var selectedModel = unchecked((int)((uint)race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)]));
        var position = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Position;
        var heading = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Heading;

        API.setEntityPosition(client.handle, position);

        Vector3 newDir = null;

        if (race.Checkpoints.Length >= 2)
        {
            Vector3 dir = race.Checkpoints[1].Subtract(race.Checkpoints[0]);
            dir.Normalize();
            newDir = dir;
        }


        var nextPos = race.Checkpoints[0];
        if (newDir == null)
        {
            API.triggerClientEvent(client, "setNextCheckpoint", nextPos, true, false);
        }
        else
        {
            API.triggerClientEvent(client, "setNextCheckpoint", nextPos, false, false, newDir, race.Checkpoints[1]);
        }
        
        var playerVehicle = API.createVehicle((VehicleHash)selectedModel, position, new Vector3(0, 0, heading), randGen.Next(70), randGen.Next(70));
        API.setPlayerIntoVehicle(client, playerVehicle, -1);

        if (freeze)
            API.setEntityPositionFrozen(client, playerVehicle, true);

        Opponent inOp = Opponents.FirstOrDefault(op => op.Client == client);

        lock (Opponents)
        {
            if (inOp != null)
            {
                inOp.Vehicle = playerVehicle;
                inOp.HasStarted = true;
            }
            else
            {
                Opponents.Add(new Opponent(client) { Vehicle = playerVehicle, HasStarted = true });
            }
        }
    }

    private void RespawnPlayer(Client client, Race race, int checkpoint)
    {
        if (race == null) return;

        Opponent inOp = Opponents.FirstOrDefault(op => op.Client == client);

        int selectedModel = 0;
        int color1 = 0;
        int color2 = 0;

        if (inOp != null)
        {
            selectedModel = API.getEntityModel(inOp.Vehicle);
            color1 = API.getVehiclePrimaryColor(inOp.Vehicle);
            color2 = API.getVehicleSecondaryColor(inOp.Vehicle);
        }

        if (selectedModel == 0)
            selectedModel = unchecked((int)((uint)race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)]));
        
            
        var position = CurrentRaceCheckpoints[checkpoint];
        var next = position;

        if (CurrentRaceCheckpoints.Count > checkpoint + 1)
        {
            next = CurrentRaceCheckpoints[checkpoint + 1];
        }
        else
        {
            next = CurrentRaceCheckpoints[checkpoint - 1];
        }

        float heading;

        var direction = next - position;
        direction.Normalize();

        var radAtan = -Math.Atan2(direction.X, direction.Y);

        heading = (float)(radAtan * 180f / Math.PI);

        API.setEntityPosition(client.handle, position);

        Vector3 newDir = null;

        if (CurrentRaceCheckpoints.Count > checkpoint + 2)
        {
            Vector3 dir = CurrentRaceCheckpoints[checkpoint+2].Subtract(CurrentRaceCheckpoints[checkpoint+1]);
            dir.Normalize();
            newDir = dir;
        }


        var nextPos = CurrentRaceCheckpoints[checkpoint+1];
        if (newDir == null)
        {
            API.triggerClientEvent(client, "setNextCheckpoint", nextPos, true, false);
        }
        else
        {
            API.triggerClientEvent(client, "setNextCheckpoint", nextPos, false, false, newDir, CurrentRaceCheckpoints[checkpoint + 2]);
        }


        var playerVehicle = API.createVehicle((VehicleHash)selectedModel, position, new Vector3(0, 0, heading), color1, color2);        
        API.setPlayerIntoVehicle(client, playerVehicle, -1);

        lock (Opponents)
        {
            if (inOp != null)
            {
                API.deleteEntity(inOp.Vehicle);
                inOp.Vehicle = playerVehicle;
                inOp.HasStarted = true;
            }
            else
            {
                Opponents.Add(new Opponent(client) { Vehicle = playerVehicle, HasStarted = true });
            }
        }
    }

    private int CalculatePlayerPositionInRace(Opponent player)
    {
        if (CurrentRace == null) return 0;

        int output = 1;
        int playerCheckpoint = player.CheckpointsPassed;
        int beforeYou = Opponents.Count(tuple => {
            if (tuple == player) return false;
            return tuple.CheckpointsPassed > playerCheckpoint;
        });

        output += beforeYou;
        var samePosAsYou = Opponents.Where(tuple => tuple.CheckpointsPassed == playerCheckpoint && tuple != player);
        output +=
            samePosAsYou.Count(
                tuple =>
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(tuple.Client.position)).Length() <
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(player.Client.position)).Length());
        return output;
    }
}


public static class RangeExtension
{
    public static bool IsInRangeOf(this Vector3 center, Vector3 dest, float radius)
    {
        return center.Subtract(dest).Length() < radius;
    }

    public static Vector3 Subtract(this Vector3 left, Vector3 right)
    {
        if (left == null || right == null)
        {
            return new Vector3(100, 100, 100);
        }

        return new Vector3()
        {
            X = left.X - right.X,
            Y = left.Y - right.Y,
            Z = left.Z - right.Z,
        };
    }

    public static float Length(this Vector3 vect)
    {
        return (float)Math.Sqrt((vect.X * vect.X) + (vect.Y * vect.Y) + (vect.Z * vect.Z));
    }
}


public class Race
{
    public Vector3[] Checkpoints;
    public SpawnPoint[] SpawnPoints;
    public VehicleHash[] AvailableVehicles;
    public bool LapsAvailable = true;
    public Vector3 Trigger;
    public SavedProp[] DecorativeProps;

    public string Filename;

    public string Name;
    public string Description;

    public Race() { }

    public Race(Race copyFrom)
    {
        Checkpoints = copyFrom.Checkpoints;
        SpawnPoints = copyFrom.SpawnPoints;
        AvailableVehicles = copyFrom.AvailableVehicles;
        LapsAvailable = copyFrom.LapsAvailable;
        Trigger = copyFrom.Trigger;
        DecorativeProps = copyFrom.DecorativeProps;

        Name = copyFrom.Name;
        Description = copyFrom.Description;
    }
}

public class SpawnPoint
{
    public Vector3 Position { get; set; }
    public float Heading { get; set; }
}

public class SavedProp
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public int Hash { get; set; }
    public bool Dynamic { get; set; }
}

public class Opponent
{
    public Opponent(Client c)
    {
        Client = c;
        CheckpointsPassed = 0;
    }

    public Client Client { get; set; }
    public int CheckpointsPassed { get; set; }
    public bool HasFinished { get; set; }
    public bool HasStarted { get; set; }
    public NetHandle Vehicle { get; set; }
    public NetHandle Blip { get; set; }
    public int RacePosition { get; set; }
    public string TimeFinished { get; set; }
}