using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkServer.Constant;
using GTANetworkServer.Managers;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using ProtoBuf;

namespace GTANetworkServer
{
    internal partial class GameServer
    {
        public static void UpdateEntityInfo(int netId, EntityType entity, Delta_EntityProperties newInfo, Client exclude = null)
        {
            var packet = new UpdateEntity
            {
                EntityType = (byte)entity,
                Properties = newInfo,
                NetHandle = netId
            };
            if (exclude == null)
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, ConnectionChannel.EntityBackend);
            else
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, exclude, ConnectionChannel.EntityBackend);
        }


        //Ped Packet
        internal void ResendPacket(PedData fullPacket, Client exception, bool pure)
        {
            byte[] full;
            var basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                if (fullPacket.NetHandle != null) basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.Fake) continue;
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (!client.ConnectionConfirmed) continue;
                if (client == exception) continue;

                var msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    //if (client.Position.DistanceToSquared(fullPacket.Position) > 10000) // 1km
                    //{
                    //    var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                    //    if (lastUpdateReceived != 0 && Program.GetTicks() - lastUpdateReceived <= 1000) continue;
                    //    msg.Write((byte)PacketType.BasicSync);
                    //    msg.Write(basic.Length);
                    //    msg.Write(basic);
                    //    Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.BasicSync);

                    //    client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                    //}
                    //else
                    //{
                        msg.Write((byte)PacketType.PedPureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.PureSync);
                    //}
                }
                else
                {
                    msg.Write((byte)PacketType.PedLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.Fake) continue;
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (!client.ConnectionConfirmed) continue;
                if (client == exception) continue;

                var msg = Server.CreateMessage();
                if (!pure) continue;

                var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                if (lastUpdateReceived != 0 && Program.GetTicks() - lastUpdateReceived <= 1000) continue;

                msg.Write((byte)PacketType.BasicSync);
                msg.Write(basic.Length);
                msg.Write(basic);
                Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.BasicSync);
                client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
            }
        }

        //Vehicle Packet
        internal void ResendPacket(VehicleData fullPacket, Client exception, bool pure)
        {
            byte[] full;
            var basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                if (fullPacket.Flag != null && PacketOptimization.CheckBit(fullPacket.Flag.Value, VehicleDataFlags.Driver))
                {
                    if (fullPacket.NetHandle != null) basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
                }
                else if (!exception.CurrentVehicle.IsNull)
                {
                    var carPos = NetEntityHandler.ToDict()[exception.CurrentVehicle.Value].Position;
                    if (fullPacket.NetHandle != null) basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, carPos);
                }
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (!client.ConnectionConfirmed) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                var msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    //if (client.Position.DistanceToSquared(fullPacket.Position) > 40000f) // 1 km
                    //{
                    //    var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                    //    if (lastUpdateReceived != 0 && Program.GetTicks() - lastUpdateReceived <= 1000) continue;
                    //    msg.Write((byte)PacketType.BasicSync);
                    //    msg.Write(basic.Length);
                    //    msg.Write(basic);
                    //    Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.BasicSync);

                    //    client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                    //}
                    //else
                    //{
                        msg.Write((byte)PacketType.VehiclePureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.PureSync);
                    //}
                }
                else
                {
                    msg.Write((byte)PacketType.VehicleLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (!client.ConnectionConfirmed) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                var msg = Server.CreateMessage();
                if (!pure) continue;
                var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                if (lastUpdateReceived != 0 && Program.GetTicks() - lastUpdateReceived <= 1000) continue;
                msg.Write((byte)PacketType.BasicSync);
                msg.Write(basic.Length);
                msg.Write(basic);
                Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.BasicSync);

                client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
            }
        }

        internal bool CheckUnoccupiedTrailerDriver(Client player, NetHandle vehicle)
        {
            NetHandle traileredBy = Program.ServerInstance.PublicAPI.getVehicleTraileredBy(vehicle);

            if (traileredBy != null)
            {
                return Program.ServerInstance.PublicAPI.getVehicleDriver(traileredBy) == player;
            }

            return false;
        }

        internal void ResendUnoccupiedPacket(VehicleData fullPacket, Client exception)
        {

            NetHandle vehicleEntity;

            if (fullPacket.NetHandle != null)
            {
                vehicleEntity = new NetHandle(fullPacket.NetHandle.Value);
            } else
            {
                vehicleEntity = new NetHandle();
            }

            var full = PacketOptimization.WriteUnOccupiedVehicleSync(fullPacket);
            var basic = PacketOptimization.WriteBasicUnOccupiedVehicleSync(fullPacket);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                // skip sending a sync packet for a trailer to it's owner.
                if (vehicleEntity.Value != 0 && CheckUnoccupiedTrailerDriver(client, vehicleEntity)) { continue; }

                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                var msg = Server.CreateMessage();
                if (client.Position == null) continue;
                if (client.Position.DistanceToSquared(fullPacket.Position) < 20000)
                {
                    msg.Write((byte)PacketType.UnoccupiedVehSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.UnreliableSequenced,
                        (int)ConnectionChannel.UnoccupiedVeh);
                }
                else
                {
                    msg.Write((byte)PacketType.BasicUnoccupiedVehSync);
                    msg.Write(basic.Length);
                    msg.Write(basic);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.UnreliableSequenced,
                        (int)ConnectionChannel.UnoccupiedVeh);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                var msg = Server.CreateMessage();

                msg.Write((byte)PacketType.BasicUnoccupiedVehSync);
                msg.Write(basic.Length);
                msg.Write(basic);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.UnreliableSequenced,
                    (int)ConnectionChannel.UnoccupiedVeh);
            }
        }


        internal void ResendBulletPacket(int netHandle, Vector3 aim, bool shooting, Client exception)
        {
            var full = PacketOptimization.WriteBulletSync(netHandle, shooting, aim);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;
                //if (range && client.Position.DistanceToSquared(exception.Position) > 80000) continue;

                var msg = Server.CreateMessage();
                msg.Write((byte)PacketType.BulletSync);
                msg.Write(full.Length);
                msg.Write(full);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.ReliableSequenced,
                    (int)ConnectionChannel.BulletSync);
            }
        }

        internal void ResendBulletPacket(int netHandle, int netHandleTarget, bool shooting, Client exception)
        {
            var full = PacketOptimization.WriteBulletSync(netHandle, shooting, netHandleTarget);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;
                //if (range && client.Position.DistanceToSquared(exception.Position) > 80000) continue; 

                var msg = Server.CreateMessage();
                msg.Write((byte)PacketType.BulletPlayerSync);
                msg.Write(full.Length);
                msg.Write(full);
                Server.SendMessage(msg, client.NetConnection, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.BulletSync);
            }
        }



        public void SendToClient(Client c, object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            var msg = Server.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendMessage(msg, c.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.UnreliableSequenced, (int)channel);
        }

        public void SendToAll(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            lock (Clients)
            {
                foreach (var client in Clients)
                {
                    if (client.Fake) continue;
                    var data = SerializeBinary(newData);
                    var msg = Server.CreateMessage();
                    msg.Write((byte)packetType);
                    msg.Write(data.Length);
                    msg.Write(data);
                    Server.SendMessage(msg, client.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int)channel);
                }
            }
        }

        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude, ConnectionChannel channel)
        {
            lock (Clients)
            {
                foreach (var client in Clients)
                {
                    if (client == exclude || client.Fake) continue;
                    var data = SerializeBinary(newData);
                    var msg = Server.CreateMessage();
                    msg.Write((byte) packetType);
                    msg.Write(data.Length);
                    msg.Write(data);
                    Server.SendMessage(msg, client.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int) channel);
                }
            }
        }

        public void SendDeleteObject(Client player, Vector3 pos, float radius, int modelHash)
        {
            var obj = new ObjectData
            {
                Position = pos,
                Radius = radius,
                modelHash = modelHash
            };
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.DeleteObject);
            msg.Write(bin.Length);
            msg.Write(bin);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.EntityBackend);
        }


        public void SendNativeCallToPlayer(Client player, bool safe, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Internal = safe,
                Arguments = ParseNativeArguments(arguments)
            };
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void SendNativeCallToAllPlayers(bool safe, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Internal = safe,
                Arguments = ParseNativeArguments(arguments),
                ReturnType = null,
                Id = 0
            };

            var bin = SerializeBinary(obj);
            lock (Clients)
            {
                foreach (var c in Clients)
                {
                    var msg = Server.CreateMessage();

                    msg.Write((byte) PacketType.NativeCall);
                    msg.Write(bin.Length);
                    msg.Write(bin);

                    Server.SendMessage(msg, c.NetConnection, NetDeliveryMethod.ReliableOrdered, (int) ConnectionChannel.NativeCall);
                }
            }
        }

        private Dictionary<uint, Action<object>> _callbacks = new Dictionary<uint, Action<object>>();
        public void GetNativeCallFromPlayer(Client player, bool safe, uint salt, ulong hash, NativeArgument returnType, Action<object> callback, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                ReturnType = returnType,
                Id = salt,
                Arguments = ParseNativeArguments(arguments),
                Internal = safe
            };

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }


        public void ChangePlayerTeam(Client target, int newTeam)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).Team = newTeam;
            }

            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerTeamChange,
                Arguments = ParseNativeArguments(target.handle.Value, newTeam)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColor(Client target, int newColor)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipColor = newColor;
            }

            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipColorChange,
                Arguments = ParseNativeArguments(target.handle.Value, newColor)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColorForPlayer(Client target, int newColor, Client forPlayer)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipColorChange,
                Arguments = ParseNativeArguments(target.handle.Value, newColor)
            };

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSprite(Client target, int newSprite)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipSprite = newSprite;
            }

            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipSpriteChange,
                Arguments = ParseNativeArguments(target.handle.Value, newSprite)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSpriteForPlayer(Client target, int newSprite, Client forPlayer)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipSpriteChange,
                Arguments = ParseNativeArguments(target.handle.Value, newSprite)
            };

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlpha(Client target, int newAlpha)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipAlpha = (byte)newAlpha;
            }

            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipAlphaChange,
                Arguments = ParseNativeArguments(target.handle.Value, newAlpha)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlphaForPlayer(Client target, int newAlpha, Client forPlayer)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerBlipAlphaChange,
                Arguments = ParseNativeArguments(target.handle.Value, newAlpha)
            };

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }


        public void SendServerEvent(ServerEventType type, params object[] arg)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)type,
                Arguments = ParseNativeArguments(arg)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SendServerEventToPlayer(Client target, ServerEventType type, params object[] arg)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)type,
                Arguments = ParseNativeArguments(arg)
            };

            SendToClient(target, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }


        public void DetachEntity(int nethandle, bool collision)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.EntityDetachment,
                Arguments = ParseNativeArguments(nethandle, collision)
            };
            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectate(Client target, bool spectating)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerSpectatorChange,
                Arguments = ParseNativeArguments(target.handle.Value, spectating)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectatePlayer(Client spectator, Client target)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerSpectatorChange,
                Arguments = ParseNativeArguments(spectator.handle.Value, true, target.handle.Value)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }


        public void PlayCustomPlayerAnimation(Client target, int flag, string animDict, string animName)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStart,
                Arguments = ParseNativeArguments(target.handle.Value, flag, animDict, animName)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimationStop(Client target)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStop,
                Arguments = ParseNativeArguments(target.handle.Value)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimationStop(Client target, string animDict, string animName)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStop,
                Arguments = ParseNativeArguments(target.handle.Value, animDict, animName)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }
    }
}
