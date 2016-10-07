using System.Collections.Generic;
using System.Linq;
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

        private readonly Vector3 _jailCenter = new Vector3(1707.69f, 2546.69f, 45.56f);
        public static Dictionary<Client, long> JailTimes = new Dictionary<Client, long>();

        public void start()
        {
            var shape = API.createSphereColShape(_jailCenter, 30f);

            shape.onEntityExitColShape += (colShape, entity) =>
            {
                if (API.getLocalEntityData(entity, "JAILED") == true)
                {
                    API.setEntityPosition(entity, _jailCenter);
                }
            };
        }

        public void jailPlayer(Client player, int seconds)
        {
            API.setLocalEntityData(player, "JAILED", true);
            API.setEntityPosition(player, _jailCenter);

            lock (JailTimes) JailTimes.Set(player, API.TickCount + seconds);
        }

        public void freePlayer(Client player)
        {
            API.resetLocalEntityData(player, "JAILED");
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