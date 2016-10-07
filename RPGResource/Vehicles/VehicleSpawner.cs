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

                var car = API.createVehicle(hash,
                    new Vector3(element.getElementData<float>("posX"), element.getElementData<float>("posY"),
                        element.getElementData<float>("posZ")),
                    new Vector3(0, 0, element.getElementData<float>("heading")), 160, 160);

                if (element.getElementData<bool>("cop"))
                    API.setLocalEntityData(car, "COPCAR", true);
            }
        }
    }
}