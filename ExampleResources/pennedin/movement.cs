using System;
using GTANetworkShared;
using GTANetworkServer;
using System.Collections.Generic;

public class Movement
{
	public Vector3 Vector { get; set; }
	public float Range { get; set; }
	public bool Positional { get; set; }
	public int Interval { get; set; }

	public Movement()
	{}

	public Movement(Vector3 vec, int interval)
	{
		Vector = vec;
		Positional = true;
		Interval = interval;
	}

	public Movement(float vec, int interval)
	{
		Range = vec;
		Positional = false;
		Interval = interval;
	}
}

public static class MovementMap
{
	public static List<Movement> Map = new List<Movement>
	{
		new Movement(new Vector3(1774.61f, 3278.92f, 41.09f), 30000),
		new Movement(new Vector3(2013.41, 3096.5f, 46.46f), 30000),
		new Movement(50f, 10000),
		new Movement(new Vector3(2232.4f, 3010.41f, 44.58f), 30000),
		new Movement(new Vector3(2327.84f, 2854.98f, 40.87f), 30000),
		new Movement(30f, 10000),
		new Movement(new Vector3(2182.9f, 2610.95f, 51.86f), 30000),
		new Movement(new Vector3(2195.52f, 2496.61f, 87.22f), 10000),
		new Movement(new Vector3(2187.38f, 2446.21f, 88.45f), 5000),
		new Movement(10f, 10000),
		new Movement(new Vector3(2156.34f, 2388.34f, 105.44f), 10000),
		new Movement(new Vector3(2162.12f, 2116.63f, 126.02f), 30000),
		new Movement(5f, 10000),
		new Movement(new Vector3(2213.24f, 2054.15f, 132.43f), 10000),
		new Movement(new Vector3(2266.84f, 1841.13f, 108.67f), 10000),
	};

}