using GTANetworkServer.Constant;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class Marker : Entity
    {
        internal Marker(ServerAPI father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public int markerType
        {
            get { return Base.getMarkerType(this); }
            set { Base.setMarkerType(this, value); }
        }

        public Vector3 scale
        {
            get { return Base.getMarkerScale(this); }
            set { Base.setMarkerScale(this, value);}
        }

        public Vector3 direction
        {
            get { return Base.getMarkerDirection(this); }
            set { Base.setMarkerDirection(this, value); }
        }

        public Color color
        {
            get { return Base.getMarkerColor(this); }
            set { Base.setMarkerColor(this, value.alpha, value.red, value.green, value.blue); }
        }
        
        #endregion

        #region Methods
        #endregion
    }
}