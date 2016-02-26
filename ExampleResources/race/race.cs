using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Text;
using GTAServer;
using MultiTheftAutoShared;
using System.Threading;



 public class RaceGamemode : Script
 {
    public RaceGamemode()
    {
        AvailableRaces = new List<Race>();
        Opponents = new List<Opponent>();
        RememberedBlips = new Dictionary<long, int>();
        CurrentRaceCheckpoints = new List<Vector3>();
        LoadRaces();

        Console.WriteLine("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");
        
        StartVote();
        
        API.onUpdate += onUpdate;
		API.onPlayerDisconnected += onDisconnect;
		API.onChatCommand += onChatCommand;
		API.onPlayerConnected += onPlayerConnect;
        API.onPlayerRespawn += onPlayerRespawn;
	}

	public bool IsRaceOngoing { get; set; }
	public List<Opponent> Opponents { get; set; }
	public Race CurrentRace { get; set; }
	public List<Race> AvailableRaces { get; set; }
	public List<Vector3> CurrentRaceCheckpoints { get; set; }
	public Dictionary<long, int> RememberedBlips { get; set; }
	public DateTime RaceStart { get; set; }


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
        if (IsRaceOngoing)
		{
			SetUpPlayerForRace(player, CurrentRace, false, 0);
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
								var t = new Thread((ThreadStart)delegate
							   {
								   Thread.Sleep(10000);
								   API.sendChatMessageToAll("Vote for next map will start in 60 seconds!");
								   Thread.Sleep(30000);
								   API.sendChatMessageToAll("Vote for next map will start in 30 seconds!");
								   Thread.Sleep(30000);
								   if (!IsVoteActive())
									   StartVote();
							   });
								t.Start();
							}

							opponent.HasFinished = true;
							var pos = Opponents.Count(o => o.HasFinished);
							var suffix = pos.ToString().EndsWith("1")
								? "st"
								: pos.ToString().EndsWith("2") ? "nd" : pos.ToString().EndsWith("3") ? "rd" : "th";
							API.sendNotificationToAll("~h~" + opponent.Client.Name + "~h~ has finished " + pos + suffix);

							Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0x45FF974EEE1C8734, opponent.Blip, 0);
							Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER");
							Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER_DIR");
							continue;
						}

						Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0xAE2AF67E9D9AF65D, opponent.Blip,
							CurrentRaceCheckpoints[opponent.CheckpointsPassed].X,
							CurrentRaceCheckpoints[opponent.CheckpointsPassed].Y,
							CurrentRaceCheckpoints[opponent.CheckpointsPassed].Z);

						Program.ServerInstance.SetNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER",
						0x28477EC23D892089, 1, CurrentRaceCheckpoints[opponent.CheckpointsPassed], new Vector3(), new Vector3(),
						new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false,
						false, false);

						if (CurrentRaceCheckpoints.Count > opponent.CheckpointsPassed + 1)
						{
							var nextCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed + 1];
							var curCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed];

							if (nextCp != null && curCp != null)
							{
								Vector3 dir = nextCp.Subtract(curCp);
								dir = dir.Normalize();

								Program.ServerInstance.SetNativeCallOnTickForPlayer(opponent.Client,
									"RACE_CHECKPOINT_MARKER_DIR",
									0x28477EC23D892089, 20, curCp.Subtract(new Vector3() { X = 0f, Y = 0f, Z = -2f }), dir,
									new Vector3() { X = 60f, Y = 0f, Z = 0f },
									new Vector3() { X = 4f, Y = 4f, Z = 4f }, 87, 193, 250, 200, false, false, 2, false,
									false,
									false, false);
							}
						}
						else
						{
							Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER_DIR");
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
		Program.ServerInstance.SetNativeCallOnTickForPlayer(player, "RACE_DISABLE_VEHICLE_EXIT", 0xFE99B66D079CF6BC, 0, 75, true);		

		if (IsRaceOngoing)
		{
			SetUpPlayerForRace(player, CurrentRace, false, 0);
		}

		if (DateTime.Now.Subtract(VoteStart).TotalSeconds < 60)
		{
			API.sendNotificationToPlayer(player, GetVoteHelpString());
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
		//Game.FadeScreenOut(500);

		CurrentRace = race;

		/*if (_raceSettings["Laps"] > 1)
        {
            _totalLaps = race.Checkpoints.Length;
            List<Vector3> tmpCheckpoints = new List<Vector3>();
            for (int i = 0; i < _raceSettings["Laps"]; i++)
            {
                tmpCheckpoints.AddRange(race.Checkpoints);
            }
            _currentRace.Checkpoints = tmpCheckpoints.ToArray();
        }*/

		Opponents.ForEach(op =>
		{
			op.HasFinished = false;
			op.CheckpointsPassed = 0;
			if (op.Vehicle != 0)
			{
				API.deleteEntity(op.Vehicle);
			}
		});


		var clients = API.getAllPlayers();

		for (int i = 0; i < clients.Count; i++)
		{
			SetUpPlayerForRace(clients[i], CurrentRace, true, i);			
		}

		CurrentRaceCheckpoints = race.Checkpoints.ToList();
		RaceStart = DateTime.UtcNow;

		Console.WriteLine("RACE: Starting race " + race.Name);

		var t = new Thread((ThreadStart)delegate
	   {
		   Thread.Sleep(10000);
                      
		   API.triggerClientEventForAll("startCountdown");
			Thread.Sleep(3000);
		   IsRaceOngoing = true;
		
		var nat = 0x428CA6DBD1094446;

		   lock (Opponents)
		   foreach (var opponent in Opponents)
			   {
			   
				   API.sendNativeToPlayer(opponent.Client, nat.ToString(), new EntityArgument(opponent.Vehicle), false);
				   opponent.HasStarted = true;
			   }
	   });
		t.Start();
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

			if (opponent.Blip != 0)
			{
				Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0x45FF974EEE1C8734, opponent.Blip, 0);
			}
		}

		Program.ServerInstance.RecallNativeCallOnTickForAllPlayers("RACE_CHECKPOINT_MARKER");
		Program.ServerInstance.RecallNativeCallOnTickForAllPlayers("RACE_CHECKPOINT_MARKER_DIR");

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

		if (race.Checkpoints.Length >= 2)
		{
			Vector3 dir = race.Checkpoints[1].Subtract(race.Checkpoints[0]);
			dir = dir.Normalize();

			Program.ServerInstance.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER_DIR",
			0x28477EC23D892089, 20, race.Checkpoints[0].Subtract(new Vector3() { X = 0f, Y = 0f, Z = -2f }), dir, new Vector3() { X = 60f, Y = 0f, Z = 0f },
			new Vector3() { X = 4f, Y = 4f, Z = 4f }, 87, 193, 250, 200, false, false, 2, false, false,
			false, false);
		}


		Program.ServerInstance.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER",
			0x28477EC23D892089, 1, race.Checkpoints[0], new Vector3(), new Vector3(),
			new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false,
			false, false);

		var playerVehicle = API.createVehicle(selectedModel, position, new Vector3(0, 0, heading), 0, 0);
		Thread.Sleep(500);
		API.setPlayerIntoVehicle(client, playerVehicle, -1);		
		
		if (freeze)
		Program.ServerInstance.SendNativeCallToPlayer(client, 0x428CA6DBD1094446, new EntityArgument(playerVehicle), true);
		
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
				Opponents.Add(new Opponent(client) { Vehicle = (int)playerVehicle, HasStarted = true });
			}
		}
		
		Opponent curOp = Opponents.FirstOrDefault(op => op.Client == client);
		if (curOp == null || curOp.Blip == 0)
		{
			Program.ServerInstance.GetNativeCallFromPlayer(client, "start_blip", 0x5A039BB0BCA604B6,
				new IntArgument(), // ADD_BLIP_FOR_COORD
				delegate (object o)
				{
					lock (Opponents)
					{
						Opponent secOp = Opponents.FirstOrDefault(op => op.Client == client);

						if (secOp != null)
						{
							secOp.Blip = (int)o;
						}
						else
							Opponents.Add(new Opponent(client) { Blip = (int)o });
					}
				}, race.Checkpoints[0].X, race.Checkpoints[0].Y, race.Checkpoints[0].Z);
		}
		else
		{
			Program.ServerInstance.SendNativeCallToPlayer(client, 0x45FF974EEE1C8734, curOp.Blip, 255);
			Program.ServerInstance.SendNativeCallToPlayer(client, 0xAE2AF67E9D9AF65D, curOp.Blip, race.Checkpoints[0].X, race.Checkpoints[0].Y, race.Checkpoints[0].Z);
		}
	}

	private int CalculatePlayerPositionInRace(Client player)
    {
		Opponent curOp = Opponents.FirstOrDefault(op => op.Client == player);
		if (curOp == null) return 0;
        int output = 1;
        int playerCheckpoint = CurrentRace.Checkpoints.Length - curOp.CheckpointsPassed;
        int beforeYou = Opponents.Count(tuple => {
			if (tuple == curOp) return false;
			return tuple.CheckpointsPassed > playerCheckpoint;
		});
		
        output += beforeYou;
        var samePosAsYou = Opponents.Where(tuple => tuple.CheckpointsPassed == playerCheckpoint && tuple != curOp);
        output +=
            samePosAsYou.Count(
                tuple =>
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(tuple.Client.Position)).Length() <
                    (CurrentRace.Checkpoints[playerCheckpoint].Subtract(curOp.Client.Position)).Length());
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

		Name = copyFrom.Name;
		Description = copyFrom.Description;
	}
}

public class SpawnPoint
{
	public Vector3 Position { get; set; }
	public float Heading { get; set; }
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
	public int Vehicle { get; set; }
	public int Blip { get; set; }
}