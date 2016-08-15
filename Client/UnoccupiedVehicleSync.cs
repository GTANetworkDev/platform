﻿using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetwork
{
    public class UnoccupiedVehicleSync
    {
        private List<RemoteVehicle> SyncedVehicles = new List<RemoteVehicle>();
        private const int UNOCCUPIED_VEH_RATE = 400;
        private long _lastUpdate;

        public void StartSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;
            
            if (veh != null)
            lock (SyncedVehicles)
            {
                SyncedVehicles.Add(veh);
            }
        }

        public bool IsSyncing(RemoteVehicle veh)
        {
            return SyncedVehicles.Contains(veh);
        }

        public void StopSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;

            if (veh != null)
            lock (SyncedVehicles)
            {
                SyncedVehicles.Remove(veh);
            }
        }

        public void StopAll()
        {
            lock (SyncedVehicles)
            {
                SyncedVehicles.Clear();
            }
        }

        public void Pulse()
        {
            foreach (var v in SyncedVehicles)
            {
                var pos = Game.Player.Character.Position;

                Function.Call(Hash.DRAW_LINE, pos.X, pos.Y, pos.Z, v.Position.X, v.Position.Y, v.Position.Z, 0, 0, 255, 255);
                UI.ShowSubtitle("Drawing line");

                if (Main.NetEntityHandler.NetToEntity(v) != null)
                {
                    var p = Main.NetEntityHandler.NetToEntity(v);

                    Function.Call(Hash.DRAW_LINE, pos.X, pos.Y, pos.Z, p.Position.X, p.Position.Y, p.Position.Z, 255,
                        0, 0, 255);
                }
            }

            if (Util.TickCount - _lastUpdate > UNOCCUPIED_VEH_RATE)
            {
                _lastUpdate = Util.TickCount;

                if (SyncedVehicles.Count > 0)
                {
                    int vehicleCount = 0;
                    List<byte> buffer = new List<byte>();

                    lock (SyncedVehicles)
                    {
                        foreach (var vehicle in SyncedVehicles.Where(v => v.StreamedIn))
                        {
                            var ent = Main.NetEntityHandler.NetToEntity(vehicle);

                            if (ent != null &&
                                (ent.Position.DistanceToSquared(vehicle.Position.ToVector()) > 0.5f ||
                                 ent.Rotation.DistanceToSquared(vehicle.Rotation.ToVector()) > 0.5f ||
                                 Math.Abs(new Vehicle(ent.Handle).EngineHealth - vehicle.Health) > 5f))
                            {
                                vehicle.Position = ent.Position.ToLVector();
                                vehicle.Rotation = ent.Rotation.ToLVector();
                                vehicle.Health = new Vehicle(ent.Handle).EngineHealth;

                                var data = new VehicleData();
                                data.VehicleHandle = vehicle.RemoteHandle;
                                data.Position = vehicle.Position;
                                data.Quaternion = vehicle.Rotation;
                                data.Velocity = ent.Velocity.ToLVector();
                                data.VehicleHealth = vehicle.Health;
                                if (ent.IsDead)
                                    data.Flag = (short) VehicleDataFlags.VehicleDead;
                                else
                                    data.Flag = 0;

                                var bin = PacketOptimization.WriteUnOccupiedVehicleSync(data);
                                
                                buffer.AddRange(bin);
                                vehicleCount++;
                            }
                        }
                    }

                    if (vehicleCount > 0)
                    {
                        buffer.Insert(0, (byte)vehicleCount);

                        var msg = Main.Client.CreateMessage();
                        msg.Write((byte)PacketType.UnoccupiedVehSync);
                        msg.Write(buffer.Count);
                        msg.Write(buffer.ToArray());

                        Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, (int) ConnectionChannel.UnoccupiedVeh);

                        Main._bytesSent += buffer.Count;
                        Main._messagesSent++;
                    }
                }
            }
        }


    }
}