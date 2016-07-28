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
    }

    public bool IsRaceOngoing { get; set; }
    public List<Opponent> Opponents { get; set; }
    public Race CurrentRace { get; set; }
    public List<Race> AvailableRaces { get; set; }
    public List<Vector3> CurrentRaceCheckpoints { get; set; }
    public Dictionary<long, int> RememberedBlips { get; set; }
    public DateTime RaceStart { get; set; }
    public List<NetHandle> Objects { get; set; }
    public List<Thread> ActiveThreads { get; set; }    
    public int RaceStartCountdown { get; set; }
    public DateTime RaceTimer { get; set; }

    // Voting
    public int TimeLeft { get; set; }
    public int VoteEnd { get; set; }
    public DateTime LastSecond { get; set; }
    public DateTime VoteStart { get; set; }
    public List<Client> Voters { get; set; }
    public Dictionary<int, int> Votes { get; set; }
    public Dictionary<int, Race> AvailableChoices { get; set; }

    public void onResourceStart(object sender, EventArgs e)
    {
        TimeLeft = -1;
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        RememberedBlips = new Dictionary<long, int>();
        CurrentRaceCheckpoints = new List<Vector3>();
        Objects = new List<NetHandle>();
        ActiveThreads = new List<Thread>();
        LoadRaces();

        API.consoleOutput("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");

        StartVote();
    }

    public bool IsVoteActive()
    {
        return DateTime.Now.Subtract(VoteStart).TotalSeconds < 60;
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

    public void onResourceStop(object sender, EventArgs e)
    {
        API.triggerClientEventForAll("resetRace");
        
        Opponents.ForEach(op =>
        {
            op.HasFinished = false;
            op.CheckpointsPassed = 0;
            if (!op.Vehicle.IsNull)
            {
                API.deleteEntity(op.Vehicle);
            }
        });

        foreach (var ent in Objects)
        {
            API.deleteEntity(ent);
        }

        foreach (var t in ActiveThreads)
        {
            if (!t.IsAlive) continue;
            t.Abort();
        }
    }

    private DateTime _lastPositionCalculation;
    private void CalculatePositions()
    {
        if (DateTime.Now.Subtract(_lastPositionCalculation).TotalMilliseconds < 1000)
            return;

        foreach (var opponent in Opponents)
        {
            if (opponent.HasFinished || !opponent.HasStarted) continue;
            var newPos = CalculatePlayerPositionInRace(opponent);
            if (true)
            {
                opponent.RacePosition = newPos;
                API.triggerClientEvent(opponent.Client, "updatePosition", newPos, Opponents.Count, opponent.CheckpointsPassed, CurrentRaceCheckpoints.Count);
            }
        }

        _lastPositionCalculation = DateTime.Now;
    }

    private void onClientEvent(Client sender, string eventName, params object[] arguments)
    {
        if (eventName == "race_castVote" && IsVoteActive() && !Voters.Contains(sender))
        {
            var voteCast = (int)arguments[0];
            Votes[voteCast]++;
            Voters.Add(sender);            
        }
        else if (eventName == "race_requestRespawn")
        {
            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == sender);
            if (curOp == null || curOp.HasFinished || !curOp.HasStarted || curOp.CheckpointsPassed == 0) return;
            RespawnPlayer(sender, CurrentRace, curOp.CheckpointsPassed - 1);
        }
    }

    public void onUpdate(object sender, EventArgs e)
    {
        if (DateTime.Now.Subtract(LastSecond).TotalMilliseconds > 1000)
        {
            LastSecond = DateTime.Now;
            if (TimeLeft > 0)
            {
                TimeLeft--;

                if (TimeLeft == 0)
                {
                    if (!IsVoteActive())
                        StartVote();
                }
                else if (TimeLeft == 30)
                {
                    API.sendChatMessageToAll("Vote for next map will start in 30 seconds!");
                }
                else if (TimeLeft == 59)
                {
                    API.sendChatMessageToAll("Vote for next map will start in 60 seconds!");
                }
            }

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

            if (VoteEnd > 0)
            {
                VoteEnd--;
                if (VoteEnd == 0)
                {
                    EndRace();
                    var raceWon = AvailableChoices[Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key];
                    API.sendChatMessageToAll("Race ~b~" + raceWon.Name + "~w~ has won the vote!");

                    API.sleep(1000);
                    StartRace(raceWon);
                }
            }
        }
        

        if (!IsRaceOngoing) return;

        CalculatePositions();

        lock (Opponents)
        {
            lock (CurrentRaceCheckpoints)
            foreach (var opponent in Opponents)
                {
                    if (opponent.HasFinished || !opponent.HasStarted) continue;
                    if (CurrentRaceCheckpoints.Any() && opponent.Client.Position.IsInRangeOf(CurrentRaceCheckpoints[opponent.CheckpointsPassed], 10f))
                    {
                        opponent.CheckpointsPassed++;
                        if (opponent.CheckpointsPassed >= CurrentRaceCheckpoints.Count)
                        {
                            if (Opponents.All(op => !op.HasFinished))
                            {
                                TimeLeft = 60;
                            }

                            opponent.HasFinished = true;
                            var pos = Opponents.Count(o => o.HasFinished);
                            var suffix = pos.ToString().EndsWith("1")
                                ? "st"
                                : pos.ToString().EndsWith("2") ? "nd" : pos.ToString().EndsWith("3") ? "rd" : "th";
                            var timeElapsed = DateTime.Now.Subtract(RaceTimer);
                            API.sendChatMessageToAll("~h~" + opponent.Client.Name + "~h~ has finished " + pos + suffix + " (" + timeElapsed.ToString("mm\\:ss\\.fff") + ")");
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

    [Command("votemap")]
    public void StartVotemapCommand(Client sender)
    {
        if (!IsVoteActive() && (!IsRaceOngoing || DateTime.UtcNow.Subtract(RaceStart).TotalSeconds > 60))
        {
            StartVote();
        }
        else
        {
            API.sendChatMessageToPlayer(sender, "A vote is already in progress, or the race has just started!");
        }
    }

    [Command("forcemap", ACLRequired = true)]
    public void ForceMapCommand(Client sender, string mapFilename)
    {
        var locatedMap = AvailableRaces.FirstOrDefault(m => m.Filename == mapFilename);
        if (locatedMap == null)
        {
            API.sendChatMessageToPlayer(sender, "~r~ERROR:~w~ No map found: " + mapFilename + "!");
            return;
        }

        VoteEnd = 0;
        EndRace();
        API.sendChatMessageToAll("Starting map ~b~" + locatedMap.Name + "!");
        API.sleep(1000);
        StartRace(locatedMap);
    }

    public void onPlayerConnect(Client player)
    {
        if (IsRaceOngoing)
        {
            SetUpPlayerForRace(player, CurrentRace, false, 0);
        }

        if (DateTime.Now.Subtract(VoteStart).TotalSeconds < 60)
        {
            object[] argumentList = new object[11];

            argumentList[0] = AvailableChoices.Count;
            for (var i = 0; i < AvailableChoices.Count; i++)
            {
                argumentList[i+1] = AvailableChoices.ElementAt(i).Value.Name;
            }

            API.triggerClientEvent(player, "race_startVotemap", argumentList);
        }
    }

    private int LoadRaces()
    {
        int counter = 0;
        if (!Directory.Exists("races")) return 0;
        foreach (string path in Directory.GetFiles("races", "*.xml"))
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Race));
            StreamReader file = new StreamReader(path);
            var raceout = (Race)serializer.Deserialize(file);
            file.Close();
            raceout.Filename = Path.GetFileName(path);
            AvailableRaces.Add(raceout);
            counter++;
        }
        return counter;
    }


    private void StartRace(Race race)
    {
        race = new Race(race);

        CurrentRace = race;

        Opponents.ForEach(op =>
        {
            op.HasFinished = false;
            op.CheckpointsPassed = 0;
            if (!op.Vehicle.IsNull)
            {
                API.deleteEntity(op.Vehicle);
            }
        });

        foreach (var ent in Objects)
        {
            API.deleteEntity(ent);
        }

        Objects.Clear();

        foreach (var prop in race.DecorativeProps)
        {
            Objects.Add(API.createObject(prop.Hash, prop.Position, prop.Rotation));
        }

        var clients = API.getAllPlayers();

        for (int i = 0; i < clients.Count; i++)
        {
            SetUpPlayerForRace(clients[i], CurrentRace, true, i);
        }

        CurrentRaceCheckpoints = race.Checkpoints.ToList();
        RaceStart = DateTime.UtcNow;

        API.consoleOutput("RACE: Starting race " + race.Name);

        RaceStartCountdown = 13;
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

    private Random randGen = new Random();
    private void SetUpPlayerForRace(Client client, Race race, bool freeze, int spawnpoint)
    {
        if (race == null) return;

        var selectedModel = unchecked((int)((uint)race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)]));
        var position = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Position;
        var heading = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Heading;

        API.setEntityPosition(client.CharacterHandle, position);

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

        API.setEntityPosition(client.CharacterHandle, position);

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
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(tuple.Client.Position)).Length() <
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(player.Client.Position)).Length());
        return output;
    }

    public void StartVote()
    {
        var pickedRaces = new List<Race>();
        var racePool = new List<Race>(AvailableRaces);
        var rand = new Random();

        for (int i = 0; i < Math.Min(9, AvailableRaces.Count); i++)
        {
            var pick = rand.Next(racePool.Count);
            pickedRaces.Add(racePool[pick]);
            racePool.RemoveAt(pick);
        }

        Votes = new Dictionary<int, int>();
        Voters = new List<Client>();
        AvailableChoices = new Dictionary<int, Race>();

        var counter = 1;
        foreach (var race in pickedRaces)
        {
            Votes.Add(counter, 0);
            AvailableChoices.Add(counter, race);
            counter++;
        }

        object[] argumentList = new object[11];

        argumentList[0] = AvailableChoices.Count;
        for (var i = 0; i < AvailableChoices.Count; i++)
        {
            argumentList[i+1] = AvailableChoices.ElementAt(i).Value.Name;
        }

        API.triggerClientEventForAll("race_startVotemap", argumentList);

        VoteStart = DateTime.Now;
        VoteEnd = 60;
    }

    private string GetVoteHelpString()
    {
        if (DateTime.Now.Subtract(VoteStart).TotalSeconds > 60)
            return null;

        var build = new StringBuilder();
        build.Append("Type /vote [id] to vote for the next race! The options are:");

        foreach (var race in AvailableChoices)
        {
            build.Append("\n" + race.Key + ": " + race.Value.Name);
        }

        return build.ToString();
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
}