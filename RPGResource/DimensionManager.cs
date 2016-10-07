using System.Collections.Generic;
using System.Linq;
using GTANetworkServer;

namespace RPGResource
{
    public static class DimensionManager
    {
        private static Dictionary<int, Client> DimensionsInUse = new Dictionary<int, Client>();

        public static int RequestPrivateDimension(Client requester)
        {
            int firstUnusedDim = 00;

            lock (DimensionsInUse)
            {
                while (DimensionsInUse.ContainsKey(--firstUnusedDim))
                {
                }

                DimensionsInUse.Add(firstUnusedDim, requester);
            }
            return firstUnusedDim;
        }

        public static void DismissPrivateDimension(Client requester)
        {
            lock (DimensionsInUse)
            {
                for (int i = DimensionsInUse.Count - 1; i >= 0; i--)
                {
                    if (DimensionsInUse.ElementAt(i).Value == requester)
                        DimensionsInUse.Remove(DimensionsInUse.ElementAt(i).Key);
                }
            }
        }
    }
}