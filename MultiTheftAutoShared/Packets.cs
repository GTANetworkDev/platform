using System;
using System.Collections.Generic;
using ProtoBuf;

namespace GTANetworkShared
{
    public enum PacketType
    {
        //VehiclePositionData = 0,
        ChatData = 1,
        PlayerDisconnect = 2,
        //PedPositionData = 3,
        NpcVehPositionData = 4,
        NpcPedPositionData = 5,
        WorldSharingStop = 6,
        DiscoveryResponse = 7,
        ConnectionRequest = 8,
        NativeCall = 9,
        NativeResponse = 10,
        PlayerRespawned = 11,
        NativeTick = 12,
        NativeTickRecall = 13,
        NativeOnDisconnect = 14,
        NativeOnDisconnectRecall = 15,
        CreateEntity = 16,
        DeleteEntity = 17,
        ScriptEventTrigger = 18,
        SyncEvent = 19,
        FileTransferTick = 20,
        FileTransferRequest = 21,
        FileTransferComplete = 22,
        ConnectionConfirmed = 23,
        PlayerKilled = 24,
        StopResource = 25,
        UpdateEntityProperties = 26,
        FileAcceptDeny = 27,
        ServerEvent = 28,
        Ack = 29,

        PedPureSync = 30,
        PedLightSync = 31,
        VehiclePureSync = 32,
        VehicleLightSync = 33,
        BasicSync = 34,
        BulletSync = 35,
    }

    public enum ScriptVersion
    {
        Unknown = 0,
        VERSION_0_6 = 1,
        VERSION_0_6_1 = 2,
        VERSION_0_7 = 3,
        VERSION_0_8_1 = 4,
        VERSION_0_9 = 5,
    }    

    public enum FileType
    {
        Normal = 0,
        Map = 1,
        Script = 2,
        EndOfTransfer = 3,
    }

    public enum SyncEventType
    {
        LandingGearChange = 0,
        DoorStateChange = 1,
        BooleanLights = 2,
        TrailerDeTach = 3,
        TireBurst = 4,
        RadioChange = 5,
        PickupPickedUp = 6,
    }

    public enum ServerEventType
    {
        PlayerTeamChange = 0,
        PlayerBlipColorChange = 1,
        PlayerBlipAlphaChange = 2,
        PlayerBlipSpriteChange = 3,
        PlayerSpectatorChange = 4,
        PlayerAnimationStart = 5,
        PlayerAnimationStop = 6,
        EntityDetachment = 7,
    }

    public enum Lights
    {
        NormalLights = 0,
        Highbeams = 1,
    }

    [Flags]
    public enum VehicleDataFlags
    {
        PressingHorn = 1 << 0,
        Shooting = 1 << 1,
        SirenActive = 1 << 2,
        VehicleDead = 1 << 3,
        Aiming = 1 << 4,
        Driver = 1 << 5,
        MountedWeapon = 1 << 6,
        BurnOut = 1 << 7,
    }
    
    [Flags]
    public enum PedDataFlags
    {
        Jumping = 1 << 0,
        Shooting = 1 << 1,
        Aiming = 1 << 2,
        ParachuteOpen = 1 << 3,
        Ragdoll = 1 << 4,
        InMeleeCombat = 1 << 5,
        InFreefall = 1 << 6,
        IsInCover = 1 << 7,
        IsInLowerCover = 1 << 8,
        IsInCoverFacingLeft = 1 << 9,
        IsReloading = 1 << 10,
    }

    public enum ConnectionChannel
    {
        Default = 0,
        FileTransfer = 1,
        NativeCall = 2,
        Chat = 3,
        EntityBackend = 4,
        PositionData = 5,
        SyncEvent = 6,
        PureSync = 7,
        LightSync = 8,
        BasicSync = 9,
        BulletSync = 10,
    }

    public struct LocalHandle
    {
        public LocalHandle(int handle)
        {
            Value = handle;
            GameValue = true;
        }

        public LocalHandle(int handle, bool gamevalue)
        {
            Value = handle;
            GameValue = gamevalue;
        }

        public int Value { get; set; }
        public bool GameValue;

        public override bool Equals(object obj)
        {
            return (obj as LocalHandle?)?.Value == Value;
        }

        public static bool operator ==(LocalHandle left, LocalHandle right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(LocalHandle left, LocalHandle right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool IsNull { get { return Value == 0; } }
    }

    public struct NetHandle
    {
        public NetHandle(int handle)
        {
            Value = handle;
        }

        public override bool Equals(object obj)
        {
            return (obj as NetHandle?)?.Value == Value;
        }

        public static bool operator == (NetHandle left, NetHandle right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(NetHandle left, NetHandle right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool IsNull { get { return Value == 0; } }

        public int Value { get; set; }
    }

    

    [ProtoContract]
    public class ConnectionResponse
    {
        [ProtoMember(1)]
        public int CharacterHandle { get; set; }
    }

    [ProtoContract]
    public class ServerMap
    {
        public ServerMap()
        {
            Objects = new Dictionary<int, EntityProperties>();
            Vehicles = new Dictionary<int, VehicleProperties>();
            Blips = new Dictionary<int, BlipProperties>();
            Markers = new Dictionary<int, MarkerProperties>();
            Pickups = new Dictionary<int, PickupProperties>();
            Players = new Dictionary<int, PedProperties>();
            TextLabels = new Dictionary<int, TextLabelProperties>();
        }

        [ProtoMember(1)]
        public Dictionary<int, EntityProperties> Objects { get; set; }

        [ProtoMember(2)]
        public Dictionary<int, VehicleProperties> Vehicles { get; set; }

        [ProtoMember(3)]
        public Dictionary<int, BlipProperties> Blips { get; set; }

        [ProtoMember(4)]
        public Dictionary<int, MarkerProperties> Markers { get; set; }

        [ProtoMember(5)]
        public Dictionary<int, PickupProperties> Pickups { get; set; }

        [ProtoMember(6)]
        public Dictionary<int, PedProperties> Players { get; set; }

        [ProtoMember(7)]
        public Dictionary<int, TextLabelProperties> TextLabels { get; set; }

        [ProtoMember(8)]
        public WorldProperties World { get; set; }
    }

    [ProtoContract]
    public class ScriptCollection
    {
        [ProtoMember(1)]
        public List<ClientsideScript> ClientsideScripts { get; set; }
    }

    [ProtoContract]
    public class ClientsideScript
    {
        [ProtoMember(1)]
        public string ResourceParent { get; set; }

        [ProtoMember(2)]
        public string Script { get; set; }

        [ProtoMember(3)]
        public string Filename { get; set; }

        [ProtoMember(4)]
        public string MD5Hash { get; set; }
    }


    [ProtoContract]
    public class DataDownloadStart
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public byte FileType { get; set; }

        [ProtoMember(3)]
        public string FileName { get; set; }

        [ProtoMember(4)]
        public string ResourceParent { get; set; }

        [ProtoMember(5)]
        public int Length { get; set; }

        [ProtoMember(6)]
        public string Md5Hash { get; set; }
    }

    [ProtoContract]
    public class DeleteEntity
    {
        [ProtoMember(1)]
        public int NetHandle { get; set; }
    }
    
    [ProtoContract]
    public class CreateEntity
    {
        [ProtoMember(1)]
        public int NetHandle { get; set; }

        [ProtoMember(2)]
        public byte EntityType { get; set; }

        [ProtoMember(3)]
        public EntityProperties Properties { get; set; }
    }

    [ProtoContract]
    public class UpdateEntity
    {
        [ProtoMember(1)]
        public int NetHandle { get; set; }

        [ProtoMember(2)]
        public byte EntityType { get; set; }

        [ProtoMember(3)]
        public Delta_EntityProperties Properties { get; set; }
    }

    [ProtoContract]
    public class SyncEvent
    {
        [ProtoMember(1)]
        public byte EventType { get; set; }

        [ProtoMember(2)]
        public List<NativeArgument> Arguments { get; set; }
    }

    [ProtoContract]
    public class DiscoveryResponse
    {
        [ProtoMember(1)]
        public string ServerName { get; set; }
        [ProtoMember(2)]
        public short MaxPlayers { get; set; }
        [ProtoMember(3)]
        public short PlayerCount { get; set; }
        [ProtoMember(4)]
        public bool PasswordProtected { get; set; }
        [ProtoMember(5)]
        public int Port { get; set; }
        [ProtoMember(6)]
        public string Gamemode { get; set; }
        [ProtoMember(7)]
        public bool LAN { get; set; }
    }

    [ProtoContract]
    public class ConnectionRequest
    {
        [ProtoMember(1)]
        public string SocialClubName { get; set; }

        [ProtoMember(2)]
        public string Password { get; set; }

        [ProtoMember(3)]
        public string DisplayName { get; set; }

        [ProtoMember(4)]
        public byte GameVersion { get; set; }

        [ProtoMember(5)]
        public ulong ScriptVersion { get; set; }
    }

    

    [ProtoContract]
    public class PlayerDisconnect
    {
        [ProtoMember(1)]
        public int Id { get; set; }
    }
    
    [ProtoContract]
    [ProtoInclude(4, typeof(Quaternion))]
    public class Vector3
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }

        private static Random randInstance = new Random();

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3(double x, double y, double z)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
        }

        public static bool operator ==(Vector3 left, Vector3 right)
        {
            if ((object) left == null && (object) right == null) return true;
            if ((object)left == null || (object)right == null) return false;
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        }

        public static bool operator !=(Vector3 left, Vector3 right)
        {
            if ((object)left == null && (object)right == null) return false;
            if ((object)left == null || (object)right == null) return true;
            return left.X != right.X || left.Y != right.Y || left.Z != right.Z;
        }

        public static Vector3 operator -(Vector3 left, Vector3 right)
        {
            if ((object)left == null || (object)right == null) return new Vector3();
            return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3 operator +(Vector3 left, Vector3 right)
        {
            if ((object)left == null || (object)right == null) return new Vector3();
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3 operator *(Vector3 left, float right)
        {
            if ((object)left == null) return new Vector3();
            return new Vector3(left.X * right, left.Y * right, left.Z * right);
        }

        public static Vector3 operator /(Vector3 left, float right)
        {
            if ((object)left == null) return new Vector3();
            return new Vector3(left.X / right, left.Y / right, left.Z / right);
        }

        public override string ToString()
        {
            return string.Format("X: {0} Y: {1} Z: {2}", X, Y, Z);
        }

        public float LengthSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        public float Length()
        {
            return (float)Math.Sqrt(LengthSquared());
        }

        public void Normalize()
        {
            var len = Length();

            X = X/len;
            Y = Y/len;
            Z = Z/len;
        }

        public Vector3 Normalized
        {
            get
            {
                var len = Length();

                return new Vector3(X/len, Y/len, Z/len);
            }
        }

        public static Vector3 RandomXY()
        {
            Vector3 v = new Vector3();
            double radian = randInstance.NextDouble()*2*Math.PI;

            v.X = (float) Math.Cos(radian);
            v.Y = (float) Math.Sin(radian);
            v.Normalize();

            return v;
        }

        public Vector3 Around(float distance)
        {
            return this + RandomXY()*distance;
        }

        public float DistanceToSquared(Vector3 right)
        {
            if ((object)right == null) return 0f;

            var nX = X - right.X;
            var nY = Y - right.Y;
            var nZ = Z - right.Z;

            return nX * nX + nY * nY + nZ * nZ;
        }

        public Vector3()
        { }
    }
    
    [ProtoContract]
    public class Quaternion : Vector3
    {
        [ProtoMember(1)]
        public float W { get; set; }

        public Quaternion()
        { }

        public override string ToString()
        {
            return string.Format("X: {0} Y: {1} Z: {2} W: {3}", X, Y, Z, W);
        }

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quaternion(double x, double y, double z, double w)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
            W = (float)w;
        }
    }

    
}