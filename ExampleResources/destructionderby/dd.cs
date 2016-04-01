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



public class DDGamemode : Script
{
    public DDGamemode()
    {
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        Objects = new List<NetHandle>();
        LoadRaces();

        API.consoleOutput("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");

        StartVote();

        API.onUpdate += onUpdate;
        API.onPlayerDisconnected += onDisconnect;
        API.onChatCommand += onChatCommand;
        API.onPlayerFinishedDownload += onPlayerConnect;
        API.onPlayerRespawn += onPlayerRespawn;
    }

    public bool IsRaceStarting { get; set; }
    public bool IsRaceOngoing { get; set; }
    public List<Opponent> Opponents { get; set; }
    public Race CurrentRace { get; set; }
    public List<Race> AvailableRaces { get; set; }
    public DateTime RaceStart { get; set; }
    public List<NetHandle> Objects { get; set; }

    // Voting
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
                    API.sendChatMessageToAll("The winner is ~b~" + winner.Client.Name + "~w~!");
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

    public void onChatCommand(Client sender, string message, CancelEventArgs e)
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
            API.sendChatMessageToPlayer(sender, "You have voted for " + AvailableChoices[choice].Name);
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
            API.sendNotificationToPlayer(player, GetVoteHelpString());
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
            if (!op.Vehicle.IsNull)
            {
                API.deleteEntity(op.Vehicle);
            }

            API.unspectatePlayer(op.Client);
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

        RaceStart = DateTime.UtcNow;

        API.consoleOutput("RACE: Starting race " + race.Name);
        IsRaceStarting = true;

        var t = new Thread((ThreadStart)delegate
        {
            Thread.Sleep(10000);
            IsRaceStarting = false;
            API.triggerClientEventForAll("startRaceCountdown");
            Thread.Sleep(3000);
            IsRaceOngoing = true;

            var nat = 0x428CA6DBD1094446;

            lock (Opponents)
            foreach (var opponent in Opponents)
                {
                    API.setEntityPositionFrozen(opponent.Client, opponent.Vehicle, false);
                }
        });
        t.IsBackground = true;
        t.Start();
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

        API.setEntityPosition(client.CharacterHandle, position);

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

        var build = new StringBuilder();
        build.Append("Type /vote [id] to vote for the next race! The options are:");

        var counter = 1;
        foreach (var race in pickedRaces)
        {
            build.Append("\n" + counter + ": " + race.Name);
            Votes.Add(counter, 0);
            AvailableChoices.Add(counter, race);
            counter++;
        }

        VoteStart = DateTime.Now;
        API.sendNotificationToAll(build.ToString());

        var t = new Thread((ThreadStart)delegate
        {
            Thread.Sleep(60 * 1000);
            EndRace();
            var raceWon = AvailableChoices[Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key];
            API.sendNotificationToAll(raceWon.Name + " has won the vote!");

            Thread.Sleep(1000);
            StartRace(raceWon);
        });
        t.IsBackground = true;
        t.Start();
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
    }

    public bool IsAlive { get; set; }    
    public Client Client { get; set; }    
    public NetHandle Vehicle { get; set; }
}