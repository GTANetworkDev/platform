using System;
using System.Collections.Generic;
using GTANetworkServer.Constant;
using GTANetworkServer.Managers;
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
        internal DateTime LastUpdate { get; set; }

        internal bool Fake { get; set; }

        internal int LastPedFlag { get; set; }
        internal int LastVehicleFlag { get; set; }
        internal NetConnection NetConnection { get; private set; }

        internal string SocialClubName { get; set; }
        internal string Name { get; set; }
        internal float Latency { get; set; }
        internal ParseableVersion RemoteScriptVersion { get; set; }
        internal int GameVersion { get; set; }
        internal List<WeaponHash> Weapons = new List<WeaponHash>();
        internal WeaponHash CurrentWeapon { get; set; }
        internal Vector3 LastAimPos { get; set; }
        internal NetHandle CurrentVehicle { get; set; }
        internal Vector3 Position { get; set; }
        internal Vector3 Rotation { get; set; }
        internal Vector3 Velocity { get; set; }
        internal int Health { get; set; }
        internal int Armor { get; set; }
        internal bool IsInVehicle { get; set; }
        internal int VehicleSeat { get; set; }


        public NetHandle handle { get; set; }

        internal PlayerProperties Properties
        {
            get { return Program.ServerInstance.NetEntityHandler.ToDict()[handle.Value] as PlayerProperties; }
        }

        internal void CommitConnection()
        {
            handle = new NetHandle(Program.ServerInstance.NetEntityHandler.GeneratePedHandle());
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
            return c.handle;
        }

        
        public override bool Equals(object obj)
        {
            Client target;
            if ((target = obj as Client) != null)
            {
                if (NetConnection == null || target.NetConnection == null)
                    return handle == target.handle;

                return NetConnection.RemoteUniqueIdentifier == target.NetConnection.RemoteUniqueIdentifier;
            }
            return false;
        }

        public static bool operator ==(Client left, Client right)
        {
            if ((object) left == null && (object) right == null) return true;
            if ((object)left == null || (object)right == null) return false;
            if (left.NetConnection == null || right.NetConnection == null) return left.handle == right.handle;

            return left.NetConnection.RemoteUniqueIdentifier == right.NetConnection.RemoteUniqueIdentifier;
        }

        public static bool operator !=(Client left, Client right)
        {
            if ((object)left == null && (object)right == null) return false;
            if ((object)left == null || (object)right == null) return true;
            if (left.NetConnection == null || right.NetConnection == null) return left.handle != right.handle;

            return left.NetConnection.RemoteUniqueIdentifier != right.NetConnection.RemoteUniqueIdentifier;
        }


        #region Properties

        public Vehicle vehicle
        {
            get
            {
                var nh = API.Public.getPlayerVehicle(this);
                if (nh.IsNull) return null;
                return new Vehicle(API.Public, nh);
            }
        }

        public int vehicleSeat
        {
            get { return API.Public.getPlayerVehicleSeat(this); }
        }

        public int team
        {
            set
            {
                API.Public.setPlayerTeam(this, value);
            }
            get { return API.Public.getPlayerTeam(this); }
        }

        public int ping
        {
            get { return API.Public.getPlayerPing(this); }
        }

        public int wantedLevel
        {
            get { return API.Public.getPlayerWantedLevel(this); }
            set { API.Public.setPlayerWantedLevel(this, value); }
        }

        public string name
        {
            get { return API.Public.getPlayerName(this); }
            set { API.Public.setPlayerName(this, value); }
        }

        public string socialClubName
        {
            get { return SocialClubName; }
        }

        public Vector3 velocity
        {
            get { return API.Public.getPlayerVelocity(this); }
            set { API.Public.setPlayerVelocity(this, value); }
        }

        public WeaponHash[] weapons
        {
            get { return API.Public.getPlayerWeapons(this); }
        }

        public WeaponHash currentWeapon
        {
            get { return API.Public.getPlayerCurrentWeapon(this); }
        }

        public string address
        {
            get { return API.Public.getPlayerAddress(this); }
        }

        public bool seatbelt
        {
            get { return API.Public.getPlayerSeatbelt(this); }
            set { API.Public.setPlayerSeatbelt(this, value); }
        }

        public int health
        {
            get { return API.Public.getPlayerHealth(this); }
            set { API.Public.setPlayerHealth(this, value); }
        }

        public int armor
        {
            get { return API.Public.getPlayerArmor(this); }
            set { API.Public.setPlayerArmor(this, value); }
        }

        public bool onFire
        {
            get { return API.Public.isPlayerOnFire(this); }
        }

        public bool isParachuting
        {
            get { return API.Public.isPlayerParachuting(this); }
        }

        public bool inFreefall
        {
            get { return API.Public.isPlayerInFreefall(this); }
        }

        public bool isAiming
        {
            get { return API.Public.isPlayerAiming(this); }
        }

        public bool isShooting
        {
            get { return API.Public.isPlayerShooting(this); }
        }

        public bool isReloading
        {
            get { return API.Public.isPlayerReloading(this); }
        }

        public bool isInCover
        {
            get { return API.Public.isPlayerInCover(this); }
        }

        public bool isOnLadder
        {
            get { return API.Public.isPlayerOnLadder(this); }
        }

        public Vector3 aimingPoint
        {
            get { return API.Public.getPlayerAimingPoint(this); }
        }

        public bool dead
        {
            get { return API.Public.isPlayerDead(this); }
        }

        public string nametag
        {
            get { return API.Public.getPlayerNametag(this); }
            set { API.Public.setPlayerNametag(this, value); }
        }

        public bool nametagVisible
        {
            get { return API.Public.getPlayerNametagVisible(this); }
            set { API.Public.setPlayerNametagVisible(this, value); }
        }

        public Color nametagColor
        {
            get { return API.Public.getPlayerNametagColor(this); }
            set { API.Public.setPlayerNametagColor(this, (byte)value.red, (byte)value.green, (byte)value.blue); }
        }

        public bool spectating
        {
            get { return API.Public.isPlayerSpectating(this); }
        }

        #endregion

        #region Methods

        public void setIntoVehicle(NetHandle car, int seat)
        {
            API.Public.setPlayerIntoVehicle(this, car, seat);
        }

        public void warpOutOfVehicle(NetHandle car)
        {
            API.Public.warpPlayerOutOfVehicle(this, car);
        }

        public void setSkin(PedHash newSkin)
        {
            API.Public.setPlayerSkin(this, newSkin);
        }

        public void setDefaultClothes()
        {
            API.Public.setPlayerDefaultClothes(this);
        }

        public void playAnimation(string animDict, string animName, int flag)
        {
            API.Public.playPlayerAnimation(this, flag, animDict, animName);
        }

        public void playScenario(string scenarioName)
        {
            API.Public.playPlayerScenario(this, scenarioName);
        }

        public void stopAnimation()
        {
            API.Public.stopPlayerAnimation(this);
        }

        public void setClothes(int slot, int drawable, int texture)
        {
            API.Public.setPlayerClothes(this, slot, drawable, texture);
        }

        public void setAccessories(int slot, int drawable, int texture)
        {
            API.Public.setPlayerAccessory(this, slot, drawable, texture);
        }

        public int getClothesDrawable(int slot)
        {
            return API.Public.getPlayerClothesDrawable(this, slot);
        }

        public int getClothesTexture(int slot)
        {
            return API.Public.getPlayerClothesTexture(this, slot);
        }

        public int getAccessoryDrawable(int slot)
        {
            return API.Public.getPlayerAccessoryDrawable(this, slot);
        }

        public int getAccessoryTexture(int slot)
        {
            return API.Public.getPlayerAccessoryTexture(this, slot);
        }

        public void clearAccessory(int slot)
        {
            API.Public.clearPlayerAccessory(this, slot);
        }

        public void giveWeapon(WeaponHash weapon, int ammo, bool equipNow, bool ammoLoaded)
        {
            API.Public.givePlayerWeapon(this, weapon, ammo, equipNow, ammoLoaded);
        }

        public void removeWeapon(WeaponHash weapon)
        {
            API.Public.removePlayerWeapon(this, weapon);
        }

        public void removeAllWeapons()
        {
            API.Public.removeAllPlayerWeapons(this);
        }

        public void setWeaponTint(WeaponHash weapon, WeaponTint tint)
        {
            API.Public.setPlayerWeaponTint(this, weapon, tint);
        }

        public WeaponTint getWeaponTint(WeaponHash weapon)
        {
            return API.Public.getPlayerWeaponTint(this, weapon);
        }

        public void setWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            API.Public.givePlayerWeaponComponent(this, weapon, component);
        }

        public void removeWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            API.Public.removePlayerWeaponComponent(this, weapon, component);
        }

        public bool hasGotWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            return API.Public.hasPlayerGotWeaponComponent(this, weapon, component);
        }

        public WeaponComponent[] GetAllWeaponComponents(WeaponHash weapon)
        {
            return API.Public.getPlayerWeaponComponents(this, weapon);
        }

        public void kick(string reason)
        {
            API.Public.kickPlayer(this, reason);
        }

        public void kick()
        {
            API.Public.kickPlayer(this);
        }

        public void ban(string reason)
        {
            API.Public.banPlayer(this, reason);
        }

        public void ban()
        {
            API.Public.banPlayer(this);
        }

        public void freeze(bool freeze)
        {
            API.Public.freezePlayer(this, freeze);
        }

        public void kill()
        {
            API.Public.setPlayerHealth(this, -1);
        }

        public void detonateStickies()
        {
            API.Public.detonatePlayerStickies(this);
        }

        public void resetNametag()
        {
            API.Public.resetPlayerNametag(this);
        }

        public void resetNametagColor()
        {
            API.Public.resetPlayerNametagColor(this);
        }

        public void spectate()
        {
            API.Public.setPlayerToSpectator(this);
        }

        public void spectate(Client player)
        {
            API.Public.setPlayerToSpectatePlayer(this, player);
        }

        public void stopSpectating()
        {
            API.Public.unspectatePlayer(this);
        }
        #endregion
    }
}