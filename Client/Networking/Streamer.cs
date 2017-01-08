using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using GTA;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetworkShared;
using NativeUI;
using Vector3 = GTA.Math.Vector3;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Networking
{
    internal class StreamerThread : Script
    {
        private List<IStreamedItem> _itemsToStreamIn;
        private List<IStreamedItem> _itemsToStreamOut;
        private Vector3 _playerPosition;

        public static SyncPed[] StreamedInPlayers;

        public StreamerThread()
        {
            _itemsToStreamIn = new List<IStreamedItem>();
            _itemsToStreamOut = new List<IStreamedItem>();
            StreamedInPlayers = new SyncPed[MAX_PLAYERS];

            Tick += StreamerTick;

            System.Threading.Thread calcucationThread = new System.Threading.Thread(StreamerCalculationsThread);
            calcucationThread.IsBackground = true;
            calcucationThread.Start();
        }

        public static int MAX_OBJECTS = 1000;
        public static int MAX_VEHICLES = 50;
        public static int MAX_PICKUPS = 30;
        public static int MAX_BLIPS = 100;
        public static int MAX_PLAYERS = 40;
        public static int MAX_PEDS = 10;
        public static int MAX_MARKERS = 100;
        public static int MAX_LABELS = 20;
        public static int MAX_PARTICLES = 50;

        void StreamerCalculationsThread()
        {
            while (true)
            {
                if (!Main.IsOnServer() || !Main.HasFinishedDownloading) goto endTick;

                var copyMap = new List<IStreamedItem>(Main.NetEntityHandler.ClientMap.Values);

                var streamedItems = copyMap.Where(item => (item as RemotePlayer) == null || (item as RemotePlayer).LocalHandle != -2);

                var position = _playerPosition.ToLVector();

                var streamedObjects = streamedItems.OfType<RemoteProp>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedVehicles = streamedItems.OfType<RemoteVehicle>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedPickups = streamedItems.OfType<RemotePickup>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedBlips = streamedItems.OfType<RemoteBlip>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedPlayers = streamedItems.OfType<SyncPed>().Where(item => item.Dimension == Main.LocalDimension || item.Dimension == 0).OrderBy(item => item.Position.DistanceToSquared(_playerPosition));
                var streamedMarkers = streamedItems.OfType<RemoteMarker>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedLabels = streamedItems.OfType<RemoteTextLabel>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedPeds = streamedItems.OfType<RemotePed>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedParticles = streamedItems.OfType<RemoteParticle>().Where(item => item.Position != null && (item.Dimension == Main.LocalDimension || item.Dimension == 0)).OrderBy(item => item.Position.DistanceToSquared(position));

                var dimensionLeftovers = streamedItems.Where(item => item.StreamedIn && item.Dimension != Main.LocalDimension && item.Dimension != 0);

                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedBlips.Skip(MAX_BLIPS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedPickups.Skip(MAX_PICKUPS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedVehicles.Skip(MAX_VEHICLES).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedPlayers.Skip(MAX_PLAYERS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedObjects.Skip(MAX_OBJECTS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedMarkers.Skip(MAX_MARKERS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedLabels.Skip(MAX_LABELS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedPeds.Skip(MAX_PEDS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedParticles.Skip(MAX_PARTICLES).Where(item => item.StreamedIn));

                    _itemsToStreamOut.AddRange(dimensionLeftovers);
                }

                lock (_itemsToStreamIn)
                {
                    _itemsToStreamIn.AddRange(streamedObjects.Take(MAX_OBJECTS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedPickups.Take(MAX_PICKUPS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedVehicles.Take(MAX_VEHICLES).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedBlips.Take(MAX_BLIPS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedPlayers.Take(MAX_PLAYERS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedMarkers.Take(MAX_MARKERS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedLabels.Take(MAX_LABELS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedPeds.Take(MAX_PEDS).Where(item => !item.StreamedIn));
                    _itemsToStreamIn.AddRange(streamedParticles.Take(MAX_PARTICLES).Where(item => !item.StreamedIn));
                }

                lock (StreamedInPlayers)
                {
                    StreamedInPlayers = streamedPlayers.Take(MAX_PLAYERS).ToArray();
                }

                endTick:
                System.Threading.Thread.Sleep(1000);
            }
        }

        public static bool StreamInProgress { private set; get; }

        void StreamerTick(object sender, System.EventArgs e)
        {
            _playerPosition = Game.Player.Character.Position;
            if (Util.Util.ModelRequest) return;
            bool spinner = false;

            if (_itemsToStreamIn.Count > 0 || _itemsToStreamIn.Count > 0)
            {
                /*
                Function.Call((Hash)0xABA17D7CE615ADBF, "STRING");
                Function.Call((Hash)0x6C188BE134E074AA, "Streaming");
                Function.Call((Hash)0xBD12F8228410D9B4, 5);
                spinner = true;
                */

                StreamInProgress = true;
            }
            
            lock (_itemsToStreamOut)
            {
                LogManager.DebugLog("STREAMING OUT " + _itemsToStreamOut.Count + " ITEMS");

                foreach (var item in _itemsToStreamOut)
                {
                    if (Main.NetEntityHandler.ClientMap.Reverse.ContainsKey(item))
                        Main.NetEntityHandler.StreamOut(item);
                }

                _itemsToStreamOut.Clear();
            }

            lock (_itemsToStreamIn)
            {
                LogManager.DebugLog("STREAMING IN " + _itemsToStreamIn.Count + " ITEMS");
                
                foreach (var item in _itemsToStreamIn)
                {
                    if (Main.NetEntityHandler.ClientMap.Reverse.ContainsKey(item))
                        Main.NetEntityHandler.StreamIn(item);
                }

                _itemsToStreamIn.Clear();
            }

            /*
            if (spinner)
                Function.Call((Hash)0x10D373323E5B9C0D);
            */

            StreamInProgress = false;
        }
    }

    internal class Streamer
    {
        internal Streamer()
        {
            ClientMap = new BiDictionary<int, IStreamedItem>();
            HandleMap = new BiDictionary<int, int>();
        }

        private int _localHandleCounter = 0;

        public int Count(Type type)
        {
            return ClientMap.Count(item => item.GetType() == type);
        }

        public void UpdateAttachments()
        {
            var attaches = new List<EntityProperties>(ClientMap.Values.Where(item => item.StreamedIn && item.AttachedTo != null).Cast<EntityProperties>());

            foreach (var item in attaches)
            {
                var attachedTo = NetToStreamedItem(item.AttachedTo.NetHandle);

                if (attachedTo == null || !attachedTo.StreamedIn) continue;

                if (attachedTo.Position != null)
                {
                    item.Position = attachedTo.Position;
                }

                Entity entityTarget;
                if (attachedTo is ILocalHandleable && !(attachedTo is RemoteBlip))
                {
                    entityTarget = NetToEntity(attachedTo.RemoteHandle);
                }
                else
                {
                    continue;
                }
                item.Position = entityTarget.Position.ToLVector();

                if (item is ILocalHandleable && !(item is RemoteBlip))
                {
                    Entity us = NetToEntity(((IStreamedItem) item).RemoteHandle);

                    if (!Function.Call<bool>(Hash.IS_ENTITY_ATTACHED_TO_ENTITY, us, entityTarget))
                    {
                        AttachEntityToEntity(((IStreamedItem) item), attachedTo, item.AttachedTo);
                    }
                }
                else
                {
                    switch ((EntityType) item.EntityType)
                    {
                        case EntityType.Blip:
                        {
                            var blipHandle = new Blip((item as RemoteBlip).LocalHandle);
                            blipHandle.Position =
                                entityTarget.GetOffsetInWorldCoords(item.AttachedTo.PositionOffset.ToVector());
                        }
                            break;
                        case EntityType.Marker:
                        {
                            item.Position =
                                entityTarget.GetOffsetInWorldCoords(item.AttachedTo.PositionOffset.ToVector())
                                    .ToLVector();
                            item.Rotation = entityTarget.Rotation.ToLVector() + item.AttachedTo.RotationOffset;
                        }
                            break;
                        case EntityType.TextLabel:
                        {
                            item.Position =
                                entityTarget.GetOffsetInWorldCoords(item.AttachedTo.PositionOffset.ToVector())
                                    .ToLVector();

                            if (entityTarget.IsPed() && new Ped(entityTarget.Handle).IsInVehicle())
                            {
                                item.Position += (new Ped(entityTarget.Handle).CurrentVehicle.Velocity/Game.FPS).ToLVector();
                            }
                        }
                            break;
                    }
                }
            }
        }

        public void UpdateMisc()
        {
            var cars =
                new List<RemoteVehicle>(
                    ClientMap.Values.Where(
                        item => item.StreamedIn && item is RemoteVehicle && new Model(((RemoteVehicle) item).ModelHash).IsHelicopter)
                        .Cast<RemoteVehicle>());

            foreach (var remoteVehicle in cars)
            {
                if (PacketOptimization.CheckBit(remoteVehicle.Flag, EntityFlag.SpecialLight))
                {
                    Function.Call(Hash.SET_VEHICLE_SEARCHLIGHT, NetToEntity(remoteVehicle), true, true);
                }
            }
        }

        public void UpdateInterpolations()
        {
            var ents =
                new List<EntityProperties>(
                    ClientMap.Values.Where(item => item.PositionMovement != null || item.RotationMovement != null).Cast<EntityProperties>());

            foreach (var ent in ents)
            {
                if (ent.PositionMovement != null)
                {
                    if (ent.PositionMovement.ServerStartTime == 0) // Assume this is the first time we see the object
                        ent.PositionMovement.ServerStartTime = Util.Util.TickCount;

                    var delta = Util.Util.TickCount - ent.PositionMovement.ServerStartTime;
                    delta += ent.PositionMovement.Start;

                    ent.Position = GTANetworkShared.Vector3.Lerp(ent.PositionMovement.StartVector,
                        ent.PositionMovement.EndVector,
                        Math.Min(((float) delta/ent.PositionMovement.Duration), 1f));

                    var item = (IStreamedItem) ent;
                    if (item.StreamedIn)
                    {
                        switch ((EntityType)item.EntityType)
                        {
                            case EntityType.Prop:
                            case EntityType.Vehicle:
                            case EntityType.Player:
                                {
                                    var gameEnt = NetToEntity(item);
                                    if (gameEnt != null) gameEnt.PositionNoOffset = ent.Position.ToVector();
                                }
                                break;
                            case EntityType.Blip:
                                {
                                    var gameEnt = NetToEntity(item);
                                    if (gameEnt != null) new Blip(gameEnt.Handle).Position = ent.Position.ToVector();
                                }
                                break;
                        }
                    }

                    if (delta >= ent.PositionMovement.Duration) ent.PositionMovement = null;
                }

                if (ent.RotationMovement != null)
                {
                    if (ent.RotationMovement.ServerStartTime == 0) // Assume this is the first time we see the object
                        ent.RotationMovement.ServerStartTime = Util.Util.TickCount;

                    var delta = Util.Util.TickCount - ent.RotationMovement.ServerStartTime;
                    delta += ent.RotationMovement.Start;

                    ent.Rotation = GTANetworkShared.Vector3.Lerp(ent.RotationMovement.StartVector,
                        ent.RotationMovement.EndVector,
                        Math.Min(((float)delta / ent.RotationMovement.Duration), 1f));

                    var item = (IStreamedItem)ent;
                    if (item.StreamedIn)
                    {
                        switch ((EntityType)item.EntityType)
                        {
                            case EntityType.Prop:
                            case EntityType.Vehicle:
                            case EntityType.Player:
                                {
                                    var gameEnt = NetToEntity(item);
                                    if (gameEnt != null)
                                    {
                                        gameEnt.Quaternion = ent.Rotation.ToVector().ToQuaternion();
                                        //gameEnt.Rotation = ent.Rotation.ToVector(); // Gimbal lock!
                                    }
                                }
                                break;
                        }
                    }

                    if (delta >= ent.RotationMovement.Duration) ent.RotationMovement = null;
                }
            }
        }

        public void DrawMarkers()
        {
            var markers = new List<RemoteMarker>(ClientMap.Values.Where(item => item is RemoteMarker && item.StreamedIn).Cast<RemoteMarker>());

            foreach (var marker in markers)
            {
                World.DrawMarker((MarkerType)marker.MarkerType, marker.Position.ToVector(),
                    marker.Direction.ToVector(), marker.Rotation.ToVector(),
                    marker.Scale.ToVector(),
                    Color.FromArgb(marker.Alpha, marker.Red, marker.Green, marker.Blue));
            }

            
            // Uncomment to debug stuff
            /*
            
            foreach (var p in ClientMap.OfType<RemoteBlip>())
            {
                if (p == null || p.Position == null) continue;
                string text = (EntityType) p.EntityType + "\nId: " + p.RemoteHandle + "\nScale: " + p.Scale;

                
                DrawLabel3D(text, p.Position.ToVector(), 100f, 0.4f);
            }
            //*/
        }

        public void DrawLabels()
        {
            var labels = new List<RemoteTextLabel>(ClientMap.Values.Where(item => item is RemoteTextLabel && item.StreamedIn).Cast<RemoteTextLabel>());

            foreach (var label in labels)
            {
                DrawLabel3D(label.Text, label.Position.ToVector(), label.Range, label.Size,
                    Color.FromArgb(label.Alpha, label.Red, label.Green, label.Blue), label.EntitySeethrough);
            }
        }

        static internal void DrawLabel3D(string text, Vector3 position, float range, float size)
        {
            DrawLabel3D(text, position, range, size, Color.White, true);
        }

        static internal void DrawLabel3D(string text, Vector3 position, float range, float size, Color col, bool entitySeethrough)
        {
            Vector3 origin = GameplayCamera.Position;
            float distanceSquared = position.DistanceToSquared(origin);

            if (string.IsNullOrWhiteSpace(text) ||
                !Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, position.X, position.Y, position.Z, 1f) ||
                distanceSquared >= range * range) return;

            float distance = position.DistanceTo(origin);

            var flags = entitySeethrough
                ? IntersectOptions.Map | IntersectOptions.Vegetation
                : IntersectOptions.Everything;

            var ray = World.Raycast(origin,
                (position - origin).Normalized,
                distance,
                flags, Game.Player.Character);

            if (ray.HitPosition.DistanceTo(origin) >=
                    distance)
            {
                var scale = Math.Max(0.3f, 1f - (distance/range));
                
                Function.Call(Hash.SET_DRAW_ORIGIN, position.X, position.Y, position.Z);
                new UIResText(text, Point.Empty, size * scale, col)
                {
                    TextAlignment = UIResText.Alignment.Centered,
                    Outline = true
                }.Draw();
                Function.Call(Hash.CLEAR_DRAW_ORIGIN);
            }
        }

        public BiDictionary<int, IStreamedItem> ClientMap; // Global, IStreamedItem
        public BiDictionary<int, int> HandleMap; // Global, GameHandle

        public WorldProperties ServerWorld;
        

        public IStreamedItem EntityToStreamedItem(int gameHandle)
        {
            return NetToStreamedItem(gameHandle, useGameHandle: true);
        }

        public IStreamedItem NetToStreamedItem(int netId, bool local = false, bool useGameHandle = false)
        {
            if (!useGameHandle)
            {
                lock (ClientMap)
                {
                    if (ClientMap.ContainsKey(netId)) return ClientMap[netId];
                    return null;
                }
            }
            else
            {
                lock (ClientMap)
                {
                    if (HandleMap.Reverse.ContainsKey(netId))
                    {
                        int remId = HandleMap.Reverse[netId];
                        if (ClientMap.ContainsKey(remId))
                            return ClientMap[remId];
                    }

                    if (netId == Game.Player.Character.Handle)
                    {
                        netId = -2;
                        if (HandleMap.Reverse.ContainsKey(-2) && ClientMap.ContainsKey(HandleMap.Reverse[-2]))
                            return ClientMap[HandleMap.Reverse[-2]];
                    }

                    //return ClientMap.OfType<ILocalHandleable>().FirstOrDefault(item => item.LocalHandle == netId) as IStreamedItem;
                    return null;
                }
            }
        }

        public RemotePlayer LocalCharacter;
        public void AddLocalCharacter(int nethandle)
        {
            lock (ClientMap)
            {
                LocalCharacter = new RemotePlayer() {LocalHandle = -2, RemoteHandle = nethandle, StreamedIn = true};
                ClientMap.Add(nethandle, LocalCharacter);
                HandleMap.Add(nethandle, -2);
            }
        }

        public Entity NetToEntity(int netId)
        {
            lock (ClientMap)
            {
                var streamedItem = NetToStreamedItem(netId);
                var handleable = streamedItem as ILocalHandleable;
                if (streamedItem == null) return null;
                if (handleable == null) return new Prop(netId);
                if (handleable.LocalHandle == -2) return Game.Player.Character;
                if (!streamedItem.StreamedIn) return null;
                return new Prop(handleable.LocalHandle);
             }
        }

        public Entity NetToEntity(IStreamedItem netId)
        {
            lock (ClientMap)
            {
                var handleable = netId as ILocalHandleable;
                if (netId == null || handleable == null) return new Prop(netId?.RemoteHandle ?? 0);
                if (handleable.LocalHandle == -2) return Game.Player.Character;
                return new Prop(handleable.LocalHandle);
            }
        }

        public bool IsBlip(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) is RemoteBlip;
        }

        public bool IsPickup(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) is RemotePickup;
        }

        public bool ContainsNethandle(int netHandle)
        {
            return NetToStreamedItem(netHandle) != null;
        }

        public bool ContainsLocalOnlyNetHandle(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) != null;
        }

        public bool ContainsLocalHandle(int localHandle)
        {
            return NetToStreamedItem(localHandle, useGameHandle: true) != null;
        }

        public int EntityToNet(int entityHandle)
        {
            if (entityHandle == 0) return 0;
            if (entityHandle == Game.Player.Character.Handle)
                return HandleMap.Reverse[-2];
            lock (ClientMap)
            {
                if (HandleMap.Reverse.ContainsKey(entityHandle))
                    return HandleMap.Reverse[entityHandle];

                return entityHandle;
            }
        }
        
        public void Remove(IStreamedItem item)
        {
            lock (ClientMap)
            {
                if (item != null)
                {
                    ClientMap.Remove(item.RemoteHandle);
                    HandleMap.Remove(item.RemoteHandle);
                }
            }
        }

        public void RemoveByNetHandle(int netHandle)
        {
            lock (ClientMap)
            {
                Remove(NetToStreamedItem(netHandle));
            }
        }

        public void RemoveByLocalHandle(int localHandle)
        {
            lock (ClientMap) Remove(NetToStreamedItem(localHandle, true));
        }

        public bool IsLocalPlayer(IStreamedItem item)
        {
            if (item == null) return false;
            return NetToEntity(item.RemoteHandle)?.Handle == Game.Player.Character.Handle;
        }

        public void UpdateWorld(Delta_EntityProperties prop)
        {
            if (prop == null || ServerWorld == null) return;

            if (prop.Position != null) ServerWorld.Position = prop.Position;
            if (prop.Rotation != null) ServerWorld.Rotation = prop.Rotation;
            if (prop.ModelHash != null) ServerWorld.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) ServerWorld.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) ServerWorld.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) ServerWorld.Flag = prop.Flag.Value;

            if (prop.Dimension != null)
            {
                ServerWorld.Dimension = prop.Dimension.Value;
            }

            if (prop.Attachables != null) ServerWorld.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                ServerWorld.AttachedTo = prop.AttachedTo;
                
            }
            if (prop.SyncedProperties != null)
            {
                if (ServerWorld.SyncedProperties == null) ServerWorld.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        ServerWorld.SyncedProperties.Remove(pair.Key);
                    else
                    {
                        NativeArgument oldValue = ServerWorld.SyncedProperties.Get(pair.Key);

                        ServerWorld.SyncedProperties.Set(pair.Key, pair.Value);

                        JavascriptHook.InvokeDataChangeEvent(new LocalHandle(0), pair.Key, Main.DecodeArgumentListPure(oldValue).FirstOrDefault());
                    }
                }
            }
        }

        public void UpdateVehicle(int netHandle, Delta_VehicleProperties prop)
        {
            RemoteVehicle veh = null;
            if (prop == null || (veh = (NetToStreamedItem(netHandle) as RemoteVehicle)) == null) return;
            
            if (prop.PrimaryColor != null) veh.PrimaryColor = prop.PrimaryColor.Value;
            if (prop.SecondaryColor != null) veh.SecondaryColor = prop.SecondaryColor.Value;
            if (prop.Health != null) veh.Health = prop.Health.Value;
            if (prop.IsDead != null) veh.IsDead = prop.IsDead.Value;
            if (prop.Mods != null)
            {
                var oldMods = veh.Mods;
                veh.Mods = prop.Mods;
                if (veh.StreamedIn)
                {
                    var car = new Vehicle(NetToEntity(veh)?.Handle ?? 0);

                    if (car.Handle != 0)
                    foreach (var pair in prop.Mods.Where(pair => !oldMods.ContainsKey(pair.Key) || oldMods[pair.Key] != pair.Value))
                    {
                        if (pair.Key <= 60)
                        {
                            if (prop.Mods.ContainsKey(pair.Key))
                            {
                                if (pair.Key >= 17 && pair.Key <= 22)
                                    car.Mods[(VehicleToggleModType)pair.Key].IsInstalled = pair.Value != 0;
                                else
                                    car.SetMod(pair.Key, pair.Value, false);
                            }
                            else
                            {
                                Function.Call(Hash.REMOVE_VEHICLE_MOD, car, pair.Key);
                            }
                        }
                        else
                        {
                            Util.Util.SetNonStandardVehicleMod(car, pair.Key, pair.Value);
                        }
                    }
                }
            }
            if (prop.Siren != null) veh.Siren = prop.Siren.Value;
            if (prop.Doors != null) veh.Doors = prop.Doors.Value;
            if (prop.Trailer != null) veh.Trailer = prop.Trailer.Value;
            if (prop.TraileredBy != null) veh.TraileredBy = prop.TraileredBy.Value;
            if (prop.Tires != null) veh.Tires = prop.Tires.Value;
            if (prop.Livery != null) veh.Livery = prop.Livery.Value;
            if (prop.NumberPlate != null)
            {
                veh.NumberPlate = prop.NumberPlate;

                if (veh.StreamedIn && Regex.IsMatch(prop.NumberPlate, "^[a-zA-Z0-9]{0,9}$"))
                {
                    new Vehicle(veh.LocalHandle).Mods.LicensePlate = prop.NumberPlate;
                }
            }
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.VehicleComponents != null) veh.VehicleComponents = prop.VehicleComponents.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.DamageModel != null) veh.DamageModel = prop.DamageModel;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateTextLabel(int netHandle, Delta_TextLabelProperties prop)
        {
            RemoteTextLabel veh = null;
            if (prop == null || (veh = (NetToStreamedItem(netHandle) as RemoteTextLabel)) == null) return;

            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.Text != null) veh.Text = prop.Text;
            if (prop.Size != null) veh.Size = prop.Size.Value;
            if (prop.EntitySeethrough != null) veh.EntitySeethrough = prop.EntitySeethrough.Value;
            if (prop.Range != null) veh.Range = prop.Range.Value;
            if (prop.Red != null) veh.Red = prop.Red.Value;
            if (prop.Green != null) veh.Green = prop.Green.Value;
            if (prop.Blue != null) veh.Blue = prop.Blue.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdatePed(int netHandle, Delta_PedProperties prop)
        {
            RemotePed veh = null;
            if (prop == null || (veh = (NetToStreamedItem(netHandle) as RemotePed)) == null) return;

            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.LoopingAnimation != null) veh.LoopingAnimation = prop.LoopingAnimation;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateProp(int netHandle, Delta_EntityProperties prop)
        {
            IStreamedItem item = null;
            if (prop == null || (item = NetToStreamedItem(netHandle)) == null) return;
            var veh = item as EntityProperties;
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;

                RemotePlayer localPl = item as RemotePlayer;
                if (localPl != null && localPl.LocalHandle == -2)
                {
                    Main.LocalDimension = prop.Dimension.Value;
                }
                else if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    LogManager.DebugLog("ATTACHING THIS ENTITY (" + ((EntityType) veh.EntityType) + " id: " + netHandle + ") TO " + attachedTo.GetType());
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }

            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                    {
                        NativeArgument oldValue = veh.SyncedProperties.Get(pair.Key);

                        veh.SyncedProperties.Set(pair.Key, pair.Value);

                        var ent = new LocalHandle(NetToEntity(veh as IStreamedItem)?.Handle ?? 0);
                        if (!ent.IsNull)
                            JavascriptHook.InvokeDataChangeEvent(ent, pair.Key, Main.DecodeArgumentListPure(oldValue).FirstOrDefault());
                    }
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateBlip(int netHandle, Delta_BlipProperties prop)
        {
            IStreamedItem item = null;
            if (prop == null || (item = NetToStreamedItem(netHandle)) == null) return;
            var veh = item as RemoteBlip;
            if (prop.Sprite != null) veh.Sprite = prop.Sprite.Value;
            if (prop.Scale != null) veh.Scale = prop.Scale.Value;
            if (prop.Color != null) veh.Color = prop.Color.Value;
            if (prop.IsShortRange != null) veh.IsShortRange = prop.IsShortRange.Value;
            if (prop.AttachedNetEntity != null) veh.AttachedNetEntity = prop.AttachedNetEntity.Value;
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.RangedBlip != null) veh.RangedBlip = prop.RangedBlip.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.Name != null) veh.Name = prop.Name;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateMarker(int netHandle, Delta_MarkerProperties prop, bool localOnly = false)
        {
            IStreamedItem item = null;
            if (prop == null || (item = NetToStreamedItem(netHandle, local: localOnly)) == null) return;
            var veh = item as RemoteMarker;
            if (prop.Direction != null) veh.Direction = prop.Direction;
            if (prop.MarkerType != null) veh.MarkerType = prop.MarkerType.Value;
            if (prop.Red != null) veh.Red = prop.Red.Value;
            if (prop.Green != null) veh.Green = prop.Green.Value;
            if (prop.Blue != null) veh.Blue = prop.Blue.Value;
            if (prop.Scale != null) veh.Scale = prop.Scale;
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateParticle(int netHandle, Delta_ParticleProperties prop)
        {
            RemoteParticle veh = null;
            if (prop == null || (veh = (NetToStreamedItem(netHandle) as RemoteParticle)) == null) return;

            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.Name != null) veh.Name = prop.Name;
            if (prop.Library != null) veh.Library = prop.Library;
            if (prop.BoneAttached != null) veh.BoneAttached = prop.BoneAttached.Value;
            if (prop.Scale != null) veh.Scale = prop.Scale.Value;
            if (prop.EntityAttached != null) veh.EntityAttached = prop.EntityAttached.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdatePlayer(int netHandle, Delta_PlayerProperties prop)
        {
            LogManager.DebugLog("UPDATING PLAYER " + netHandle + " PROP NULL? " + (prop == null));

            if (IsLocalPlayer(NetToStreamedItem(netHandle)))
            {
                UpdateRemotePlayer(netHandle, prop);
                return;
            }

            if (prop == null) return;
            var veh = GetPlayer(netHandle);
            if (prop.Props != null) veh.Props = prop.Props;
            if (prop.Textures != null) veh.Textures = prop.Textures;
            if (prop.BlipSprite != null) veh.BlipSprite = prop.BlipSprite.Value;
            if (prop.Team != null) veh.Team = prop.Team.Value;
            if (prop.BlipColor != null) veh.BlipColor = prop.BlipColor.Value;
            if (prop.BlipAlpha != null) veh.BlipAlpha = prop.BlipAlpha.Value;
            if (prop.Accessories != null) veh.Accessories = prop.Accessories;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.WeaponTints != null)
            {
                veh.WeaponTints = prop.WeaponTints;
                veh.DirtyWeapons = true;
            }
            if (prop.WeaponComponents != null)
            {
                veh.WeaponComponents = prop.WeaponComponents;
                veh.DirtyWeapons = true;
            }
            if (prop.Name != null)
            {
                veh.Name = prop.Name;
                LogManager.DebugLog("New name: " + prop.Name);
            }
            if (prop.Position != null) veh.Position = prop.Position.ToVector();
            if (prop.Rotation != null) veh.Rotation = prop.Rotation.ToVector();
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;

            if (prop.NametagText != null) veh.NametagText = prop.NametagText;
            if (prop.NametagSettings != null) veh.NametagSettings = prop.NametagSettings.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdateRemotePlayer(int netHandle, Delta_PlayerProperties prop)
        {
            RemotePlayer veh = NetToStreamedItem(netHandle) as RemotePlayer;
            if (prop == null || veh == null) return;
            if (prop.Props != null) veh.Props = prop.Props;
            if (prop.Textures != null) veh.Textures = prop.Textures;
            if (prop.BlipSprite != null) veh.BlipSprite = prop.BlipSprite.Value;
            if (prop.Team != null) veh.Team = prop.Team.Value;
            if (prop.BlipColor != null) veh.BlipColor = prop.BlipColor.Value;
            if (prop.BlipAlpha != null) veh.BlipAlpha = prop.BlipAlpha.Value;
            if (prop.Accessories != null) veh.Accessories = prop.Accessories;
            if (prop.Name != null)
            {
                veh.Name = prop.Name;
                LogManager.DebugLog("New name: " + prop.Name);
            }
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.WeaponTints != null) veh.WeaponTints = prop.WeaponTints;
            if (prop.WeaponComponents != null) veh.WeaponComponents = prop.WeaponComponents;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.NametagText != null) veh.NametagText = prop.NametagText;
            if (prop.NametagSettings != null) veh.NametagSettings = prop.NametagSettings.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public void UpdatePickup(int netHandle, Delta_PickupProperties prop)
        {
            IStreamedItem item = null;
            if (prop == null || (item = NetToStreamedItem(netHandle)) == null) return;
            var veh = item as RemotePickup;
            if (prop.Amount != null) veh.Amount = prop.Amount.Value;
            if (prop.PickedUp != null) veh.PickedUp = prop.PickedUp.Value;
            if (prop.Position != null) veh.Position = prop.Position;
            if (prop.Rotation != null) veh.Rotation = prop.Rotation;
            if (prop.ModelHash != null) veh.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) veh.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) veh.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) veh.Flag = prop.Flag.Value;
            if (prop.IsInvincible != null) veh.IsInvincible = prop.IsInvincible.Value;
            if (prop.CustomModel != null) veh.CustomModel = prop.CustomModel.Value;

            if (prop.Dimension != null)
            {
                veh.Dimension = prop.Dimension.Value;
                if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        veh.SyncedProperties.Remove(pair.Key);
                    else
                        veh.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) veh.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) veh.RotationMovement = prop.RotationMovement;
        }

        public RemoteVehicle CreateVehicle(int model, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, int netHash)
        {
            short vehComp = ~0;
            if (model == unchecked((int)VehicleHash.Taxi))
                vehComp = 1 << 5;
            else if (model == (int)VehicleHash.Police)
                vehComp = 1 << 2;
            else if (model == (int)VehicleHash.Skylift)
                vehComp = -1537;

            RemoteVehicle rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHash, rem = new RemoteVehicle()
                {
                    RemoteHandle = netHash,
                    ModelHash = model,
                    Position = position,
                    Rotation = rotation,
                    StreamedIn = false,
                    LocalOnly = false,
                    IsDead = false,
                    Health = 1000,
                    Alpha = 255,
                    Livery = 0,
                    NumberPlate = "NETWORK",
                    EntityType = (byte)EntityType.Vehicle,
                    PrimaryColor = 0,
                    SecondaryColor = 0,
                    Dimension = 0,
                    VehicleComponents = vehComp,
                });
            }
            return rem;
        }

        public RemoteVehicle CreateVehicle(int netHandle, VehicleProperties prop)
        {
            RemoteVehicle rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteVehicle()
                {
                    RemoteHandle = netHandle,

                    PrimaryColor = prop.PrimaryColor,
                    SecondaryColor = prop.SecondaryColor,
                    Health = prop.Health,
                    IsDead = prop.IsDead,
                    Mods = prop.Mods,
                    Siren = prop.Siren,
                    Doors = prop.Doors,
                    Trailer = prop.Trailer,
                    TraileredBy = prop.TraileredBy,
                    Tires = prop.Tires,
                    Livery = prop.Livery,
                    NumberPlate = prop.NumberPlate,
                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    ModelHash = prop.ModelHash,
                    EntityType = prop.EntityType,
                    Dimension = prop.Dimension,
                    Alpha = prop.Alpha,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    IsInvincible = prop.IsInvincible,
                    Flag = prop.Flag,
                    VehicleComponents = prop.VehicleComponents,
                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,
                    DamageModel = prop.DamageModel,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemotePed CreatePed(int netHandle, PedProperties prop)
        {
            RemotePed rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemotePed()
                {
                    RemoteHandle = netHandle,

                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    ModelHash = prop.ModelHash,
                    EntityType = prop.EntityType,
                    Dimension = prop.Dimension,
                    Alpha = prop.Alpha,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    Flag = prop.Flag,
                    IsInvincible = prop.IsInvincible,
                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,

                    LoopingAnimation = prop.LoopingAnimation,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteProp CreateObject(int model, Vector3 position, Vector3 rotation, bool dynamic, int netHash)
        {
            RemoteProp rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHash, rem = new RemoteProp()
                {
                    RemoteHandle = netHash,
                    ModelHash = model,
                    EntityType = 2,
                    Position = position.ToLVector(),
                    Rotation = rotation.ToLVector(),
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteProp CreateObject(int netHandle, EntityProperties prop)
        {
            RemoteProp rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteProp()
                {
                    RemoteHandle = netHandle,

                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    Dimension = prop.Dimension,
                    ModelHash = prop.ModelHash,
                    EntityType = 2,
                    Alpha = prop.Alpha,
                    IsInvincible = prop.IsInvincible,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    Flag = prop.Flag,
                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteBlip CreateBlip(GTANetworkShared.Vector3 pos, int netHandle)
        {
            RemoteBlip rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteBlip()
                {
                    RemoteHandle = netHandle,
                    Position = pos,
                    StreamedIn = false,
                    LocalOnly = false,
                    Alpha = 255,
                    Dimension = 0,
                    Sprite = 0,
                    Scale = 1f,
                    AttachedNetEntity = 0,
                    EntityType = (byte) EntityType.Blip,
                });
            }
            return rem;
        }

        public RemoteBlip CreateBlip(int netHandle, BlipProperties prop)
        {
            RemoteBlip rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteBlip()
                {
                    RemoteHandle = netHandle,
                    SyncedProperties = prop.SyncedProperties,
                    Sprite = prop.Sprite,
                    Scale = prop.Scale,
                    Color = prop.Color,
                    Dimension = prop.Dimension,
                    IsShortRange = prop.IsShortRange,
                    AttachedNetEntity = prop.AttachedNetEntity,
                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    ModelHash = prop.ModelHash,
                    EntityType = (byte)EntityType.Blip,
                    Alpha = prop.Alpha,
                    IsInvincible = prop.IsInvincible,
                    RangedBlip = prop.RangedBlip,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,
                    Flag = prop.Flag,
                    Name = prop.Name,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteBlip CreateBlip(IStreamedItem entity, int netHandle)
        {
            RemoteBlip rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteBlip()
                {
                    RemoteHandle = netHandle,
                    AttachedNetEntity = entity.RemoteHandle,
                    EntityType = (byte)EntityType.Blip,
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public void CreateMarker(int type, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, GTANetworkShared.Vector3 dir, GTANetworkShared.Vector3 scale, int r, int g, int b, int a,
            int netHandle)
        {
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, new RemoteMarker()
                {
                    MarkerType = type,
                    Position = position,
                    Rotation = rotation,
                    Direction = dir,
                    Scale = scale,
                    Red = r,
                    Green = g,
                    Blue = b,
                    Alpha = (byte)a,
                    RemoteHandle = netHandle,
                    EntityType = (byte)EntityType.Marker,
                });
            }
        }

        public RemoteMarker CreateMarker(int netHandle, MarkerProperties prop)
        {
            RemoteMarker rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteMarker()
                {
                    RemoteHandle = netHandle,

                    Direction = prop.Direction,
                    MarkerType = prop.MarkerType,
                    Red = prop.Red,
                    Green = prop.Green,
                    Blue = prop.Blue,
                    Scale = prop.Scale,
                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    Dimension = prop.Dimension,
                    ModelHash = prop.ModelHash,
                    EntityType = (byte)EntityType.Marker,
                    Alpha = prop.Alpha,
                    IsInvincible = prop.IsInvincible,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    Flag = prop.Flag,

                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteTextLabel CreateTextLabel(int netHandle, TextLabelProperties prop)
        {
            RemoteTextLabel rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteTextLabel()
                {
                    RemoteHandle = netHandle,

                    Red = prop.Red,
                    Green = prop.Green,
                    Blue = prop.Blue,
                    Alpha = prop.Alpha,
                    Size = prop.Size,
                    Position = prop.Position,
                    Dimension = prop.Dimension,
                    EntityType = (byte)EntityType.TextLabel,
                    Text = prop.Text,
                    Range = prop.Range,
                    IsInvincible = prop.IsInvincible,
                    EntitySeethrough = prop.EntitySeethrough,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,

                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,

                    StreamedIn = false,
                    LocalOnly = false,
                    Flag = prop.Flag,
                });
            }
            return rem;
        }

        public RemoteParticle CreateParticle(int netHandle, ParticleProperties prop)
        {
            RemoteParticle rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemoteParticle()
                {
                    RemoteHandle = netHandle,

                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    ModelHash = prop.ModelHash,
                    EntityType = prop.EntityType,
                    Dimension = prop.Dimension,
                    Alpha = prop.Alpha,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    IsInvincible = prop.IsInvincible,
                    Flag = prop.Flag,
                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,
                    Library = prop.Library,
                    Name = prop.Name,
                    EntityAttached = prop.EntityAttached,
                    BoneAttached = prop.BoneAttached,
                    Scale = prop.Scale,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        internal SyncPed GetPlayer(int netHandle)
        {
            SyncPed rem = NetToStreamedItem(netHandle) as SyncPed;
            if (rem == null)
            {
                lock (ClientMap)
                {
                    ClientMap.Add(netHandle, rem = new SyncPed()
                    {
                        RemoteHandle = netHandle,
                        EntityType = (byte) EntityType.Player,
                        StreamedIn = false, // change me
                        LocalOnly = false,

                        BlipSprite = -1,
                        BlipColor = -1,
                        BlipAlpha = 255,
                        Alpha = 255,
                        Team = -1,
                    });
                }
            }
            return rem;
        }

        public void UpdatePlayer(int netHandle, PlayerProperties prop)
        {
            RemotePlayer rem = NetToStreamedItem(netHandle) as RemotePlayer;
            if (rem == null) return;
            
            rem.Props = prop.Props;
            rem.Textures = prop.Textures;
            rem.Team = prop.Team;
            rem.BlipSprite = prop.BlipSprite;
            rem.BlipColor = prop.BlipColor;
            rem.BlipAlpha = prop.BlipAlpha;
            rem.Accessories = prop.Accessories;
            rem.Name = prop.Name;
            rem.ModelHash = prop.ModelHash;
            rem.EntityType = prop.EntityType;
            rem.Alpha = prop.Alpha;
            rem.Dimension = prop.Dimension;
            rem.RemoteHandle = netHandle;
            rem.IsInvincible = prop.IsInvincible;
            rem.SyncedProperties = prop.SyncedProperties;
            rem.AttachedTo = prop.AttachedTo;
            rem.Attachables = prop.Attachables;
            rem.Flag = prop.Flag;
            rem.PositionMovement = prop.PositionMovement;
            rem.RotationMovement = prop.RotationMovement;
            rem.WeaponTints = prop.WeaponTints;
            rem.WeaponComponents = prop.WeaponComponents;
            rem.NametagText = prop.NametagText;
            rem.NametagSettings = prop.NametagSettings;
            
            if (rem is SyncPed)
            {
                if (prop.Position != null)
                    ((SyncPed)rem).Position = prop.Position.ToVector();
                if (prop.Rotation != null)
                    ((SyncPed)rem).Rotation = prop.Rotation.ToVector();

                ((SyncPed) rem).DirtyWeapons = true;
            }
        }

        public RemotePickup CreatePickup(Vector3 pos, Vector3 rot, int pickupHash, int amount, int netHandle)
        {
            RemotePickup rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemotePickup()
                {
                    RemoteHandle = netHandle,
                    Position = pos.ToLVector(),
                    Rotation = rot.ToLVector(),
                    ModelHash = pickupHash,
                    Amount = amount,
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemotePickup CreatePickup(int netHandle, PickupProperties prop)
        {
            RemotePickup rem;
            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem = new RemotePickup()
                {
                    RemoteHandle = netHandle,

                    Amount = prop.Amount,
                    PickedUp = prop.PickedUp,
                    Position = prop.Position,
                    Rotation = prop.Rotation,
                    ModelHash = prop.ModelHash,
                    EntityType = prop.EntityType,
                    Alpha = prop.Alpha,
                    Dimension = prop.Dimension,
                    SyncedProperties = prop.SyncedProperties,
                    AttachedTo = prop.AttachedTo,
                    Attachables = prop.Attachables,
                    IsInvincible = prop.IsInvincible,
                    CustomModel = prop.CustomModel,

                    PositionMovement = prop.PositionMovement,
                    RotationMovement = prop.RotationMovement,

                    Flag = prop.Flag,

                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public int CreateLocalMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b, int dimension = 0)
        {
            var newId = --_localHandleCounter;
            RemoteMarker mark;
            ClientMap.Add(newId, mark = new RemoteMarker()
            {
                MarkerType = markerType,
                Position = pos.ToLVector(),
                Direction = dir.ToLVector(),
                Rotation = rot.ToLVector(),
                Scale = scale.ToLVector(),
                Alpha = (byte)alpha,
                Red = r,
                Green = g,
                Blue = b,
                Dimension = dimension,
                EntityType = (byte)EntityType.Marker,
                LocalOnly = true,
                StreamedIn = true,
                RemoteHandle = newId,
            });

            if (Count(typeof(RemoteMarker)) < StreamerThread.MAX_MARKERS)
                StreamIn(mark);

            return newId;
        }

        public int CreateLocalVehicle(int model, GTANetworkShared.Vector3 pos, float heading)
        {
            var veh = CreateVehicle(model, pos, new GTANetworkShared.Vector3(0, 0, heading), --_localHandleCounter);
            veh.LocalOnly = true;

            if (Count(typeof(RemoteVehicle)) < StreamerThread.MAX_VEHICLES)
                StreamIn(veh);

            return veh.RemoteHandle;
        }

        public int CreateLocalBlip(GTANetworkShared.Vector3 pos)
        {
            var b = CreateBlip(pos, --_localHandleCounter);
            b.LocalOnly = true;

            if (Count(typeof(RemoteBlip)) < StreamerThread.MAX_BLIPS)
                StreamIn(b);

            return b.RemoteHandle;
        }

        public int CreateLocalObject(int model, Vector3 pos, Vector3 rot)
        {
            var p = CreateObject(model, pos, rot, false, --_localHandleCounter);
            p.LocalOnly = true;

            if (Count(typeof(RemoteProp)) < StreamerThread.MAX_OBJECTS)
                StreamIn(p);

            return p.RemoteHandle;
        }

        public int CreateLocalPickup(int model, Vector3 pos, Vector3 rot, int amount)
        {
            var p = CreatePickup(pos, rot, model, amount, --_localHandleCounter);
            p.LocalOnly = true;

            if (Count(typeof(RemotePickup)) < StreamerThread.MAX_PICKUPS)
                StreamIn(p);

            return p.RemoteHandle;
        }

        public int CreateLocalPed(int model, GTANetworkShared.Vector3 pos, float heading)
        {
            var pp = new PedProperties();
            pp.EntityType = (byte)EntityType.Ped;
            pp.Position = pos;
            pp.Alpha = 255;
            pp.ModelHash = model;
            pp.Rotation = new GTANetworkShared.Vector3(0, 0, heading);
            pp.Dimension = 0;

            var handle = --_localHandleCounter;

            var p = CreatePed(handle, pp);
            p.LocalOnly = true;
            p.RemoteHandle = handle;

            if (Count(typeof(RemotePed)) < StreamerThread.MAX_PEDS)
                StreamIn(p);

            return p.RemoteHandle;
        }



        public int CreateLocalLabel(string text, Vector3 pos, float range, float size, bool entitySeethrough, int dimension = 0)
        {
            var newId = --_localHandleCounter;
            RemoteTextLabel label;
            ClientMap.Add(newId, label = new RemoteTextLabel()
            {
                Position = pos.ToLVector(),
                Size = size,
                Alpha = 255,
                Red = 255,
                Green = 255,
                Blue = 255,
                Dimension = dimension,
                EntityType = (byte)EntityType.TextLabel,
                LocalOnly = true,
                RemoteHandle = newId,
                Text = text,
                Range = range,
                EntitySeethrough = entitySeethrough,
            });

            if (Count(typeof(RemoteTextLabel)) < StreamerThread.MAX_LABELS)
                StreamIn(label);

            return newId;
        }

        public void StreamIn(IStreamedItem item)
        {
            if (item.StreamedIn) return;

            if (item.Dimension != Main.LocalDimension && item.Dimension != 0) return;

            item.StreamedIn = true;
            LogManager.DebugLog("STREAMING IN " + (EntityType) item.EntityType);

            switch ((EntityType) item.EntityType)
            {
                case EntityType.Vehicle:
                    StreamInVehicle((RemoteVehicle) item);
                    break;
                case EntityType.Prop:
                    StreamInProp((RemoteProp) item);
                    break;
                case EntityType.Pickup:
                    StreamInPickup((RemotePickup) item);
                    break;
                case EntityType.Blip:
                    StreamInBlip((RemoteBlip) item);
                    break;
                case EntityType.Player:
                    if (item is SyncPed) ((SyncPed) item).StreamedIn = true;
                    break;
                case EntityType.Ped:
                    StreamInPed((RemotePed) item);
                    break;
                case EntityType.Marker:
                case EntityType.TextLabel:
                    item.StreamedIn = true;
                    break;
                case EntityType.Particle:
                    StreamInParticle((RemoteParticle) item);
                    break;
            }

            if (item is ILocalHandleable)
            {
                var han = item as ILocalHandleable;

                if (han.LocalHandle != 0)
                {
                    lock (HandleMap)
                    {
                        if (HandleMap.ContainsKey(item.RemoteHandle))
                        {
                            HandleMap[item.RemoteHandle] = han.LocalHandle;
                        }
                        else
                        {
                            HandleMap.Add(item.RemoteHandle, han.LocalHandle);
                        }
                    }
                }
            }

            if (item is EntityProperties && ((EntityProperties) item).Attachables != null)
            {
                foreach (var attachable in ((EntityProperties)item).Attachables)
                {
                    var att = NetToStreamedItem(attachable);
                    if (att != null) StreamIn(att);
                }
            }

            if (item is EntityProperties && ((EntityProperties)item).AttachedTo != null)
            {
                LogManager.DebugLog("ITEM " + item.RemoteHandle + " IS ATTACHED TO " + ((EntityProperties)item).AttachedTo);

                var target = NetToStreamedItem(((EntityProperties) item).AttachedTo.NetHandle);
                if (target != null)
                {
                    LogManager.DebugLog("ATTACHED TO " + target.GetType());
                    AttachEntityToEntity(item, target, ((EntityProperties)item).AttachedTo);
                }
            }
        }
        public void StreamOut(IStreamedItem item)
        {
            if (item == null) return;
            if (!item.StreamedIn) return;

            switch ((EntityType) item.EntityType)
            {
                case EntityType.Prop:
                case EntityType.Vehicle:
                case EntityType.Ped:
                    StreamOutEntity((ILocalHandleable) item);
                    break;
                case EntityType.Blip:
                    StreamOutBlip((ILocalHandleable) item);
                    break;
                case EntityType.Pickup:
                    StreamOutPickup((ILocalHandleable) item);
                    break;
                case EntityType.Player:
                    if (item is SyncPed)
                    {
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(((SyncPed) item).Character?.Handle ?? 0), (int)EntityType.Player);
                        ((SyncPed) item).Clear();
                        ((SyncPed) item).StreamedIn = false;
                    }
                    break;
                case EntityType.Marker:
                case EntityType.TextLabel:
                    item.StreamedIn = false;
                    break;
                case EntityType.Particle:
                    StreamOutParticle((ILocalHandleable) item);
                    break;
            }

            item.StreamedIn = false;

            if (item is ILocalHandleable)
            {
                if (HandleMap.ContainsKey(item.RemoteHandle)) HandleMap.Remove(item.RemoteHandle);
            }

            if (item.Attachables != null)
            {
                foreach (var attachable in item.Attachables)
                {
                    var att = NetToStreamedItem(attachable);
                    if (att != null) StreamOut(att);
                }
            }
        }

        public void AttachEntityToEntity(IStreamedItem ent, IStreamedItem entTarget, Attachment info)
        {
            if (!ent.StreamedIn || !entTarget.StreamedIn || info == null) return;
            LogManager.DebugLog("AE2E_1");
            if (entTarget.EntityType == (byte) EntityType.Blip ||
                entTarget.EntityType == (byte)EntityType.TextLabel || // Can't attach to a blip, textlabel or marker
                entTarget.EntityType == (byte)EntityType.Marker ||
                ent.EntityType == (byte)EntityType.Marker ||
                ent.EntityType == (byte)EntityType.TextLabel || // If we're attaching blip/label/marker, UpdateAttachments will take care of it for us.
                ent.EntityType == (byte)EntityType.Blip ||
                ent.EntityType == (byte)EntityType.Pickup) // TODO: Make pickups attachable.
            {
                return;
            }
            LogManager.DebugLog("AE2E_2");
            var handleSource = NetToEntity(ent.RemoteHandle);
            var handleTarget = NetToEntity(entTarget.RemoteHandle);
            LogManager.DebugLog("AE2E_3");
            if (handleSource == null || handleTarget == null) return;
            LogManager.DebugLog("AE2E_4");
            int bone = 0;

            if (!string.IsNullOrWhiteSpace(info.Bone))
            {
                if (entTarget is RemotePlayer || entTarget is RemotePed)
                {
                    bone = Function.Call<int>(Hash.GET_PED_BONE_INDEX, handleTarget.Handle, (int) Enum.Parse(typeof (Bone), info.Bone, true));
                }
                else
                {
                    bone = new Prop(handleTarget.Handle).GetBoneIndex(info.Bone);
                }
            }

            if (bone == -1) bone = 0;
            
            LogManager.DebugLog("ATTACHING " + handleSource.Handle + " TO " + handleTarget.Handle +
                                " WITH BONE " + bone);

            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, handleSource.Handle, handleTarget.Handle,
                bone,
                info.PositionOffset.X, info.PositionOffset.Y, info.PositionOffset.Z,
                info.RotationOffset.X, info.RotationOffset.Y, info.RotationOffset.Z,
                false, // p9
                false, // useSoftPinning
                false, // collision
                false, // p12
                2, // vertexIndex
                true // fixedRot
                );
        }

        public void DetachEntity(IStreamedItem ent, bool collision)
        {
            if (ent == null || ent.AttachedTo == null) return;

            var target = NetToStreamedItem(ent.AttachedTo.NetHandle);

            if (target != null && target.Attachables != null)
            {
                target.Attachables.Remove(ent.RemoteHandle);
            }

            var entHandle = NetToEntity(ent.RemoteHandle);

            if (entHandle != null && entHandle.Handle != 0 && !(ent is RemoteBlip))
            {
                Function.Call(Hash.DETACH_ENTITY, entHandle.Handle, true, collision);
            }

            ent.AttachedTo = null;
        }

        public void ReattachAllEntities(IStreamedItem ent, bool recursive)
        {
            var prop = ent as EntityProperties;
            if (prop == null) return;
            if (prop.Attachables != null)
            {
                LogManager.DebugLog("REATTACHING ALL ENTITIES FOR " + ent.GetType());

                foreach (var i in prop.Attachables)
                {
                    LogManager.DebugLog("REATTACHING ENTITY " + i);

                    var target = NetToStreamedItem(i);

                    if (target == null) continue;
                    AttachEntityToEntity(target, ent, ent.AttachedTo);

                    if (recursive)
                        ReattachAllEntities(target, true);
                }
            }
        }

        private void StreamOutEntity(ILocalHandleable data)
        {
            JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)(data is RemoteVehicle ? EntityType.Vehicle : EntityType.Prop));
            LogManager.DebugLog("PRESTREAM OUT " + data.LocalHandle);
            new Prop(data.LocalHandle).Delete();
            LogManager.DebugLog("POSTSTREAM OUT " + data.LocalHandle);
            LogManager.DebugLog("POSTSTREAM OUT SUCCESS? " + !new Prop(data.LocalHandle).Exists());
        }

        private void StreamOutBlip(ILocalHandleable blip)
        {
            JavascriptHook.InvokeStreamOutEvent(new LocalHandle(blip.LocalHandle), (int)EntityType.Blip);
            new Blip(blip.LocalHandle).Remove();
        }

        private void StreamOutPickup(ILocalHandleable pickup)
        {
            JavascriptHook.InvokeStreamOutEvent(new LocalHandle(pickup.LocalHandle), (int)EntityType.Pickup);
            Function.Call(Hash.REMOVE_PICKUP, pickup.LocalHandle);
        }

        private void StreamOutParticle(ILocalHandleable particle)
        {
            JavascriptHook.InvokeStreamOutEvent(new LocalHandle(particle.LocalHandle), (int)EntityType.Particle);
            Function.Call(Hash.REMOVE_PARTICLE_FX, particle.LocalHandle, false);
        }

        private void StreamInBlip(RemoteBlip item)
        {
            Blip ourBlip;
            if (item.AttachedNetEntity != 0)
            {
                var entAtt = NetToStreamedItem(item.AttachedNetEntity, item.LocalOnly);
                StreamIn(entAtt);
                ourBlip = NetToEntity(item.AttachedNetEntity).AttachBlip();
            }
            else if (item.RangedBlip != 0)
            {
                ourBlip = World.CreateBlip(item.Position.ToVector(), item.RangedBlip);
            }
            else
                ourBlip = World.CreateBlip(item.Position.ToVector());

            if (item.Sprite != 0)
                ourBlip.Sprite = (BlipSprite)item.Sprite;
            ourBlip.Color = (BlipColor)item.Color;
            ourBlip.Alpha = item.Alpha;
            ourBlip.IsShortRange = item.IsShortRange;
            ourBlip.Scale = item.Scale;

            item.StreamedIn = true;
            item.LocalHandle = ourBlip.Handle;

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(ourBlip.Handle), (int)EntityType.Blip);
        }

        private void StreamInPed(RemotePed data)
        {
            if (data == null || (object) data.Position == null || (object) data.Rotation == null) return;
            var model = new Model(data.ModelHash);
            if (!model.IsValid || !model.IsInCdImage) return;
            if (!model.IsLoaded) Util.Util.LoadModel(model);

            var ped = World.CreatePed(model, data.Position.ToVector(), data.Rotation.Z);
            model.MarkAsNoLongerNeeded();

            if (ped == null)
            {
                data.StreamedIn = false;
                return;
            }

            ped.PositionNoOffset = data.Position.ToVector();

            ped.CanBeTargetted = true;
            ped.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped, true);
            ped.RelationshipGroup = Main.RelGroup;
            ped.IsInvincible = true;
            ped.CanRagdoll = false;

            Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped);

            Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, ped, false);

            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, ped, true);
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, true);
            Function.Call(Hash.SET_PED_GET_OUT_UPSIDE_DOWN_VEHICLE, ped, false);
            Function.Call(Hash.SET_PED_AS_ENEMY, ped, false);
            Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, ped, true, false);

            ped.IsPositionFrozen = true;

            if (!string.IsNullOrEmpty(data.LoopingAnimation))
            {
                string[] dictsplit = data.LoopingAnimation.Split();
                if (dictsplit.Length >= 2)
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, ped,
                        Util.Util.LoadAnimDictStreamer(data.LoopingAnimation.Split()[0]), data.LoopingAnimation.Split()[1],
                        8f, 10f, -1, 1, -8f, 1, 1, 1);
                }
                else
                {
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped, data.LoopingAnimation, 0, 0);
                }
            }

            data.LocalHandle = ped.Handle;
            data.StreamedIn = true;
        }

        private void StreamInParticle(RemoteParticle data)
        {
            if (data == null || (object)data.Position == null || (object)data.Rotation == null) return;

            Util.Util.LoadPtfxAsset(data.Library);
            Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, data.Library);

            int handle;

            if (data.EntityAttached == 0)
            {
                handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_AT_COORD, data.Name,
                    data.Position.X, data.Position.Y, data.Position.Z,
                    data.Rotation.X, data.Rotation.Y, data.Rotation.Z,
                    data.Scale, 0, 0, 0, 0);
            }
            else
            {
                var targetItem = NetToStreamedItem(data.EntityAttached);
                if (!targetItem.StreamedIn) StreamIn(targetItem);

                var target = NetToEntity(data.EntityAttached);

                if (data.BoneAttached <= 0)
                {
                    handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_ON_ENTITY, data.Name,
                        target,
                        data.Position.X, data.Position.Y, data.Position.Z,
                        data.Rotation.X, data.Rotation.Y, data.Rotation.Z,
                        data.Scale, 0, 0, 0);
                }
                else if (target.IsPed())
                {
                    handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_ON_PED_BONE, data.Name,
                        target,
                        data.Position.X, data.Position.Y, data.Position.Z,
                        data.Rotation.X, data.Rotation.Y, data.Rotation.Z,
                        data.BoneAttached, data.Scale, 0, 0, 0);
                }
                else
                {
                    handle = Function.Call<int>(Hash._START_PARTICLE_FX_LOOPED_ON_ENTITY_BONE, data.Name,
                        target,
                        data.Position.X, data.Position.Y, data.Position.Z,
                        data.Rotation.X, data.Rotation.Y, data.Rotation.Z,
                        data.BoneAttached, data.Scale, 0, 0, 0);
                }
            }
            
            data.LocalHandle = handle;
            data.StreamedIn = true;
        }

        private void StreamInVehicle(RemoteVehicle data)
        {
            if (data == null || (object) data.Position == null || (object) data.Rotation == null) return;
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage) return;
            LogManager.DebugLog("CREATING VEHICLE FOR NETHANDLE " + data.RemoteHandle);
            if (!model.IsLoaded) Util.Util.LoadModel(model);
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, data.Position.X, data.Position.Y, data.Position.Z);
            Function.Call(Hash.REQUEST_ADDITIONAL_COLLISION_AT_COORD, data.Position.X, data.Position.Y, data.Position.Z);
            LogManager.DebugLog("LOAD COMPLETE. AVAILABLE: " + model.IsLoaded);

            LogManager.DebugLog("POSITION: " + data.Position?.ToVector());

            var veh = World.CreateVehicle(model, data.Position.ToVector(), data.Rotation.Z);

            LogManager.DebugLog("VEHICLE CREATED. NULL? " + (veh == null) + " EXISTS? " + (veh?.Exists()));

            if (veh == null || !veh.Exists())
            {
#if DEBUG
                LogManager.LogException(
                    new Exception("Vehicle was null or didnt spawn, model=" + model.Hash + ", loaded=" + model.IsLoaded +
                                  ", vehicleHandle=" + (veh?.Handle)), "StreamInVehicle");
#endif
                data.StreamedIn = false;
                return;
            }
            
            data.LocalHandle = veh.Handle;
            veh.Rotation = data.Rotation.ToVector();
            veh.Mods.Livery = data.Livery;

            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, veh, true);
            Function.Call(Hash.TRACK_VEHICLE_VISIBILITY, veh);
            Function.Call(Hash.SET_SIREN_WITH_NO_DRIVER, veh, true);
            Function.Call((Hash)0x068F64F2470F9656, false);

            LogManager.DebugLog("LOCAL HANDLE: " + veh.Handle);
            LogManager.DebugLog("POS: " + veh.Position);

            if ((data.PrimaryColor & 0xFF000000) > 0)
                veh.Mods.CustomPrimaryColor = Color.FromArgb(data.PrimaryColor);
            else
                veh.Mods.PrimaryColor = (VehicleColor)data.PrimaryColor;

            if ((data.SecondaryColor & 0xFF000000) > 0)
                veh.Mods.CustomSecondaryColor = Color.FromArgb(data.SecondaryColor);
            else
                veh.Mods.SecondaryColor = (VehicleColor)data.SecondaryColor;

            veh.Mods.PearlescentColor = (VehicleColor)0;
            veh.Mods.RimColor = (VehicleColor)0;
            veh.EngineHealth = data.Health;
            veh.SirenActive = data.Siren;
            veh.Mods.LicensePlate = data.NumberPlate;
            veh.Mods.WheelType = 0;
            veh.Wash();
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, veh, 0);
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, veh, 0);

            if (data.Trailer != 0)
            {
                var trailerId = NetToStreamedItem(data.Trailer);
                if (trailerId != null)
                {
                    StreamIn(trailerId);
                    var trailer = new Vehicle(((RemoteVehicle)trailerId).LocalHandle);

                    if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                                        (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    {
                        Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, veh, trailer, true, 0, 0, 0);
                    }
                    else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    {
                        veh.DropCargobobHook(CargobobHook.Hook);
                        Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, veh, 0, 0, 0, 0);
                    }
                    else
                    {
                        Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, veh, trailer, 4f);
                    }
                }
            }


            Function.Call(Hash.SET_VEHICLE_MOD_KIT, veh, 0);

            if (data.Mods != null)
            {
                for (int i = 0; i <= 100; i++)
                {
                    if (i <= 60)
                    {
                        if (data.Mods.ContainsKey((byte)i))
                        {
                            if (i >= 17 && i <= 22)
                                veh.Mods[(VehicleToggleModType) i].IsInstalled = data.Mods[(byte)i] != 0;
                            else
                                veh.Mods[(VehicleModType) i].Index = data.Mods[(byte)i];
                        }
                        else
                        {
                            Function.Call(Hash.REMOVE_VEHICLE_MOD, veh, i);
                        }
                    }
                    else
                    {
                        if (data.Mods.ContainsKey((byte)i)) Util.Util.SetNonStandardVehicleMod(veh, i, data.Mods[(byte)i]);
                    }
                }
            }

            if (data.IsDead)
            {
                veh.IsInvincible = false;
                Function.Call(Hash.EXPLODE_VEHICLE, veh, false, true);
            }
            else
                veh.IsInvincible = data.IsInvincible;

            if (data.Alpha < 255) veh.Opacity = (int)data.Alpha;
            LogManager.DebugLog("ALPHA: " + veh.Opacity);


            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, veh, false);

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.Collisionless))
            {
                veh.IsCollisionEnabled = false;
            }

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.EngineOff))
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, veh, false, true, true);
                Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, veh, true);
            }
            else
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, veh, true, true, true);
            }

            for (int i = 0; i < 16; i++)
            {
                if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, veh, i)) continue;
                bool turnedOn = (data.VehicleComponents & 1 << i) != 0;
                if (Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, veh, i) ^ turnedOn)
                    Function.Call(Hash.SET_VEHICLE_EXTRA, veh, i, turnedOn ? 0 : -1);
            }

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.SpecialLight))
            {
                if (model.IsHelicopter)
                {
                    Function.Call(Hash.SET_VEHICLE_SEARCHLIGHT, veh, true, true);
                }
                else
                {
                    veh.TaxiLightOn = true;
                }
            }
            else
            {
                veh.SearchLightOn = false;
                veh.TaxiLightOn = false;
            }


            for (int i = 0; i < 8; i++)
            {
                if ((data.Doors & 1 << i) != 0)
                {
                    veh.Doors[(VehicleDoorIndex)i].Open(false, false);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if ((data.Tires & 1 << i) != 0)
                {
                    veh.Wheels[i].Burst();
                }
            }

            if (data.DamageModel != null) veh.SetVehicleDamageModel(data.DamageModel, false);

            if (data.LocalOnly)
            {
                veh.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
            }

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.VehicleLocked))
            {
                veh.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
            }
            

            LogManager.DebugLog("PROPERTIES SET");
            data.StreamedIn = true;
            LogManager.DebugLog("DISCARDING MODEL");
            model.MarkAsNoLongerNeeded();
            LogManager.DebugLog("CREATEVEHICLE COMPLETE");

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(veh.Handle), (int)EntityType.Vehicle);
        }
    

        private void StreamInProp(RemoteProp data)
        {
            var model = new Model(data.ModelHash);
            LogManager.DebugLog("PROP MODEL VALID: " + model.IsValid);
            if (model == null || !model.IsValid || !model.IsInCdImage || data.Position == null || data.Rotation == null) return;
            LogManager.DebugLog("CREATING OBJECT FOR NETHANDLE " + data.RemoteHandle);

            if (!model.IsLoaded)
            {
                Util.Util.LoadModel(model);
            }

            LogManager.DebugLog("LOAD COMPLETE. AVAILABLE: " + model.IsLoaded);
            var ourVeh = new Prop(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, model.Hash, data.Position.X, data.Position.Y, data.Position.Z, false, true, false));
            LogManager.DebugLog("PROP HANDLE: " + ourVeh.Handle);
            LogManager.DebugLog("ROTATION: " + data.Rotation.GetType());
            if (data.Rotation is Quaternion)
            {
                ourVeh.Quaternion = ((Quaternion) data.Rotation).ToQuaternion();
            }
            else
            {
                ourVeh.Rotation = data.Rotation.ToVector();
            }

            LogManager.DebugLog("SETTING MISC PROPERTIES");

            if (data.Alpha < 255) ourVeh.Opacity = (int)data.Alpha;
            ourVeh.IsPositionFrozen = true;
            ourVeh.LodDistance = 3000;

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.Collisionless))
            {
                ourVeh.IsCollisionEnabled = false;
            }

            data.StreamedIn = true;
            data.LocalHandle = ourVeh.Handle;
            LogManager.DebugLog("STREAMIN DONE");

            model.MarkAsNoLongerNeeded();

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(ourVeh.Handle), (int)EntityType.Prop);
        }

        private void StreamInPickup(RemotePickup pickup)
        {
            int model = 0;

            if (pickup.CustomModel != 0)
            {
                Util.Util.LoadModel(new Model(pickup.CustomModel));
                model = pickup.CustomModel;
            }

            var newPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, pickup.ModelHash,
                pickup.Position.X, pickup.Position.Y, pickup.Position.Z,
                pickup.Rotation.X, pickup.Rotation.Y, pickup.Rotation.Z,
                515, pickup.Amount, 0, true, model);

            var start = 0;
            while (Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup) == -1 && start < 20)
            {
                start++;
                Script.Yield();
            }

            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).IsPositionFrozen = true;
            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).IsPersistent = true;

            if ((pickup.Flag & (byte)EntityFlag.Collisionless) != 0)
            {
                new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).IsCollisionEnabled = false;
            }

            pickup.StreamedIn = true;
            pickup.LocalHandle = newPickup;

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(newPickup), (int)EntityType.Pickup);
        }

        public void ClearAll()
        {
            LogManager.DebugLog("STARTING CLEARALL");

            lock (ClientMap)
            {
                LogManager.DebugLog("HANDLEMAP LOCKED");
                LogManager.DebugLog("HANDLEMAP SIZE: " + ClientMap.Count);

                foreach (var pair in ClientMap.Values)
                {
                    if (!pair.StreamedIn) continue;

                    StreamOut(pair);                    
                }

                LogManager.DebugLog("CLEARING LISTS");
                ClientMap.Clear();
                HandleMap.Clear();
                _localHandleCounter = 0;
            }

        }
    }
}