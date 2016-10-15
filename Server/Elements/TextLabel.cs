using GTANetworkServer.Constant;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class TextLabel : Entity
    {
        internal TextLabel(API father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public string text
        {
            get { return Base.getTextLabelText(this); }
            set { Base.setTextLabelText(this, value); }
        }

        public Color color
        {
            get { return Base.getTextLabelColor(this); }
            set { Base.setTextLabelColor(this, value.red, value.green, value.blue,value.alpha); }
        }

        public bool seethrough
        {
            get { return Base.getTextLabelSeethrough(this); }
            set { Base.setTextLabelSeethrough(this, value); }
        }
        
        
        
        #endregion

        #region Methods
        #endregion
    }
}