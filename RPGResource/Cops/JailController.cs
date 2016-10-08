using System.Collections.Generic;
using System.Linq;
using System.Media;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Cops
{
    public class JailController : Script
    {
        public JailController()
        {
            API.onUpdate += onUpdate;
            API.onResourceStart += start;
        }

        public readonly Vector3 JailCenter = new Vector3(1707.69f, 2546.69f, 45.56f);
        public static Dictionary<Client, long> JailTimes = new Dictionary<Client, long>();

        public void start()
        {
            var shape = API.createSphereColShape(JailCenter, 30f);

            shape.onEntityExitColShape += (colShape, entity) =>
            {
                if (API.getLocalEntityData(entity, "Jailed") == true)
                {
                    API.setEntityPosition(entity, JailCenter);
                }
            };
        }

        public void jailPlayer(Client player, int seconds)
        {
            API.setLocalEntityData(player, "Jailed", true);
            API.setEntityPosition(player, JailCenter);

            API.setLocalEntityData(player, "WantedLevel", 0);
            API.setPlayerWantedLevel(player, 0);
            API.resetLocalEntityData(player, "Crimes");
            API.setLocalEntityData(player, "JailTime", seconds);

            API.removeAllPlayerWeapons(player);

            lock (JailTimes) JailTimes.Set(player, API.TickCount + seconds*1000);
        }

        public void freePlayer(Client player)
        {
            API.resetLocalEntityData(player, "Jailed");
            API.resetLocalEntityData(player, "JailTime");
            API.sendChatMessageToPlayer(player, "~g~You have served your sentence! You're free to go.");
            lock (JailTimes) JailTimes.Remove(player);
        }
        
        public void onUpdate()
        {
            lock (JailTimes)
            {
                for (int i = JailTimes.Count - 1; i >= 0; i--)
                {
                    var pair = JailTimes.ElementAt(i);

                    if (API.TickCount - pair.Value > 0)
                    {
                        freePlayer(pair.Key);
                    }
                }
            }
        }

    }
}