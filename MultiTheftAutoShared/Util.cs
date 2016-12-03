using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace GTANetworkShared
{
    public static class Extensions
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value > max) return max;
            if (value < min) return min;
            return value;
        }

        public static int FromArgb(byte a, byte r, byte g, byte b)
        {
            return b | g << 8 | r << 16 | a << 24;
        }

        public static void ToArgb(int argb, out byte a, out byte r, out byte g, out byte b)
        {
            b = (byte)(argb & 0xFF);
            g = (byte)((argb & 0xFF00) >> 8);
            r = (byte)((argb & 0xFF0000) >> 16);
            a = (byte)((argb & 0xFF000000) >> 24);
        }

        public static void Set<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }

        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            else
            {
                return default(TValue);
            }
        }

        public static int Get(this IDictionary<int, int> dict, int key)
        {
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            else
            {
                return -1;
            }
        }
    }

    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public string MasterServerAddress { get; set; }
        public List<string> FavoriteServers { get; set; }
        public List<string> RecentServers { get; set; }
        public bool ScaleChatWithSafezone { get; set; }
        public bool SteamPowered { get; set; }
        public string UpdateChannel { get; set; }
        public bool DisableRockstarEditor { get; set; }
        public Keys ScreenshotKey { get; set; }
        public int CefFps { get; set; }
        public bool StartGameInOfflineMode { get; set; }
        
        public PlayerSettings()
        {
            MasterServerAddress = "http://148.251.18.67:8888/";
            FavoriteServers = new List<string>();
            RecentServers = new List<string>();
            ScaleChatWithSafezone = true;
            SteamPowered = false;
            StartGameInOfflineMode = true;
            UpdateChannel = "stable";
            DisableRockstarEditor = true;
            ScreenshotKey = Keys.F8;
            CefFps = 30;
        }
    }

    public class ImpatientWebClient : WebClient
    {
        public int Timeout { get; set; }

        public ImpatientWebClient()
        {
            Timeout = 10000;
        }

        public ImpatientWebClient(int timeout)
        {
            Timeout = timeout;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest w = base.GetWebRequest(address);
            if (w != null)
            {
                w.Timeout = Timeout;
            }
            return w;
        }
    }

    public struct ParseableVersion : IComparable<ParseableVersion>
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public int Build { get; set; }

        public ParseableVersion(int major, int minor, int build, int rev)
        {
            Major = major;
            Minor = minor;
            Revision = rev;
            Build = build;
        }

        public override string ToString()
        {
            return Major + "." + Minor + "." + Build + "." + Revision;
        }

        public int CompareTo(ParseableVersion right)
        {
            return CreateComparableInteger().CompareTo(right.CreateComparableInteger());
        }

        public long CreateComparableInteger()
        {
            return (long)((Revision) + (Build * Math.Pow(10, 4)) + (Minor * Math.Pow(10, 8)) + (Major * Math.Pow(10, 12)));
        }

        public static bool operator >(ParseableVersion left, ParseableVersion right)
        {
            return left.CreateComparableInteger() > right.CreateComparableInteger();
        }

        public static bool operator <(ParseableVersion left, ParseableVersion right)
        {
            return left.CreateComparableInteger() < right.CreateComparableInteger();
        }

        public ulong ToLong()
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes((ushort)Revision));
            bytes.AddRange(BitConverter.GetBytes((ushort)Build));
            bytes.AddRange(BitConverter.GetBytes((ushort)Minor));
            bytes.AddRange(BitConverter.GetBytes((ushort)Major));

            return BitConverter.ToUInt64(bytes.ToArray(), 0);
        }

        public static ParseableVersion FromLong(ulong version)
        {
            ushort rev = (ushort)(version & 0xFFFF);
            ushort build = (ushort)((version & 0xFFFF0000) >> 16);
            ushort minor = (ushort)((version & 0xFFFF00000000) >> 32);
            ushort major = (ushort)((version & 0xFFFF000000000000) >> 48);
            
            return new ParseableVersion(major, minor, rev, build);
        }

        public static ParseableVersion Parse(string version)
        {
            var split = version.Split('.');
            if (split.Length < 2) throw new ArgumentException("Argument version is in wrong format");

            var output = new ParseableVersion();
            output.Major = int.Parse(split[0]);
            output.Minor = int.Parse(split[1]);
            if (split.Length >= 3) output.Build = int.Parse(split[2]);
            if (split.Length >= 4) output.Revision = int.Parse(split[3]);
            return output;
        }

        public static ParseableVersion FromAssembly()
        {
            var ourVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return new ParseableVersion()
            {
                Major = ourVersion.Major,
                Minor = ourVersion.Minor,
                Revision = ourVersion.Revision,
                Build = ourVersion.Build,
            };
        }

        public static ParseableVersion FromAssembly(Assembly assembly)
        {
            var ourVersion = assembly.GetName().Version;
            return new ParseableVersion()
            {
                Major = ourVersion.Major,
                Minor = ourVersion.Minor,
                Revision = ourVersion.Revision,
                Build = ourVersion.Build,
            };
        }
    }
}