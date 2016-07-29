using System;
using System.Collections.Generic;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class PickupManager
    {
        private Dictionary<int, long> PickupsToRespawn = new Dictionary<int, long>();

        public void Add(int netHandle)
        {
            lock (PickupsToRespawn)
            {
                if (!PickupsToRespawn.ContainsKey(netHandle))
                {
                    PickupsToRespawn.Add(netHandle, Program.GetTicks());
                    Program.Output("ADDING PICKUP AT " + Program.GetTicks());
                }
            }
        }

        public void Pulse()
        {
            lock (PickupsToRespawn)
            {
                foreach (var pair in new Dictionary<int, long>(PickupsToRespawn))
                {
                    var prop = Program.ServerInstance.NetEntityHandler.NetToProp<PickupProperties>(pair.Key);

                    if (prop != null)
                    {
                        if (prop.RespawnTime > 0 && prop.PickedUp && Program.GetTicks() - pair.Value > prop.RespawnTime)
                        {
                            RespawnPickup(pair.Key, prop);
                            PickupsToRespawn.Remove(pair.Key);
                            Program.ServerInstance.RunningResources.ForEach(res => res.Engines.ForEach(en => en.InvokePickupRespawn(new NetHandle(pair.Key))));
                        }
                    }
                    else
                    {
                        PickupsToRespawn.Remove(pair.Key);
                    }
                }
            }
        }

        public void RespawnPickup(int netHandle, PickupProperties prop)
        {
            prop.PickedUp = false;

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Pickup;
            packet.Properties = prop;
            packet.NetHandle = netHandle;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);
        }
    }
}