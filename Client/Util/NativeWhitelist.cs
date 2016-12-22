using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace GTANetwork.Util
{
    public static class NativeWhitelist
    {
        public static void Init()
        {
            var list = new List<ulong>();

            using (var file = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("GTANetwork.natives.txt")))
            {
                string currentLine;
                while (!string.IsNullOrEmpty((currentLine = file.ReadLine())))
                {
                    list.Add(ulong.Parse(currentLine));
                }
            }

            list.Sort();

            _list = list.ToArray();
        }

        private static ulong[] _list = new ulong[0];

        public static bool IsAllowed(ulong native)
        {
            return Array.BinarySearch(_list, native) >= 0;
        }
    }
}