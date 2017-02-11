using GTANetworkShared;

namespace GTANetworkServer
{
    public class Pickup : Entity
    {
        internal Pickup(ServerAPI father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public int amount
        {
            get { return Base.getPickupAmount(this); }
        }

        public int customModel
        {
            get { return Base.getPickupCustomModel(this); }
        }

        public bool pickedUp
        {
            get { return Base.getPickupPickedUp(this); }
        }

        #endregion

        #region Methods

        public void respawn()
        {
            Base.respawnPickup(this);
        }
        #endregion
    }
}