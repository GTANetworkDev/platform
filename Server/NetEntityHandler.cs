using System;
using System.Collections.Generic;
using System.Linq;
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

        public T NetToProp<T>(int handle) where T : EntityProperties
        {
            if (ServerEntities.ContainsKey(handle) && ServerEntities[handle] is T) return (T)ServerEntities[handle];
            return null;
        }

        public void UpdateMovements()
        {
            lock (ServerEntities)
            {
                // Get all entities who are interpolating
                foreach (var pair in ServerEntities
                    .Where(pair => pair.Value.PositionMovement != null || pair.Value.RotationMovement != null))
                {
                    var currentTime = Program.GetTicks();

                    if (pair.Value.PositionMovement != null)
                    {
                        var delta = currentTime - pair.Value.PositionMovement.ServerStartTime;
                        pair.Value.PositionMovement.Start = delta;

                        pair.Value.Position = Vector3.Lerp(pair.Value.PositionMovement.StartVector,
                            pair.Value.PositionMovement.EndVector,
                            Math.Min(((float) delta) / pair.Value.PositionMovement.Duration, 1f));

                        if (delta >= pair.Value.PositionMovement.Duration)
                            pair.Value.PositionMovement = null;
                    }

                    if (pair.Value.RotationMovement != null)
                    {
                        var delta = currentTime - pair.Value.RotationMovement.ServerStartTime;
                        pair.Value.RotationMovement.Start = delta;

                        pair.Value.Rotation = Vector3.Lerp(pair.Value.RotationMovement.StartVector,
                            pair.Value.RotationMovement.EndVector,
                            Math.Min(((float)delta) / pair.Value.RotationMovement.Duration, 1f));

                        if (delta >= pair.Value.RotationMovement.Duration)
                            pair.Value.RotationMovement = null;
                    }
                }
            }
        }

        private bool _hasWorldBeenCreated;
        public void CreateWorld()
        {
            if (_hasWorldBeenCreated) return;

            var obj = new WorldProperties();
            obj.EntityType = 255;
            obj.Hours = (byte)DateTime.Now.Hour;
            obj.Minutes = (byte)DateTime.Now.Minute;
            obj.Weather = "CLEAR";
            obj.LoadedIpl = new List<string>();
            obj.RemovedIpl = new List<string>();

            ServerEntities.Add(1, obj);
            _hasWorldBeenCreated = true;
        }

        public int CreateVehicle(int model, Vector3 pos, Vector3 rot, int color1, int color2, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new VehicleProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.IsDead = false;
            obj.Health = 1000;
            obj.Alpha = 255;
            obj.Livery = 0;
            obj.NumberPlate = "NETWORK";
            obj.EntityType = (byte)EntityType.Vehicle;
            obj.PrimaryColor = color1;
            obj.SecondaryColor = color2;
            obj.Dimension = dimension;

            if (model == (int)VehicleHash.Taxi)
                obj.VehicleComponents = 1 << 5;
            else if (model == (int) VehicleHash.Police)
                obj.VehicleComponents = 1 << 2;
            else
                obj.VehicleComponents = ~0;


            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Vehicle;
            packet.NetHandle = localEntityHash;
            packet.Properties = obj;
            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Vector3 rot, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new EntityProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.Dimension = dimension;
            obj.Alpha = 255;
            obj.EntityType = (byte)EntityType.Prop;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Prop;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Quaternion rot, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new EntityProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.Dimension = dimension;
            obj.Alpha = 255;
            obj.EntityType = (byte)EntityType.Prop;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Prop;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreatePickup(int model, Vector3 pos, Vector3 rot, int amount, uint respawnTime, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new PickupProperties();
            obj.Position = pos;
            obj.Rotation = rot;
            obj.ModelHash = model;
            obj.RespawnTime = respawnTime;
            obj.Amount = amount;
            obj.Dimension = dimension;
            obj.Alpha = 255;
            obj.EntityType = (byte)EntityType.Pickup;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Pickup;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(NetHandle ent)
        {
            if (ent.IsNull || !ent.Exists()) return 0;

            int localEntityHash = ++EntityCounter;
            var obj = new BlipProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.AttachedNetEntity = ent.Value;
            obj.Dimension = ServerEntities[ent.Value].Dimension;
            obj.Position = ServerEntities[ent.Value].Position;
            obj.Sprite = 0;
            obj.Alpha = 255;
            obj.Scale = 1f;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Blip;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new BlipProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.Position = pos;
            obj.Dimension = dimension;
            obj.Sprite = 0;
            obj.Scale = 1f;
            obj.Alpha = 255;
            obj.AttachedNetEntity = 0;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte) EntityType.Blip;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos, float range, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new BlipProperties();
            obj.EntityType = (byte)EntityType.Blip;
            obj.Position = pos;
            obj.Dimension = dimension;
            obj.Sprite = 0;
            obj.Scale = 1f;
            obj.RangedBlip = range;
            obj.Alpha = 255;
            obj.AttachedNetEntity = 0;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Blip;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b, int dimension)
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
                Dimension = dimension,
                EntityType = (byte) EntityType.Marker,
            };
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Marker;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateTextLabel(string text, float size, float range, int r, int g, int b, Vector3 pos, bool entitySeethrough, int dimension)
        {
            int localEntityHash = ++EntityCounter;
            var obj = new TextLabelProperties();
            obj.EntityType = (byte)EntityType.TextLabel;
            obj.Position = pos;
            obj.Size = size;
            obj.Blue = b;
            obj.Green = g;
            obj.Range = range;
            obj.Red = r;
            obj.Text = text;
            obj.Alpha = 255;
            obj.EntitySeethrough = entitySeethrough;
            obj.Dimension = dimension;
            ServerEntities.Add(localEntityHash, obj);

            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.TextLabel;
            packet.Properties = obj;
            packet.NetHandle = localEntityHash;

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public void DeleteEntity(int netId)
        {
            if (!ServerEntities.ContainsKey(netId)) return;

            var packet = new DeleteEntity();
            packet.NetHandle = netId;
            Program.ServerInstance.SendToAll(packet, PacketType.DeleteEntity, true, ConnectionChannel.NativeCall);

            ServerEntities.Remove(netId);
        }

        public void DeleteEntityQuiet(int netId)
        {
            ServerEntities.Remove(netId);
        }

        public int GeneratePedHandle()
        {
            var localHan = ++EntityCounter;

            ServerEntities.Add(localHan, new PlayerProperties()
            {
                EntityType = (byte) EntityType.Player,
                BlipSprite = 1,
                BlipAlpha = 255,
                Alpha = 255,
            });

            return localHan;
        }
    }

    internal static class NetHandleExtension
    {
        internal static bool Exists(this NetHandle ent)
        {
            if (ent.IsNull) return false;
            return Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(ent.Value);
        }
    }
}