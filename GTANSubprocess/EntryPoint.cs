using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;
using PlayGTANetwork;
using Ionic.Zip;

namespace GTANetwork
{
    public class MainBehaviour : ISubprocessBehaviour
    {
        public void Start()
        {
            /*
            WE START HERE:

                1. Check for new update
                2. Start GTAVLauncher.exe
                3. Spin until GTAVLauncher.exe process is kill
                4. Is there a GTA5.exe process? No -> terminate self. Yes -> continue
                5. Move all mods/whatever to temporary folder.
                6. Move our mod into the game directory.
                7. Spin until GTA5.exe terminates
                8. Delete our mod files.
                9. Move the temporary mod files back
                10. Terminate

            */

            var settings = ReadSettings("settings.xml");

            if (settings == null)
            {
                MessageBox.Show("No settings were found.");
            }

            ParseableVersion fileVersion = new ParseableVersion(0, 0, 0, 0);
            if (File.Exists("bin\\scripts\\GTANetwork.dll"))
                fileVersion = ParseableVersion.Parse(FileVersionInfo.GetVersionInfo(Path.GetFullPath("bin\\scripts\\GTANetwork.dll")).FileVersion);

            // Check for new version
            using (var wc = new ImpatientWebClient())
            {
                try
                {
                    var lastVersion = ParseableVersion.Parse(wc.DownloadString(settings.MasterServerAddress.Trim('/') + "/version"));
                    if (lastVersion > fileVersion)
                    {
                        // Download latest version.
                        if (!Directory.Exists("tempstorage")) Directory.CreateDirectory("tempstorage");
                        wc.DownloadFile(settings.MasterServerAddress.Trim('/') + "/files", "tempstorage\\files.zip");
                        using (var zipfile = ZipFile.Read("tempstorage\\files.zip"))
                        {
                            foreach (var entry in zipfile)
                            {
                                entry.Extract("bin", ExtractExistingFileAction.OverwriteSilently);
                            }
                        }

                        File.Delete("tempstorage\\files.zip");
                    }
                }
                catch (WebException)
                {
                    MessageBox.Show(
                        "The master server is unavailable at this time. Unable to check for latest version.", "Warning");
                }
            }

            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show("GTA V is already running. Please shut down the game before starting GTA Network.");
                return;
            }

            var dictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V";
            var keyName = "InstallFolder";
            var installFolder = (string)Registry.GetValue(dictPath, keyName, "");


            if ((string) Registry.GetValue(dictPath, "GTANetworkInstallDir", null) != AppDomain.CurrentDomain.BaseDirectory)
            {
                try
                {
                    Registry.SetValue(dictPath, "GTANetworkInstallDir", AppDomain.CurrentDomain.BaseDirectory);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show("We have no access to the registry. Please start the program as Administrator.",
                        "UNAUTHORIZED ACCESS");
                    return;
                }
            }

            var mySettings = GameSettings.LoadGameSettings();
            if (mySettings.Video != null && mySettings.Video.PauseOnFocusLoss != null)
                mySettings.Video.PauseOnFocusLoss.Value = 0;
            else
            {
                mySettings.Video = new GameSettings.Video();
                mySettings.Video.PauseOnFocusLoss = new GameSettings.PauseOnFocusLoss();
                mySettings.Video.PauseOnFocusLoss.Value = 0;
            }

            var GTALauncherProcess = Process.Start(installFolder + "\\GTAVLauncher.exe");

            Thread.Sleep(5000);

            while (!GTALauncherProcess.HasExited)
            {
                Thread.Sleep(10);
            }

            Thread.Sleep(5000);

            Process gta5Process;
            var start = DateTime.Now;

            while ((gta5Process = Process.GetProcessesByName("GTA5").FirstOrDefault(p => p != null)) == null)
            {
                Thread.Sleep(10);

                if (DateTime.Now.Subtract(start).TotalMilliseconds > 10000)
                {
                    return;
                }
            }

            // Close the splashscreen here.

            if (Directory.Exists("tempstorage"))
            {
                Directory.Delete("tempstorage", false);
            }

            Directory.CreateDirectory("tempstorage");

            var filesRoot = Directory.GetFiles(installFolder, "*.asi");
            foreach (var s in filesRoot)
            {
                File.Move(s, "tempstorage\\" + Path.GetFileName(s));
            }

            if (File.Exists(installFolder + "\\dinput8.dll"))
                File.Move(installFolder + "\\dinput8.dll", "tempstorage\\dinput8.dll");

            if (File.Exists(installFolder + "\\dsound.dll"))
                File.Move(installFolder + "\\dsound.dll", "tempstorage\\dsound.dll");

            if (File.Exists(installFolder + "\\scripthookv.dll"))
                File.Move(installFolder + "\\scripthookv.dll", "tempstorage\\scripthookv.dll");

            if (Directory.Exists(installFolder + "\\scripts"))
                Directory.Move(installFolder + "\\scripts", "tempstorage\\scripts");

            // Moving our stuff

            List<string> ourFiles = new List<string>();

            foreach (var path in Directory.GetFiles("bin"))
            {
                File.Copy(path, installFolder + "\\" + Path.GetFileName(path));
                ourFiles.Add(installFolder + "\\" + Path.GetFileName(path));
            }

            Directory.CreateDirectory(installFolder + "\\scripts");

            foreach (var path in Directory.GetFiles("bin\\scripts"))
            {
                File.Copy(path, installFolder + "\\scripts\\" + Path.GetFileName(path));
                ourFiles.Add(installFolder + "\\scripts\\" + Path.GetFileName(path));
            }

            // Wait for GTA5 to exit

            while (!gta5Process.HasExited)
            {
                Thread.Sleep(1000);
            }

            // Move everything back

            foreach (var file in ourFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch(Exception e)
                { }
            }
            
            foreach (var path in Directory.GetFiles("tempstorage"))
            {
                File.Move(path, installFolder + "\\" + Path.GetFileName(path));
            }

            if (Directory.Exists("tempstorage\\scripts"))
            foreach (var path in Directory.GetFiles("tempstorage\\scripts"))
            {
                File.Move(path, installFolder + "\\scripts\\" + Path.GetFileName(path));
            }

            
        }

        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);
            }

            return settings;
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

    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }
        public List<string> FavoriteServers { get; set; }
        public List<string> RecentServers { get; set; }
        public bool ScaleChatWithSafezone { get; set; }


        public PlayerSettings()
        {
            MaxStreamedNpcs = 10;
            MasterServerAddress = "http://148.251.18.67:8888/";
            ActivationKey = Keys.F9;
            FavoriteServers = new List<string>();
            RecentServers = new List<string>();
            ScaleChatWithSafezone = true;
        }
    }

    public struct ParseableVersion : IComparable<ParseableVersion>
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public int Build { get; set; }

        public ParseableVersion(int major, int minor, int rev, int build)
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
    }
}
