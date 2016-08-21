using System.Collections.Generic;
using System.IO;
using GTANetworkShared;
using Newtonsoft.Json;

namespace GTANetworkServer
{
    public struct ConstantVehicleData
    {
        public string DisplayName;
        public float MaxSpeed;
        public float MaxBraking;
        public float MaxTraction;
        public float MaxAcceleration;
        public float _0xBFBA3BA79CFF7EBF;
        public float _0x53409B5163D5B846;
        public float _0xC6AD107DDC9054CC;
        public float _0x5AA3F878A178C4FC;
        public int MaxNumberOfPassengers;
        public int MaxOccupants;
        public int VehicleClass;
    }

    public static class ConstantVehicleDataOrganizer
    {
        public static Dictionary<int, ConstantVehicleData> Data = new Dictionary<int, ConstantVehicleData>();

        public static readonly string[] VehicleClasses = new[]
        {
            "Compacts",
            "Sedans",
            "SUVs",
            "Coupes",
            "Muscle",
            "Sports Classics",
            "Sports",
            "Super",
            "Motorcycles",
            "Off-road",
            "Industrial",
            "Utility",
            "Vans",
            "Cycles",
            "Boats",
            "Helicopters",
            "Planes",
            "Service",
            "Emergency",
            "Military",
            "Commercial",
            "Trains",
        };

        public static void Initialize()
        {
            if (!File.Exists("vehicleData.json")) return;
            string text = File.ReadAllText("vehicleData.json");
            Data = JsonConvert.DeserializeObject<Dictionary<int, ConstantVehicleData>>(text);
        }

        public static ConstantVehicleData Get(int model)
        {
            return Data.Get(model);
        }

        public static ConstantVehicleData Get(VehicleHash model)
        {
            return Data.Get((int) model);
        }
    }
}