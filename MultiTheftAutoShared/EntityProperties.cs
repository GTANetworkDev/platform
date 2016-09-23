using System;
using System.Collections.Generic;
using GTANetworkShared;
using ProtoBuf;

namespace GTANetworkShared
{
    public enum EntityType
    {
        Vehicle = 1,
        Prop = 2,
        Blip = 3,
        Marker = 4,
        Pickup = 5,
        Player = 6,
        TextLabel = 7,
        Ped = 8,
        Particle = 9,

        World = 255,
    }

    public enum EntityFlag
    {
        Collisionless = 1 << 0,
        EngineOff = 1 << 1,
        SpecialLight = 1 << 2,
        PlayerSpectating = 1 << 3,
        VehicleLocked = 1 << 4,
    }

    [ProtoContract]
    public class VehicleDamageModel
    {
        [ProtoMember(1)]
        public byte BrokenWindows { get; set; }

        [ProtoMember(2)]
        public byte BrokenDoors { get; set; }

        [ProtoMember(3)]
        public int BrokenLights { get; set; }
    }

    [ProtoContract]
    public class Attachment
    {
        [ProtoMember(1)]
        public int NetHandle { get; set; }
        [ProtoMember(2)]
        public Vector3 PositionOffset { get; set; }
        [ProtoMember(3)]
        public Vector3 RotationOffset { get; set; }
        [ProtoMember(4)]
        public string Bone { get; set; }
    }

    [ProtoContract]
    public class Movement
    {
        [ProtoMember(1)]
        public long Duration { get; set; }

        [ProtoMember(2)]
        public long Start { get; set; }

        [ProtoMember(3)]
        public Vector3 StartVector { get; set; }

        [ProtoMember(4)]
        public Vector3 EndVector { get; set; }

        public long ServerStartTime { get; set; }
    }

    [ProtoContract]
    [ProtoInclude(14, typeof(VehicleProperties))]
    [ProtoInclude(15, typeof(BlipProperties))]
    [ProtoInclude(16, typeof(MarkerProperties))]
    [ProtoInclude(17, typeof(PickupProperties))]
    [ProtoInclude(18, typeof(PlayerProperties))]
    [ProtoInclude(19, typeof(TextLabelProperties))]
    [ProtoInclude(20, typeof(WorldProperties))]
    [ProtoInclude(21, typeof(PedProperties))]
    [ProtoInclude(22, typeof(ParticleProperties))]
    public class EntityProperties
    {
        [ProtoMember(1)]
        public Vector3 Position { get; set; }

        [ProtoMember(2)]
        public Vector3 Rotation { get; set; }

        [ProtoMember(3)]
        public int ModelHash { get; set; }

        [ProtoMember(4)]
        public byte EntityType { get; set; }

        [ProtoMember(5)]
        public byte Alpha { get; set; }

        [ProtoMember(6)]
        public int Dimension { get; set; }

        [ProtoMember(7)]
        public Attachment AttachedTo { get; set; }

        [ProtoMember(8)]
        public List<int> Attachables { get; set; }

        [ProtoMember(9)]
        public byte Flag { get; set; }

        [ProtoMember(10)]
        public Dictionary<string, NativeArgument> SyncedProperties { get; set; }

        [ProtoMember(11)]
        public Movement PositionMovement { get; set; }

        [ProtoMember(12)]
        public Movement RotationMovement { get; set; }

        [ProtoMember(13)]
        public bool IsInvincible { get; set; }
    }

    [ProtoContract]
    public class VehicleProperties : EntityProperties
    {
        public VehicleProperties()
        {
            Mods = new Dictionary<byte, int>();
            EntityType = (byte)GTANetworkShared.EntityType.Vehicle;
        }

        [ProtoMember(1)]
        public int PrimaryColor { get; set; }

        [ProtoMember(2)]
        public int SecondaryColor { get; set; }

        [ProtoMember(3)]
        public float Health { get; set; }

        [ProtoMember(4)]
        public bool IsDead { get; set; }

        [ProtoMember(5)]
        public Dictionary<byte, int> Mods { get; set; }

        [ProtoMember(6)]
        public bool Siren { get; set; }

        [ProtoMember(7)]
        public byte Doors { get; set; }

        [ProtoMember(8)]
        public int Trailer { get; set; }

        [ProtoMember(9)]
        public byte Tires { get; set; }

        [ProtoMember(10)]
        public int Livery { get; set; }

        [ProtoMember(11)]
        public string NumberPlate { get; set; }

        [ProtoMember(12)]
        public short VehicleComponents { get; set; }

        [ProtoMember(13)]
        public int TraileredBy { get; set; }

        [ProtoMember(14)]
        public VehicleDamageModel DamageModel { get; set; }
    }

    [ProtoContract]
    public class BlipProperties : EntityProperties
    {
        public BlipProperties()
        {
            EntityType = (byte)GTANetworkShared.EntityType.Blip;
        }

        [ProtoMember(1)]
        public int Sprite { get; set; }

        [ProtoMember(2)]
        public float Scale { get; set; }

        [ProtoMember(3)]
        public int Color { get; set; }

        [ProtoMember(4)]
        public bool IsShortRange { get; set; }

        [ProtoMember(5)]
        public int AttachedNetEntity { get; set; }

        [ProtoMember(6)]
        public float RangedBlip { get; set; }

        [ProtoMember(7)]
        public string Name { get; set; }
    }

    [ProtoContract]
    public class MarkerProperties : EntityProperties
    {
        public MarkerProperties()
        {
            EntityType = (byte)GTANetworkShared.EntityType.Marker;
        }

        [ProtoMember(1)]
        public Vector3 Direction { get; set; }

        [ProtoMember(2)]
        public int MarkerType { get; set; }

        [ProtoMember(3)]
        public int Red { get; set; }

        [ProtoMember(4)]
        public int Green { get; set; }

        [ProtoMember(5)]
        public int Blue { get; set; }

        [ProtoMember(7)]
        public Vector3 Scale { get; set; }
    }

    [ProtoContract]
    public class PickupProperties : EntityProperties
    {
        public PickupProperties()
        {
            EntityType = (byte)GTANetworkShared.EntityType.Pickup;
        }

        [ProtoMember(1)]
        public int Amount { get; set; }

        [ProtoMember(2)]
        public bool PickedUp { get; set; }

        [ProtoMember(3)]
        public uint RespawnTime { get; set; }

        [ProtoMember(4)]
        public int CustomModel { get; set; }
    }

    [ProtoContract]
    public class PlayerProperties : EntityProperties
    {
        public PlayerProperties()
        {
            Props = new Dictionary<byte, byte>();
            Textures = new Dictionary<byte, byte>();
            Accessories = new Dictionary<byte, Tuple<byte, byte>>();
            EntityType = (byte)GTANetworkShared.EntityType.Player;
            WeaponTints = new Dictionary<int, byte>();
            WeaponComponents = new Dictionary<int, List<int>>();
        }

        [ProtoMember(1)]
        public Dictionary<byte, byte> Props { get; set; }

        [ProtoMember(2)]
        public Dictionary<byte, byte> Textures { get; set; }

        [ProtoMember(3)]
        public int BlipSprite { get; set; }

        [ProtoMember(4)]
        public int Team { get; set; }

        [ProtoMember(5)]
        public int BlipColor { get; set; }

        [ProtoMember(6)]
        public byte BlipAlpha { get; set; }

        [ProtoMember(7)]
        public Dictionary<byte, Tuple<byte, byte>> Accessories { get; set; }

        [ProtoMember(8)]
        public string Name { get; set; }

        [ProtoMember(9)]
        public Dictionary<int, byte> WeaponTints { get; set; }

        [ProtoMember(10)]
        public Dictionary<int, List<int>> WeaponComponents { get; set; } 

        [ProtoMember(11)]
        public string NametagText { get; set; }

        [ProtoMember(12)]
        public int NametagSettings { get; set; }
    }

    [ProtoContract]
    public class TextLabelProperties : EntityProperties
    {
        public TextLabelProperties()
        {
            EntityType = (byte) GTANetworkShared.EntityType.TextLabel;
        }

        [ProtoMember(1)]
        public string Text { get; set; }

        [ProtoMember(2)]
        public int Red { get; set; }

        [ProtoMember(3)]
        public int Green { get; set; }

        [ProtoMember(4)]
        public int Blue { get; set; }

        [ProtoMember(5)]
        public float Size { get; set; }

        [ProtoMember(6)]
        public float Range { get; set; }

        [ProtoMember(7)]
        public bool EntitySeethrough { get; set; }
    }

    [ProtoContract]
    public class PedProperties : EntityProperties
    {
        public PedProperties()
        {
            EntityType = (byte)GTANetworkShared.EntityType.Player;
        }

        [ProtoMember(1)]
        public string LoopingAnimation { get; set; }
    }

    [ProtoContract]
    public class WorldProperties : EntityProperties
    {
        [ProtoMember(1)]
        public byte Hours { get; set; }

        [ProtoMember(2)]
        public byte Minutes { get; set; }

        [ProtoMember(3)]
        public string Weather { get; set; }

        [ProtoMember(4)]
        public List<string> LoadedIpl { get; set; }

        [ProtoMember(5)]
        public List<string> RemovedIpl { get; set; }
    }

    [ProtoContract]
    public class ParticleProperties : EntityProperties
    {
        public ParticleProperties()
        {
            EntityType = (byte) GTANetworkShared.EntityType.Particle;
        }

        [ProtoMember(1)]
        public string Library { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public float Scale { get; set; }

        [ProtoMember(4)]
        public int EntityAttached { get; set; }

        [ProtoMember(5)]
        public int BoneAttached { get; set; }
    }

    /*
     * DELTA COMPRESSION
     * */

    #region DeltaCompressed
    [ProtoContract]
    [ProtoInclude(14, typeof(Delta_VehicleProperties))]
    [ProtoInclude(15, typeof(Delta_BlipProperties))]
    [ProtoInclude(16, typeof(Delta_MarkerProperties))]
    [ProtoInclude(17, typeof(Delta_PickupProperties))]
    [ProtoInclude(18, typeof(Delta_PlayerProperties))]
    [ProtoInclude(19, typeof(Delta_TextLabelProperties))]
    [ProtoInclude(20, typeof(Delta_WorldProperties))]
    [ProtoInclude(21, typeof(Delta_PedProperties))]
    [ProtoInclude(22, typeof(Delta_ParticleProperties))]
    public class Delta_EntityProperties
    {
        [ProtoMember(1)]
        public Vector3 Position { get; set; }

        [ProtoMember(2)]
        public Vector3 Rotation { get; set; }

        [ProtoMember(3)]
        public int? ModelHash { get; set; }

        [ProtoMember(4)]
        public byte? EntityType { get; set; }

        [ProtoMember(5)]
        public byte? Alpha { get; set; }

        [ProtoMember(6)]
        public int? Dimension { get; set; }

        [ProtoMember(7)]
        public Attachment AttachedTo { get; set; }

        [ProtoMember(8)]
        public List<int> Attachables { get; set; }

        [ProtoMember(9)]
        public byte? Flag { get; set; }

        [ProtoMember(10)]
        public Dictionary<string, NativeArgument> SyncedProperties { get; set; }

        [ProtoMember(11)]
        public Movement PositionMovement { get; set; }

        [ProtoMember(12)]
        public Movement RotationMovement { get; set; }

        [ProtoMember(13)]
        public bool? IsInvincible { get; set; }
    }

    [ProtoContract]
    public class Delta_VehicleProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public int? PrimaryColor { get; set; }

        [ProtoMember(2)]
        public int? SecondaryColor { get; set; }

        [ProtoMember(3)]
        public float? Health { get; set; }

        [ProtoMember(4)]
        public bool? IsDead { get; set; }

        [ProtoMember(5)]
        public Dictionary<byte, int> Mods { get; set; }

        [ProtoMember(6)]
        public bool? Siren { get; set; }

        [ProtoMember(7)]
        public byte? Doors { get; set; }

        [ProtoMember(8)]
        public int? Trailer { get; set; }

        [ProtoMember(9)]
        public byte? Tires { get; set; }

        [ProtoMember(10)]
        public int? Livery { get; set; }

        [ProtoMember(11)]
        public string NumberPlate { get; set; }

        [ProtoMember(12)]
        public short? VehicleComponents { get; set; }

        [ProtoMember(13)]
        public int? TraileredBy { get; set; }

        [ProtoMember(14)]
        public VehicleDamageModel DamageModel { get; set; }
    }

    [ProtoContract]
    public class Delta_BlipProperties : Delta_EntityProperties
    {

        [ProtoMember(1)]
        public int? Sprite { get; set; }

        [ProtoMember(2)]
        public float? Scale { get; set; }

        [ProtoMember(3)]
        public int? Color { get; set; }

        [ProtoMember(4)]
        public bool? IsShortRange { get; set; }

        [ProtoMember(5)]
        public int? AttachedNetEntity { get; set; }

        [ProtoMember(6)]
        public float? RangedBlip { get; set; }

        [ProtoMember(7)]
        public string Name { get; set; }
    }

    [ProtoContract]
    public class Delta_MarkerProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public Vector3 Direction { get; set; }

        [ProtoMember(2)]
        public int? MarkerType { get; set; }

        [ProtoMember(3)]
        public int? Red { get; set; }

        [ProtoMember(4)]
        public int? Green { get; set; }

        [ProtoMember(5)]
        public int? Blue { get; set; }

        [ProtoMember(7)]
        public Vector3 Scale { get; set; }
    }

    [ProtoContract]
    public class Delta_PickupProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public int? Amount { get; set; }

        [ProtoMember(2)]
        public bool? PickedUp { get; set; }

        [ProtoMember(3)]
        public uint? RespawnTime { get; set; }

        [ProtoMember(4)]
        public int? CustomModel { get; set; }
    }

    [ProtoContract]
    public class Delta_PlayerProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public Dictionary<byte, byte> Props { get; set; }

        [ProtoMember(2)]
        public Dictionary<byte, byte> Textures { get; set; }

        [ProtoMember(3)]
        public int? BlipSprite { get; set; }

        [ProtoMember(4)]
        public int? Team { get; set; }

        [ProtoMember(5)]
        public int? BlipColor { get; set; }

        [ProtoMember(6)]
        public byte? BlipAlpha { get; set; }

        [ProtoMember(7)]
        public Dictionary<byte, Tuple<byte, byte>> Accessories { get; set; }

        [ProtoMember(8)]
        public string Name { get; set; }

        [ProtoMember(9)]
        public Dictionary<int, byte> WeaponTints { get; set; }

        [ProtoMember(10)]
        public Dictionary<int, List<int>> WeaponComponents { get; set; }

        [ProtoMember(11)]
        public string NametagText { get; set; }

        [ProtoMember(12)]
        public int? NametagSettings { get; set; }
    }

    [ProtoContract]
    public class Delta_TextLabelProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public string Text { get; set; }

        [ProtoMember(2)]
        public int? Red { get; set; }

        [ProtoMember(3)]
        public int? Green { get; set; }

        [ProtoMember(4)]
        public int? Blue { get; set; }

        [ProtoMember(5)]
        public float? Size { get; set; }

        [ProtoMember(6)]
        public float? Range { get; set; }

        [ProtoMember(7)]
        public bool? EntitySeethrough { get; set; }
    }

    [ProtoContract]
    public class Delta_PedProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public string LoopingAnimation { get; set; }
    }

    [ProtoContract]
    public class Delta_WorldProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public byte? Hours { get; set; }

        [ProtoMember(2)]
        public byte? Minutes { get; set; }

        [ProtoMember(3)]
        public string Weather { get; set; }

        [ProtoMember(4)]
        public List<string> LoadedIpl { get; set; }

        [ProtoMember(5)]
        public List<string> RemovedIpl { get; set; }
    }

    [ProtoContract]
    public class Delta_ParticleProperties : Delta_EntityProperties
    {
        [ProtoMember(1)]
        public string Library { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public float? Scale { get; set; }

        [ProtoMember(4)]
        public int? EntityAttached { get; set; }

        [ProtoMember(5)]
        public int? BoneAttached { get; set; }
    }

    #endregion
}
