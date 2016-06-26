using System.Collections.Generic;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class NetEntityHandler
    {
        private int EntityCounter = 1;
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
            obj.IsDead = false;
            obj.Health = 1000;
            obj.EntityType = (byte)EntityType.Vehicle;
            obj.PrimaryColor = color1;
            obj.SecondaryColor = color2;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Vehicle;
            var props = new VehicleProperties();
            props.ModelHash = model;
            props.Rotation = rot;
            props.Position = pos;
            props.PrimaryColor = color1;
            props.SecondaryColor = color2;
            packet.NetHandle = localEntityHash;
            packet.Properties = props;
            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Vector3 rot)
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
            packet.Properties = new EntityProperties();
            packet.Properties.ModelHash = model;
            packet.Properties.Rotation = rot;
            packet.Properties.Position = pos;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreatePickup(int model, Vector3 pos, Vector3 rot, int amount)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new PickupProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.Amount = amount;
            obj.EntityType = (byte)EntityType.Pickup;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Pickup;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateBlip(NetHandle ent)
        {
            if (ent.IsNull || !ent.Exists()) return 0;

            int localEntityHash = ++EntityCounter;
            var obj = new BlipProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.AttachedNetEntity = ent.Value;
            obj.Position = ServerEntities[ent.Value].Position;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Blip;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new BlipProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.Position = pos;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Blip;
            packet.Properties = new BlipProperties();
            packet.Properties.Position = pos;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true);

            return localEntityHash;
        }

        public int CreateMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b)
        {
            int localEntityHash = ++EntityCounter;
            
            var obj = new MarkerProperties()
            {
                MarkerType = markerType,
                Position = pos,
                Direction = dir,
                Rotation = rot,
                Scale = scale,
                Alpha = (byte) alpha,
                Red = r,
                Green = g,
                Blue = b,
                EntityType = (byte) EntityType.Marker,
            };
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Marker;
            packet.Properties = obj;
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

        public int GeneratePedHandle()
        {
            var localHan = ++EntityCounter;

            ServerEntities.Add(localHan, new PedProperties()
            {
                EntityType = (byte) EntityType.Ped,
            });

            return localHan;
        }
    }

    internal static class NetHandleExtension
    {
        internal static bool Exists(this NetHandle ent)
        {
            return !ent.IsNull || Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(ent.Value);
        }
    }
}