using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
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
        API.onChatCommand += onChatCommand;
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

    // Voting
    public DateTime VoteStart { get; set; }
    public List<Client> Voters { get; set; }
    public Dictionary<int, int> Votes { get; set; }
    public Dictionary<int, Race> AvailableChoices { get; set; }

    public void onResourceStart(object sender, EventArgs e)
    {
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        RememberedBlips = new Dictionary<long, int>();
        CurrentRaceCheckpoints = new List<Vector3>();
        Objects = new List<NetHandle>();
        ActiveThreads = new List<Thread>();
        LoadRaces();

        API.consoleOutput("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");

        StartVote();

        API.startThread(CalculatePositions);
    }

    public bool IsVoteActive()
    {
        return DateTime.Now.Subtract(VoteStart).TotalSeconds < 60;
    }

    public void onPlayerRespawn(Client player)
    {
        if (IsRaceOngoing)
        {
            SetUpPlayerForRace(player, CurrentRace, false, 0);
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

    private void CalculatePositions()
    {
        if (!IsRaceOngoing)
        {
            goto end;
        }

        foreach (var opponent in Opponents)
        {
            if (opponent.HasFinished || !opponent.HasStarted) continue;
            var newPos = CalculatePlayerPositionInRace(opponent);
            if (newPos != opponent.RacePosition)
            {
                opponent.RacePosition = newPos;
                API.triggerClientEvent(opponent.Client, "updatePosition", newPos, Opponents.Count);
            }
        }

        end:
        Thread.Sleep(1000);
        CalculatePositions();
    }

    private void onClientEvent(Client sender, string eventName, params object[] arguments)
    {
        if (eventName == "race_castVote" && IsVoteActive() && !Voters.Contains(sender))
        {
            var voteCast = (int)arguments[0];
            Votes[voteCast]++;
            Voters.Add(sender);            
        }
    }

    public void onUpdate(object sender, EventArgs e)
    {
        if (!IsRaceOngoing) return;

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
                                API.startThread((ThreadStart)delegate
                                {
                                    API.sleep(10000);
                                    API.sendChatMessageToAll("Vote for next map will start in 60 seconds!");
                                    API.sleep(30000);
                                    API.sendChatMessageToAll("Vote for next map will start in 30 seconds!");
                                    API.sleep(30000);
                                    if (!IsVoteActive())
                                        StartVote();
                                });
                            }

                            opponent.HasFinished = true;
                            var pos = Opponents.Count(o => o.HasFinished);
                            var suffix = pos.ToString().EndsWith("1")
                                ? "st"
                                : pos.ToString().EndsWith("2") ? "nd" : pos.ToString().EndsWith("3") ? "rd" : "th";
                            API.sendChatMessageToAll("~h~" + opponent.Client.Name + "~h~ has finished " + pos + suffix);
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
                                dir = dir.Normalize();
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

    public void onChatCommand(Client sender, string message, CancelEventArgs e)
    {
        if (message == "/votemap" && !IsVoteActive() && (!IsRaceOngoing || DateTime.UtcNow.Subtract(RaceStart).TotalSeconds > 60))
        {
            StartVote();
            return;
        }
        else if (message.StartsWith("/vote"))
        {
            if (DateTime.Now.Subtract(VoteStart).TotalSeconds > 60)
            {
                API.sendChatMessageToPlayer(sender, "No current vote is in progress.");
                return;
            }

            var args = message.Split();

            if (args.Length <= 1)
            {
                API.sendChatMessageToPlayer(sender, "USAGE", "/vote [id]");
                return;
            }

            if (Voters.Contains(sender))
            {
                API.sendChatMessageToPlayer(sender, "ERROR", "You have already voted!");
                return;
            }

            int choice;
            if (!int.TryParse(args[1], out choice) || choice <= 0 || choice > AvailableChoices.Count)
            {
                API.sendChatMessageToPlayer(sender, "USAGE", "/vote [id]");
                return;
            }

            Votes[choice]++;
            API.sendChatMessageToPlayer(sender, "You have voted for " + AvailableChoices[choice].Name);
            Voters.Add(sender);
            return;
        }
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

        API.startThread((ThreadStart)delegate
        {
            API.sleep(10000);
            API.triggerClientEventForAll("startRaceCountdown");
            API.sleep(3000);
            IsRaceOngoing = true;

            var nat = 0x428CA6DBD1094446;

            lock (Opponents)
            foreach (var opponent in Opponents)
                {
                    API.setEntityPositionFrozen(opponent.Client, opponent.Vehicle, false);
                    opponent.HasStarted = true;
                }
        });
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
            dir = dir.Normalize();
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


        var playerVehicle = API.createVehicle(selectedModel, position, new Vector3(0, 0, heading), 0, 0);
        Thread.Sleep(500);
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

    private int CalculatePlayerPositionInRace(Opponent player)
    {
        int output = 1;
        int playerCheckpoint = CurrentRace.Checkpoints.Length - player.CheckpointsPassed;
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
        
        API.startThread((ThreadStart)delegate
        {
            API.sleep(60000);
            EndRace();
            var raceWon = AvailableChoices[Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key];
            API.sendChatMessageToAll("Race ~b~" + raceWon.Name + "~w~ has won the vote!");

            API.sleep(1000);
            StartRace(raceWon);
        });
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

    public static Vector3 Normalize(this Vector3 vect)
    {
        float length = vect.Length();
        if (length == 0) return vect;

        float num = 1 / length;

        return new Vector3()
        {
            X = vect.X * num,
            Y = vect.Y * num,
            Z = vect.Z * num,
        };
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