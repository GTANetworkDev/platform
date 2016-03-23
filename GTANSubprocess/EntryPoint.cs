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
                fileVersion = ParseableVersion.Parse(FileVersionInfo.GetVersionInfo(Path.GetFullPath("bin\\scripts\\GTANetwork.dll")).FileVersion);

            splashScreen.SetPercent(10);

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

            splashScreen.SetPercent(40);

            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show("GTA V is already running. Please shut down the game before starting GTA Network.");
                return;
            }

            var dictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V";
            var keyName = "InstallFolder";
            var installFolder = (string)Registry.GetValue(dictPath, keyName, null);

            if (string.IsNullOrEmpty(installFolder))
            {
                var diag = new OpenFileDialog();

                diag.Filter = "GTA5 Executable|GTA5.exe";
                diag.RestoreDirectory = true;
                diag.CheckFileExists = true;
                diag.CheckPathExists = true;

                if (diag.ShowDialog() == DialogResult.OK)
                {
                    installFolder = Path.GetDirectoryName(diag.FileName);
                    try
                    {
                        Registry.SetValue(dictPath, keyName, installFolder);
                    }
                    catch(UnauthorizedAccessException)
                    { }
                }
                else
                {
                    return;
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
                    MessageBox.Show("We have no access to the registry. Please start the program as Administrator.",
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


            Process.Start(installFolder + "\\GTAVLauncher.exe");

            splashScreen.SetPercent(65);
            
            Process gta5Process;

            var counter = 0;

            while ((gta5Process = Process.GetProcessesByName("GTA5").FirstOrDefault(p => p != null)) == null)
            {
                Thread.Sleep(100);

                if (Process.GetProcessesByName("GTAVLauncher").FirstOrDefault(p => p != null) == null)
                {
                    counter++;
                    if (counter > 50)
                        return;
                }
            }

            splashScreen.SetPercent(70);

            if (Directory.Exists("tempstorage"))
            {
                Directory.Delete("tempstorage", true);
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
                File.Copy(path, installFolder + "\\" + Path.GetFileName(path), true);
                ourFiles.Add(installFolder + "\\" + Path.GetFileName(path));
            }

            Directory.CreateDirectory(installFolder + "\\scripts");

            foreach (var path in Directory.GetFiles("bin\\scripts"))
            {
                File.Copy(path, installFolder + "\\scripts\\" + Path.GetFileName(path), true);
                ourFiles.Add(installFolder + "\\scripts\\" + Path.GetFileName(path));
            }

            splashScreen.SetPercent(100);

            // Close the splashscreen here.

            Thread.Sleep(1000);

            splashScreen.Stop();

            // Wait for GTA5 to exit

            while (!gta5Process.HasExited)
            {
                Thread.Sleep(1000);
            }

            Thread.Sleep(5000);

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
                File.Copy(path, installFolder + "\\" + Path.GetFileName(path), true);
            }

            if (Directory.Exists("tempstorage\\scripts"))
            foreach (var path in Directory.GetFiles("tempstorage\\scripts"))
            {
                File.Copy(path, installFolder + "\\scripts\\" + Path.GetFileName(path), true);
            }

            Directory.Delete("tempstorage", true);
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
    }

    public class SplashScreenThread
    {
        private Thread _thread;
        private bool _hasToClose = false;
        private SplashScreen _splashScreen;

        private delegate void CloseForm();
        private delegate void SetPercentDel(int newPercent);
        
        public SplashScreenThread()
        {
            _thread = new Thread(Show);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void SetPercent(int newPercent)
        {
            while (_splashScreen == null) Thread.Sleep(10);
            if (_splashScreen.InvokeRequired)
                _splashScreen.Invoke(new SetPercentDel(SetPercent), newPercent);
            else
                _splashScreen.progressBar1.Value = newPercent;
        }

        public void Stop()
        {
            if (_splashScreen.InvokeRequired)
                _splashScreen.Invoke(new CloseForm(Stop));
            else
                _splashScreen.Close();
        }

        public void Show()
        {
            _splashScreen = new SplashScreen();
            _splashScreen.ShowDialog();
        }
    }
}
