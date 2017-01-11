using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using Ionic.Zip;
using Microsoft.Win32;

namespace PlayGTANetworkUpdater
{

    static class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [STAThread]
        static void Main()
        {

            #region Check if GTA5 or GTAVLauncher is running
            if (Process.GetProcessesByName("GTA5").Any() || Process.GetProcessesByName("GTAVLauncher").Any() || Process.GetProcessesByName("GTANSubprocess").Any())
            {
                var updateResult = MessageBox.Show("GTAN Launcher has found a running instance of either GTA5/GTAVLauncher/GTANSubprocess, Close before proceeding?", "Alert", MessageBoxButtons.YesNo);
                if (updateResult == DialogResult.Yes)
                {
                    foreach (var process in Process.GetProcessesByName("GTA5"))
                    {
                        process.Kill();
                    }
                    foreach (var process in Process.GetProcessesByName("GTAVLauncher"))
                    {
                        process.Kill();
                    }
                    foreach (var process in Process.GetProcessesByName("GTANSubprocess"))
                    {
                        process.Kill();
                    }
                }
                else
                {
                    return;
                }
            }
            #endregion

            Thread.Sleep(1000);

            #region Check for dependencies
            if (!Environment.Is64BitOperatingSystem)
            {
                MessageBox.Show("GTA Network does not work on 32bit machines.", "Incompatible");
                return;
            }

            if (Environment.OSVersion.ToString().Contains("Windows NT 6.1"))
            {
                MessageBox.Show("You may run into loading to Singleplayer issue using Windows 7", "Just a little reminder :)");
            }

            var NetPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
            if ((int)Registry.GetValue(NetPath, "Release", null) < 379893) //379893 == .NET Framework v4.5.2
            {
                MessageBox.Show("Missing or outdated .NET Framework, required version: 4.5.2 or newer.", "Missing Dependency");
                return;
            }

            var Redist2013x86 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x86";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2013x86, "Version", null)))
            {
                MessageBox.Show("Microsoft Visual C++ 2013 Redistributable (x86) is missing.", "Missing Dependency");
                return;
            }

            var Redist2013x64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x64";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2013x64, "Version", null)))
            {
                MessageBox.Show("Microsoft Visual C++ 2013 Redistributable (x64) is missing.", "Missing Dependency");
                return;
            }

            var Redist2015x86 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2015x86, "Version", null)))
            {
                MessageBox.Show("Microsoft Visual C++ 2015 Redistributable (x86) is missing.", "Missing Dependency");
                return;
            }

            var Redist2015x64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2015x64, "Version", null)))
            {
                MessageBox.Show("Microsoft Visual C++ 2015 Redistributable (x64) is missing.", "Missing Dependency");
                return;
            }
            #endregion

            var dictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V";
            var GTANFolder = (string)Registry.GetValue(dictPath, "GTANetworkInstallDir", null);
            if (GTANFolder != AppDomain.CurrentDomain.BaseDirectory)
            {
                try
                {
                    Registry.SetValue(dictPath, "GTANetworkInstallDir", AppDomain.CurrentDomain.BaseDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Insufficient permissions, Please run as an Admin to avoid permission issues.", "Unauthorized access");
                    return;
                }
            }

            PlayerSettings Settings = new PlayerSettings();

            try
            {
                if (File.Exists("settings.xml") && !string.IsNullOrWhiteSpace(File.ReadAllText("settings.xml")))
                {
                    var ser = new XmlSerializer(typeof(PlayerSettings));
                    using (var stream = File.OpenRead("settings.xml"))
                    {
                        Settings = (PlayerSettings)ser.Deserialize(stream);
                    }
                }
                else if (File.Exists("launcher\\updater.xml") && !string.IsNullOrWhiteSpace(File.ReadAllText("launcher\\updater.xml")))
                {

                    var ser = new XmlSerializer(typeof(PlayerSettings));
                    using (var stream = File.OpenRead("launcher\\updater.xml"))
                    {
                        Settings = (PlayerSettings)ser.Deserialize(stream);
                    }
                }
                else
                {
                    var ser = new XmlSerializer(typeof(PlayerSettings));
                    using (var stream = File.OpenWrite("launcher\\updater.xml"))
                    {
                        ser.Serialize(stream, Settings);
                    }
                }

            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Error");
            }

            ParseableVersion fileVersion = new ParseableVersion(0, 0, 0, 0);
            if (File.Exists("launcher" + "\\" + "GTANetwork.dll"))
            {
                fileVersion = ParseableVersion.Parse(FileVersionInfo.GetVersionInfo(Path.GetFullPath("launcher" + "\\" + "GTANetwork.dll")).FileVersion);
            }

            using (var wc = new ImpatientWebClient())
            {
                try
                {
                    var lastVersion = ParseableVersion.Parse(wc.DownloadString(Settings.MasterServerAddress.Trim('/') + $"/update/{Settings.UpdateChannel}/launcher/version"));
                    if (lastVersion > fileVersion)
                    {
                        var updateResult =
                            MessageBox.Show("New GTANLauncher update is available! Download now?\n\nUpdate Version: " +
                                lastVersion + "\nInstalled Version: " + fileVersion, "Update Available",
                                MessageBoxButtons.YesNo);

                        if (updateResult == DialogResult.Yes)
                        {
                            // Download latest version.
                            if (!Directory.Exists("tempstorage")) Directory.CreateDirectory("tempstorage");
                            wc.Timeout = Int32.MaxValue;
                            wc.DownloadFile(Settings.MasterServerAddress.Trim('/') + $"/update/{Settings.UpdateChannel}/launcher/files", "tempstorage" + "\\" + "files.zip");
                            using (var zipfile = ZipFile.Read("tempstorage" + "\\" + "files.zip"))
                            {
                                zipfile.ParallelDeflateThreshold = -1; // http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract
                                foreach (var entry in zipfile)
                                {
                                    entry.Extract("launcher", ExtractExistingFileAction.OverwriteSilently);
                                }
                            }

                            File.Delete("tempstorage" + "\\" + "files.zip");
                        }
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show("Unable to contact master server, Please check your internet connection and try again.", "Warning");
                    File.AppendAllText("logs" + "\\" + "launcher.log", "MASTER SERVER LOOKUP EXCEPTION AT " + DateTime.Now + "\n\n" + ex);
                }
            }

            Process.Start("launcher\\GTANSubprocess.exe");
        }
        private static void Download(string file, string outputfile, string channel, string MasterServer)
        {
            using (var wc = new ImpatientWebClient())
            {
                try
                {
                    wc.DownloadFile(MasterServer.Trim('/') + $"/update/{channel}/files/launcher/" + file, outputfile);
                }
                catch (WebException e)
                {
                    MessageBox.Show(e.ToString(), "CRITICAL ERROR");
                }
            }
        }
    }

    public class PlayerSettings
    {
        public string MasterServerAddress = "https://master.gtanet.work/";
        public string UpdateChannel = "stable";
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

    internal class XML
    {
        public static dynamic Config(string str)
        {
            dynamic output;
            XElement doc = null;


            return output = (from el in doc.Descendants(str) select el).FirstOrDefault();

        }
    }

    internal struct ParseableVersion : IComparable<ParseableVersion>
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

        public ulong CreateComparableInteger()
        {
            return (ulong)((Revision) + (Build * Math.Pow(10, 4)) + (Minor * Math.Pow(10, 8)) + (Major * Math.Pow(10, 12)));
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
