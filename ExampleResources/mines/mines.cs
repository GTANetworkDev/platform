using System;
using GTANetworkServer;
using GTANetworkShared;

public class MinesTest : Script
{
    public MinesTest()
    {
        API.onResourceStart += myResourceStart;
    }

    public void myResourceStart()
    {
        API.consoleOutput("Starting mines!");
    }

    [Command("mine")]
    public void PlaceMine(Client sender, float MineRange = 10f)
    {
        var pos = API.getEntityPosition(sender);
        var playerDimension = API.getEntityDimension(sender);

        var prop = API.createObject(API.getHashKey("prop_bomb_01"), pos - new Vector3(0, 0, 1f), new Vector3(), playerDimension);     
        var shape = API.createSphereColShape(pos, MineRange);
        shape.dimension = playerDimension;
        
        bool mineArmed = false;
        
        shape.onEntityEnterColShape += (s, ent) =>
        {
            if (!mineArmed) return;
            API.createOwnedExplosion(sender, ExplosionType.HiOctane, pos, 1f, playerDimension);
            API.deleteEntity(prop);
            API.deleteColShape(shape);
        };

        shape.onEntityExitColShape += (s, ent) =>
        {
            if (ent == sender.handle && !mineArmed)
            {
                mineArmed = true;
                API.sendNotificationToPlayer(sender, "Mine has been ~r~armed~w~!", true);
            }
        };
    }
}
