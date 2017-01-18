using GTANetworkServer;
using GTANetworkShared;
using RPGResource.Cops;

namespace RPGResource.Global
{
    public class RespawnManager : Script
    {
        public RespawnManager()
        {
            API.onPlayerRespawn += RespawnPlayer;
            API.onVehicleDeath += OnVehicleExplode;
        }

        public void RespawnPlayer(Client player)
        {
            if (API.getEntityData(player, "IS_COP") == true)
            {
                API.call("SpawnManager", "SpawnCop", player);
            }
            else if (API.getEntityData(player, "Jailed") == true)
            {
                API.setEntityPosition(player, JailController.JailCenter);
            }
        }

        public void OnVehicleExplode(NetHandle vehicle)
        {
            if (API.getEntityData(vehicle, "RESPAWNABLE") == true)
            {
                API.delay(10000, true, () =>
                {
                    var color1 = API.getVehiclePrimaryColor(vehicle);
                    var color2 = API.getVehicleSecondaryColor(vehicle);
                    var model = API.getEntityModel(vehicle);
                    var copcar = API.getEntityData(vehicle, "COPCAR");
                    var spawnPos = API.getEntityData(vehicle, "SPAWN_POS");
                    var spawnH = API.getEntityData(vehicle, "SPAWN_ROT");

                    API.deleteEntity(vehicle);

                    API.createVehicle((VehicleHash) model, spawnPos, new Vector3(0, 0, spawnH), color1, color2);

                    API.setEntityData(vehicle, "COPCAR", copcar);
                    API.setEntityData(vehicle, "SPAWN_POS", spawnPos);
                    API.setEntityData(vehicle, "SPAWN_ROT", spawnH);
                    API.setEntityData(vehicle, "RESPAWNABLE", true);
                });
            }
        }
    }
}