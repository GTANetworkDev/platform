using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;

namespace GTANetwork.Util
{
    public static class FrameworkData
    {

        private const int Count = 20;
        private struct Data
        {
            public int Skipped { get; set; }

        }

        public class DateTimeCache
        {
            private static Data _data;
            public DateTimeCache()
            {
                _data = new Data();
            }

            private static DateTime _recentTime = DateTime.Now;

            public static DateTime Ex()
            {
                _data.Skipped++;
                if (_data.Skipped <= Count) return _recentTime;
                _recentTime = DateTime.Now;
                _data.Skipped = 0;
                return _recentTime;
            }
        }

        //public class PlayerChar
        //{
        //    private static Data _data;
        //    public PlayerChar()
        //    {
        //        _data = new Data();
        //    }

        //    public static Ped _recentPlayerChar = Game.Player.Character;

        //    public static Ped Ex()
        //    {
        //        _data.Skipped++;
        //        if (_data.Skipped <= Count) return _recentPlayerChar;
        //        _recentPlayerChar = Game.Player.Character;
        //        _data.Skipped = 0;
        //        return _recentPlayerChar;
        //    }
        //}

        //public class PlayerP
        //{
        //    private static Data _data;
        //    public PlayerP()
        //    {
        //        _data = new Data();
        //    }

        //    public static Player _recentPlayer = Game.Player;

        //    public static Player Ex()
        //    {
        //        _data.Skipped++;
        //        if (_data.Skipped <= Count) return _recentPlayer;
        //        _recentPlayer = Game.Player;
        //        _data.Skipped = 0;
        //        return _recentPlayer;
        //    }
        //}


    }
}
