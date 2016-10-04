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
        #endregion
    }
}