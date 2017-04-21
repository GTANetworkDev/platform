using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTANetwork.Util;
using GTANetworkShared;
using Lidgren.Network;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork.Streamer
{
    public class UnoccupiedVehSync : Script
    {
        public UnoccupiedVehSync() { Tick += OnTick; }
        private static long _lastCheck;

        private static long _lastEntityRemoval;
        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected() && Util.Util.TickCount - _lastEntityRemoval > 500) // Save ressource
            {
                _lastEntityRemoval = Util.Util.TickCount;
                RemoteVehicle[] myCars;
                if (StreamerThread.StreamedInVehicles == null || StreamerThread.StreamedInVehicles.Length == 0) return;

                lock (StreamerThread.StreamedInVehicles)
                {
                    myCars = StreamerThread.StreamedInVehicles.ToArray();
                }

                for (var index = myCars.Length - 1; index >= 0; index--)
                {
                    var entity_ = myCars[index];
                    if (entity_ == null) continue;

                    var entity = new Vehicle(entity_.LocalHandle);

                    if (Util.Util.IsVehicleEmpty(entity) && !Main.VehicleSyncManager.IsInterpolating(entity.Handle) &&
                        entity_.TraileredBy == 0 && !Main.VehicleSyncManager.IsSyncing(entity_) &&
                        ((entity.Handle == Game.Player.LastVehicle?.Handle &&
                          DateTime.Now.Subtract(Events.LastCarEnter).TotalMilliseconds > 3000) ||
                         entity.Handle != Game.Player.LastVehicle?.Handle))
                    {
                        if (entity.Position.DistanceToSquared(entity_.Position.ToVector()) > 2f)
                        {
                            entity.PositionNoOffset = entity_.Position.ToVector();
                            entity.Quaternion = entity_.Rotation.ToVector().ToQuaternion();
                        }
                    }

                    //veh.Position = entity.Position.ToLVector();
                    //veh.Rotation = entity.Rotation.ToLVector();
                }
            }

        }

    }


    internal class UnoccupiedVehicleSync
    {
        private List<RemoteVehicle> SyncedVehicles = new List<RemoteVehicle>();
        private const int UNOCCUPIED_VEH_RATE = 400;
        private long _lastUpdate;

        private Dictionary<int, UnoccupiedVehicleInterpolator> Interpolations = new Dictionary<int, UnoccupiedVehicleInterpolator>();

        internal void Interpolate(int netHandle, int gameHandle, Vector3 newPos, GTANetworkShared.Vector3 newVelocity, Vector3 newRotation)
        {
            if (!Interpolations.ContainsKey(netHandle))
            {
                var interp = new UnoccupiedVehicleInterpolator(gameHandle, netHandle);
                interp.SetTargetPosition(newPos, newVelocity, newRotation);

                Interpolations.Set(netHandle, interp);
            }
            else
            {
                Interpolations[netHandle].SetTargetPosition(newPos, newVelocity, newRotation);
            }
        }

        internal bool IsInterpolating(int gameHandle)
        {
            return Interpolations.Any(p => p.Value.GameHandle == gameHandle);
        }

        internal void StartSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;

            if (veh != null)
            {
                lock (SyncedVehicles)
                {
                    SyncedVehicles.Add(veh);
                }

                if (veh.StreamedIn)
                {
                    var ent = Main.NetEntityHandler.NetToEntity(veh);
                    if (ent != null)
                    {
                        ent.IsInvincible = veh.IsInvincible;
                    }
                }
            }
        }

        internal bool IsSyncing(RemoteVehicle veh)
        {
            lock (SyncedVehicles)
            {
                return SyncedVehicles.Contains(veh);
            }
        }

        internal void StopSyncing(int vehicle)
        {
            var veh = Main.NetEntityHandler.NetToStreamedItem(vehicle) as RemoteVehicle;

            if (veh != null)
            {
                lock (SyncedVehicles)
                {
                    SyncedVehicles.Remove(veh);
                }

                if (veh.StreamedIn)
                {
                    var ent = Main.NetEntityHandler.NetToEntity(veh);
                    if (ent != null) ent.IsInvincible = true;
                }
            }
        }

        internal void StopAll()
        {
            lock (SyncedVehicles)
            {
                SyncedVehicles.Clear();
            }
        }

        internal void Pulse()
        {
            if (Util.Util.TickCount - _lastUpdate > UNOCCUPIED_VEH_RATE)
            {
                _lastUpdate = Util.Util.TickCount;

                if (SyncedVehicles.Count > 0)
                {
                    var vehicleCount = 0;
                    var buffer = new List<byte>();

                    lock (SyncedVehicles)
                    {
                        foreach (var vehicle in SyncedVehicles)
                        {
                            var ent = Main.NetEntityHandler.NetToEntity(vehicle);

                            if (ent == null || !vehicle.StreamedIn) continue;

                            var dist = ent.Model.IsBoat ? ent.Position.DistanceToSquared2D(vehicle.Position.ToVector()) : ent.Position.DistanceToSquared(vehicle.Position.ToVector());

                            var veh = new Vehicle(ent.Handle);

                            byte BrokenDoors = 0;
                            byte BrokenWindows = 0;

                            for (var i = 0; i < 8; i++)
                            {
                                if (veh.Doors[(VehicleDoorIndex)i].IsBroken) BrokenDoors |= (byte)(1 << i);
                                if (!veh.Windows[(VehicleWindowIndex)i].IsIntact) BrokenWindows |= (byte)(1 << i);
                            }

                            var syncUnocVeh = dist > 2f ||
                                          ent.Rotation.DistanceToSquared(vehicle.Rotation.ToVector()) > 2f ||
                                          Math.Abs(new Vehicle(ent.Handle).EngineHealth - vehicle.Health) > 1f ||
                                          Util.Util.BuildTyreFlag(new Vehicle(ent.Handle)) != vehicle.Tires ||
                                          vehicle.DamageModel == null ||
                                          vehicle.DamageModel.BrokenWindows != BrokenWindows ||
                                          vehicle.DamageModel.BrokenDoors != BrokenDoors;

                            if (!syncUnocVeh) continue;
                            {
                                vehicle.Position = ent.Position.ToLVector();
                                vehicle.Rotation = ent.Rotation.ToLVector();
                                vehicle.Health = veh.EngineHealth;
                                vehicle.Tires = (byte)Util.Util.BuildTyreFlag(veh);

                                if (vehicle.DamageModel == null) vehicle.DamageModel = new VehicleDamageModel();

                                vehicle.DamageModel.BrokenWindows = BrokenWindows;
                                vehicle.DamageModel.BrokenDoors = BrokenDoors;

                                var data = new VehicleData
                                {
                                    VehicleHandle = vehicle.RemoteHandle,
                                    Position = vehicle.Position,
                                    Quaternion = vehicle.Rotation,
                                    Velocity = ent.Velocity.ToLVector(),
                                    VehicleHealth = vehicle.Health,
                                    DamageModel = new VehicleDamageModel()
                                    {
                                        BrokenWindows = BrokenWindows,
                                        BrokenDoors = BrokenDoors,
                                    }
                                };

                                if (ent.IsDead)
                                {
                                    data.Flag = (short) VehicleDataFlags.VehicleDead;
                                }
                                else
                                {
                                    data.Flag = 0;
                                }

                                byte tyreFlag = 0;

                                for (int i = 0; i < 8; i++) if (veh.IsTireBurst(i)) tyreFlag |= (byte)(1 << i);

                                data.PlayerHealth = tyreFlag;

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

                        Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, (int)ConnectionChannel.UnoccupiedVeh);

                        Main.BytesSent += buffer.Count;
                        Main.MessagesSent++;
                    }
                }
            }

            for (int i = Interpolations.Count - 1; i >= 0; i--)
            {
                var pair = Interpolations.ElementAt(i);

                pair.Value.Pulse();

                if (pair.Value.HasFinished) Interpolations.Remove(pair.Key);
            }
        }
    }

    internal class UnoccupiedVehicleInterpolator
    {
        internal int GameHandle;
        internal int NetHandle;
        private Vehicle _entity;
        private RemoteVehicle _prop;
        private GTANetworkShared.Vector3 _velocity;
        private Vector3 _rotation;

        private Vector3 _startPos;
        private Vector3 _startRot;

        private NetInterpolation NetInterpolation;

        internal bool HasFinished;

        internal UnoccupiedVehicleInterpolator(int gameHandle, int netHandle)
        {
            GameHandle = gameHandle;
            NetHandle = netHandle;
            _entity = new Vehicle(gameHandle);
            _prop = Main.NetEntityHandler.NetToStreamedItem(netHandle) as RemoteVehicle;
        }

        internal void SetTargetPosition(Vector3 targetPos, GTANetworkShared.Vector3 velocity, Vector3 rotation)
        {
            var dir = targetPos - _prop.Position.ToVector();

            NetInterpolation.vecTarget = targetPos;
            NetInterpolation.vecError = dir;

            NetInterpolation.StartTime = Util.Util.TickCount;
            NetInterpolation.FinishTime = NetInterpolation.StartTime + 400;
            NetInterpolation.LastAlpha = 0f;

            _velocity = velocity;
            _rotation = rotation;

            _startPos = _prop.Position.ToVector();
            _startRot = _prop.Rotation.ToVector();

            _prop.Rotation = rotation.ToLVector();
            _prop.Position = targetPos.ToLVector();

            HasFinished = false;
        }

        internal void Pulse()
        {
            if (!HasFinished)
            {
                long currentTime = Util.Util.TickCount;
                float alpha = Util.Util.Unlerp(NetInterpolation.StartTime, currentTime, NetInterpolation.FinishTime);

                alpha = Util.Util.Clamp(0f, alpha, 1.5f);

                Vector3 comp = Util.Util.Lerp(new Vector3(), alpha, NetInterpolation.vecError);

                if (alpha == 1.5f)
                {
                    NetInterpolation.FinishTime = 0;
                    HasFinished = true;
                }

                var newPos = _startPos + comp;
                _entity.Velocity = _velocity.ToVector() + 3 * (newPos - _entity.Position);

                _entity.Quaternion = GTA.Math.Quaternion.Slerp(_startRot.ToQuaternion(), _rotation.ToQuaternion(), Math.Min(1.5f, alpha));
            }
        }
    }

    struct NetInterpolation
    {
        internal GTA.Math.Vector3 vecStart;
        internal GTA.Math.Vector3 vecTarget;
        internal Vector3 vecError;
        internal long StartTime;
        internal long FinishTime;
        internal float LastAlpha;
    }
}