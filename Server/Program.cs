using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Serialization;

namespace GTANetworkServer
{
    public static class Program
    {
        public static void Output(string str)
        {
            Console.WriteLine("[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str);
        }


        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static GameServer ServerInstance { get; set; }
        private static bool CloseProgram = false;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        static void Main(string[] args)
        {
            var settings = ReadSettings(Program.Location + "settings.xml");

            Console.WriteLine("=======================================================================");
            Console.WriteLine("= GRAND THEFT AUTO NETWORK v1.0");
            Console.WriteLine("=======================================================================");
            Console.WriteLine("= Server Name: " + settings.Name);
            Console.WriteLine("= Server Port: " + settings.Port);
            Console.WriteLine("=");
            Console.WriteLine("= Player Limit: " + settings.MaxPlayers);
            Console.WriteLine("=======================================================================");

            if (settings.Port != 4499)
                Output("WARN: Port is not the default one, players on your local network won't be able to automatically detect you!");

            Output("Starting...");

            ServerInstance = new GameServer(settings.Port, settings.Name);
            ServerInstance.PasswordProtected = !String.IsNullOrWhiteSpace(settings.Password);
            ServerInstance.Password = settings.Password;
            ServerInstance.AnnounceSelf = settings.Announce;
            ServerInstance.MasterServer = settings.MasterServer;
            ServerInstance.MaxPlayers = settings.MaxPlayers;
            ServerInstance.AllowDisplayNames = true;

            ServerInstance.Start(settings.Resources.Select(r => r.Path).ToArray());

            Output("Started! Waiting for connections.");

            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);

            while (!CloseProgram)
            {
                ServerInstance.Tick();
                Thread.Sleep(10); // Reducing CPU Usage (Win7 from average 15 % to 0-1 %, Linux from 100 % to 0-2 %)
            }

        }


        #region unmanaged

        private static bool ConsoleCtrlCheck(CtrlTypes ctrType)
        {
            ServerInstance.IsClosing = true;
            Program.Output("Terminating...");
            DateTime start = DateTime.Now;
            while (!ServerInstance.ReadyToClose)
            {
                Thread.Sleep(10);
            }
            CloseProgram = true;
            return true;
        }

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.

        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        #endregion


        static ServerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(ServerSettings));

            ServerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (ServerSettings)ser.Deserialize(stream);

                //using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new ServerSettings());
            }

            return settings;
        }
    }
}
