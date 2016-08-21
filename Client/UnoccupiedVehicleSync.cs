using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Lidgren.Network;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public class UnoccupiedVehicleSync
    {
        private List<RemoteVehicle> SyncedVehicles = new List<RemoteVehicle>();
        private const int UNOCCUPIED_VEH_RATE = 400;
        private long _lastUpdate;

        private Dictionary<int, UnoccupiedVehicleInterpolator> Interpolations = new Dictionary<int, UnoccupiedVehicleInterpolator>();

        public void Interpolate(int netHandle, int gameHandle, Vector3 newPos, GTANetworkShared.Vector3 newVelocity, Vector3 newRotation)
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

        public bool IsInterpolating(int gameHandle)
        {
            return Interpolations.Any(p => p.Value.GameHandle == gameHandle);
        }

        public void StartSyncing(int vehicle)
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

        public bool IsSyncing(RemoteVehicle veh)
        {
            return SyncedVehicles.Contains(veh);
        }

        public void StopSyncing(int vehicle)
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

        public void StopAll()
        {
            lock (SyncedVehicles)
            {
                SyncedVehicles.Clear();
            }
        }

        public void Pulse()
        {
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

                            if (ent == null) continue;

                            float dist = 0f;

                            if (ent.Model.IsBoat)
                            {
                                dist = ent.Position.DistanceToSquared2D(vehicle.Position.ToVector());
                            }
                            else
                            {
                                dist = ent.Position.DistanceToSquared(vehicle.Position.ToVector());
                            }

                            if ((dist) > 2f ||
                                 ent.Rotation.DistanceToSquared(vehicle.Rotation.ToVector()) > 2f ||
                                 Math.Abs(new Vehicle(ent.Handle).EngineHealth - vehicle.Health) > 1f ||
                                 Util.BuildTyreFlag(new Vehicle(ent.Handle)) != vehicle.Tires)
                            {
                                var veh = new Vehicle(ent.Handle);

                                vehicle.Position = ent.Position.ToLVector();
                                vehicle.Rotation = ent.Rotation.ToLVector();
                                vehicle.Health = veh.EngineHealth;
                                vehicle.Tires = (byte)Util.BuildTyreFlag(veh);


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

                                byte tyreFlag = 0;

                                for (int i = 0; i < 8; i++)
                                {
                                    if (veh.IsTireBurst(i))
                                        tyreFlag |= (byte)(1 << i);
                                }

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

                        Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, (int) ConnectionChannel.UnoccupiedVeh);

                        Main._bytesSent += buffer.Count;
                        Main._messagesSent++;
                    }
                }
            }

            for (int i = Interpolations.Count - 1; i >= 0; i--)
            {
                var pair = Interpolations.ElementAt(i);

                pair.Value.Pulse();

                if (pair.Value.HasFinished)
                    Interpolations.Remove(pair.Key);
            }
        }
    }

    public class UnoccupiedVehicleInterpolator
    {
        public int GameHandle;
        public int NetHandle;
        private Vehicle _entity;
        private RemoteVehicle _prop;
        private GTANetworkShared.Vector3 _velocity;
        private Vector3 _rotation;

        private Vector3 _startPos;
        private Vector3 _startRot;

        private NetInterpolation NetInterpolation;

        public bool HasFinished;

        public UnoccupiedVehicleInterpolator(int gameHandle, int netHandle)
        {
            GameHandle = gameHandle;
            NetHandle = netHandle;
            _entity = new Vehicle(gameHandle);
            _prop = Main.NetEntityHandler.NetToStreamedItem(netHandle) as RemoteVehicle;
        }

        public void SetTargetPosition(Vector3 targetPos, GTANetworkShared.Vector3 velocity, Vector3 rotation)
        {
            var dir = targetPos - _prop.Position.ToVector();

            NetInterpolation.vecTarget = targetPos;
            NetInterpolation.vecError = dir;

            NetInterpolation.StartTime = Util.TickCount;
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

        public void Pulse()
        {
            if (!HasFinished)
            {
                long currentTime = Util.TickCount;
                float alpha = Util.Unlerp(NetInterpolation.StartTime, currentTime, NetInterpolation.FinishTime);

                alpha = Util.Clamp(0f, alpha, 1.5f);

                Vector3 comp = Util.Lerp(new Vector3(), alpha, NetInterpolation.vecError);

                if (alpha == 1.5f)
                {
                    NetInterpolation.FinishTime = 0;
                    HasFinished = true;
                }

                var newPos = _startPos + comp;
                _entity.Velocity = _velocity.ToVector() + 3*(newPos - _entity.Position);

                _entity.Quaternion = GTA.Math.Quaternion.Slerp(_startRot.ToQuaternion(),
                    _rotation.ToQuaternion(),
                    Math.Min(1.5f, alpha));
            }
        }
    }

    struct NetInterpolation
    {
        public GTA.Math.Vector3 vecStart;
        public GTA.Math.Vector3 vecTarget;
        public Vector3 vecError;
        public long StartTime;
        public long FinishTime;
        public float LastAlpha;
    }
}