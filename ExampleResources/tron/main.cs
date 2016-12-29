using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class Tron : Script
{
    public Tron()
    {
        API.onUpdate += update;
    }

    public void update()
    {
        var players = API.getAllPlayers();

        foreach(var player in players)
        {
            if (!API.isPlayerInAnyVehicle(player) ||
                API.getEntityModel(API.getPlayerVehicle(player)) != -405626514) // Shotaro
                continue;

            Vector3 _lastPos;
            if ((_lastPos = API.getEntityData(player, "TRON_LAST_PLACED_POS")) == null)
            {
                API.setEntityData(player, "TRON_LAST_PLACED_POS", API.getEntityPosition(player));
                continue;
            }

            Vector3 currentPos = API.getEntityPosition(player);

            if (_lastPos.DistanceToSquared(currentPos) > 25f)
            {
                var dir = currentPos - _lastPos;
                dir.Normalize();
                var radAtan = -Math.Atan2(dir.X, dir.Y);
                var heading = (float)(radAtan * 180f / Math.PI);
                heading += 90f;

                API.setEntityData(player, "TRON_LAST_PLACED_POS", _lastPos + dir*4f);

                API.createObject(API.getHashKey("prop_const_fence01b_cr"), _lastPos + dir*2f - new Vector3(0, 0, 2f), new Vector3(0, 0, heading));
            }
        }
    }
}
