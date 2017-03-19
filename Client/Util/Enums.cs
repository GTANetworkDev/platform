using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTANetwork.Util
{
    public static class Enums
    {
        public static readonly string[] _weather = new string[]
        {
            "EXTRASUNNY",
            "CLEAR",
            "CLOUDS",
            "SMOG",
            "FOGGY",
            "OVERCAST",
            "RAIN",
            "THUNDER",
            "CLEARING",
            "NEUTRAL",
            "SNOW",
            "BLIZZARD",
            "SNOWLIGHT",
            "XMAS "
        };
        public enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1 << 1,
            ReturnsEntity = 1 << 2,
            NeedsModel = 1 << 3,
            NeedsModel1 = 1 << 4,
            NeedsModel2 = 1 << 5,
            NeedsModel3 = 1 << 6,
            TimeSet = 1 << 7,
            WeatherSet = 1 << 8,
            VehicleWarp = 1 << 9,
            EntityWarp = 1 << 10,
            NeedsAnimDict = 1 << 11,
            PtfxAssetRequest = 1 << 12,
            PlayerSkinChange = 1 << 13,
        }
    }
}
