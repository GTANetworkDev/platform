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
        Ped = 6,
        TextLabel = 7,
        World = 255,
    }

    public enum EntityFlag
    {
        Collisionless = 1 << 0,
        EngineOff = 1 << 1,
        SpecialLight = 1 << 2,
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
    [ProtoInclude(11, typeof(VehicleProperties))]
    [ProtoInclude(12, typeof(BlipProperties))]
    [ProtoInclude(13, typeof(MarkerProperties))]
    [ProtoInclude(14, typeof(PickupProperties))]
    [ProtoInclude(15, typeof(PedProperties))]
    [ProtoInclude(16, typeof(TextLabelProperties))]
    [ProtoInclude(17, typeof(WorldProperties))]
    public class EntityProperties
    {
        public EntityProperties()
        {
        }

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
    }

    [ProtoContract]
    public class VehicleProperties : EntityProperties
    {
        public VehicleProperties()
        {
            Mods = new Dictionary<int, int>();

            Doors = new bool[7];
            Tires = new bool[8];
            
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
        public Dictionary<int, int> Mods { get; set; }

        [ProtoMember(6)]
        public bool Siren { get; set; }

        [ProtoMember(7)]
        public bool[] Doors { get; set; }

        [ProtoMember(8)]
        public int Trailer { get; set; }

        [ProtoMember(9)]
        public bool[] Tires { get; set; }

        [ProtoMember(10)]
        public int Livery { get; set; }

        [ProtoMember(11)]
        public string NumberPlate { get; set; }

        [ProtoMember(12)]
        public short VehicleComponents { get; set; }
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
    }

    [ProtoContract]
    public class PedProperties : EntityProperties
    {
        public PedProperties()
        {
            Props = new Dictionary<byte, byte>();
            Textures = new Dictionary<byte, byte>();
            Accessories = new Dictionary<byte, Tuple<byte, byte>>();
            EntityType = (byte)GTANetworkShared.EntityType.Ped;
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

    /*
     * DELTA COMPRESSION
     * */

    #region DeltaCompressed
    [ProtoContract]
    [ProtoInclude(11, typeof(Delta_VehicleProperties))]
    [ProtoInclude(12, typeof(Delta_BlipProperties))]
    [ProtoInclude(13, typeof(Delta_MarkerProperties))]
    [ProtoInclude(14, typeof(Delta_PickupProperties))]
    [ProtoInclude(15, typeof(Delta_PedProperties))]
    [ProtoInclude(16, typeof(Delta_TextLabelProperties))]
    [ProtoInclude(17, typeof(Delta_WorldProperties))]
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
        public Dictionary<int, int> Mods { get; set; }

        [ProtoMember(6)]
        public bool? Siren { get; set; }

        [ProtoMember(7)]
        public bool[] Doors { get; set; }

        [ProtoMember(8)]
        public int? Trailer { get; set; }

        [ProtoMember(9)]
        public bool[] Tires { get; set; }

        [ProtoMember(10)]
        public int? Livery { get; set; }

        [ProtoMember(11)]
        public string NumberPlate { get; set; }
        [ProtoMember(12)]
        public short? VehicleComponents { get; set; }
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
    }

    [ProtoContract]
    public class Delta_PedProperties : Delta_EntityProperties
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
        public bool EntitySeethrough { get; set; }
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

    #endregion
}
