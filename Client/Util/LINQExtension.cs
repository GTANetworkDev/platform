using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GTANetwork.Util
{
    public static class LINQExtension
    {
        public static bool ContainsEx<T>(this List<T> list, T value)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].Equals(value))
                {
                    return true;
                }
            }
            return false;
        }


        public static string GetBetween(this string content, string startString, string endString)
        {
            if (!content.Contains(startString) || !content.Contains(endString)) return string.Empty;
            var Start = content.IndexOf(startString, 0) + startString.Length;
            var End = content.IndexOf(endString, Start);
            return content.Substring(Start, End - Start);
        }
    }


}