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
        
        shape.onEntityEnterColShape += (shape, ent) =>
        {
            if (!mineArmed) return;
            API.createOwnedExplosion(sender, ExplosionType.EXPLOSION_HI_OCTANE, pos, 1f, playerDimension);
            API.deleteEntity(prop);
            API.deleteColShape(shape);
        };

        shape.onEntityExitColShape += (shape, ent) =>
        {
            if (ent == sender.CharacterHandle && !mineArmed)
            {
                mineArmed = true;
                API.sendNotificationToPlayer(sender, "Mine has been ~r~armed~w~!", true);
            }
        };
    }
}
