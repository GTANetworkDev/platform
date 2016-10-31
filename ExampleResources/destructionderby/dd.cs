using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;



public class DDGamemode : Script
{
    public DDGamemode()
    {
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        Objects = new List<NetHandle>();
        LoadRaces();

        API.consoleOutput("Destruction Derby gamemode started! Loaded " + AvailableRaces.Count + " races.");

        StartVote();

        API.onUpdate += onUpdate;
        API.onPlayerDisconnected += onDisconnect;
        API.onChatCommand += onChatCommand;
        API.onPlayerFinishedDownload += onPlayerConnect;
        API.onPlayerRespawn += onPlayerRespawn;
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

    public bool IsVoteActive()
    {
        return DateTime.Now.Subtract(VoteStart).TotalSeconds < 60;
    }

    public void onPlayerRespawn(Client player)
    {
        if (IsRaceStarting)
        {
            SetUpPlayerForRace(player, CurrentRace, false, 0);
        }
        else if (IsRaceOngoing)
        {            
            API.setPlayerToSpectator(player);
            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == player);
            if (curOp == null) return;
            curOp.IsAlive = false;
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
                    API.sendChatMessageToAll("Race ~b~" + raceWon.name + "~w~ has won the vote!");

                    API.sleep(1000);
                    StartRace(raceWon);
                }
            }
        }

        if (!IsRaceOngoing) return;

        lock (Opponents)
        {
            var count = 0;
            foreach(var playa in Opponents)
            {                
                if (playa.Client.Position.Z <= 0 || playa.Client.Position.Z <= CurrentRace.Checkpoints[0].Z)
                {
                    API.setPlayerHealth(playa.Client, -1);
                    playa.IsAlive = false;
                }

                if (playa.IsAlive) count++;
            }

            if (count <= 1)
            {
                StartVote();
                var winner = Opponents.FirstOrDefault(op => op.IsAlive);
                if (winner != null)
                {
                    API.sendChatMessageToAll("The winner is ~b~" + winner.Client.name + "~w~!");
                }
                else API.sendChatMessageToAll("There are no winners!");                

                IsRaceOngoing = false;
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

    public void onChatCommand(Client sender, string message)
    {
        if (message == "/votemap" && !IsVoteActive() && (!IsRaceStarting || DateTime.UtcNow.Subtract(RaceStart).TotalSeconds > 60))
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
            API.sendChatMessageToPlayer(sender, "You have voted for " + AvailableChoices[choice].name);
            Voters.Add(sender);
            return;
        }
    }

    public void onPlayerConnect(Client player)
    {
        if (IsRaceStarting)
        {
            SetUpPlayerForRace(player, CurrentRace, true, 0);
        }
        else if (IsRaceOngoing)
        {
            API.setPlayerToSpectator(player);
        }

        if (DateTime.Now.Subtract(VoteStart).TotalSeconds < 60)
        {
            object[] argumentList = new object[11];

            argumentList[0] = AvailableChoices.Count;
            for (var i = 0; i < AvailableChoices.Count; i++)
            {
                argumentList[i+1] = AvailableChoices.ElementAt(i).Value.name;
            }

            API.triggerClientEvent(player, "race_startVotemap", argumentList);
        }
    }

    private int LoadRaces()
    {
        int counter = 0;
        if (!Directory.Exists("dd-races")) return 0;
        foreach (string path in Directory.GetFiles("dd-races", "*.xml"))
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

        API.consoleOutput("RACE: Starting race " + race.name);

        RaceStartCountdown = 13;
    }

    private void EndRace()
    {
        IsRaceStarting = false;
        IsRaceOngoing = false;
        CurrentRace = null;

        foreach (var opponent in Opponents)
        {
            opponent.IsAlive = false;
        }

        API.triggerClientEventForAll("resetRace");
    }

    private Random randGen = new Random();
    private void SetUpPlayerForRace(Client client, Race race, bool freeze, int spawnpoint)
    {
        if (race == null) return;

        var selectedModel = unchecked((int)((uint)race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)]));
        var position = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Position;
        var heading = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Heading;

        API.setEntityPosition(client.handle, position);

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
                inOp.IsAlive = true;
            }
            else
            {
                Opponents.Add(new Opponent(client) { Vehicle = playerVehicle, IsAlive = true });
            }
        }
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
            argumentList[i+1] = AvailableChoices.ElementAt(i).Value.name;
        }

        API.triggerClientEventForAll("race_startVotemap", argumentList);

        VoteStart = DateTime.Now;
        VoteEnd = 60;
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

        Name = copyFrom.name;
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
    }

    public bool IsAlive { get; set; }    
    public Client Client { get; set; }    
    public NetHandle Vehicle { get; set; }
}