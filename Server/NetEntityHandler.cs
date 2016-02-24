using System.Collections.Generic;
using MultiTheftAutoShared;
using ProtoBuf;

namespace GTAServer
{
    public class NetEntityHandler
    {
        private int EntityCounter;
        private Dictionary<int, EntityProperties> ServerEntities;

        public NetEntityHandler()
        {
            ServerEntities = new Dictionary<int, EntityProperties>();
        }

        public Dictionary<int, EntityProperties> ToDict()
        {
            return ServerEntities;
        }

        public int CreateVehicle(int model, Vector3 pos, Vector3 rot, int color1, int color2)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new VehicleProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.EntityType = (byte)EntityType.Vehicle;
            obj.PrimaryColor = color1;
            obj.SecondaryColor = color2;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Vehicle;
            packet.Model = model;
            packet.Rotation = rot;
            packet.Position = pos;
            packet.Color1 = color1;
            packet.Color2 = color2;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Vector3 rot, bool dynamic)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new EntityProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.EntityType = (byte)EntityType.Prop;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Prop;
            packet.Model = model;
            packet.Rotation = rot;
            packet.Position = pos;
            packet.Dynamic = dynamic;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new EntityProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.Position = pos;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Blip;
            packet.Position = pos;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }
        
        public void DeleteEntity(int netId)
        {
            if (!ServerEntities.ContainsKey(netId)) return;

            var packet = new DeleteEntity();
            packet.NetHandle = netId;
            Program.ServerInstance.SendToAll(packet, PacketType.DeleteEntity, true);

            ServerEntities.Remove(netId);
        }

        public int GenerateHandle()
        {
            var localHan = ++EntityCounter;

            ServerEntities.Add(localHan, new EntityProperties());

            return localHan;
        }
    }
}