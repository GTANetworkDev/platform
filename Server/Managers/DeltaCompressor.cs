using System.Collections.Generic;
using GTANetworkShared;

namespace GTANetworkServer.Managers
{
    internal class DeltaCompressor
    {
        public DeltaCompressor(Client client)
        {
            _parent = client;
        }

        private Client _parent;

        public Dictionary<int, object> DataReceived = new Dictionary<int, object>();
        public object LastPacketReceived;

        public object DecompressData(object compressedPacket)
        {
            if (LastPacketReceived == null || compressedPacket.GetType() != LastPacketReceived.GetType())
            {
                LastPacketReceived = compressedPacket;
                return compressedPacket;
            }
            else if (LastPacketReceived is PedData && compressedPacket is PedData)
            {
                var fullPacket = (PedData) LastPacketReceived;
                var compPacket = (PedData) compressedPacket;

                if (compPacket.PedModelHash != null) fullPacket.PedModelHash = compPacket.PedModelHash;
                if (compPacket.Position != null) fullPacket.Position = compPacket.Position;
                if (compPacket.Quaternion != null) fullPacket.Quaternion = compPacket.Quaternion;
                if (compPacket.AimCoords != null) fullPacket.AimCoords = compPacket.AimCoords;
                if (compPacket.WeaponHash != null) fullPacket.WeaponHash = compPacket.WeaponHash;
                if (compPacket.PlayerHealth != null) fullPacket.PlayerHealth = compPacket.PlayerHealth;
                if (compPacket.Latency != null) fullPacket.Latency = compPacket.Latency;
                if (compPacket.NetHandle != null) fullPacket.NetHandle = compPacket.NetHandle;
                if (compPacket.Speed != null) fullPacket.Speed = compPacket.Speed;
                if (compPacket.PedArmor != null) fullPacket.PedArmor = compPacket.PedArmor;
                if (compPacket.Action != null) fullPacket.Action = compPacket.Action;
                if (compPacket.Velocity != null) fullPacket.Velocity = compPacket.Velocity;

                return fullPacket;
            }
            else if (LastPacketReceived is VehicleData && compressedPacket is VehicleData)
            {
                var fullPacket = (VehicleData)LastPacketReceived;
                var compPacket = (VehicleData)compressedPacket;

                if (compPacket.PedModelHash != null) fullPacket.PedModelHash = compPacket.PedModelHash;
                if (compPacket.WeaponHash != null) fullPacket.WeaponHash = compPacket.WeaponHash;
                if (compPacket.Position != null) fullPacket.Position = compPacket.Position;
                if (compPacket.Quaternion != null) fullPacket.Quaternion = compPacket.Quaternion;
                if (compPacket.VehicleSeat != null) fullPacket.VehicleSeat = compPacket.VehicleSeat;
                if (compPacket.VehicleHealth != null) fullPacket.VehicleHealth = compPacket.VehicleHealth;
                if (compPacket.PlayerHealth != null) fullPacket.PlayerHealth = compPacket.PlayerHealth;
                if (compPacket.Latency != null) fullPacket.Latency = compPacket.Latency;
                if (compPacket.VehicleHandle != null) fullPacket.VehicleHandle = compPacket.VehicleHandle;
                if (compPacket.NetHandle != null) fullPacket.NetHandle = compPacket.NetHandle;
                if (compPacket.Velocity != null) fullPacket.Velocity = compPacket.Velocity;
                if (compPacket.PedArmor != null) fullPacket.PedArmor = compPacket.PedArmor;
                if (compPacket.AimCoords != null) fullPacket.AimCoords = compPacket.AimCoords;
                if (compPacket.RPM != null) fullPacket.RPM = compPacket.RPM;
                if (compPacket.Flag != null) fullPacket.Flag = compPacket.Flag;
                if (compPacket.Steering != null) fullPacket.Steering = compPacket.Steering;
                if (compPacket.Trailer != null) fullPacket.Trailer = compPacket.Trailer;

                return fullPacket;
            }

            return null;
        }

        public object PositionalCompressData(int netHandle, object fullPacket)
        {
            if (fullPacket is PedData)
            {
                var full = (PedData)fullPacket;

                if ((_parent.Position - full.Position).LengthSquared() > 40000f) // 200 * 200
                {
                    var compressed = new PedData();
                    compressed.NetHandle = netHandle;
                    compressed.Position = full.Position;
                    compressed.Quaternion = full.Quaternion;
                    return compressed;
                }
                else
                {
                    return fullPacket;
                }
            }
            else if (fullPacket is VehicleData)
            {
                var full = (VehicleData)fullPacket;
                if ((_parent.Position - full.Position).LengthSquared() > 40000f) // 200 * 200
                {
                    var compressed = new VehicleData();
                    compressed.NetHandle = netHandle;
                    compressed.VehicleHandle = ((VehicleData) fullPacket).VehicleHandle;
                    compressed.Position = full.Position;
                    compressed.Quaternion = full.Quaternion;
                    return compressed;
                }
                else
                {
                    return fullPacket;
                }
            }

            return null; // This should not happen
        }

        public object CompressData(int netHandle, object fullPacket)
        {
            if (!DataReceived.ContainsKey(netHandle))
            {
                DataReceived.Add(netHandle, fullPacket);
                return fullPacket;
            }
            else if (DataReceived[netHandle].GetType() != fullPacket.GetType())
            {
                DataReceived[netHandle] = fullPacket;
                return fullPacket;
            }
            else if (fullPacket is PedData && DataReceived[netHandle] is PedData)
            {
                var full = (PedData)fullPacket;
                var comparable = (PedData)DataReceived[netHandle];
                var compressed = new PedData();
                compressed.NetHandle = netHandle;

                if ((_parent.Position - full.Position).LengthSquared() < 4000f) // 200 * 200
                {
                    compressed.Position = full.Position;
                }
                else
                {
                    if (full.PedModelHash != comparable.PedModelHash) compressed.PedModelHash = full.PedModelHash;
                    if (full.Position != comparable.Position) compressed.Position = full.Position;
                    if (full.Quaternion != comparable.Quaternion) compressed.Quaternion = full.Quaternion;
                    if (full.AimCoords != comparable.AimCoords) compressed.AimCoords = full.AimCoords;
                    if (full.WeaponHash != comparable.WeaponHash) compressed.WeaponHash = full.WeaponHash;
                    if (full.PlayerHealth != comparable.PlayerHealth) compressed.PlayerHealth = full.PlayerHealth;
                    if (full.Latency != comparable.Latency) compressed.Latency = full.Latency;
                    if (full.NetHandle != comparable.NetHandle) compressed.NetHandle = full.NetHandle;
                    if (full.Speed != comparable.Speed) compressed.Speed = full.Speed;
                    if (full.PedArmor != comparable.PedArmor) compressed.PedArmor = full.PedArmor;
                    if (full.Action != comparable.Action) compressed.Action = full.Action;
                    if (full.Velocity != comparable.Velocity) compressed.Velocity = full.Velocity;
                }
                DataReceived[netHandle] = full;

                return compressed;
            }
            else if (fullPacket is VehicleData && DataReceived[netHandle] is VehicleData)
            {
                var full = (VehicleData)fullPacket;
                var comparable = (VehicleData)DataReceived[netHandle];
                var compressed = new VehicleData();
                compressed.NetHandle = netHandle;

                if ((_parent.Position - full.Position).LengthSquared() < 4000f) // 200 * 200
                {
                    compressed.Position = full.Position;
                    if (full.VehicleHandle != full.VehicleHandle) compressed.VehicleHandle = full.VehicleHandle;
                }
                else
                {
                    if (full.PedModelHash != comparable.PedModelHash) compressed.PedModelHash = full.PedModelHash;
                    if (full.WeaponHash != comparable.WeaponHash) compressed.WeaponHash = full.WeaponHash;
                    if (full.Position != comparable.Position) compressed.Position = full.Position;
                    if (full.Quaternion != comparable.Quaternion) compressed.Quaternion = full.Quaternion;
                    if (full.VehicleSeat != comparable.VehicleSeat) compressed.VehicleSeat = full.VehicleSeat;
                    if (full.VehicleHealth != comparable.VehicleHealth) compressed.VehicleHealth = full.VehicleHealth;
                    if (full.PlayerHealth != comparable.PlayerHealth) compressed.PlayerHealth = full.PlayerHealth;
                    if (full.Latency != comparable.Latency) compressed.Latency = full.Latency;
                    if (full.VehicleHandle != comparable.VehicleHandle) compressed.VehicleHandle = full.VehicleHandle;
                    if (full.NetHandle != comparable.NetHandle) compressed.NetHandle = full.NetHandle;
                    if (full.Velocity != comparable.Velocity) compressed.Velocity = full.Velocity;
                    if (full.PedArmor != comparable.PedArmor) compressed.PedArmor = full.PedArmor;
                    if (full.AimCoords != comparable.AimCoords) compressed.AimCoords = full.AimCoords;
                    if (full.RPM != comparable.RPM) compressed.RPM = full.RPM;
                    if (full.Flag != comparable.Flag) compressed.Flag = full.Flag;
                    if (full.Steering != comparable.Steering) compressed.Steering = full.Steering;
                    if (full.Trailer != comparable.Trailer) compressed.Trailer = full.Trailer;
                }

                return compressed;
            }

            return null; // This should not happen
        }

    }
}
