using System;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Vehicles
{
    public class VehicleSpawner : Script
    {
        public VehicleSpawner()
        {
            API.onResourceStart += onResStart;
        }

        public void onResStart()
        {
            var cars = API.loadConfig("vehicles.xml");

            foreach (var element in cars.getElementsByType("vehicle"))
            {
                var model = element.getElementData<string>("model");
                var hash = (VehicleHash) Enum.Parse(typeof (VehicleHash), model, true);

                var spawnPos = new Vector3(element.getElementData<float>("posX"), element.getElementData<float>("posY"),
                    element.getElementData<float>("posZ"));
                var heading = element.getElementData<float>("heading");

                var car = API.createVehicle(hash,
                    spawnPos,
                    new Vector3(0, 0, heading), 160, 160);

                if (element.getElementData<bool>("cop"))
                    API.setEntityData(car, "COPCAR", true);

                API.setEntityData(car, "RESPAWNABLE", true);

                API.setEntityData(car, "SPAWN_POS", spawnPos);
                API.setEntityData(car, "SPAWN_ROT", heading);
            }
        }
    }
}