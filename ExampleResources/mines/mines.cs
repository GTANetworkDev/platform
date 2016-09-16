using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class MinesTest : Script
{
	public MinesTest()
	{
		API.consoleOutput("Starting mines!");
	}

	public const float MineRange = 10f;

	[Command("mine")]
	public void PlaceMine(Client sender)
	{
		var pos = API.getEntityPosition(sender);
		var playerDim = API.getEntityDimension(sender);

		var mine = new Mine();
		mine.Prop = API.createObject(848107085, pos - new Vector3(0, 0, 1f), new Vector3(), playerDim); // prop_bomb_01
		mine.Owner = sender;
		mine.Shape = API.createSphereColShape(pos, MineRange);
		mine.Position = pos;

		mine.Shape.onEntityEnterColShape += (shape, ent) =>
		{
			if (!mine.Armed) return;
			API.createOwnedExplosion(mine.Owner, ExplosionType.EXPLOSION_HI_OCTANE, mine.Position, 1f, playerDim);
			API.deleteEntity(mine.Prop);
			API.deleteColShape(mine.Shape);
		};

		mine.Shape.onEntityExitColShape += (shape, ent) =>
		{
			if (ent == mine.Owner.CharacterHandle && !mine.Armed)
			{
				mine.Armed = true;
				API.sendNotificationToPlayer(mine.Owner, "Mine has been ~r~armed~w~!", true);
			}
		};
	}
}

public struct Mine
{
	public ColShape Shape { get; set; }
	public Client Owner { get; set; }
	public NetHandle Prop { get; set; }
	public bool Armed { get; set; }
	public Vector3 Position { get; set; }
}