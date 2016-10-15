using GTANetworkServer.Constant;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class Player : Entity
    {
        internal Player(Client representation, API father, NetHandle handle) : base(father, handle)
        {
            Father = representation;
        }

        protected Client Father { get; set; }

        public static implicit operator Client(Player obj)
        {
            return obj.Father;
        }

        public static Player fromName(string name)
        {
            var c = Program.ServerInstance.PublicAPI.getPlayerFromName(name);

            return c != null ? c.CharacterHandle: null;
        }

        public static Player fromHandle(NetHandle handle)
        {
            var c = Program.ServerInstance.PublicAPI.getPlayerFromHandle(handle);

            return c != null ? c.CharacterHandle : null;
        }

        #region Properties

        public Vehicle vehicle
        {
            get
            {
                var nh = Base.getPlayerVehicle(Father);
                if (nh.IsNull) return null;
                return new Vehicle(Base, nh);
            }
        }

        public int vehicleSeat
        {
            get { return Base.getPlayerVehicleSeat(Father); }
        }

        public int team
        {
            set
            {
                Base.setPlayerTeam(Father, value);
            }
            get { return Base.getPlayerTeam(Father); }
        }

        public int ping
        {
            get { return Base.getPlayerPing(Father); }
        }

        public int wantedLevel
        {
            get { return Base.getPlayerWantedLevel(this); }
            set { Base.setPlayerWantedLevel(this, value); }
        }

        public string name
        {
            get { return Base.getPlayerName(this); }
            set { Base.setPlayerName(this, value); }
        }

        public Vector3 velocity
        {
            get { return Base.getPlayerVelocity(this); }
            set { Base.setPlayerVelocity(this, value); }
        }

        public WeaponHash[] weapons
        {
            get { return Base.getPlayerWeapons(this); }
        }

        public WeaponHash currentWeapon
        {
            get { return Base.getPlayerCurrentWeapon(this); }
        }

        public string address
        {
            get { return Base.getPlayerAddress(this); }
        }

        public bool seatbelt
        {
            get { return Base.getPlayerSeatbelt(this); }
            set { Base.setPlayerSeatbelt(this, value); }
        }

        public int health
        {
            get { return Base.getPlayerHealth(this); }
            set { Base.setPlayerHealth(this, value);}
        }

        public int armor
        {
            get { return Base.getPlayerArmor(this); }
            set { Base.setPlayerArmor(this, value); }
        }

        public bool onFire
        {
            get { return Base.isPlayerOnFire(this); }
        }

        public bool isParachuting
        {
            get { return Base.isPlayerParachuting(this); }
        }

        public bool inFreefall
        {
            get { return Base.isPlayerInFreefall(this); }
        }

        public bool isAiming
        {
            get { return Base.isPlayerAiming(this); }
        }

        public bool isShooting
        {
            get { return Base.isPlayerShooting(this); }
        }

        public bool isReloading
        {
            get { return Base.isPlayerReloading(this); }
        }

        public bool isInCover
        {
            get { return Base.isPlayerInCover(this); }
        }

        public bool isOnLadder
        {
            get { return Base.isPlayerOnLadder(this); }
        }

        public Vector3 aimingPoint
        {
            get { return Base.getPlayerAimingPoint(this); }
        }

        public bool dead
        {
            get { return Base.isPlayerDead(this); }
        }

        public string nametag
        {
            get { return Base.getPlayerNametag(this); }
            set { Base.setPlayerNametag(this, value); }
        }

        public bool nametagVisible
        {
            get { return Base.getPlayerNametagVisible(this); }
            set { Base.setPlayerNametagVisible(this, value); }
        }

        public Color nametagColor
        {
            get { return Base.getPlayerNametagColor(this); }
            set { Base.setPlayerNametagColor(this, (byte)value.red, (byte)value.green, (byte)value.blue);}
        }

        public bool spectating
        {
            get { return Base.isPlayerSpectating(this); }
        }

        #endregion

        #region Methods

        public void setIntoVehicle(NetHandle car, int seat)
        {
            Base.setPlayerIntoVehicle(Father, car, seat);
        }

        public void warpOutOfVehicle(NetHandle car)
        {
            Base.warpPlayerOutOfVehicle(Father, car);
        }

        public void setSkin(PedHash newSkin)
        {
            Base.setPlayerSkin(Father, newSkin);
        }

        public void setDefaultClothes()
        {
            Base.setPlayerDefaultClothes(Father);
        }

        public void playAnimation(string animDict, string animName, int flag)
        {
            Base.playPlayerAnimation(Father, flag, animDict, animName);
        }

        public void playScenario(string scenarioName)
        {
            Base.playPlayerScenario(Father, scenarioName);
        }

        public void stopAnimation()
        {
            Base.stopPlayerAnimation(Father);
        }

        public void setClothes(int slot, int drawable, int texture)
        {
            Base.setPlayerClothes(Father, slot, drawable, texture);
        }

        public void setAccessories(int slot, int drawable, int texture)
        {
            Base.setPlayerAccessory(Father, slot, drawable, texture);
        }

        public int getClothesDrawable(int slot)
        {
            return Base.getPlayerClothesDrawable(this, slot);
        }

        public int getClothesTexture(int slot)
        {
            return Base.getPlayerClothesTexture(this, slot);
        }

        public int getAccessoryDrawable(int slot)
        {
            return Base.getPlayerAccessoryDrawable(this, slot);
        }

        public int getAccessoryTexture(int slot)
        {
            return Base.getPlayerAccessoryTexture(this, slot);
        }

        public void clearAccessory(int slot)
        {
            Base.clearPlayerAccessory(this, slot);
        }

        public void giveWeapon(WeaponHash weapon, int ammo, bool equipNow, bool ammoLoaded)
        {
            Base.givePlayerWeapon(this, weapon, ammo, equipNow, ammoLoaded);
        }

        public void removeWeapon(WeaponHash weapon)
        {
            Base.removePlayerWeapon(this, weapon);
        }

        public void removeAllWeapons()
        {
            Base.removeAllPlayerWeapons(this);
        }

        public void setWeaponTint(WeaponHash weapon, WeaponTint tint)
        {
            Base.setPlayerWeaponTint(this, weapon, tint);
        }

        public WeaponTint getWeaponTint(WeaponHash weapon)
        {
            return Base.getPlayerWeaponTint(this, weapon);
        }

        public void setWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            Base.givePlayerWeaponComponent(this, weapon, component);
        }

        public void removeWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            Base.removePlayerWeaponComponent(this, weapon, component);
        }

        public bool hasGotWeaponComponent(WeaponHash weapon, WeaponComponent component)
        {
            return Base.hasPlayerGotWeaponComponent(this, weapon, component);
        }

        public WeaponComponent[] GetAllWeaponComponents(WeaponHash weapon)
        {
            return Base.getPlayerWeaponComponents(this, weapon);
        }

        public void kick(string reason)
        {
            Base.kickPlayer(this, reason);
        }

        public void kick()
        {
            Base.kickPlayer(this);
        }

        public void ban(string reason)
        {
            Base.banPlayer(this, reason);
        }

        public void ban()
        {
            Base.banPlayer(this);
        }

        public void freeze(bool freeze)
        {
            Base.freezePlayer(this, freeze);
        }

        public void kill()
        {
            Base.setPlayerHealth(this, -1);
        }

        public void detonateStickies()
        {
            Base.detonatePlayerStickies(Father);
        }

        public void resetNametag()
        {
            Base.resetPlayerNametag(this);
        }

        public void resetNametagColor()
        {
            Base.resetPlayerNametagColor(this);
        }

        public void spectate()
        {
            Base.setPlayerToSpectator(this);
        }

        public void spectate(Client player)
        {
            Base.setPlayerToSpectatePlayer(this, player);
        }

        public void stopSpectating()
        {
            Base.unspectatePlayer(this);
        }
        #endregion
    }
}