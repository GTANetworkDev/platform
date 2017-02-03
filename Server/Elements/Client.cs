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
        internal bool CEF { get; set; }
        internal float Latency { get; set; }
        internal ParseableVersion RemoteScriptVersion { get; set; }
        internal int GameVersion { get; set; }
        //internal List<WeaponHash> Weapons = new List<WeaponHash>();
        internal Dictionary<WeaponHash, int> Weapons = new Dictionary<WeaponHash, int>();
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
        internal int Ammo { get; set; }
        internal int ModelHash { get; set; }

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
                var nh = API.shared.getPlayerVehicle(this);
                if (nh.IsNull) return null;
                return new Vehicle(API.shared, nh);
            }
        }

        public bool isInVehicle
        {
            get
            {
                return IsInVehicle;
            }
        }

        public int vehicleSeat
        {
            get { return API.shared.getPlayerVehicleSeat(this); }
        }

        public int team
        {
            set
            {
                API.shared.setPlayerTeam(this, value);
            }
            get { return API.shared.getPlayerTeam(this); }
        }

        public int ping
        {
            get { return API.shared.getPlayerPing(this); }
        }

        public int wantedLevel
        {
            get { return API.shared.getPlayerWantedLevel(this); }
            set { API.shared.setPlayerWantedLevel(this, value); }
        }

        public string name
        {
            get { return API.shared.getPlayerName(this); }
            set { API.shared.setPlayerName(this, value); }
        }

        public string socialClubName
        {
            get { return SocialClubName; }
        }

        public bool isCEFenabled
        {
            get { return CEF; }
        }

        public Vector3 velocity
        {
            get { return API.shared.getPlayerVelocity(this); }
            set { API.shared.setPlayerVelocity(this, value); }
        }

        public WeaponHash[] weapons
        {
            get { return API.shared.getPlayerWeapons(this); }
        }

        public WeaponHash currentWeapon
        {
            get { return API.shared.getPlayerCurrentWeapon(this); }
        }

        public string address
        {
            get { return API.shared.getPlayerAddress(this); }
        }

        public bool seatbelt
        {
            get { return API.shared.getPlayerSeatbelt(this); }
            set { API.shared.setPlayerSeatbelt(this, value); }
        }

        public int health
        {
            get { return API.shared.getPlayerHealth(this); }
            set { API.shared.setPlayerHealth(this, value); }
        }

        public int armor
        {
            get { return API.shared.getPlayerArmor(this); }
            set { API.shared.setPlayerArmor(this, value); }
        }

        public bool onFire
        {
            get { return API.shared.isPlayerOnFire(this); }
        }

        public bool isParachuting
        {
            get { return API.shared.isPlayerParachuting(this); }
        }

        public bool inFreefall
        {
            get { return API.shared.isPlayerInFreefall(this); }
        }

        public bool isAiming
        {
            get { return API.shared.isPlayerAiming(this); }
        }

        public bool isShooting
        {
            get { return API.shared.isPlayerShooting(this); }
        }

        public bool isReloading
        {
            get { return API.shared.isPlayerReloading(this); }
        }

        public bool isInCover
        {
            get { return API.shared.isPlayerInCover(this); }
        }

        public bool isOnLadder
        {
            get { return API.shared.isPlayerOnLadder(this); }
        }

        public Vector3 aimingPoint
        {
            get { return API.shared.getPlayerAimingPoint(this); }
        }

        public bool dead
        {
            get { return API.shared.isPlayerDead(this); }
        }

        public string nametag
        {
            get { return API.shared.getPlayerNametag(this); }
            set { API.shared.setPlayerNametag(this, value); }
        }

        public bool nametagVisible
        {
            get { return API.shared.getPlayerNametagVisible(this); }
            set { API.shared.setPlayerNametagVisible(this, value); }
        }

        public Color nametagColor
        {
            get { return API.shared.getPlayerNametagColor(this); }
            set { API.shared.setPlayerNametagColor(this, (byte)value.red, (byte)value.green, (byte)value.blue); }
        }

        public bool spectating
        {
            get { return API.shared.isPlayerSpectating(this); }
        }

        #endregion

        #region Methods

        public void sendChatMessage(string message)
        {
            API.shared.sendChatMessageToPlayer(this, message);
        }

        public void sendChatMessage(string sender, string message)
        {
            API.shared.sendChatMessageToPlayer(this, sender, message);
        }

        public void sendNotification(string sender, string message, bool flashing = true)
        {
            API.shared.sendNotificationToPlayer(this, message, flashing);
        }

        public void sendPictureNotificationToPlayer(string body, string pic, int flash, int iconType, string sender, string subject)
        {
            API.shared.sendPictureNotificationToPlayer(this, body, pic, flash, iconType, sender, subject);
        }

        public void setIntoVehicle(NetHandle car, int seat)
        {
            API.shared.setPlayerIntoVehicle(this, car, seat);
        }

        public void warpOutOfVehicle(NetHandle car)
        {
            API.shared.warpPlayerOutOfVehicle(this, car);
        }

        public void setSkin(PedHash newSkin)
        {
            API.shared.setPlayerSkin(this, newSkin);
        }

        public void setDefaultClothes()
        {
            API.shared.setPlayerDefaultClothes(this);
        }

        public void playAnimation(string animDict, string animName, int flag)
        {
            API.shared.playPlayerAnimation(this, flag, animDict, animName);
        }

        public void playScenario(string scenarioName)
        {
            API.shared.playPlayerScenario(this, scenarioName);
        }

        public void stopAnimation()
        {
            API.shared.stopPlayerAnimation(this);
        }

        public void setClothes(int slot, int drawable, int texture)
        {
            API.shared.setPlayerClothes(this, slot, drawable, texture);
        }

        public void setAccessories(int slot, int drawable, int texture)
        {
            API.shared.setPlayerAccessory(this, slot, drawable, texture);
        }

        public int getClothesDrawable(int slot)
        {
            return API.shared.getPlayerClothesDrawable(this, slot);
        }

        public int getClothesTexture(int slot)
        {
            return API.shared.getPlayerClothesTexture(this, slot);
        }

        public int getAccessoryDrawable(int slot)
        {
            return API.shared.getPlayerAccessoryDrawable(this, slot);
        }

        public int getAccessoryTexture(int slot)
        {
            return API.shared.getPlayerAccessoryTexture(this, slot);
        }

        public void clearAccessory(int slot)
        {
            API.shared.clearPlayerAccessory(this, slot);
        }

        public void giveWeapon(WeaponHash weapon, int ammo, bool equipNow, bool ammoLoaded)
        {
            API.shared.givePlayerWeapon(this, weapon, ammo, equipNow, ammoLoaded);
        }

        public void setWeaponAmmo(WeaponHash weapon, int ammo)
        {
            API.shared.setPlayerWeaponAmmo(this, weapon, ammo);
        }

        public int getWeaponAmmo(WeaponHash weapon)
        {
            return API.shared.getPlayerWeaponAmmo(this, weapon);
        }

        public void removeWeapon(WeaponHash weapon)
        {
            API.shared.removePlayerWeapon(this, weapon);
        }

        public void removeAllWeapons()
        {
            API.shared.removeAllPlayerWeapons(this);
        }

        public void setWeaponTint(WeaponHash weapon, WeaponTint tint)
        {
            API.shared.setPlayerWeaponTint(this, weapon, tint);
        }

        public WeaponTint getWeaponTint(WeaponHash weapon)
        {
            return API.shared.getPlayerWeaponTint(this, weapon);
        }

        public void setWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            API.shared.givePlayerWeaponComponent(this, weapon, component);
        }

        public void removeWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            API.shared.removePlayerWeaponComponent(this, weapon, component);
        }

        public bool hasGotWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            return API.shared.hasPlayerGotWeaponComponent(this, weapon, component);
        }

        public WeaponComponent[] GetAllWeaponComponents(WeaponHash weapon)
        {
            return API.shared.getPlayerWeaponComponents(this, weapon);
        }

        public void kick(string reason)
        {
            API.shared.kickPlayer(this, reason);
        }

        public void kick()
        {
            API.shared.kickPlayer(this);
        }

        public void ban(string reason)
        {
            API.shared.banPlayer(this, reason);
        }

        public void ban()
        {
            API.shared.banPlayer(this);
        }

        public void freeze(bool freeze)
        {
            API.shared.freezePlayer(this, freeze);
        }

        public void kill()
        {
            API.shared.setPlayerHealth(this, -1);
        }

        public void detonateStickies()
        {
            API.shared.detonatePlayerStickies(this);
        }

        public void resetNametag()
        {
            API.shared.resetPlayerNametag(this);
        }

        public void resetNametagColor()
        {
            API.shared.resetPlayerNametagColor(this);
        }

        public void spectate()
        {
            API.shared.setPlayerToSpectator(this);
        }

        public void spectate(Client player)
        {
            API.shared.setPlayerToSpectatePlayer(this, player);
        }

        public void stopSpectating()
        {
            API.shared.unspectatePlayer(this);
        }

        #endregion

        #region Entity Inheritance

        public bool freezePosition
        {
            set
            {
                API.shared.setEntityPositionFrozen(this, value);
            }
        }

        public virtual Vector3 position
        {
            set
            {
                API.shared.setEntityPosition(this, value);
            }
            get
            {
                return API.shared.getEntityPosition(this);
            }
        }

        public virtual Vector3 rotation
        {
            set
            {
                API.shared.setEntityRotation(this, value);
            }
            get
            {
                return API.shared.getEntityRotation(this);
            }
        }

        public bool IsNull
        {
            get { return handle.IsNull; }
        }

        public bool exists
        {
            get { return API.shared.doesEntityExist(this); }
        }

        public EntityType type
        {
            get { return API.shared.getEntityType(this); }
        }

        public virtual int transparency
        {
            set
            {
                API.shared.setEntityTransparency(this, value);
            }
            get { return API.shared.getEntityTransparency(this); }
        }

        public int dimension
        {
            set
            {
                API.shared.setEntityDimension(this, value);
            }
            get { return API.shared.getEntityDimension(this); }
        }

        public bool invincible
        {
            set
            {
                API.shared.setEntityInvincible(this, value);
            }
            get { return API.shared.getEntityInvincible(this); }
        }

        public bool collisionless
        {
            set
            {
                API.shared.setEntityCollisionless(this, value);
            }
            get { return API.shared.getEntityCollisionless(this); }
        }

        public int model
        {
            get { return API.shared.getEntityModel(this); }
        }


        #region Methods

        public void delete()
        {
            API.shared.deleteEntity(this);
        }

        public void movePosition(Vector3 target, int duration)
        {
            API.shared.moveEntityPosition(this, target, duration);
        }

        public void moveRotation(Vector3 target, int duration)
        {
            API.shared.moveEntityRotation(this, target, duration);
        }

        public void attachTo(NetHandle entity, string bone, Vector3 offset, Vector3 rotation)
        {
            API.shared.attachEntityToEntity(this, entity, bone, offset, rotation);
        }

        public void detach()
        {
            API.shared.detachEntity(this);
        }

        public void detach(bool resetCollision)
        {
            API.shared.detachEntity(this, resetCollision);
        }

        public void createParticleEffect(string ptfxLib, string ptfxName, Vector3 offset, Vector3 rotation, float scale, int bone = -1)
        {
            API.shared.createParticleEffectOnEntity(ptfxLib, ptfxName, this, offset, rotation, scale, bone, dimension);
        }

        public void setSyncedData(string key, object value)
        {
            API.shared.setEntitySyncedData(this, key, value);
        }

        public dynamic getSyncedData(string key)
        {
            return API.shared.getEntitySyncedData(this, key);
        }

        public void resetSyncedData(string key)
        {
            API.shared.resetEntitySyncedData(this, key);
        }

        public bool hasSyncedData(string key)
        {
            return API.shared.hasEntitySyncedData(this, key);
        }

        public void setData(string key, object value)
        {
            API.shared.setEntityData(this, key, value);
        }

        public dynamic getData(string key)
        {
            return API.shared.getEntityData(this, key);
        }

        public void resetData(string key)
        {
            API.shared.resetEntityData(this, key);
        }

        public bool hasData(string key)
        {
            return API.shared.hasEntityData(this, key);
        }

        public void triggerEvent(string eventName, params object[] args)
        {
            API.shared.triggerClientEvent(this, eventName, args);
        }

        public void downloadData(string data)
        {
            API.shared.downloadData(this, data);
        }

        #endregion
        #endregion

    }
}