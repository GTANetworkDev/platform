using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetworkServer.Managers
{
    internal class UnoccupiedVehicleManager
    {
        private const int UPDATE_RATE = 250;
        private const float SYNC_RANGE = 180;
        private const float SYNC_RANGE_SQUARED = SYNC_RANGE*SYNC_RANGE;
        private const float DROPOFF = 30;
        private const float DROPOFF_SQUARED = DROPOFF*DROPOFF;

        private long _lastUpdate;

        private Dictionary<int, Client> Syncer = new Dictionary<int, Client>();

        public void Pulse()
        {
            if (Program.GetTicks() - _lastUpdate <= UPDATE_RATE) return;
            _lastUpdate = Program.GetTicks();
            Task.Run((Action)Update);
        }

        public Client GetSyncer(int handle)
        {
            return Syncer.Get(handle);
        }

        public void UnsyncAllFrom(Client player)
        {
            for (var i = Syncer.Count - 1; i >= 0; i--)
            {
                var el = Syncer.ElementAt(i);

                if (el.Value == player)
                {
                    StopSync(el.Value, el.Key);
                    Syncer.Remove(el.Key);
                }
            }
        }

        private static bool IsVehicleUnoccupied(NetHandle vehicle)
        {
            var players = Program.ServerInstance.PublicAPI.getAllPlayers();
            var vehicles = Program.ServerInstance.NetEntityHandler.ToCopy().Select(pair => pair.Value).Where(p => p is VehicleProperties).Cast<VehicleProperties>();
            var prop = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value);

            return players.TrueForAll(c => c.CurrentVehicle != vehicle) && vehicles.All(v => v.Trailer != vehicle.Value) && prop.AttachedTo == null;
        }

        private void Update()
        {
            for (var index = Program.ServerInstance.PublicAPI.getAllVehicles().Count - 1; index >= 0; index--)
            {
                UpdateVehicle(Program.ServerInstance.PublicAPI.getAllVehicles()[index].Value, Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(Program.ServerInstance.PublicAPI.getAllVehicles()[index].Value));
            }
        }

        private void UpdateVehicle(int handle, EntityProperties prop)
        {
            if (handle == 0 || prop == null) return;

            if (!IsVehicleUnoccupied(new NetHandle(handle))) //OCCUPIED
            {
                if (Syncer.ContainsKey(handle))
                {
                    StopSync(Syncer[handle], handle);
                }
                return;
            }

            if (prop.Position == null) return;

            var players = Program.ServerInstance.PublicAPI.getAllPlayers().Where(c => (c.Properties.Dimension == prop.Dimension || prop.Dimension == 0) && c.Position != null).OrderBy(c => c.Position.DistanceToSquared2D(prop.Position)).Take(1).ToArray();
            if (players[0] == null) return;

            if (players[0].Position.DistanceToSquared(prop.Position) < SYNC_RANGE_SQUARED && (players[0].Properties.Dimension == prop.Dimension || prop.Dimension == 0))
            {
                if (Syncer.ContainsKey(handle))
                {
                    if (Syncer[handle] != players[0])
                    {
                        StopSync(Syncer[handle], handle);
                        StartSync(players[0], handle);
                    }
                }
                else
                {
                    StartSync(players[0], handle);
                }
            }
            else
            {
                if (Syncer.ContainsKey(handle))
                {
                    StopSync(players[0], handle);
                }
            }
        }

        private void StartSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(true);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
            //Console.WriteLine("[DEBUG MESSAGE] [+] Starting sync for: " + player.Name + " | Vehicle: " + vehicle);

            Syncer.Set(vehicle, player);
        }

        private void StopSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(false);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
            //Console.WriteLine("[DEBUG MESSAGE] [-] Stopping sync for: " + player.Name + " | Vehicle: " + vehicle);

            Syncer.Remove(vehicle);
        }
    }
}