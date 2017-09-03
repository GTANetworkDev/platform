using GTA;
using GTA.Native;
using GTANetwork.GUI;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Streamer;
using GTANetwork.Sync;
using GTANetwork.Util;
using GTANetworkShared;
using Lidgren.Network;
using NativeUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using VehicleHash = GTA.VehicleHash;
using WeaponHash = GTA.WeaponHash;

namespace GTANetwork
{
    internal partial class Main
    {
        private void ProcessDataMessage(NetIncomingMessage msg, PacketType type)
        {
            #region Data
            //LogManager.DebugLog("RECEIVED DATATYPE " + type);
            switch (type)
            {
                case PacketType.RedownloadManifest:
                    {
                        StartFileDownload($"http://{_currentServerIp}:{_currentServerPort}");
                    }
                    break;
                case PacketType.VehiclePureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPureVehicleSync(data);
                        HandleVehiclePacket(packet, true);
                    }
                    break;
                case PacketType.VehicleLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightVehicleSync(data);
                        //LogManager.DebugLog("RECEIVED LIGHT VEHICLE PACKET");
                        HandleVehiclePacket(packet, false);
                    }
                    break;
                case PacketType.PedPureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPurePedSync(data);
                        HandlePedPacket(packet, true);
                    }
                    break;
                case PacketType.PedLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightPedSync(data);
                        HandlePedPacket(packet, false);
                    }
                    break;
                case PacketType.BasicSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        GTANetworkShared.Vector3 position;
                        PacketOptimization.ReadBasicSync(data, out int nethandle, out position);

                        HandleBasicPacket(nethandle, position.ToVector());
                    }
                    break;
                case PacketType.BulletSync:
                    {
                        //Util.Util.SafeNotify("Bullet Packet" + DateTime.Now.Millisecond);
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        GTANetworkShared.Vector3 position;
                        var shooting = PacketOptimization.ReadBulletSync(data, out int nethandle, out position);

                        HandleBulletPacket(nethandle, shooting, position.ToVector());
                    }
                    break;
                case PacketType.BulletPlayerSync:
                    {
                        //Util.Util.SafeNotify("Bullet Player Packet" + DateTime.Now.Millisecond);
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        var shooting = PacketOptimization.ReadBulletSync(data, out int nethandle, out int nethandleTarget);
                        HandleBulletPacket(nethandle, shooting, nethandleTarget);
                    }
                    break;
                case PacketType.UnoccupiedVehStartStopSync:
                    {
                        var veh = msg.ReadInt32();
                        var startSyncing = msg.ReadBoolean();

                        if (startSyncing)
                        {
                            VehicleSyncManager.StartSyncing(veh);
                        }
                        else
                        {
                            VehicleSyncManager.StopSyncing(veh);
                        }
                    }
                    break;

                    //CHECK
                case PacketType.UnoccupiedVehSync:
                    {
                        var len = msg.ReadInt32();
                        var bin = msg.ReadBytes(len);
                        var data = PacketOptimization.ReadUnoccupiedVehicleSync(bin);

                        if (data != null)
                        {
                            HandleUnoccupiedVehicleSync(data);
                        }
                    }
                    break;
                case PacketType.BasicUnoccupiedVehSync:
                    {
                        var len = msg.ReadInt32();
                        var bin = msg.ReadBytes(len);
                        var data = PacketOptimization.ReadBasicUnoccupiedVehicleSync(bin);

                        if (data != null)
                        {
                            HandleUnoccupiedVehicleSync(data);
                        }
                    }
                    break;
                case PacketType.CreateEntity:
                    {
                        var len = msg.ReadInt32();
                        //LogManager.DebugLog("Received CreateEntity");
                        if (DeserializeBinary<CreateEntity>(msg.ReadBytes(len)) is CreateEntity data && data.Properties != null)
                        {
                            switch (data.EntityType)
                            {
                                case (byte)EntityType.Vehicle:
                                    {
                                        NetEntityHandler.CreateVehicle(data.NetHandle, (VehicleProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteVehicle)) < StreamerThread.MAX_VEHICLES)
                                        //    NetEntityHandler.StreamIn(veh);
                                    }
                                    break;
                                case (byte)EntityType.Prop:
                                    {
                                        NetEntityHandler.CreateObject(data.NetHandle, data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteProp)) < StreamerThread.MAX_OBJECTS)
                                        //    NetEntityHandler.StreamIn(prop);
                                    }
                                    break;
                                case (byte)EntityType.Blip:
                                    {
                                        NetEntityHandler.CreateBlip(data.NetHandle, (BlipProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteBlip)) < StreamerThread.MAX_BLIPS)
                                        //    NetEntityHandler.StreamIn(blip);
                                    }
                                    break;
                                case (byte)EntityType.Marker:
                                    {
                                        NetEntityHandler.CreateMarker(data.NetHandle, (MarkerProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteMarker)) < StreamerThread.MAX_MARKERS)
                                        //    NetEntityHandler.StreamIn(mark);
                                    }
                                    break;
                                case (byte)EntityType.Pickup:
                                    {
                                        NetEntityHandler.CreatePickup(data.NetHandle, (PickupProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemotePickup)) < StreamerThread.MAX_PICKUPS)
                                        //    NetEntityHandler.StreamIn(pickup);
                                    }
                                    break;
                                case (byte)EntityType.TextLabel:
                                    {
                                        NetEntityHandler.CreateTextLabel(data.NetHandle, (TextLabelProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteTextLabel)) < StreamerThread.MAX_LABELS)
                                        //    NetEntityHandler.StreamIn(label);
                                    }
                                    break;
                                case (byte)EntityType.Ped:
                                    {
                                        NetEntityHandler.CreatePed(data.NetHandle, data.Properties as PedProperties);
                                        //if (NetEntityHandler.Count(typeof(RemotePed)) < StreamerThread.MAX_PEDS)
                                        //    NetEntityHandler.StreamIn(ped);
                                    }
                                    break;
                                case (byte)EntityType.Particle:
                                    {
                                        var ped = NetEntityHandler.CreateParticle(data.NetHandle, data.Properties as ParticleProperties);
                                        if (NetEntityHandler.Count(typeof(RemoteParticle)) < StreamerThread.MAX_PARTICLES) NetEntityHandler.StreamIn(ped);
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.UpdateEntityProperties:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                        if (data?.Properties != null)
                        {
                            switch ((EntityType)data.EntityType)
                            {
                                case EntityType.Blip:
                                    NetEntityHandler.UpdateBlip(data.NetHandle, data.Properties as Delta_BlipProperties);
                                    break;
                                case EntityType.Marker:
                                    NetEntityHandler.UpdateMarker(data.NetHandle, data.Properties as Delta_MarkerProperties);
                                    break;
                                case EntityType.Player:
                                    NetEntityHandler.UpdatePlayer(data.NetHandle, data.Properties as Delta_PlayerProperties);
                                    break;
                                case EntityType.Pickup:
                                    NetEntityHandler.UpdatePickup(data.NetHandle, data.Properties as Delta_PickupProperties);
                                    break;
                                case EntityType.Prop:
                                    NetEntityHandler.UpdateProp(data.NetHandle, data.Properties);
                                    break;
                                case EntityType.Vehicle:
                                    NetEntityHandler.UpdateVehicle(data.NetHandle, data.Properties as Delta_VehicleProperties);
                                    break;
                                case EntityType.Ped:
                                    NetEntityHandler.UpdatePed(data.NetHandle, data.Properties as Delta_PedProperties);
                                    break;
                                case EntityType.TextLabel:
                                    NetEntityHandler.UpdateTextLabel(data.NetHandle, data.Properties as Delta_TextLabelProperties);
                                    break;
                                case EntityType.Particle:
                                    NetEntityHandler.UpdateParticle(data.NetHandle, data.Properties as Delta_ParticleProperties);
                                    break;
                                case EntityType.World:
                                    NetEntityHandler.UpdateWorld(data.Properties);
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.DeleteEntity:
                    {
                        var len = msg.ReadInt32();
                        if (DeserializeBinary<DeleteEntity>(msg.ReadBytes(len)) is DeleteEntity data)
                        {
                            LogManager.DebugLog("RECEIVED DELETE ENTITY " + data.NetHandle);

                            var streamItem = NetEntityHandler.NetToStreamedItem(data.NetHandle);
                            if (streamItem != null)
                            {
                                VehicleSyncManager.StopSyncing(data.NetHandle);
                                NetEntityHandler.Remove(streamItem);
                                NetEntityHandler.StreamOut(streamItem);
                            }
                        }
                    }
                    break;
                case PacketType.StopResource:
                    {
                        var resourceName = msg.ReadString();
                        JavascriptHook.StopScript(resourceName);
                    }
                    break;
                case PacketType.FileTransferRequest:
                    {
                        var len = msg.ReadInt32();
                        if (DeserializeBinary<DataDownloadStart>(msg.ReadBytes(len)) is DataDownloadStart data)
                        {
                            var acceptDownload = DownloadManager.StartDownload(data.Id,
                                data.ResourceParent + Path.DirectorySeparatorChar + data.FileName,
                                (FileType)data.FileType, data.Length, data.Md5Hash, data.ResourceParent);
                            LogManager.DebugLog("FILE TYPE: " + (FileType)data.FileType);
                            LogManager.DebugLog("DOWNLOAD ACCEPTED: " + acceptDownload);
                            var newMsg = Client.CreateMessage();
                            newMsg.Write((byte)PacketType.FileAcceptDeny);
                            newMsg.Write(data.Id);
                            newMsg.Write(acceptDownload);
                            Client.SendMessage(newMsg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                        }
                        else
                        {
                            LogManager.DebugLog("DATA WAS NULL ON REQUEST");
                        }
                    }
                    break;
                case PacketType.FileTransferTick:
                    {
                        var channel = msg.ReadInt32();
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        DownloadManager.DownloadPart(channel, data);
                    }
                    break;
                case PacketType.FileTransferComplete:
                    {
                        var id = msg.ReadInt32();
                        DownloadManager.End(id);
                    }
                    break;
                case PacketType.ChatData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                        if (!string.IsNullOrEmpty(data?.Message))
                        {
                            Chat.AddMessage(data.Sender, data.Message);
                        }
                    }
                    break;
                case PacketType.ServerEvent:
                    {
                        var len = msg.ReadInt32();
                        if (DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) is SyncEvent data)
                        {
                            Ped PlayerChar = Game.Player.Character;
                            var args = DecodeArgumentListPure(data.Arguments?.ToArray() ?? new NativeArgument[0]).ToList();
                            switch ((ServerEventType)data.EventType)
                            {
                                case ServerEventType.PlayerSpectatorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var spectating = (bool)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            if (NetEntityHandler.NetToStreamedItem(netHandle) is SyncPed pair)
                                            {
                                                pair.IsSpectating = spectating;
                                                if (spectating)
                                                    pair.Clear();
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == PlayerChar.Handle)
                                        {
                                            IsSpectating = spectating;
                                            if (spectating)
                                                _preSpectatorPos = PlayerChar.Position;
                                            if (spectating && args.Count >= 3)
                                            {
                                                var target = (int)args[2];
                                                SpectatingEntity = target;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipColorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newColor = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            if (NetEntityHandler.NetToStreamedItem(netHandle) is SyncPed pair)
                                            {
                                                pair.BlipColor = newColor;
                                                if (pair.Character != null &&
                                                    pair.Character.AttachedBlip != null)
                                                {
                                                    pair.Character.AttachedBlip.Color = (BlipColor)newColor;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipSpriteChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newSprite = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            if (NetEntityHandler.NetToStreamedItem(netHandle) is SyncPed pair)
                                            {
                                                pair.BlipSprite = newSprite;
                                                if (pair.Character != null && pair.Character.AttachedBlip != null)
                                                    pair.Character.AttachedBlip.Sprite =
                                                        (BlipSprite)newSprite;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipAlphaChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newAlpha = (int)args[1];
                                        if (NetEntityHandler.NetToStreamedItem(netHandle) is SyncPed pair)
                                        {
                                            pair.BlipAlpha = (byte)newAlpha;
                                            if (pair.Character != null &&
                                                pair.Character.AttachedBlip != null)
                                                pair.Character.AttachedBlip.Alpha = newAlpha;
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerTeamChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newTeam = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            if (NetEntityHandler.NetToStreamedItem(netHandle) is SyncPed pair)
                                            {
                                                pair.Team = newTeam;
                                                if (pair.Character != null)
                                                    pair.Character.RelationshipGroup = (newTeam == LocalTeam &&
                                                                                                newTeam != -1)
                                                        ? Main.FriendRelGroup
                                                        : Main.RelGroup;
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == PlayerChar.Handle)
                                        {
                                            LocalTeam = newTeam;
                                            foreach (var opponent in NetEntityHandler.ClientMap.Values.Where(item => item is SyncPed && ((SyncPed)item).LocalHandle != -2).Cast<SyncPed>())
                                            {
                                                if (opponent.Character != null &&
                                                    (opponent.Team == newTeam && newTeam != -1))
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.FriendRelGroup;
                                                }
                                                else if (opponent.Character != null)
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.RelGroup;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStart:
                                    {
                                        var netHandle = (int)args[0];
                                        var animFlag = (int)args[1];
                                        var animDict = (string)args[2];
                                        var animName = (string)args[3];

                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null && pair.Character != null && pair.Character.Exists())
                                            {
                                                pair.IsCustomAnimationPlaying = true;
                                                pair.CustomAnimationName = animName;
                                                pair.CustomAnimationDictionary = animDict;
                                                pair.CustomAnimationFlag = animFlag;
                                                pair.CustomAnimationStartTime = Util.Util.TickCount;

                                                if (!string.IsNullOrEmpty(animName) &&
                                                    string.IsNullOrEmpty(animDict))
                                                {
                                                    pair.IsCustomScenarioPlaying = true;
                                                    pair.HasCustomScenarioStarted = false;
                                                }
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == PlayerChar.Handle)
                                        {
                                            AnimationFlag = 0;
                                            CustomAnimation = null;

                                            if (string.IsNullOrEmpty(animDict))
                                            {
                                                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, PlayerChar, animName, 0, 0);
                                            }
                                            else
                                            {
                                                Function.Call(Hash.TASK_PLAY_ANIM, PlayerChar,
                                                    Util.Util.LoadDict(animDict), animName, 8f, 10f, -1, animFlag, -8f, 1, 1, 1);
                                                if ((animFlag & 1) != 0)
                                                {
                                                    CustomAnimation = animDict + " " + animName;
                                                    AnimationFlag = animFlag;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStop:
                                    {
                                        var netHandle = (int)args[0];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != PlayerChar.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null && pair.Character != null && pair.Character.Exists() && pair.IsCustomAnimationPlaying)
                                            {
                                                pair.Character.Task.ClearAll();
                                                pair.IsCustomAnimationPlaying = false;
                                                pair.CustomAnimationName = null;
                                                pair.CustomAnimationDictionary = null;
                                                pair.CustomAnimationFlag = 0;
                                                pair.IsCustomScenarioPlaying = false;
                                                pair.HasCustomScenarioStarted = false;

                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == PlayerChar.Handle)
                                        {
                                            PlayerChar.Task.ClearAll();
                                            AnimationFlag = 0;
                                            CustomAnimation = null;
                                        }
                                    }
                                    break;
                                case ServerEventType.EntityDetachment:
                                    {
                                        var netHandle = (int)args[0];
                                        bool col = (bool)args[1];
                                        NetEntityHandler.DetachEntity(NetEntityHandler.NetToStreamedItem(netHandle), col);
                                    }
                                    break;
                                case ServerEventType.WeaponPermissionChange:
                                    {
                                        var isSingleWeaponChange = (bool)args[0];

                                        if (isSingleWeaponChange)
                                        {
                                            var hash = (int)args[1];
                                            var hasPermission = (bool)args[2];

                                            if (hasPermission) WeaponInventoryManager.Allow((GTANetworkShared.WeaponHash)hash);
                                            else WeaponInventoryManager.Deny((GTANetworkShared.WeaponHash)hash);
                                        }
                                        else
                                        {
                                            WeaponInventoryManager.Clear();
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.SyncEvent:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                        if (data != null)
                        {
                            var args = DecodeArgumentList(data.Arguments.ToArray()).ToList();
                            if (args.Count > 0)
                                LogManager.DebugLog("RECEIVED SYNC EVENT " + ((SyncEventType)data.EventType) + ": " + args.Aggregate((f, s) => f.ToString() + ", " + s.ToString()));
                            switch ((SyncEventType)data.EventType)
                            {
                                case SyncEventType.LandingGearChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newState = (int)args[1];
                                        if (veh == null) return;
                                        Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, veh, newState);
                                    }
                                    break;
                                case SyncEventType.DoorStateChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var doorId = (int)args[1];
                                        var newFloat = (bool)args[2];
                                        if (veh == null) return;
                                        if (newFloat)
                                            new Vehicle(veh.Handle).Doors[(VehicleDoorIndex)doorId].Open(false, true);
                                        else
                                            new Vehicle(veh.Handle).Doors[(VehicleDoorIndex)doorId].Close(true);

                                        var item = NetEntityHandler.NetToStreamedItem((int)args[0]) as RemoteVehicle;
                                        if (item != null)
                                        {
                                            if (newFloat)
                                                item.Tires |= (byte)(1 << doorId);
                                            else
                                                item.Tires &= (byte)~(1 << doorId);
                                        }
                                    }
                                    break;
                                case SyncEventType.BooleanLights:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var lightId = (Lights)(int)args[1];
                                        var state = (bool)args[2];
                                        if (veh == null) return;
                                        if (lightId == Lights.NormalLights)
                                            new Vehicle(veh.Handle).AreLightsOn = state;
                                        else if (lightId == Lights.Highbeams)
                                            Function.Call(Hash.SET_VEHICLE_FULLBEAM, veh.Handle, state);
                                    }
                                    break;
                                case SyncEventType.TrailerDeTach:
                                    {
                                        var newState = (bool)args[0];
                                        if (!newState)
                                        {
                                            var vObj =
                                                NetEntityHandler.NetToStreamedItem((int)args[1]) as RemoteVehicle;
                                            var tObj = NetEntityHandler.NetToStreamedItem(vObj.Trailer) as RemoteVehicle;

                                            vObj.Trailer = 0;
                                            if (tObj != null) tObj.TraileredBy = 0;

                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            if (car != null)
                                            {
                                                if ((VehicleHash)car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash)car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    var trailer = Function.Call<Vehicle>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_TOW_TRUCK, trailer);
                                                }
                                                else if ((VehicleHash)car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    var trailer =
                                                        Function.Call<Vehicle>(Hash.GET_VEHICLE_ATTACHED_TO_CARGOBOB,
                                                            car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_CARGOBOB, trailer);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, car.Handle);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var vObj =
                                                NetEntityHandler.NetToStreamedItem((int)args[1]) as RemoteVehicle;
                                            var tObj = NetEntityHandler.NetToStreamedItem((int)args[2]) as RemoteVehicle;

                                            vObj.Trailer = (int)args[2];
                                            if (tObj != null) tObj.TraileredBy = (int)args[1];

                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            var trailer = NetEntityHandler.NetToEntity((int)args[2]);
                                            if (car != null && trailer != null)
                                            {
                                                if ((VehicleHash)car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash)car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, car, trailer, true, 0, 0, 0);
                                                }
                                                else if ((VehicleHash)car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    new Vehicle(car.Handle).DropCargobobHook(CargobobHook.Hook);
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, car, 0, 0, 0, 0);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, car, trailer, 4f);
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case SyncEventType.TireBurst:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var tireId = (int)args[1];
                                        var isBursted = (bool)args[2];
                                        if (veh == null) return;
                                        if (isBursted)
                                            new Vehicle(veh.Handle).Wheels[tireId].Burst();
                                        else
                                            new Vehicle(veh.Handle).Wheels[tireId].Fix();

                                        var item = NetEntityHandler.NetToStreamedItem((int)args[0]) as RemoteVehicle;
                                        if (item != null)
                                        {
                                            if (isBursted)
                                                item.Tires |= (byte)(1 << tireId);
                                            else
                                                item.Tires &= (byte)~(1 << tireId);
                                        }
                                    }
                                    break;
                                case SyncEventType.RadioChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newRadio = (int)args[1];
                                        if (veh != null)
                                        {
                                            var rad = (RadioStation)newRadio;
                                            string radioName = "OFF";
                                            if (rad != RadioStation.RadioOff)
                                            {
                                                radioName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME,
                                                    newRadio);
                                            }
                                            Function.Call(Hash.SET_VEH_RADIO_STATION, veh, radioName);
                                        }
                                    }
                                    break;
                                case SyncEventType.PickupPickedUp:
                                    {
                                        var pickupItem = NetEntityHandler.NetToStreamedItem((int)args[0]);
                                        if (pickupItem != null)
                                        {
                                            NetEntityHandler.StreamOut(pickupItem);
                                            NetEntityHandler.Remove(pickupItem);
                                        }
                                    }
                                    break;
                                case SyncEventType.StickyBombDetonation:
                                    {
                                        var playerId = (int)args[0];
                                        var syncP = NetEntityHandler.NetToStreamedItem(playerId) as SyncPed;

                                        if (syncP != null && syncP.StreamedIn && syncP.Character != null)
                                        {
                                            Function.Call(Hash.EXPLODE_PROJECTILES, syncP.Character, (int)WeaponHash.StickyBomb, true);
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.PlayerDisconnect:
                    {
                        var len = msg.ReadInt32();

                        var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                        SyncPed target = null;
                        if (data != null && (target = NetEntityHandler.NetToStreamedItem(data.Id) as SyncPed) != null)
                        {
                            NetEntityHandler.StreamOut(target);
                            target.Clear();
                            lock (Npcs)
                            {
                                foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id))
                                {
                                    Npcs.Remove(pair.Key);
                                    pair.Value.Clear();
                                }
                            }
                        }
                        if (data != null) NetEntityHandler.RemoveByNetHandle(data.Id);
                    }
                    break;
                case PacketType.ScriptEventTrigger:
                    {
                        var len = msg.ReadInt32();
                        var data =
                            DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                        if (data != null)
                        {
                            if (data.Arguments != null && data.Arguments.Count > 0)
                                JavascriptHook.InvokeServerEvent(data.EventName, data.Resource,
                                    DecodeArgumentListPure(data.Arguments?.ToArray()).ToArray());
                            else
                                JavascriptHook.InvokeServerEvent(data.EventName, data.Resource, new object[0]);
                        }
                    }
                    break;
                case PacketType.NativeCall:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                        if (data == null) return;
                        LogManager.DebugLog("RECEIVED NATIVE CALL " + data.Hash);
                        DecodeNativeCall(data);
                    }
                    break;
                case PacketType.DeleteObject:
                    {
                        var len = msg.ReadInt32();
                        var data = (ObjectData)DeserializeBinary<ObjectData>(msg.ReadBytes(len));
                        if (data == null) return;
                        DeleteObject(data.Position, data.Radius, data.modelHash);
                    }
                    break;
            }
            #endregion
        }

        public void ProcessMessages(NetIncomingMessage msg, bool safeThreaded)
        {
            var type = PacketType.WorldSharingStop;
            //LogManager.DebugLog("RECEIVED MESSAGE " + msg.MessageType);
            try
            {
                MessagesReceived++;
                BytesReceived += msg.LengthBytes;
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Data:
                        type = (PacketType)msg.ReadByte();
                        ProcessDataMessage(msg, type);
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        Latency = msg.ReadFloat();
                        break;
                    case NetIncomingMessageType.StatusChanged:

                        #region StatusChanged
                        var newStatus = (NetConnectionStatus)msg.ReadByte();
                        //LogManager.DebugLog("NEW STATUS: " + newStatus);
                        switch (newStatus)
                        {
                            case NetConnectionStatus.InitiatedConnect:
                                Util.Util.SafeNotify("Connecting...");
                                /*World.RenderingCamera = null;*/
                                LocalTeam = -1;
                                LocalDimension = 0;
                                ResetPlayer();
                                CEFManager.Initialize(Main.screen);
                                StringCache?.Dispose();

                                StringCache = new StringCache();
                                break;
                            case NetConnectionStatus.Connected:
                                foreach (var i in InternetList)
                                {
                                    var spl = i.Split(':');
                                    if (_currentServerIp == Dns.GetHostAddresses(spl[0])[0].ToString()) _currentServerIp = spl[0];
                                }
                                AddServerToRecent(_currentServerIp + ":" + _currentServerPort);
                                Util.Util.SafeNotify("Connection established!");
                                var respLen = msg.SenderConnection.RemoteHailMessage.ReadInt32();
                                var respObj = DeserializeBinary<ConnectionResponse>(msg.SenderConnection.RemoteHailMessage.ReadBytes(respLen)) as ConnectionResponse;

                                if (respObj == null)
                                {
                                    Util.Util.SafeNotify("ERROR WHILE READING REMOTE HAIL MESSAGE");
                                    return;
                                }

                                NetEntityHandler.AddLocalCharacter(respObj.CharacterHandle);

                                var confirmObj = Client.CreateMessage();
                                confirmObj.Write((byte)PacketType.ConnectionConfirmed);
                                confirmObj.Write(false);
                                Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                                JustJoinedServer = true;

                                MainMenu.Tabs.Remove(_welcomePage);

                                if (!MainMenu.Tabs.Contains(_serverItem)) MainMenu.Tabs.Insert(0, _serverItem);
                                if (!MainMenu.Tabs.Contains(_mainMapItem)) MainMenu.Tabs.Insert(0, _mainMapItem);

                                MainMenu.RefreshIndex();

                                if (respObj.Settings != null)
                                {
                                    //OnFootLagCompensation = respObj.Settings.OnFootLagCompensation;
                                    //VehicleLagCompensation = respObj.Settings.VehicleLagCompensation;

                                    HTTPFileServer = respObj.Settings.UseHttpServer;

                                    if (respObj.Settings.ModWhitelist != null)
                                    {
                                        if (!DownloadManager.ValidateExternalMods(respObj.Settings.ModWhitelist))
                                        {
                                            Client.Disconnect("");
                                            MainMenu.Visible = false;
                                            _mainWarning = new Warning("Failed to connect", "Unallowed mods!\nThe server has strictly disallowed the use of non-whitelisted mods.")
                                            {
                                                OnAccept = () => { _mainWarning.Visible = false; MainMenu.Visible = true; }
                                            };
                                        }

                                    }
                                }

                                if (ParseableVersion.Parse(respObj.ServerVersion) < VersionCompatibility.LastCompatibleServerVersion)
                                {
                                    Client.Disconnect("");
                                    MainMenu.Visible = false;
                                    _mainWarning = new Warning("Failed to connect", "Outdated server!\nPlease inform the server administrator of the issue.")
                                    {
                                        OnAccept = () => { _mainWarning.Visible = false; }
                                    };

                                }

                                if (HTTPFileServer)
                                {
                                    StartFileDownload($"http://{_currentServerIp}:{_currentServerPort}");

                                    if (Main.JustJoinedServer)
                                    {
                                        World.RenderingCamera = null;
                                        Main.MainMenu.TemporarilyHidden = false;
                                        Main.MainMenu.Visible = false;
                                        Main.JustJoinedServer = false;
                                    }
                                }
                                break;
                            case NetConnectionStatus.Disconnected:
                                var reason = msg.ReadString();

                                OnLocalDisconnect();
                                if (!string.IsNullOrEmpty(reason) && reason != "Quit" && reason != "Switching servers")
                                {
                                    MainMenu.Visible = false;
                                    _mainWarning = new Warning("Disconnected", reason)
                                    {
                                        OnAccept = () =>
                                        {
                                            _mainWarning.Visible = false;
                                            MainMenu.Visible = true;
                                        }
                                    };
                                }
                                else
                                {
                                    Util.Util.SafeNotify("Disconnected: " + reason);
                                }
                                break;
                        }
                        break;

                        #endregion

                    case NetIncomingMessageType.DiscoveryResponse:

                        #region DiscoveryResponse
                        msg.ReadByte();
                        var len = msg.ReadInt32();
                        var bin = msg.ReadBytes(len);
                        var data = DeserializeBinary<DiscoveryResponse>(bin) as DiscoveryResponse;
                        if (data == null) return;

                        var itemText = msg.SenderEndPoint.Address + ":" + data.Port;

                        foreach (var i in InternetList)
                        {
                            var spl = i.Split(':');
                            if (msg.SenderEndPoint.Address.ToString() == Dns.GetHostAddresses(spl[0])[0].ToString()) itemText = i;
                        }

                        var gamemode = Regex.Replace(data.Gamemode, @"(~.*?~|~|'|""|∑|\\|¦)", string.Empty);
                        var name = Regex.Replace(data.ServerName, @"(∑|¦|\\|%|$|^|')", string.Empty);

                        if (string.IsNullOrWhiteSpace(gamemode)) gamemode = "freeroam";
                        if (string.IsNullOrWhiteSpace(name)) name = "Simple GTA Network Server";

                        var map = string.Empty;
                        if (!string.IsNullOrWhiteSpace(data.Map)) map = " (" + Regex.Replace(data.Map, @"(~.*?~|~|<|>|'|""|∑|\\|¦)", string.Empty) + ")";

                        var ourItem = new UIMenuItem(itemText) {  Description = itemText, Text = name };

                        ourItem.SetRightLabel(gamemode + map + " - " + data.PlayerCount + "/" + data.MaxPlayers);

                        if (PlayerSettings.FavoriteServers.Contains(ourItem.Description)) ourItem.SetRightBadge(UIMenuItem.BadgeStyle.Star);

                        if (data.PasswordProtected) ourItem.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                        if (ourItem.Text != itemText && ourItem.Text != ourItem.Description)
                        {
                            var gMsg = msg;
                            ourItem.Activated += (sender, selectedItem) =>
                            {
                                if (IsOnServer())
                                {
                                    Client.Disconnect("Switching servers");

                                    NetEntityHandler.ClearAll();

                                    if (Npcs != null)
                                    {
                                        lock (Npcs)
                                        {
                                            for (var index = Npcs.ToList().Count - 1; index >= 0; index--)
                                            {
                                                Npcs.ToList()[index].Value.Clear();
                                            }
                                            Npcs.Clear();
                                        }
                                    }
                                    while (IsOnServer()) Script.Yield();
                                }
                                var pass = data.PasswordProtected;
                                ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port, pass);
                                MainMenu.TemporarilyHidden = true;
                                _connectTab.RefreshIndex();
                            };
                        }


                        if (!_serverBrowser.Items.Contains(ourItem))
                        {
                            if (_serverBrowser.Items.Any(i => i.Description.GetBetween("", ":") == Dns.GetHostAddresses(ourItem.Description.GetBetween("", ":"))[0].ToString())) _serverBrowser.Items.Remove(_serverBrowser.Items.First(i => i.Description.GetBetween("", ":") == Dns.GetHostAddresses(ourItem.Description.GetBetween("", ":"))[0].ToString()));
                            _serverBrowser.Items.Insert(_serverBrowser.Items.Count, ourItem);
                        }
                        if (ListSorting)
                        {
                            try
                            {
                                _serverBrowser.Items = _serverBrowser.Items
                                    .OrderByDescending(o => Convert.ToInt32(o.RightLabel.GetBetween(" - ", "/")))
                                    .ToList();
                                
                            }
                            catch (FormatException)
                            {
                                //Ignored
                            }
                        }
                        _serverBrowser.RefreshIndex();

                        if (!_Verified.Items.Contains(ourItem) && VerifiedList.Contains(itemText))
                        {
                            _Verified.Items.Insert(_Verified.Items.Count, ourItem);
                        }

                        if (PlayerSettings.FavoriteServers.Contains(itemText))
                        {
                            if (_favBrowser.Items.Any(i => i.Description == ourItem.Description)) _favBrowser.Items.Remove(_favBrowser.Items.FirstOrDefault(i => i.Description == ourItem.Description));
                            _favBrowser.Items.Insert(_favBrowser.Items.Count, ourItem);
                        }

                        if (PlayerSettings.RecentServers.Contains(itemText))
                        {
                            if (_recentBrowser.Items.Any(i => i.Description == ourItem.Description)) _recentBrowser.Items.Remove(_recentBrowser.Items.FirstOrDefault(i => i.Description == ourItem.Description));
                            if (_recentBrowser.Items.Any(i => i.Description.GetBetween("", ":") == Dns.GetHostAddresses(ourItem.Description.GetBetween("", ":"))[0].ToString())) _recentBrowser.Items.Remove(_recentBrowser.Items.FirstOrDefault(i => i.Description.GetBetween("", ":") == Dns.GetHostAddresses(ourItem.Description.GetBetween("", ":"))[0].ToString()));
                            _recentBrowser.Items.Insert(_recentBrowser.Items.Count, ourItem);
                        }

                        if (isIPLocal(msg.SenderEndPoint.Address.ToString()) && !_lanBrowser.Items.Contains(ourItem) && _lanBrowser.Items.All(i => i.Description != ourItem.Description))
                        {
                            _lanBrowser.Items.Insert(_lanBrowser.Items.Count, ourItem);
                        }

                        break;

                        #endregion
                }
            }
            catch (Exception e)
            {
                if (safeThreaded)
                {
                    Util.Util.SafeNotify("Unhandled Exception ocurred in Process Messages");
                    Util.Util.SafeNotify("Message Type: " + msg.MessageType);
                    Util.Util.SafeNotify("Data Type: " + type);
                    Util.Util.SafeNotify(e.Message);
                }
                LogManager.LogException(e, "PROCESS MESSAGES (TYPE: " + msg.MessageType + " DATATYPE: " + type + ")");
            }
        }

    }
}
