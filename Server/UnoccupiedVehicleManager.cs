using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetworkServer
{
    public class UnoccupiedVehicleManager
    {
        private const int UPDATE_RATE = 500;
        private long _lastUpdate;

        private Dictionary<int, Client> Syncers = new Dictionary<int, Client>();

        public void Pulse()
        {
            if (Program.GetTicks() - _lastUpdate > UPDATE_RATE)
            {
                _lastUpdate = Program.GetTicks();

                Task.Run((Action)Update);
            }
        }

        public Client GetSyncer(int handle)
        {
            return Syncers.Get(handle);
        }
        
        public static bool IsVehicleUnoccupied(NetHandle vehicle)
        {
            var players = Program.ServerInstance.PublicAPI.getAllPlayers();
            var vehicles = Program.ServerInstance.NetEntityHandler.ToDict().Select(pair => pair.Value).Where(p => p is VehicleProperties).Cast<VehicleProperties>();
            var prop = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value);

            return players.TrueForAll(c => c.CurrentVehicle != vehicle) && vehicles.All(v => v.Trailer != vehicle.Value) &&
                   prop.AttachedTo == null;
        }

        public void Update()
        {
            foreach (var vehicle in Program.ServerInstance.PublicAPI.getAllVehicles())
            {
                UpdateVehicle(vehicle.Value, Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value));
            }
        }

        public void UpdateVehicle(int handle, VehicleProperties prop)
        {
            if (handle == 0 || prop == null) return;
            if (!IsVehicleUnoccupied(new NetHandle(handle)))
            {
                if (Syncers.ContainsKey(handle))
                {
                    StopSync(Syncers[handle], handle);
                }

                return;
            }

            if (Syncers.ContainsKey(handle)) // This vehicle already has a syncer
            {
                if (Syncers[handle].Position.DistanceToSquared(prop.Position) > 100 || (Syncers[handle].Properties.Dimension != prop.Dimension && prop.Dimension != 0))
                {
                    StopSync(Syncers[handle], handle);

                    FindSyncer(handle, prop);
                }
            }
            else // This car has no syncer
            {
                FindSyncer(handle, prop);
            }
        }

        public void OverrideSyncer(int vehicleHandle, Client newSyncer)
        {
            if (Syncers.ContainsKey(vehicleHandle)) // We are currently syncing this vehicle
            {
                if (Syncers[vehicleHandle] == newSyncer) return;

                StopSync(Syncers[vehicleHandle], vehicleHandle);
                Syncers[vehicleHandle] = newSyncer;
            }
            else
            {
                Syncers.Add(vehicleHandle, newSyncer);
            }

            StartSync(newSyncer, vehicleHandle);
        }

        public void FindSyncer(int handle, VehicleProperties prop)
        {
            var players =
                Program.ServerInstance.PublicAPI.getAllPlayers()
                    .OrderBy(c => c.Position.DistanceToSquared(prop.Position));

            Client targetPlayer;

            if ((targetPlayer = players.FirstOrDefault()) != null && targetPlayer.Position.DistanceToSquared(prop.Position) < 70f)
            {
                StartSync(targetPlayer, handle);
            }
        }

        public void StartSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(true);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableOrdered,
                (int) ConnectionChannel.SyncEvent);

            Syncers.Set(vehicle, player);
        }

        public void StopSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(false);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableOrdered,
                (int)ConnectionChannel.SyncEvent);

            Syncers.Remove(vehicle);
        }
    }
}