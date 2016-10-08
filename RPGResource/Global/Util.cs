using System.Text;

namespace RPGResource.Global
{
    public static class Util
    {
        public static string Repeat(string str, int times)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < times; i++)
            {
                sb.Append(str);
            }

            return sb.ToString();
        }
    }
}