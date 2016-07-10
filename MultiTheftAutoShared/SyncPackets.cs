using ProtoBuf;

namespace GTANetworkShared
{
    [ProtoContract]
    public class VehicleData
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public int? VehicleModelHash { get; set; }
        [ProtoMember(3)]
        public int? PedModelHash { get; set; }
        [ProtoMember(4)]
        public int? WeaponHash { get; set; }
        [ProtoMember(5)]
        public Vector3 Position { get; set; }
        [ProtoMember(6)]
        public Vector3 Quaternion { get; set; }
        [ProtoMember(7)]
        public short? VehicleSeat { get; set; }
        [ProtoMember(8)]
        public float? VehicleHealth { get; set; }
        [ProtoMember(9)]
        public byte? PlayerHealth { get; set; }
        [ProtoMember(10)]
        public float? Latency { get; set; }
        [ProtoMember(11)]
        public int? VehicleHandle { get; set; }
        [ProtoMember(12)]
        public int? NetHandle { get; set; }
        [ProtoMember(13)]
        public Vector3 Velocity { get; set; }
        [ProtoMember(14)]
        public byte? PedArmor { get; set; }
        [ProtoMember(15)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(16)]
        public float? RPM { get; set; }
        [ProtoMember(17)]
        public byte? Flag { get; set; }
        [ProtoMember(18)]
        public float? Steering { get; set; }
        [ProtoMember(19)]
        public Vector3 Trailer { get; set; }
    }

    [ProtoContract]
    public class PedData
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public int? PedModelHash { get; set; }
        [ProtoMember(3)]
        public Vector3 Position { get; set; }
        [ProtoMember(4)]
        public Vector3 Quaternion { get; set; }
        [ProtoMember(5)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(6)]
        public int? WeaponHash { get; set; }
        [ProtoMember(7)]
        public byte? PlayerHealth { get; set; }
        [ProtoMember(8)]
        public float? Latency { get; set; }
        [ProtoMember(9)]
        public int? NetHandle { get; set; }
        [ProtoMember(10)]
        public byte? Speed { get; set; }
        [ProtoMember(11)]
        public byte? PedArmor { get; set; }
        [ProtoMember(12)]
        public int? Flag { get; set; }
        [ProtoMember(13)]
        public Vector3 Velocity { get; set; }
    }

}
