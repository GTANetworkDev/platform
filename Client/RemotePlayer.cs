using System;
using GTA;
using GTANetworkShared;

namespace GTANetwork
{
    public class RemoteVehicle
    {
        public RemoteVehicle()
        {
            Properties = new VehicleProperties();
        }

        public VehicleProperties Properties { get; set; }
        public int NetHandle { get; set; }
    }


    public class RemotePlayer
    {
        public Ped Character { get; set; }
        public Vehicle Vehicle { get; set; }

        public PedProperties Properties { get; set; }
        public RemoteVehicle RemoteVehicle { get; set; }
        public int NetHandle { get; set; }
        public string Name { get; set; }
        public DateTime LastUpdateReceived { get; set; }

        public RemotePlayer(int handle)
        {
            Properties = new PedProperties();
            RemoteVehicle = new RemoteVehicle();
            NetHandle = handle;
        }

        public void UpdateData(PedData data)
        {
            
        }

        public void UpdateData(VehicleData data)
        {
            RemoteVehicle.NetHandle = data.NetHandle;
            Name = data.Name;
            LastUpdateReceived = DateTime.Now;
            
        }
    }
}