using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GTANetworkShared
{
    public class LauncherSettings
    {
        public static string[] GameParams = new string[8];

        public interface ISubprocessBehaviour
        {
            void Start(string[] args);
        }
    }
}
