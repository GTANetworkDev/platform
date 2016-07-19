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
		new Movement(new Vector3(1777.592f, 3260.052f, 40.86165f), 30000),
		new Movement(new Vector3(1993.448f, 3113.31f, 46.09095f),  30000),
		new Movement(new Vector3(2178.93f, 2946.182f, 45.50336f),  30000),
		new Movement(50, 10000),
		new Movement(new Vector3(2076.971f, 2702.37f, 55.06715f),  30000),
		new Movement(new Vector3(1942.375f, 2507.483f, 53.80276f), 30000),
		new Movement(new Vector3(1781.51f, 2180.196f, 61.60954f),  30000),		
		new Movement(new Vector3(1737.766f, 2082.719f, 62.46634f), 30000),
		new Movement(new Vector3(1679.457f, 2052.939f, 73.94624f), 30000),
		new Movement(25, 10000),
		new Movement(new Vector3(1622.88f, 1949.191f, 87.79281f),  20000),
		new Movement(new Vector3(1560.336f, 1885.141f, 96.12678f), 20000),
		new Movement(new Vector3(1515.662f, 1758.566f, 107.9671f), 20000),
		new Movement(new Vector3(1429.954f, 1551.615f, 108.901f),  20000),		
		new Movement(new Vector3(1359.237f, 1300.179f, 106.0672f), 20000),
		new Movement(new Vector3(1418.91f, 1226.055f, 109.7782f),  20000),
		new Movement(10, 10000),
		new Movement(new Vector3(1499.049f, 1182.914f, 113.2241f), 20000),
		new Movement(new Vector3(1353.98f, 1036.651f, 113.8853f),  20000),
		new Movement(new Vector3(1306.987f, 1025.231f, 104.8914f), 20000),
		new Movement(new Vector3(1280.302f, 847.937f, 104.563f), 20000),		
		new Movement(new Vector3(1186.332f, 623.3387f, 98.08354f), 15000),
		new Movement(new Vector3(1099.49f, 551.6512f, 95.17292f),  15000),
		new Movement(new Vector3(1114.992f, 615.054f, 108.4361f),  15000),
		new Movement(new Vector3(1062.651f, 519.1472f, 94.0758f),  15000),
		new Movement(5, 10000),
		new Movement(new Vector3(998.3989f, 447.4419f, 93.75185f), 15000),
		new Movement(new Vector3(948.0231f, 346.8295f, 88.51055f), 15000),
		new Movement(new Vector3(1065.824f, 451.6826f, 91.49496f), 15000),
		new Movement(new Vector3(1154.033f, 366.9916f, 90.43996f), 15000),
		new Movement(new Vector3(1160.344f, 317.0197f, 90.60883f), 15000),
		new Movement(new Vector3(1004.223f, 195.3185f, 79.91518f), 15000),
		new Movement(3, 10000),
		new Movement(new Vector3(1019.871f, 183.5943f, 79.85587f), 15000),
		new Movement(new Vector3(1064.245f, 246.3719f, 79.85587f), 15000),
		new Movement(1, 10000),
		new Movement(new Vector3(1147.623f, 239.858f, 80.8703f), 10000),
		new Movement(new Vector3(989.8643f, -25.61431f, 80.84769f), 10000),
	};

}