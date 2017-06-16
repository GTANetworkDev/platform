using ProtoBuf;

namespace GTANetworkShared
{
    [ProtoContract]
    public class VehicleData
    {
        [ProtoMember(1)]
        public Vector3 Position { get; set; }
        [ProtoMember(2)]
        public int? VehicleHandle { get; set; }
        [ProtoMember(3)]
        public int? NetHandle { get; set; }
        [ProtoMember(4)]
        public int? PedModelHash { get; set; }
        [ProtoMember(5)]
        public int? WeaponHash { get; set; }
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
        public Vector3 Velocity { get; set; }
        [ProtoMember(12)]
        public byte? PedArmor { get; set; }
        [ProtoMember(13)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(14)]
        public float? RPM { get; set; }
        [ProtoMember(15)]
        public short? Flag { get; set; }
        [ProtoMember(16)]
        public float? Steering { get; set; }
        [ProtoMember(17)]
        public Vector3 Trailer { get; set; }
        [ProtoMember(18)]
        public VehicleDamageModel DamageModel { get; set; }
    }

    [ProtoContract]
    public class PedData
    {
        [ProtoMember(1)]
        public Vector3 Position { get; set; }
        [ProtoMember(2)]
        public int? NetHandle { get; set; }
        [ProtoMember(4)]
        public int? PedModelHash { get; set; }
        [ProtoMember(5)]
        public Vector3 Quaternion { get; set; }
        [ProtoMember(6)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(7)]
        public int? WeaponHash { get; set; }
        [ProtoMember(8)]
        public byte? PlayerHealth { get; set; }
        [ProtoMember(9)]
        public float? Latency { get; set; }
        [ProtoMember(10)]
        public byte? Speed { get; set; }
        [ProtoMember(11)]
        public byte? PedArmor { get; set; }
        [ProtoMember(12)]
        public byte? Action { get; set; }
        [ProtoMember(13)]
        public Vector3 Velocity { get; set; }
        [ProtoMember(14)]
        public int? VehicleTryingToEnter { get; set; }
        [ProtoMember(15)]
        public sbyte? SeatTryingToEnter { get; set; }
        [ProtoMember(16)]
        public int? WeaponAmmo { get; set; }
    }

}
