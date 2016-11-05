using System;
using System.Linq;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

public class Round
{
	public Round()
	{
		Objectives = new List<Objective>();
		Spawnpoints = new List<Spawnpoint>();
        Cleanup = new List<Entity>();
	}

	public List<Objective> Objectives { get; set; }
	public List<Spawnpoint> Spawnpoints { get; set; }

    public List<Entity> Cleanup { get; set; }
}

public class Objective
{
	public Objective()
	{
		Range = 10f;
	    Timer = 10000;
		RequiredObjectives = new List<int>();
	}

	public Vector3 Position { get; set; }
	public float Range { get; set; }
	public string name { get; set; }
	public int Id { get; set; }
	public List<int> RequiredObjectives { get; set; }
    public int Timer { get; set; }

    public long LastLabelUpdate { get; set; }
    public bool Spawned { get; set; }
    public int TimeLeft { get; set; }
    public long LastActiveUpdate { get; set; }
    public bool Active { get; set; }
	public bool Captured { get; set; }
    public Blip Blip;
    public Marker Marker;
    public TextLabel TextLabel;
}

public class Spawnpoint
{
    public Spawnpoint()
    {
        RequiredObjectives = new int[0];
    }

	public int Team { get; set; }
	public Vector3 Position { get; set; }
	public float Heading { get; set; }

    public int[] RequiredObjectives { get; set; }

	public PedHash[] Skins { get; set; }
	public WeaponHash[] Weapons { get; set; }
	public int[] Ammo { get; set; }
}