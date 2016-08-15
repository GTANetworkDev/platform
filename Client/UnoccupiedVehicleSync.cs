using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetwork
{
    public class UnoccupiedVehicleSync
    {
        private List<RemoteVehicle> SyncedVehicles = new List<RemoteVehicle>();
        private const int UNOCCUPIED_VEH_RATE = 400;
        private long _lastUpdate;

        public void StartSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;
            
            if (veh != null)
            lock (SyncedVehicles)
            {
                SyncedVehicles.Add(veh);
            }
        }

        public void StopSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;

            if (veh != null)
            lock (SyncedVehicles)
            {
                SyncedVehicles.Remove(veh);
            }
        }

        public void StopAll()
        {
            lock (SyncedVehicles)
            {
                SyncedVehicles.Clear();
            }
        }

        public void Pulse()
        {
            if (Util.TickCount - _lastUpdate > UNOCCUPIED_VEH_RATE)
            {
                _lastUpdate = Util.TickCount;

                if (SyncedVehicles.Count > 0)
                {
                    int vehicleCount = 0;
                    List<byte> buffer = new List<byte>();

                    lock (SyncedVehicles)
                    {
                        foreach (var vehicle in SyncedVehicles.Where(v => v.StreamedIn))
                        {
                            var ent = Main.NetEntityHandler.NetToEntity(vehicle);

                            if (ent != null &&
                                (ent.Position.DistanceToSquared(vehicle.Position.ToVector()) > 1f ||
                                 ent.Rotation.DistanceToSquared(vehicle.Rotation.ToVector()) > 1f ||
                                 Math.Abs(new Vehicle(ent.Handle).EngineHealth - vehicle.Health) > 1f))
                            {
                                vehicle.Position = ent.Position.ToLVector();
                                vehicle.Rotation = ent.Rotation.ToLVector();
                                vehicle.Health = new Vehicle(ent.Handle).EngineHealth;

                                var data = new VehicleData();
                                data.VehicleHandle = vehicle.RemoteHandle;
                                data.Position = vehicle.Position;
                                data.Quaternion = vehicle.Rotation;
                                data.Velocity = ent.Velocity.ToLVector();
                                data.VehicleHealth = vehicle.Health;
                                if (ent.IsDead)
                                    data.Flag = (short) VehicleDataFlags.VehicleDead;
                                else
                                    data.Flag = 0;

                                var bin = PacketOptimization.WriteUnOccupiedVehicleSync(data);
                                //UI.Notify("Written " + bin.Length);
                                buffer.AddRange(bin);
                                vehicleCount++;
                            }
                        }
                    }

                    if (vehicleCount > 0)
                    {
                        buffer.Insert(0, (byte)vehicleCount);

                        var msg = Main.Client.CreateMessage();
                        msg.Write((byte)PacketType.UnoccupiedVehSync);
                        msg.Write(buffer.Count);
                        msg.Write(buffer.ToArray());

                        Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, (int) ConnectionChannel.UnoccupiedVeh);
                    }
                }
            }
        }


    }
}