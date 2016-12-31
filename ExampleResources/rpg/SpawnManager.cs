using System;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource
{
    public class SpawnManager : Script
    {
        private readonly Vector3 _skinSelectorPos = new Vector3(3507.47f, 5122.82f, 6.22f);
        private readonly Vector3 _skinSelectorCamPos = new Vector3(3513.92f, 5118.72f, 5.76f);
        private readonly float _skinSelectorHead = 235.89f;

        private readonly Vector3 _copSpawnpoint = new Vector3(447.1f, -984.21f, 30.69f);
        private readonly Vector3 _crookSpawnpoint = new Vector3(-25.27f, -1554.27f, 30.69f);

        public SpawnManager()
        {
            API.onClientEventTrigger += ClientEvent;
        }

        public void ClientEvent(Client sender, string eventName, object[] args)
        {
            if (eventName == "skin_select_accept")
            {
                var skin = args[0];
                bool isCop = (bool)args[1];

                //DimensionManager.DismissPrivateDimension(sender);
                //API.setEntityDimension(sender, 0);

                if (isCop)
                    SpawnCop(sender);
                else SpawnCitizen(sender);
            }
        }

        // Exported

        public void CreateSkinSelection(Client target)
        {
            //var ourDim = DimensionManager.RequestPrivateDimension(target);

            //API.setEntityDimension(target, ourDim);

            API.setEntityPosition(target, _skinSelectorPos);
            API.setEntityRotation(target, new Vector3(0, 0, _skinSelectorHead));

            API.triggerClientEvent(target, "skin_select_start", _skinSelectorCamPos);
        }

        public void SpawnCop(Client target)
        {
            API.setPlayerNametagColor(target, 55, 135, 240);

            API.setEntityData(target, "IS_COP", true);
            API.setEntityData(target, "IS_CROOK", false);

            API.setEntityPosition(target, _copSpawnpoint);
            API.removeAllPlayerWeapons(target);

            API.givePlayerWeapon(target, WeaponHash.Nightstick, 1, false, true);
            API.givePlayerWeapon(target, WeaponHash.Pistol, 500, true, true);
            API.givePlayerWeapon(target, WeaponHash.StunGun, 500, true, true);
            
            // TODO: Give more weapons depending on level

            API.sendChatMessageToPlayer(target, "You are a cop! Protect and serve!");
            API.sendChatMessageToPlayer(target, "Type /wanted to see all wanted players.");
            API.sendChatMessageToPlayer(target, "Shoot someone with a taser to arrest them.");
            API.sendChatMessageToPlayer(target, "Type /ar[rest] [Player] to arrest someone.");
        }

        public void SpawnCitizen(Client target)
        {
            API.resetPlayerNametagColor(target);

            API.setEntityData(target, "IS_COP", false);
            API.setEntityData(target, "IS_CROOK", true);

            if (API.getEntityData(target, "Jailed") == true)
            {
                API.call("JailController", "jailPlayer", API.getEntityData(target, "JailTime"));
            }
            else
            {
                API.setEntityPosition(target, _crookSpawnpoint);
                API.removeAllPlayerWeapons(target);
            }

            API.sendChatMessageToPlayer(target, "You are a citizen! Do various jobs to earn money and reputation!");

            API.setPlayerWantedLevel(target, (int)Math.Ceiling((float)API.getEntityData(target, "WantedLevel") / 2f));
        }
    }
}