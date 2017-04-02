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

        private static EventHandler _handler;

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
                switch (category)
                {
                    case LogCat.Info:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case LogCat.Warn:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogCat.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogCat.Debug:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(category), category, null);
                }
                Console.WriteLine("[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str);
            }

            if (!_log) return;
            lock (_filelock)
            {
                File.AppendAllText("server.log", "[" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + str + Environment.NewLine);
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
            var bytes = Encoding.UTF8.GetBytes(text);
            var hashstring = new SHA256Managed();
            var hash = hashstring.ComputeHash(bytes);
            return hash.Aggregate(string.Empty, (current, x) => current + $"{x:x2}");
        }


        private static string Location => AppDomain.CurrentDomain.BaseDirectory;

        internal static GameServer ServerInstance { get; set; }
        internal static bool CloseProgram;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);


        private static void Main()
        {
            _handler += Handler;
            var p = (int)Environment.OSVersion.Platform;
            if (p == 4 || p == 6 || p == 128)
            {
                setupHandlers();
            }
            else
            {
                SetConsoleCtrlHandler(_handler, true);
            }

            var settings = ServerSettings.ReadSettings(Location + "settings.xml");
            
            _log = settings.LogToFile;

            if (_log) File.AppendAllText("server.log", "-> SERVER STARTED AT " + DateTime.Now);

            var serverVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

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

            if (settings.Port != 4499) Output("WARN: Port is not the default one, players on your local network won't be able to automatically detect you!");

            Output("Starting...");

            //AppDomain.CurrentDomain.SetShadowCopyFiles();

            if (!Directory.Exists("resources"))
            {
                Output("ERROR: Necessary \"resources\" folder does not exist!");
                Console.Read();
                return;
            }

            ServerInstance = new GameServer(settings) {AllowDisplayNames = true};

            ServerInstance.Start(settings.Resources.Select(r => r.Path).ToArray());

            Output("Started! Waiting for connections.");

 
            while (!CloseProgram)
            {
                ServerInstance.Tick();
                Thread.Sleep(1000/60);
            }

        }

        private static bool Handler(CtrlType sig)
        {
            Output("Terminating...");
            ServerInstance.IsClosing = true;
            while (!ServerInstance.ReadyToClose) { Thread.Sleep(10); }
            CloseProgram = true;
            Console.WriteLine("Terminated.");
            return true;
        }

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool _masterExit;
        private static void setupHandlers()
        {
            var newthread = new Thread(SigHan);
            newthread.Start();
        }

        private static void SigHan()
        {
            UnixSignal[] signals = {
                new UnixSignal (Signum.SIGINT),
                new UnixSignal (Signum.SIGTERM),
                new UnixSignal (Signum.SIGQUIT),
                };

            while (!_masterExit)
            {
                var index = UnixSignal.WaitAny(signals, -1);
                var signal = signals[index].Signum;
                sigHandler(signal);
            };
        }

        private static void sigHandler(Signum signal)
        {
            switch (signal)
            {
                case Signum.SIGINT:    // Control-C
                case Signum.SIGTERM:
                case Signum.SIGQUIT:
                case Signum.SIGHUP:
                case Signum.SIGILL:
                case Signum.SIGTRAP:
                case Signum.SIGABRT:
                case Signum.SIGBUS:
                case Signum.SIGFPE:
                case Signum.SIGKILL:
                case Signum.SIGUSR1:
                case Signum.SIGSEGV:
                case Signum.SIGUSR2:
                case Signum.SIGPIPE:
                case Signum.SIGALRM:
                case Signum.SIGSTKFLT:
                case Signum.SIGCLD:
                case Signum.SIGCONT:
                case Signum.SIGSTOP:
                case Signum.SIGTSTP:
                case Signum.SIGTTIN:
                case Signum.SIGTTOU:
                case Signum.SIGURG:
                case Signum.SIGXCPU:
                case Signum.SIGXFSZ:
                case Signum.SIGVTALRM:
                case Signum.SIGPROF:
                case Signum.SIGWINCH:
                case Signum.SIGPOLL:
                case Signum.SIGPWR:
                case Signum.SIGSYS:
                    _masterExit = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(signal), signal, null);
            }

            if (_masterExit)
            {
                Handler(CtrlType.CTRL_C_EVENT);
            }
        }
    }
}
