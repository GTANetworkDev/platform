using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource
{
    public class Debug : Script
    {
        [Command]
        public void givegun(Client sender, WeaponHash gun)
        {
            API.givePlayerWeapon(sender, gun, 500, true, true);
            
        }
    }
}