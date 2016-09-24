using System;
using System.Collections.Generic;
using GTANetworkServer.Constant;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetworkServer
{
    public class Client
    {
        internal bool IsInVehicleInternal { get; set; }
        internal int VehicleHandleInternal { get; set; }
        internal Dictionary<int, long> LastPacketReceived = new Dictionary<int, long>();
        internal Streamer Streamer { get; set; }

        internal bool Fake { get; set; }

        internal int LastPedFlag { get; set; }
        internal int LastVehicleFlag { get; set; }
        internal NetConnection NetConnection { get; private set; }
        public string SocialClubName { get; set; }
        public string Name { get; set; }
        public float Latency { get; set; }
        public ParseableVersion RemoteScriptVersion { get; set; }
        public int GameVersion { get; set; }
        public List<WeaponHash> Weapons = new List<WeaponHash>();
        public WeaponHash CurrentWeapon { get; set; }
        public Vector3 LastAimPos { get; set; }

        public NetHandle CurrentVehicle { get; set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Rotation { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public int Health { get; internal set; }
        public int Armor { get; internal set; }
        public bool IsInVehicle { get; internal set; }
        public int VehicleSeat { get; internal set; }

        public DateTime LastUpdate { get; internal set; }

        public NetHandle CharacterHandle { get; set; }

        public PlayerProperties Properties
        {
            get { return Program.ServerInstance.NetEntityHandler.ToDict()[CharacterHandle.Value] as PlayerProperties; }
        }

        internal void CommitConnection()
        {
            CharacterHandle = new NetHandle(Program.ServerInstance.NetEntityHandler.GeneratePedHandle());
        }

        public Client(NetConnection nc)
        {
            Health = 100;
            Armor = 0;
            
            NetConnection = nc;
            Streamer = new Streamer(this);
        }

        public static implicit operator NetHandle(Client c)
        {
            return c.CharacterHandle;
        }

        public override bool Equals(object obj)
        {
            Client target;
            if ((target = obj as Client) != null)
            {
                if (NetConnection == null || target.NetConnection == null)
                    return CharacterHandle == target.CharacterHandle;

                return NetConnection.RemoteUniqueIdentifier == target.NetConnection.RemoteUniqueIdentifier;
            }
            return false;
        }

        public static bool operator ==(Client left, Client right)
        {
            if ((object) left == null && (object) right == null) return true;
            if ((object)left == null || (object)right == null) return false;
            if (left.NetConnection == null || right.NetConnection == null) return left.CharacterHandle == right.CharacterHandle;

            return left.NetConnection.RemoteUniqueIdentifier == right.NetConnection.RemoteUniqueIdentifier;
        }

        public static bool operator !=(Client left, Client right)
        {
            if ((object)left == null && (object)right == null) return false;
            if ((object)left == null || (object)right == null) return true;
            if (left.NetConnection == null || right.NetConnection == null) return left.CharacterHandle != right.CharacterHandle;

            return left.NetConnection.RemoteUniqueIdentifier != right.NetConnection.RemoteUniqueIdentifier;
        }
    }
}