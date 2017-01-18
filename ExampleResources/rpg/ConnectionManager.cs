using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource
{
    public class ConnectionManager : Script
    {
        private readonly Vector3 _skinSelectorPos = new Vector3(3507.47f, 5122.82f, 6.22f);
        private readonly Vector3 _skinSelectorCamPos = new Vector3(3514.92f, 5117.72f, 5.76f);

        public ConnectionManager()
        {
            API.onPlayerConnected += onPlayerConnected;
            API.onPlayerFinishedDownload += onPlayerDownloaded;
            API.onPlayerDisconnected += onPlayerLeave;
            API.onChatCommand += onPlayerCommand;
        }

        public void onPlayerCommand(Client player, string arg, CancelEventArgs ce)
        {
            if (API.getEntityData(player, "DOWNLOAD_FINISHED") != true)
            {
                ce.Cancel = true;
            }
        }

        public void onPlayerConnected(Client player)
        {
            
        }

        public void onPlayerDownloaded(Client player)
        {
            API.setEntityData(player, "DOWNLOAD_FINISHED", true);

            API.sendChatMessageToPlayer(player, "Welcome to %GENERICRPG%! Type /login to log into your account or /register to create an account.");
            API.triggerClientEvent(player, "createCamera", _skinSelectorCamPos, _skinSelectorPos);
        }

        public void onPlayerLeave(Client player, string reason)
        {
            Database.SavePlayerAccount(player);
        }
    }
}