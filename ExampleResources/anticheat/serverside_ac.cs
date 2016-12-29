using System;
using System.Linq;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

public class AntiCheat : Script
{
    public AntiCheat()
    {
        API.onUpdate += MainUpdate;
        API.onClientEventTrigger += ClientEventTrigger;
    }

    public void ClientEventTrigger(Client sender, string eventName, object[] args)
    {
        onCheatDetected(sender, eventName);
    }

    const int PED_INTERPOLATION_WARP_THRESHOLD = 15;
    const int PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 5;

    const int VEHICLE_INTERPOLATION_WARP_THRESHOLD = 15;
    const int VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 10;

    private DateTime _lastUpdate = DateTime.Now;
    public void MainUpdate()
    {
        if (DateTime.Now.Subtract(_lastUpdate).TotalMilliseconds < 100) return;
        _lastUpdate = DateTime.Now;

        var players = API.getAllPlayers();

        foreach (var p in players)
        {
            if (API.getEntityData(p, "ANTICHEAT_LAST_POS") == null || API.getEntityData(p, "ANTICHEAT_LAST_POS") == new Vector3())
            {
                API.setEntityData(p, "ANTICHEAT_LAST_POS", API.getEntityPosition(p));
                continue;
            }

            var lastLegitTp = API.getEntityData(p, "__LAST_POSITION_SET");
            var lastLegitDeath = API.getEntityData(p, "__LAST_PLAYER_DEATH") ?? 0;
            var lastLegitRespawn = API.getEntityData(p, "__LAST_PLAYER_RESPAWN") ?? 0;
            var stillDead = lastLegitRespawn < lastLegitDeath;

            if ((lastLegitTp == null || API.TickCount - lastLegitTp > 500) && !stillDead)
            {
                var lastPos = API.getEntityData(p, "ANTICHEAT_LAST_POS");
                var velocity = API.getPlayerVelocity(p).Length();
                var newPos = API.getEntityPosition(p);

                float threshold;

                if (!API.isPlayerInAnyVehicle(p) && !API.isPlayerParachuting(p))
                    threshold = (PED_INTERPOLATION_WARP_THRESHOLD +
                                 PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * velocity);
                else
                    threshold = (VEHICLE_INTERPOLATION_WARP_THRESHOLD +
                                 VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * velocity);


                if (lastPos.DistanceToSquared(newPos) > threshold*threshold)
                {
                    onCheatDetected(p, "CHEAT_TELEPORT");
                }
            }

            API.setEntityData(p, "ANTICHEAT_LAST_POS", API.getEntityPosition(p));
        }
    }

    private void onCheatDetected(Client player, string cheat)
    {
        if (!API.hasSetting(cheat)) return;

        switch(API.getSetting<int>(cheat))
        {
            case 1:
                API.consoleOutput("[ANTICHEAT] Player " + player.name + " is cheating with " + cheat + "!");
                break;
            case 2:
                API.kickPlayer(player, "Cheating: " + cheat);
                break;
            case 3:
                API.banPlayer(player, "Cheating: " + cheat);
                break;
        }
    }
}