using GTANetworkShared;

namespace GTANetworkServer
{
    public class Blip : Entity
    {
        internal Blip(API father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public override Vector3 position
        {
            get { return base.position; }
            set
            {
                Base.setBlipPosition(handle, value);
            }
        }

        public int color
        {
            get { return Base.getBlipColor(handle); }
            set { Base.setBlipColor(handle, value); }
        }

        public string name
        {
            get { return Base.getBlipName(this); }
            set { Base.setBlipName(this, value); }
        }

        public override int transparency
        {
            get { return base.transparency; }
            set { Base.setBlipTransparency(this, value); }
        }

        public bool shortRange
        {
            get { return Base.getBlipShortRange(this); }
            set { Base.setBlipShortRange(this, value); }
        }

        public int sprite
        {
            get { return Base.getBlipSprite(this); }
            set { Base.setBlipSprite(this, value); }
        }

        public float scale
        {
            get { return Base.getBlipScale(this); }
            set { Base.setBlipScale(this, value); }
        }

        #endregion

        #region Methods

        #endregion
    }
}