using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using GTANetworkShared;
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
                return;
            }
            

            // Create splash screen

            var splashScreen = new SplashScreenThread();
            
            ParseableVersion fileVersion = new ParseableVersion(0, 0, 0, 0);
            if (File.Exists("bin\\scripts\\GTANetwork.dll"))
            {
                fileVersion = ParseableVersion.Parse(FileVersionInfo.GetVersionInfo(Path.GetFullPath("bin\\scripts\\GTANetwork.dll")).FileVersion);
            }

            splashScreen.SetPercent(10);

            // Check for new version
            using (var wc = new ImpatientWebClient())
            {
                try
                {
                    var lastVersion = ParseableVersion.Parse(wc.DownloadString(settings.MasterServerAddress.Trim('/') + $"/update/{settings.UpdateChannel}/version"));
                    if (lastVersion > fileVersion)
                    {
                        var updateResult =
                            MessageBox.Show(splashScreen.SplashScreen,
                                "New GTA Network version is available! Download now?\n\nInternet Version: " +
                                lastVersion + "\nOur Version: " + fileVersion, "Update Available",
                                MessageBoxButtons.YesNo);

                        if (updateResult == DialogResult.Yes)
                        {
                            // Download latest version.
                            if (!Directory.Exists("tempstorage")) Directory.CreateDirectory("tempstorage");
                            wc.Timeout = Int32.MaxValue;
                            wc.DownloadFile(settings.MasterServerAddress.Trim('/') + $"/update/{settings.UpdateChannel}/files", "tempstorage\\files.zip");
                            using (var zipfile = ZipFile.Read("tempstorage\\files.zip"))
                            {
                                zipfile.ParallelDeflateThreshold = -1; // http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract
                                foreach (var entry in zipfile)
                                {
                                    entry.Extract("bin", ExtractExistingFileAction.OverwriteSilently);
                                }
                            }

                            File.Delete("tempstorage\\files.zip");
                        }
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show(splashScreen.SplashScreen,
                        "The master server is unavailable at this time. Unable to check for latest version.", "Warning");
                    File.AppendAllText("logs\\launcher.log", "MASTER SERVER LOOKUP EXCEPTION AT " + DateTime.Now + "\n\n" + ex);
                }
            }

            splashScreen.SetPercent(40);

            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show(splashScreen.SplashScreen, "GTA V is already running. Please shut down the game before starting GTA Network.");
                return;
            }

            var dictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V";
            var steamDictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\GTAV";
            var steamKeyName = "InstallFolderSteam";
            var keyName = "InstallFolder";




            InstallFolder = (string)Registry.GetValue(dictPath, keyName, null);

            if (string.IsNullOrEmpty(InstallFolder))
            {
                InstallFolder = (string) Registry.GetValue(steamDictPath, steamKeyName, null);
                settings.SteamPowered = true;

                try
                {
                    SaveSettings("settings.xml", settings);
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show(splashScreen.SplashScreen, "We require administrative privileges to continue. Please restart as administrator.", "Unauthorized access");
                    return;
                }

                if (string.IsNullOrEmpty(InstallFolder))
                {
                    var diag = new OpenFileDialog();
                    diag.Filter = "GTA5 Executable|GTA5.exe";
                    diag.RestoreDirectory = true;
                    diag.CheckFileExists = true;
                    diag.CheckPathExists = true;

                    if (diag.ShowDialog() == DialogResult.OK)
                    {
                        InstallFolder = Path.GetDirectoryName(diag.FileName);
                        try
                        {
                            Registry.SetValue(dictPath, keyName, InstallFolder);
                        }
                        catch(UnauthorizedAccessException)
                        { }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            splashScreen.SetPercent(50);

            if ((string) Registry.GetValue(dictPath, "GTANetworkInstallDir", null) != AppDomain.CurrentDomain.BaseDirectory)
            {
                try
                {
                    Registry.SetValue(dictPath, "GTANetworkInstallDir", AppDomain.CurrentDomain.BaseDirectory);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(splashScreen.SplashScreen, "We have no access to the registry. Please start the program as Administrator.",
                        "UNAUTHORIZED ACCESS");
                    return;
                }
            }

            splashScreen.SetPercent(60);

            var mySettings = GameSettings.LoadGameSettings();
            if (mySettings.Video != null && mySettings.Video.PauseOnFocusLoss != null)
                mySettings.Video.PauseOnFocusLoss.Value = 0;
            else
            {
                mySettings.Video = new GameSettings.Video();
                mySettings.Video.PauseOnFocusLoss = new GameSettings.PauseOnFocusLoss();
                mySettings.Video.PauseOnFocusLoss.Value = 0;
            }

            splashScreen.SetPercent(65);

            MoveStuffIn();


            if (!settings.SteamPowered)
            {
                Process.Start(InstallFolder + "\\GTAVLauncher.exe");
            }
            else
            {
                Process.Start("steam://run/271590");
            }

            splashScreen.SetPercent(80);

            Process gta5Process;

            var counter = 0;

            while ((gta5Process = Process.GetProcessesByName("GTA5").FirstOrDefault(p => p != null)) == null)
            {
                Thread.Sleep(100);

                if (Process.GetProcessesByName("GTAVLauncher").FirstOrDefault(p => p != null) == null)
                {
                    counter++;
                    if (counter > 50)
                    {
                        MoveStuffOut();
                        return;
                    }
                }
            }

            splashScreen.SetPercent(100);

            // Close the splashscreen here.

            Thread.Sleep(1000);

            splashScreen.Stop();

            // Wait for GTA5 to exit

            var launcherProcess = Process.GetProcessesByName("GTAVLauncher").FirstOrDefault(p => p != null);

            while (!gta5Process.HasExited || (launcherProcess != null && !launcherProcess.HasExited))
            {
                Thread.Sleep(1000);
            }

            Thread.Sleep(5000);

            // Move everything back

            MoveStuffOut();
        }

        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = new PlayerSettings();

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);
            }

            return settings;
        }

        private List<string> OurFiles = new List<string>();
        private string InstallFolder;

        public void MoveStuffIn()
        {
            if (Directory.Exists("tempstorage"))
            {
                DeleteDirectory("tempstorage");
            }

            Directory.CreateDirectory("tempstorage");

            var filesRoot = Directory.GetFiles(InstallFolder, "*.asi");
            foreach (var s in filesRoot)
            {
                MoveFile(s, "tempstorage\\" + Path.GetFileName(s));
            }

            if (File.Exists(InstallFolder + "\\dinput8.dll"))
                MoveFile(InstallFolder + "\\dinput8.dll", "tempstorage\\dinput8.dll");

            if (File.Exists(InstallFolder + "\\dsound.dll"))
                MoveFile(InstallFolder + "\\dsound.dll", "tempstorage\\dsound.dll");

            if (File.Exists(InstallFolder + "\\scripthookv.dll"))
                MoveFile(InstallFolder + "\\scripthookv.dll", "tempstorage\\scripthookv.dll");

            if (File.Exists(InstallFolder + "\\commandline.txt"))
                MoveFile(InstallFolder + "\\commandline.txt", "tempstorage\\commandline.txt");


            if (Directory.Exists(InstallFolder + "\\scripts"))
                MoveDirectory(InstallFolder + "\\scripts", "tempstorage\\scripts");

            // Moving our stuff

            foreach (var path in Directory.GetFiles("bin"))
            {
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }

            Directory.CreateDirectory(InstallFolder + "\\scripts");

            foreach (var path in Directory.GetFiles("bin\\scripts"))
            {
                File.Copy(path, InstallFolder + "\\scripts\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\scripts\\" + Path.GetFileName(path));
            }
            
            foreach (var path in Directory.GetFiles("cef"))
            {
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }

            foreach (var path in Directory.GetDirectories("cef"))
            {
                CopyFolder(path, InstallFolder + "\\" + Path.GetFileName(path));
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }
        }

        public void MoveStuffOut()
        {
            foreach (var file in OurFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                { }
            }

            foreach (var path in Directory.GetFiles("tempstorage"))
            {
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
            }

            if (Directory.Exists("tempstorage\\scripts"))
            {
                if (!Directory.Exists(InstallFolder + "\\scripts"))
                    Directory.CreateDirectory(InstallFolder + "\\scripts");

                foreach (var path in Directory.GetFiles("tempstorage\\scripts"))
                {
                    File.Copy(path, InstallFolder + "\\scripts\\" + Path.GetFileName(path), true);
                }
            }

            DeleteDirectory("tempstorage");
        }

        public static void SaveSettings(string path, PlayerSettings set)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            if (File.Exists(path)) using (var stream = new FileStream(path, FileMode.Truncate)) ser.Serialize(stream, set);
            else using (var stream = new FileStream(path, FileMode.Create)) ser.Serialize(stream, set);
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, true);
        }

        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest, true);
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        public static void MoveFile(string sourceFile, string destFile)
        {
            File.Copy(sourceFile, destFile);
            File.SetAttributes(sourceFile, FileAttributes.Normal);
            File.Delete(sourceFile);
        }

        public static void MoveDirectory(string sourceDir, string destDir)
        {
            CopyFolder(sourceDir, destDir);
            DeleteDirectory(sourceDir);
        }
    }

    public class SplashScreenThread
    {
        private Thread _thread;
        private bool _hasToClose = false;

        private delegate void CloseForm();
        private delegate void SetPercentDel(int newPercent);

        public SplashScreen SplashScreen;
        
        public SplashScreenThread()
        {
            _thread = new Thread(Show);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void SetPercent(int newPercent)
        {
            while (SplashScreen == null) Thread.Sleep(10);
            if (SplashScreen.InvokeRequired)
                SplashScreen.Invoke(new SetPercentDel(SetPercent), newPercent);
            else
                SplashScreen.progressBar1.Value = newPercent;
        }

        public void Stop()
        {
            if (SplashScreen.InvokeRequired)
                SplashScreen.Invoke(new CloseForm(Stop));
            else
                SplashScreen.Close();
        }

        public void Show()
        {
            SplashScreen = new SplashScreen();
            SplashScreen.ShowDialog();
        }
    }
}
