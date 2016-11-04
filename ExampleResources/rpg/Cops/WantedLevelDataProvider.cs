using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Cops
{
    public static class WantedLevelDataProvider
    {
        // TODO: load the data
        public static Dictionary<int, CrimeData> Crimes = new Dictionary<int, CrimeData>
        {
            {
                0,
                new CrimeData()
                {
                    Name = "Murder",
                    TicketCost = 0,
                    WantedLevel = 4,
                }
            },
            {
                1,
                new CrimeData()
                {
                    Name = "Illegal Device Detonation",
                    TicketCost = 1500,
                    WantedLevel = 1,
                }
            },
            {
                2,
                new CrimeData()
                {
                    Name = "Cop Murder", // Better name?
                    TicketCost = 0,
                    WantedLevel = 5,
                }
            },
            {
                3,
                new CrimeData()
                {
                    Name = "Car Jacking",
                    TicketCost = 1500,
                    WantedLevel = 1,
                }
            },

        };
        

       public static int GetTimeFromWantedLevel(int wantedLevel)
        {
            switch (wantedLevel)
            {
                default:
                    return 0;
                case 5:
                    return 60;
                case 7:
                    return 100;
                case 8:
                    return 150;
                case 9:
                    return 300;
                case 10:
                    return 600;
            }
        }



    }

    public struct CrimeData
    {
        public string Name;
        public int WantedLevel;
        public int TicketCost;
    }
}