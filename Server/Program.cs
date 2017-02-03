using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using GTANetworkShared;
using Mono.Unix;
using Mono.Unix.Native;
using GTANetworkServer.Constant;

namespace GTANetworkServer
{
    internal static class Program
    {

        [DllImport("Kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        private static object _consolelock = new object();
        private static object _filelock = new object();
        private static bool _log;

        public static long GetTicks()
        {
            return DateTime.Now.Ticks/10000;
        }

        public static void ToFile(string path, string str)
        {
            File.AppendAllText(path, "[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str + Environment.NewLine);
        }

        public static void Output(string str, LogCat category = LogCat.Info)
        {
            lock (_consolelock)
            {
                if (category == LogCat.Info)
                    Console.ForegroundColor = ConsoleColor.Gray;
                else if (category == LogCat.Warn)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (category == LogCat.Error)
                    Console.ForegroundColor = ConsoleColor.Red;
                else if (category == LogCat.Debug)
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str);
            }

            if (_log)
            {
                lock (_filelock)
                {
                    File.AppendAllText("server.log", "[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str + Environment.NewLine);
                }
            }
        }

        public static int GetHash(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            var bytes = Encoding.UTF8.GetBytes(input.ToLower().ToCharArray());
            uint hash = 0;

            for (int i = 0, length = bytes.Length; i < length; i++)
            {
                hash += bytes[i];
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }

            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);

            return unchecked((int)hash);
        }

        public static string GetHashSHA256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            string hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return hashString;
        }


        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        internal static GameServer ServerInstance { get; set; }
        internal static bool CloseProgram = false;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);


        static void Main(string[] args)
        {

            _handler += new EventHandler(Handler);
            int p = (int)Environment.OSVersion.Platform;
            if ((p == 4) || (p == 6) || (p == 128))
            {
                setupHandlers();
            }
            else
            {
                SetConsoleCtrlHandler(_handler, true);
            }

            var settings = ServerSettings.ReadSettings(Program.Location + "settings.xml");
            
            _log = settings.LogToFile;

            if (_log)
                File.AppendAllText("server.log", "-> SERVER STARTED AT " + DateTime.Now);

            ParseableVersion serverVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

            Console.WriteLine("=======================================================================");
            Console.WriteLine("= GRAND THEFT AUTO NETWORK v{0}", serverVersion);
            Console.WriteLine("=======================================================================");
            Console.WriteLine("= Server Name: " + settings.Name);
            Console.WriteLine("= Server Port: " + settings.Port);
            Console.WriteLine("= Server FQDN: " + settings.fqdn);
            Console.WriteLine("=");
            Console.WriteLine("= Player Limit: " + settings.MaxPlayers);
            Console.WriteLine("= Log Level: " + settings.LogLevel + " (1: ERROR, 2: DEBUG, 3: VERBOSE)");
            Console.WriteLine("=======================================================================");

            if (settings.Port != 4499)
                Output("WARN: Port is not the default one, players on your local network won't be able to automatically detect you!");

            Output("Starting...");

            //AppDomain.CurrentDomain.SetShadowCopyFiles();

            if (!Directory.Exists("resources"))
            {
                Output("ERROR: Necessary \"resources\" folder does not exist!");
                Console.Read();
                return;
            }

            ServerInstance = new GameServer(settings);
            ServerInstance.AllowDisplayNames = true;

            ServerInstance.Start(settings.Resources.Select(r => r.Path).ToArray());

            Output("Started! Waiting for connections.");

 
            while (!CloseProgram)
            {
                ServerInstance.Tick();
                Thread.Sleep(1000/settings.RefreshHz);
            }

        }

        private static bool Handler(CtrlType sig)
        {
            Program.Output("Terminating...");
            ServerInstance.IsClosing = true;
            DateTime start = DateTime.Now;
            while (!ServerInstance.ReadyToClose)
            {
                Thread.Sleep(10);
            }
            CloseProgram = true;
            Console.WriteLine("Terminated.");
            return true;
        }

        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public static bool masterExit = false;
        private static void setupHandlers()
        {
            Thread newthread = new Thread(new ThreadStart(sigHan));
            newthread.Start();
        }

        private static void sigHan()
        {
            UnixSignal[] signals = new UnixSignal[] {
                new UnixSignal (Signum.SIGINT),
                new UnixSignal (Signum.SIGTERM),
                new UnixSignal (Signum.SIGQUIT),
                };

            while (!masterExit)
            {
                int index = UnixSignal.WaitAny(signals, -1);
                Signum signal = signals[index].Signum;
                sigHandler(signal);
            };
        }

        private static void sigHandler(Signum signal)
        {
            switch (signal)
            {
                case Signum.SIGINT:    // Control-C
                    Console.WriteLine("Processing SIGINT Signal");
                    masterExit = true;
                    break;
                case Signum.SIGTERM:
                    Console.WriteLine("Processing SIGTERM Signal");
                    masterExit = true;
                    break;
                case Signum.SIGQUIT:
                    Console.WriteLine("Processing SIGQUIT Signal");
                    masterExit = true;
                    break;
            }

            if (masterExit)
            {
                Handler(CtrlType.CTRL_C_EVENT);
            }
        }
    }
}
