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
    }

    [ProtoContract]
    [ProtoInclude(7, typeof(VehicleProperties))]
    [ProtoInclude(8, typeof(BlipProperties))]
    [ProtoInclude(9, typeof(MarkerProperties))]
    [ProtoInclude(10, typeof(PickupProperties))]
    [ProtoInclude(11, typeof(PedProperties))]
    [ProtoInclude(12, typeof(TextLabelProperties))]
    public class EntityProperties
    {
        public EntityProperties()
        {
            Alpha = 255;
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
    }

    [ProtoContract]
    public class VehicleProperties : EntityProperties
    {
        public VehicleProperties()
        {
            Mods = new Dictionary<int, int>();

            Health = 1000;
            Doors = new bool[7];
            Tires = new bool[8];
            Livery = 0;
            NumberPlate = "NETWORK";

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
    }

    [ProtoContract]
    public class BlipProperties : EntityProperties
    {
        public BlipProperties()
        {
            Sprite = 0;
            Scale = 1f;
            EntityType = (byte)GTANetworkShared.EntityType.Blip;
            AttachedNetEntity = 0;
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
    }

    [ProtoContract]
    public class PedProperties : EntityProperties
    {
        public PedProperties()
        {
            Props = new Dictionary<byte, byte>();
            Textures = new Dictionary<byte, byte>();
            Accessories = new Dictionary<byte, Tuple<byte, byte>>();
            BlipSprite = 1;
            BlipAlpha = 255;
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

    /*
     * DELTA COMPRESSION
     * */

    #region DeltaCompressed
    [ProtoContract]
    [ProtoInclude(7, typeof(Delta_VehicleProperties))]
    [ProtoInclude(8, typeof(Delta_BlipProperties))]
    [ProtoInclude(9, typeof(Delta_MarkerProperties))]
    [ProtoInclude(10, typeof(Delta_PickupProperties))]
    [ProtoInclude(11, typeof(Delta_PedProperties))]
    [ProtoInclude(12, typeof(Delta_TextLabelProperties))]
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
    #endregion
}
