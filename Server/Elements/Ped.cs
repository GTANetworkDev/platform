using GTANetworkShared;

namespace GTANetworkServer
{
    public class Ped : Entity
    {
        internal Ped(API father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        #endregion

        #region Methods

        public void playAnimation(string dictionary, string name, bool looped)
        {
            Base.playPedAnimation(this, looped, dictionary, name);
        }

        public void playScenario(string scenario)
        {
            Base.playPedScenario(this, scenario);
        }

        public void stopAnimation()
        {
            Base.stopPedAnimation(this);
        }

        #endregion
    }
}