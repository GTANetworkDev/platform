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
        UnoccupiedVehStartStopSync = 36,
        UnoccupiedVehSync = 37,
        BasicUnoccupiedVehSync = 38,
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
        CustomData = 4,
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
        StickyBombDetonation = 7,
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
        HasAimData = 1 << 6,
        BurnOut = 1 << 7,
        ExitingVehicle = 1 << 8,
        PlayerDead = 1 << 9,
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
        HasAimData = 1 << 11,
        IsOnLadder = 1 << 12,
        IsVaulting = 1 << 13,
        EnteringVehicle = 1 << 14,
        ClosingVehicleDoor = 1 << 15,
        OnFire = 1 << 16,
        PlayerDead = 1 << 17,
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
        UnoccupiedVeh = 11,
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

        [ProtoMember(2)]
        public SharedSettings Settings { get; set; }
    }

    [ProtoContract]
    public class SharedSettings
    {
        [ProtoMember(1)]
        public bool VehicleLagCompensation { get; set; }

        [ProtoMember(2)]
        public bool OnFootLagCompensation { get; set; }
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
            Players = new Dictionary<int, PlayerProperties>();
            TextLabels = new Dictionary<int, TextLabelProperties>();
            Peds = new Dictionary<int, EntityProperties>();
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
        public Dictionary<int, PlayerProperties> Players { get; set; }

        [ProtoMember(7)]
        public Dictionary<int, TextLabelProperties> TextLabels { get; set; }

        [ProtoMember(8)]
        public Dictionary<int, EntityProperties> Peds { get; set; }

        [ProtoMember(9)]
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
}