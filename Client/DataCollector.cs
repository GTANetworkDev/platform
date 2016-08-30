using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA;
using GTA.Native;
using Newtonsoft.Json;

namespace GTANetwork
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

    public static class DataCollector
    {
        public static void Collect()
        {
            VehicleHash[] models =
                Enum.GetValues(typeof (VehicleHash)).Cast<VehicleHash>().ToArray();
            Dictionary<int, ConstantVehicleData> datas = new Dictionary<int, ConstantVehicleData>();

            foreach (var model in models)
            {
                var cD = new ConstantVehicleData();
                cD.DisplayName = Function.Call<string>(Hash._GET_LABEL_TEXT,
                    Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, (int) model));
                cD.MaxSpeed = Function.Call<float>((Hash) 0xF417C2502FFFED43, (int) model);
                cD.MaxBraking = Function.Call<float>((Hash)0xDC53FD41B4ED944C, (int)model);
                cD._0xBFBA3BA79CFF7EBF = Function.Call<float>((Hash)0xBFBA3BA79CFF7EBF, (int)model);
                cD.MaxTraction = Function.Call<float>((Hash)0x539DE94D44FDFD0D, (int)model);
                cD.MaxAcceleration = Function.Call<float>((Hash)0x8C044C5C84505B6A, (int)model);
                cD._0x53409B5163D5B846 = Function.Call<float>((Hash)0x53409B5163D5B846, (int)model);
                cD._0xC6AD107DDC9054CC = Function.Call<float>((Hash)0xC6AD107DDC9054CC, (int)model);
                cD._0x5AA3F878A178C4FC = Function.Call<float>((Hash)0x5AA3F878A178C4FC, (int)model);
                cD.MaxNumberOfPassengers = Function.Call<int>(Hash._GET_VEHICLE_MODEL_MAX_NUMBER_OF_PASSENGERS, (int) model);
                cD.MaxOccupants = cD.MaxNumberOfPassengers + 1;
                
                cD.VehicleClass = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (int)model);

                datas.Add((int)model, cD);
            }

            string jsonData = JsonConvert.SerializeObject(datas);

            File.WriteAllText("scripts\\vehicleData.json", jsonData);
        }
    }
}