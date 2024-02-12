﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetwork.Sync;
using GTANetworkShared;
using NativeUI;
using Quaternion = GTANetworkShared.Quaternion;
using Vector3 = GTA.Math.Vector3;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Streamer
{
    public class DrawLabels : Script
    {
        public DrawLabels()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected()) Main.NetEntityHandler.DrawLabels();
        }
    }

    public class DrawMarkers : Script
    {
        public DrawMarkers()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected()) Main.NetEntityHandler.DrawMarkers();
        }
    }

    public class DrawLocalMarkers : Script
    {
        public DrawLocalMarkers()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                lock (Main._localMarkers)
                {
                    for (var index = Main._localMarkers.Count - 1; index >= 0; index--)
                    {
                        var marker = Main._localMarkers.ElementAt(index);
                        World.DrawMarker((MarkerType) marker.Value.MarkerType, marker.Value.Position.ToVector(),
                            marker.Value.Direction.ToVector(), marker.Value.Rotation.ToVector(),
                            marker.Value.Scale.ToVector(),
                            Color.FromArgb(marker.Value.Alpha, marker.Value.Red, marker.Value.Green, marker.Value.Blue), marker.Value.BobUpAndDown);
                    }
                }
            }
        }
    }

    public class UpdateAttachements : Script
    {
        public UpdateAttachements()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected()) Main.NetEntityHandler.UpdateAttachments();
        }
    }

    public class UpdateMisc : Script
    {
        public UpdateMisc()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected()) Main.NetEntityHandler.UpdateMisc();
        }
    }

    public class UpdateInterpolations : Script
    {
        public UpdateInterpolations()
        {
            Tick += Draw;
        }

        private static void Draw(object sender, EventArgs e)
        {
            if (Main.IsConnected()) Main.NetEntityHandler.UpdateInterpolations();
        }
    }

    internal partial class Streamer
    {
        internal Streamer()
        {
            ClientMap = new BiDictionary<int, IStreamedItem>();
            HandleMap = new BiDictionary<int, int>();
        }

        private int _localHandleCounter = 0;
        public BiDictionary<int, IStreamedItem> ClientMap; // Global, IStreamedItem
        public BiDictionary<int, int> HandleMap; // Global, GameHandle

        public WorldProperties ServerWorld;
        public RemotePlayer LocalCharacter;

        #region Create
        public RemoteVehicle CreateVehicle(int model, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, int netHash)
        {
            short vehComp = ~0;
            switch (model)
            {
                case unchecked((int)VehicleHash.Taxi):
                    vehComp = 1 << 5;
                    break;
                case (int)VehicleHash.Police:
                    vehComp = 1 << 2;
                    break;
                case (int)VehicleHash.Skylift:
                    vehComp = -1537;
                    break;
            }

            RemoteVehicle rem = new RemoteVehicle()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHash, rem);
            }
            return rem;
        }

        public RemoteVehicle CreateVehicle(int netHandle, VehicleProperties prop)
        {
            RemoteVehicle rem = new RemoteVehicle()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemotePed CreatePed(int netHandle, PedProperties prop)
        {
            RemotePed rem = new RemotePed()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteProp CreateObject(int model, Vector3 position, Vector3 rotation, bool dynamic, int netHash)
        {
            RemoteProp rem = new RemoteProp()
            {
                RemoteHandle = netHash,
                ModelHash = model,
                EntityType = 2,
                Position = position.ToLVector(),
                Rotation = rotation.ToLVector(),
                StreamedIn = false,
                LocalOnly = false,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHash, rem);
            }
            return rem;
        }

        public RemoteProp CreateObject(int netHandle, EntityProperties prop)
        {
            RemoteProp rem = new RemoteProp()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteBlip CreateBlip(GTANetworkShared.Vector3 pos, int netHandle)
        {
            RemoteBlip rem = new RemoteBlip()
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
                EntityType = (byte)EntityType.Blip,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteBlip CreateBlip(int netHandle, BlipProperties prop)
        {
            RemoteBlip rem = new RemoteBlip()
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
                RouteVisible = prop.RouteVisible,
                RouteColor = prop.RouteColor,

                StreamedIn = false,
                LocalOnly = false,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteBlip CreateBlip(IStreamedItem entity, int netHandle)
        {
            RemoteBlip rem = new RemoteBlip()
            {
                RemoteHandle = netHandle,
                AttachedNetEntity = entity.RemoteHandle,
                EntityType = (byte)EntityType.Blip,
                StreamedIn = false,
                LocalOnly = false,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public void CreateMarker(int type, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, GTANetworkShared.Vector3 dir, GTANetworkShared.Vector3 scale, int r, int g, int b, int a, int netHandle, bool bobUpAndDown = false)
        {
            RemoteMarker rem = new RemoteMarker()
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
                BobUpAndDown = bobUpAndDown,
                RemoteHandle = netHandle,
                EntityType = (byte)EntityType.Marker,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
        }

        public RemoteMarker CreateMarker(int netHandle, MarkerProperties prop)
        {
            RemoteMarker rem = new RemoteMarker()
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
                BobUpAndDown = prop.BobUpAndDown,
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteTextLabel CreateTextLabel(int netHandle, TextLabelProperties prop)
        {
            RemoteTextLabel rem = new RemoteTextLabel()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemoteParticle CreateParticle(int netHandle, ParticleProperties prop)
        {
            RemoteParticle rem = new RemoteParticle()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemotePickup CreatePickup(Vector3 pos, Vector3 rot, int pickupHash, int amount, int netHandle)
        {
            RemotePickup rem = new RemotePickup()
            {
                RemoteHandle = netHandle,
                Position = pos.ToLVector(),
                Rotation = rot.ToLVector(),
                ModelHash = pickupHash,
                Amount = amount,
                StreamedIn = false,
                LocalOnly = false,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public RemotePickup CreatePickup(int netHandle, PickupProperties prop)
        {
            RemotePickup rem = new RemotePickup()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public int CreateLocalMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b, int dimension = 0, bool bobUpAndDown = false)
        {
            var newId = --_localHandleCounter;
            RemoteMarker mark  = new RemoteMarker()
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
                BobUpAndDown = bobUpAndDown,
                EntityType = (byte)EntityType.Marker,
                LocalOnly = true,
                StreamedIn = true,
                RemoteHandle = newId,
            };

            lock (ClientMap)
            {
                ClientMap.Add(newId, mark);
            }

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
            RemoteTextLabel label = new RemoteTextLabel()
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
            };

            lock (ClientMap)
            {
                ClientMap.Add(newId, label);
            }
            
            if (Count(typeof(RemoteTextLabel)) < StreamerThread.MAX_LABELS)
                StreamIn(label);

            return newId;
        }
        #endregion

        #region Update
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

            if (!(rem is SyncPed)) return;
            if (prop.Position != null)
                ((SyncPed)rem).Position = prop.Position.ToVector();
            if (prop.Rotation != null)
                ((SyncPed)rem).Rotation = prop.Rotation.ToVector();

            ((SyncPed)rem).DirtyWeapons = true;
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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

        private void UpdateRemotePlayer(int netHandle, Delta_PlayerProperties prop)
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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
            IStreamedItem item;
            if (prop == null || (item = NetToStreamedItem(netHandle)) == null) return;
            var veh = item as EntityProperties;
            if (veh == null) return;
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

                var localPl = item as RemotePlayer;
                if (localPl != null && localPl.LocalHandle == -2) Main.LocalDimension = prop.Dimension.Value;
                //else if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) veh.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                veh.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    LogManager.DebugLog("ATTACHING THIS ENTITY (" + ((EntityType)veh.EntityType) + " id: " + netHandle + ") TO " + attachedTo.GetType());
                    AttachEntityToEntity(veh as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }

            if (prop.SyncedProperties != null)
            {
                if (veh.SyncedProperties == null) veh.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                    {
                        veh.SyncedProperties.Remove(pair.Key);
                    }
                    else
                    {
                        NativeArgument oldValue = veh.SyncedProperties.Get(pair.Key);

                        veh.SyncedProperties.Set(pair.Key, pair.Value);

                        var ent = new LocalHandle(NetToEntity(veh as IStreamedItem)?.Handle ?? 0);
                        if (!ent.IsNull) JavascriptHook.InvokeDataChangeEvent(ent, pair.Key, Main.DecodeArgumentListPure(oldValue).FirstOrDefault());
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
            var blip = item as RemoteBlip;
            if (prop.Sprite != null) blip.Sprite = prop.Sprite.Value;
            if (prop.Scale != null) blip.Scale = prop.Scale.Value;
            if (prop.Color != null) blip.Color = prop.Color.Value;
            if (prop.IsShortRange != null) blip.IsShortRange = prop.IsShortRange.Value;
            if (prop.AttachedNetEntity != null) blip.AttachedNetEntity = prop.AttachedNetEntity.Value;
            if (prop.Position != null) blip.Position = prop.Position;
            if (prop.Rotation != null) blip.Rotation = prop.Rotation;
            if (prop.ModelHash != null) blip.ModelHash = prop.ModelHash.Value;
            if (prop.EntityType != null) blip.EntityType = prop.EntityType.Value;
            if (prop.Alpha != null) blip.Alpha = prop.Alpha.Value;
            if (prop.Flag != null) blip.Flag = prop.Flag.Value;
            if (prop.RangedBlip != null) blip.RangedBlip = prop.RangedBlip.Value;
            if (prop.IsInvincible != null) blip.IsInvincible = prop.IsInvincible.Value;
            if (prop.Name != null) blip.Name = prop.Name;
            if (prop.RouteVisible != null) blip.RouteVisible = prop.RouteVisible.Value;
            if (prop.RouteColor != null) blip.RouteColor = prop.RouteColor.Value;

            if (prop.Dimension != null)
            {
                blip.Dimension = prop.Dimension.Value;
                //if (blip.Dimension != Main.LocalDimension && item.StreamedIn && blip.Dimension != 0) StreamOut(item);
            }

            if (prop.Attachables != null) blip.Attachables = prop.Attachables;
            if (prop.AttachedTo != null)
            {
                blip.AttachedTo = prop.AttachedTo;
                var attachedTo = NetToStreamedItem(prop.AttachedTo.NetHandle);
                if (attachedTo != null)
                {
                    AttachEntityToEntity(blip as IStreamedItem, attachedTo, prop.AttachedTo);
                }
            }
            if (prop.SyncedProperties != null)
            {
                if (blip.SyncedProperties == null) blip.SyncedProperties = new Dictionary<string, NativeArgument>();
                foreach (var pair in prop.SyncedProperties)
                {
                    if (pair.Value is LocalGamePlayerArgument)
                        blip.SyncedProperties.Remove(pair.Key);
                    else
                        blip.SyncedProperties.Set(pair.Key, pair.Value);
                }
            }

            if (prop.PositionMovement != null) blip.PositionMovement = prop.PositionMovement;
            if (prop.RotationMovement != null) blip.RotationMovement = prop.RotationMovement;
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
            if (prop.BobUpAndDown != null) veh.BobUpAndDown = prop.BobUpAndDown.Value;
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
                //if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
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
                //if (veh.Dimension != Main.LocalDimension && veh.StreamedIn && veh.Dimension != 0) StreamOut(veh);
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
                //if (veh.Dimension != Main.LocalDimension && item.StreamedIn && veh.Dimension != 0) StreamOut(item);
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

        public void UpdateAttachments()
        {
            var attaches = new List<EntityProperties>(ClientMap.Values.Where(item => item.StreamedIn && item.AttachedTo != null).Cast<EntityProperties>());

            for (var index = attaches.Count - 1; index >= 0; index--)
            {
                var item = attaches[index];
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
                            var remoteBlip = item as RemoteBlip;
                            if (remoteBlip != null)
                            {
                                var blipHandle = new Blip(remoteBlip.LocalHandle)
                                {
                                    Position =
                                        entityTarget.GetOffsetInWorldCoords(item.AttachedTo.PositionOffset.ToVector())
                                };
                            }
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
                                item.Position +=
                                    (new Ped(entityTarget.Handle).CurrentVehicle.Velocity / Game.FPS).ToLVector();
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
                    ClientMap.Values.OfType<RemoteVehicle>().Where(
                        item => item.StreamedIn && new Model(item.ModelHash).IsHelicopter && (VehicleHash)item.ModelHash == VehicleHash.Maverick));

            for (var index = cars.Count - 1; index >= 0; index--)
            {
                var remoteVehicle = cars[index];
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
                    ClientMap.Values.Where(item => item.StreamedIn && item.PositionMovement != null || item.RotationMovement != null).Cast<EntityProperties>());

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
                        Math.Min(((float)delta / ent.PositionMovement.Duration), 1f));

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

                    if (delta >= ent.PositionMovement.Duration)
                    {
                        // Ensure that the position will be the one that was given
                        ent.Position = ent.PositionMovement.EndVector;
                        ent.PositionMovement = null;
                    }
                }

                if (ent.RotationMovement != null)
                {
                    // TODO: fix rotation
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
                                        //gameEnt.Quaternion = ent.Rotation.ToVector().ToQuaternion();
                                        gameEnt.Rotation = ent.Rotation.ToVector(); // Gimbal lock!
                                    }
                                }
                                break;
                        }
                    }

                    if (delta >= ent.RotationMovement.Duration)
                    {
                        // Ensure that the position will be the one that was given
                        ent.Rotation = ent.RotationMovement.EndVector;
                        ent.RotationMovement = null;
                    }
                        
                }
            }
        }
        #endregion

        #region StreamIn
        public void StreamIn(IStreamedItem item)
        {
            if (item.StreamedIn) return;

            if (item.Dimension != Main.LocalDimension && item.Dimension != 0) return;

            switch ((EntityType)item.EntityType)
            {
                case EntityType.Vehicle:
                    {
                        StreamInVehicle((RemoteVehicle)item);
                    }
                    break;

                case EntityType.Prop:
                    {
                        StreamInProp((RemoteProp)item);
                    }
                    break;

                case EntityType.Pickup:
                    {
                        StreamInPickup((RemotePickup)item);
                    }
                    break;

                case EntityType.Blip:
                    {
                        StreamInBlip((RemoteBlip)item);
                    }
                    break;

                case EntityType.Player:
                    {
                        var ped = item as SyncPed;
                        if (ped != null)
                        {
                            ped.StreamedIn = true;
                            JavascriptHook.InvokeStreamInEvent(new LocalHandle(ped.LocalHandle), (int)EntityType.Player);
                        }
                    }
                    break;

                case EntityType.Ped:
                    {
                        StreamInPed((RemotePed)item);
                    }
                    break;

                case EntityType.Marker:
                    {
                        item.StreamedIn = true;
                        //var data = (ILocalHandleable)item;
                        //JavascriptHook.InvokeStreamInEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Marker);
                    }
                    break;

                case EntityType.TextLabel:
                    {
                        item.StreamedIn = true;
                        //var data = (ILocalHandleable)item;
                        //JavascriptHook.InvokeStreamInEvent(new LocalHandle(data.LocalHandle), (int)EntityType.TextLabel);
                    }
                    break;

                case EntityType.Particle:
                    {
                        StreamInParticle((RemoteParticle)item);
                    }
                    break;
            }

            var handleable = item as ILocalHandleable;
            if (handleable != null)
            {
                var han = handleable;
                if (han.LocalHandle == 0) return;

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

            //if ((item as EntityProperties)?.Attachables != null)
            //{
            //    foreach (var attachable in ((EntityProperties)item).Attachables)
            //    {
            //        var att = NetToStreamedItem(attachable);
            //        if (att != null) StreamIn(att);
            //    }
            //}

            //if ((item as EntityProperties)?.AttachedTo != null)
            //{
            //    var target = NetToStreamedItem(((EntityProperties)item).AttachedTo.NetHandle);
            //    if (target == null) return;
            //    AttachEntityToEntity(item, target, ((EntityProperties)item).AttachedTo);
            //}
        }

        public void StreamOut(IStreamedItem item)
        {
            if (item == null) return;
            if (!item.StreamedIn) return;

            switch ((EntityType)item.EntityType)
            {
                case EntityType.Prop:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Prop);
                        var obj = new Prop(data.LocalHandle);
                        if (obj.Exists()) obj.Delete();
                    }
                    break;
                case EntityType.Vehicle:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Vehicle);
                        var obj = new Prop(data.LocalHandle);
                        if (obj.Exists()) obj.Delete();
                    }
                    break;
                case EntityType.Ped:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Ped);
                        var obj = new Prop(data.LocalHandle);
                        if (obj.Exists()) obj.Delete();
                    }
                    break;
                case EntityType.Blip:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Blip);
                        var obj = new Blip(data.LocalHandle);
                        if (obj.Exists()) obj.Delete();
                    }
                    break;
                case EntityType.Pickup:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Pickup);
                        var obj = new Pickup(data.LocalHandle);
                        if (obj.Exists()) obj.Delete();
                    }
                    break;

                case EntityType.Particle:
                    {
                        var data = (ILocalHandleable)item;
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Particle);
                        Function.Call(Hash.REMOVE_PARTICLE_FX, data.LocalHandle, false);
                    }
                    break;
                case EntityType.Player:
                    var ped = item as SyncPed;
                    if (ped != null)
                    {
                        JavascriptHook.InvokeStreamOutEvent(new LocalHandle(ped.Character?.Handle ?? 0), (int)EntityType.Player);
                        ped.Clear(); //TODO
                    }
                    break;
            }

            item.StreamedIn = false;

            lock (HandleMap)
            {
                if (HandleMap.ContainsKey(item.RemoteHandle))
                {
                    HandleMap.Remove(item.RemoteHandle);
                }
            }

            //if (item.Attachables == null) return;
            //for (var index = item.Attachables.Count - 1; index >= 0; index--)
            //{
            //    var attachable = item.Attachables[index];
            //    var att = NetToStreamedItem(attachable);
            //    if (att != null) StreamOut(att);
            //}
        }

        private void StreamInVehicle(RemoteVehicle data)
        {
            if ((object) data?.Position == null || (object)data.Rotation == null) return;
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage) return;

            if (!model.IsLoaded) Util.Util.LoadModel(model);

            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, data.Position.X, data.Position.Y, data.Position.Z);
            Function.Call(Hash.REQUEST_ADDITIONAL_COLLISION_AT_COORD, data.Position.X, data.Position.Y, data.Position.Z);

            Vehicle veh = null;
            if (model.IsLoaded) veh = World.CreateVehicle(model, data.Position.ToVector(), data.Rotation.Z);

            if (veh == null || !veh.Exists())
            {
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

            if ((data.PrimaryColor & 0xFF000000) > 0)
            {
                veh.Mods.CustomPrimaryColor = Color.FromArgb(data.PrimaryColor);
            }
            else
            {
                veh.Mods.PrimaryColor = (VehicleColor)data.PrimaryColor;
            }

            if ((data.SecondaryColor & 0xFF000000) > 0)
            {
                veh.Mods.CustomSecondaryColor = Color.FromArgb(data.SecondaryColor);
            }
            else
            {
                veh.Mods.SecondaryColor = (VehicleColor)data.SecondaryColor;
            }

            veh.Mods.PearlescentColor = 0;
            veh.Mods.RimColor = 0;
            veh.EngineHealth = data.Health;
            veh.IsSirenActive = data.Siren;
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

                    switch ((VehicleHash)veh.Model.Hash)
                    {
                        case VehicleHash.TowTruck:
                        case VehicleHash.TowTruck2:
                            Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, veh, trailer, true, 0, 0, 0);
                            break;
                        case VehicleHash.Cargobob:
                        case VehicleHash.Cargobob2:
                        case VehicleHash.Cargobob3:
                        case VehicleHash.Cargobob4:
                            veh.DropCargobobHook(CargobobHook.Hook);
                            Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, veh, 0, 0, 0, 0);
                            break;
                        default:
                            Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, veh, trailer, 4f);
                            break;
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
                                veh.Mods[(VehicleToggleModType)i].IsInstalled = data.Mods[(byte)i] != 0;
                            else
                                veh.Mods[(VehicleModType)i].Index = data.Mods[(byte)i];
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
            {
                veh.IsInvincible = data.IsInvincible;
            }

            if (data.Alpha < 255) veh.Opacity = data.Alpha;

            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, veh, false);

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.Collisionless)) veh.IsCollisionEnabled = false;

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
                if (Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, veh, i) ^ turnedOn) Function.Call(Hash.SET_VEHICLE_EXTRA, veh, i, turnedOn ? 0 : -1);
            }

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.SpecialLight))
            {
                if (model.IsHelicopter)
                {
                    Function.Call(Hash.SET_VEHICLE_SEARCHLIGHT, veh, true, true);
                }
                else
                {
                    veh.IsTaxiLightOn = true;
                }
            }
            else
            {
                veh.IsSearchLightOn = false;
                veh.IsTaxiLightOn = false;
            }


            for (int i = 0; i < 8; i++)
            {
                if ((data.Doors & 1 << i) != 0)
                {
                    veh.Doors[(VehicleDoorIndex)i].Open();
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

            if (data.LocalOnly) veh.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.VehicleLocked)) veh.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;

            data.StreamedIn = true;
            //model.MarkAsNoLongerNeeded();

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(veh.Handle), (int)EntityType.Vehicle);
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
            {
                ourBlip = World.CreateBlip(item.Position.ToVector());
            }

            if (item.Sprite != 0) ourBlip.Sprite = (BlipSprite)item.Sprite;
            ourBlip.Color = (BlipColor)item.Color;
            ourBlip.Alpha = item.Alpha;
            ourBlip.IsShortRange = item.IsShortRange;
            ourBlip.Scale = item.Scale;
            ourBlip.ShowRoute = item.RouteVisible;
            Function.Call(Hash.SET_BLIP_ROUTE_COLOUR, ourBlip, item.RouteColor);

            item.StreamedIn = true;
            item.LocalHandle = ourBlip.Handle;

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(ourBlip.Handle), (int)EntityType.Blip);
        }

        private void StreamInParticle(RemoteParticle data)
        {
            if ((object) data?.Position == null || (object)data.Rotation == null) return;

            Util.Util.LoadPtfxAsset(data.Library);
            Function.Call(Hash._USE_PARTICLE_FX_ASSET_NEXT_CALL, data.Library);

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
            JavascriptHook.InvokeStreamInEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Particle);
        }

        private static void StreamInPickup(RemotePickup pickup)
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

        private static void StreamInProp(RemoteProp data)
        {
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage || data.Position == null || data.Rotation == null) return;
            if (!model.IsLoaded) Util.Util.LoadModel(model);

            Prop ourProp = null; 
            if(model.IsLoaded) ourProp = new Prop(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, model.Hash, data.Position.X, data.Position.Y, data.Position.Z, false, true, false));
            if (ourProp == null || !ourProp.Exists())
            {
                data.StreamedIn = false;
                return;
            }

            var rotation = data.Rotation as Quaternion;
            if (rotation != null)
            {
                ourProp.Quaternion = rotation.ToQuaternion();
            }
            else
            {
                ourProp.Rotation = data.Rotation.ToVector();
            }

            if (PacketOptimization.CheckBit(data.Flag, EntityFlag.Collisionless)) ourProp.IsCollisionEnabled = false;
            if (data.Alpha < 255) ourProp.Opacity = data.Alpha;
            ourProp.IsPositionFrozen = true;
            ourProp.LodDistance = 400;

            data.LocalHandle = ourProp.Handle;
            data.StreamedIn = true;

            //model.MarkAsNoLongerNeeded();

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(ourProp.Handle), (int)EntityType.Prop);
        }

        private static void StreamInPed(RemotePed data)
        {
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage || data.Position == null || data.Rotation == null) return;
            if (!model.IsLoaded) Util.Util.LoadModel(model);

            Ped ped = null;
            if (model.IsLoaded) ped = World.CreatePed(model, data.Position.ToVector(), data.Rotation.Z);

            //model.MarkAsNoLongerNeeded();

            if (ped == null || !ped.Exists())
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
                var dictsplit = data.LoopingAnimation.Split();
                if (dictsplit.Length >= 2)
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, ped, Util.Util.LoadAnimDictStreamer(data.LoopingAnimation.Split()[0]), data.LoopingAnimation.Split()[1], 8f, 10f, -1, 1, -8f, 1, 1, 1);
                }
                else
                {
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped, data.LoopingAnimation, 0, 0);
                }
            }

            data.LocalHandle = ped.Handle;
            data.StreamedIn = true;

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(data.LocalHandle), (int)EntityType.Ped);
        }
        #endregion

        #region Handling
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
                    return ClientMap.ContainsKey(netId) ? ClientMap[netId] : null;
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

        public void AddLocalCharacter(int nethandle)
        {
            LocalCharacter = new RemotePlayer() { LocalHandle = -2, RemoteHandle = nethandle, StreamedIn = true };
            lock (ClientMap)
            {
                ClientMap.Add(nethandle, LocalCharacter);
                HandleMap.Add(nethandle, -2);
            }
        }

        public Entity NetToEntity(int netId)
        {
            lock (ClientMap)
            {
                var streamedItem = NetToStreamedItem(netId);
                if (streamedItem == null || !streamedItem.StreamedIn) return null;
                var handleable = streamedItem as ILocalHandleable;
                if (handleable == null) return new Prop(netId);
                if (handleable.LocalHandle == -2) return Game.Player.Character;
                return new Prop(handleable.LocalHandle);
            }
        }

        public Entity NetToEntity(IStreamedItem netId)
        {
            lock (ClientMap)
            {
                var handleable = netId as ILocalHandleable;
                if (handleable == null || netId == null) return new Prop(netId?.RemoteHandle ?? 0);
                if (handleable.LocalHandle == -2) return Game.Player.Character;
                return new Prop(handleable.LocalHandle);
            }
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

        internal SyncPed GetPlayer(int netHandle)
        {
            SyncPed rem = NetToStreamedItem(netHandle) as SyncPed;
            if (rem != null) return rem;

            rem = new SyncPed()
            {
                RemoteHandle = netHandle,
                EntityType = (byte)EntityType.Player,
                StreamedIn = false, // change me
                LocalOnly = false,

                BlipSprite = -1,
                BlipColor = -1,
                BlipAlpha = 255,
                Alpha = 255,
                Team = -1,
            };

            lock (ClientMap)
            {
                ClientMap.Add(netHandle, rem);
            }
            return rem;
        }

        public bool IsLocalPlayer(IStreamedItem item)
        {
            if (item == null) return false;
            return NetToEntity(item.RemoteHandle)?.Handle == Game.Player.Character.Handle;
        }

        public int Count(Type type)
        {
            return ClientMap.Count(item => item.GetType() == type);
        }

        public void ClearAll()
        {
            lock (ClientMap)
            {
                foreach (var pair in ClientMap.Values)
                {
                    if (!pair.StreamedIn) continue;
                    StreamOut(pair);
                }
                ClientMap.Clear();
                HandleMap.Clear();
                _localHandleCounter = 0;
            }

        }
        #endregion

        #region Draw
        public void DrawMarkers()
        {
            var markers = new List<RemoteMarker>(ClientMap.Values.OfType<RemoteMarker>().Where(item => item.StreamedIn));

            for (var index = markers.Count - 1; index >= 0; index--)
            {
                var marker = markers[index];
                World.DrawMarker((MarkerType)marker.MarkerType, marker.Position.ToVector(), marker.Direction.ToVector(), marker.Rotation.ToVector(), marker.Scale.ToVector(), Color.FromArgb(marker.Alpha, marker.Red, marker.Green, marker.Blue), marker.BobUpAndDown);
            }

            // TODO: Uncomment to debug stuff
            /*
            foreach (var p in ClientMap.OfType<RemoteBlip>())
            {
                if (p == null || p.Position == null) continue;
                string text = (EntityType) p.EntityType + "\nId: " + p.RemoteHandle + "\nScale: " + p.Scale;

                DrawLabel3D(text, p.Position.ToVector(), 100f, 0.4f);
            }
            */
        }

        public void DrawLabels()
        {
            Vector3 origin = GameplayCamera.Position;
            var labels = new List<RemoteTextLabel>(ClientMap.Values.OfType<RemoteTextLabel>().Where(item => item.StreamedIn));
            for (var index = labels.Count - 1; index >= 0; index--)
            {
                var label = labels[index];
                DrawLabel3D(origin, label.Text, label.Position.ToVector(), label.Range, label.Size, Color.FromArgb(label.Alpha, label.Red, label.Green, label.Blue), label.EntitySeethrough);
            }
        }

        private static void DrawLabel3D(Vector3 origin, string text, Vector3 position, float range, float size, Color col, bool entitySeethrough)
        {
            float distanceSquared = position.DistanceToSquared(origin);
            float rangeSquared = range * range;

            if (string.IsNullOrWhiteSpace(text) ||
                distanceSquared >= rangeSquared ||
                !Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, position.X, position.Y, position.Z, 1f)) return;

            var scale = Math.Max(0.3f, 1f - (distanceSquared / rangeSquared));

            Function.Call(Hash.SET_DRAW_ORIGIN, position.X, position.Y, position.Z);
            new UIResText(text, Point.Empty, size * scale, col)
            {
                TextAlignment = UIResText.Alignment.Centered,
                Outline = true
            }.Draw();
            Function.Call(Hash.CLEAR_DRAW_ORIGIN);
        }
        #endregion
    }
}
