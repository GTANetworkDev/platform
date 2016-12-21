using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using GTANetworkShared;
using Microsoft.Win32;
using Ionic.Zip;

namespace GTANetwork
{
    public class MainBehaviour : LauncherSettings.ISubprocessBehaviour
    {

        public static void EntryPoint(params string[] args)
        {
            new MainBehaviour().Start(args);
        }
        public void Start(params string[] args)
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
            var keyName = "InstallFolder";
            var keyNameSteam = "InstallFolderSteam";
            var gameVer = "GAMEVERSION";

            InstallFolder = (string)Registry.GetValue(dictPath, keyName, null);
            settings.SteamPowered = ((string) Registry.GetValue(dictPath, gameVer, "1") == "2");

            if (string.IsNullOrEmpty(InstallFolder))
            {
                InstallFolder = (string) Registry.GetValue(steamDictPath, keyNameSteam, null);
                settings.SteamPowered = true;

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
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    InstallFolder = InstallFolder.Replace("Grand Theft Auto V\\GTAV", "Grand Theft Auto V");
                }
            }

            try
            {
                SaveSettings("settings.xml", settings);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(splashScreen.SplashScreen, "We require administrative privileges to continue. Please restart as administrator.", "Unauthorized access");
                return;
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
            if (mySettings.Video != null)
            {
                if (mySettings.Video.PauseOnFocusLoss != null)
                {
                    _pauseOnFocusLoss = mySettings.Video.PauseOnFocusLoss.Value;
                    mySettings.Video.PauseOnFocusLoss.Value = 0;
                }

                if (mySettings.Video.Windowed != null)
                {
                    _windowedMode = mySettings.Video.Windowed.Value;
                    if (settings.AutosetBorderlessWindowed)
                        mySettings.Video.Windowed.Value = 2;
                }
            }
            else
            {
                mySettings.Video = new GameSettings.Video();
                mySettings.Video.PauseOnFocusLoss = new GameSettings.PauseOnFocusLoss();
                mySettings.Video.PauseOnFocusLoss.Value = 0;
                mySettings.Video.Windowed = new GameSettings.Windowed();
                mySettings.Video.Windowed.Value = 2;
            }

            MoveAuxilliaryStuffIn();

            GameSettings.SaveSettings(mySettings);

            ReadStartupSettings();

            PatchStartup();

            if (settings.StartGameInOfflineMode)
                InsertCommandline(InstallFolder);

            splashScreen.SetPercent(65);

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
                        MoveStuffOut(InstallFolder);
                        return;
                    }
                }
            }

            splashScreen.SetPercent(100);

            // Close the splashscreen here.


            splashScreen.Stop();

            Thread.Sleep(15000);

            InjectOurselves(gta5Process);

            // Wait for GTA5 to exit

            var launcherProcess = Process.GetProcessesByName("GTAVLauncher").FirstOrDefault(p => p != null);

            while (!gta5Process.HasExited || (launcherProcess != null && !launcherProcess.HasExited))
            {
                Thread.Sleep(1000);
            }

            Thread.Sleep(1000);

            // Move everything back

            PatchStartup(_startupFlow, _landingPage);

            MoveStuffOut(InstallFolder);

            mySettings.Video.PauseOnFocusLoss.Value = _pauseOnFocusLoss;
            mySettings.Video.Windowed.Value = _windowedMode;

            GameSettings.SaveSettings(mySettings);

            var scSubfilePath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.DoNotVerify) + "\\Rockstar Games\\GTA V";

            if (File.Exists(scSubfilePath + "\\silentlauncher"))
            {
                try
                {
                    File.Delete(scSubfilePath + "\\silentlauncher");
                }
                catch { }
            }

            var fils = Directory.GetFiles(scSubfilePath, "*-*");

            foreach (var file in fils)
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
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

        private byte _startupFlow;
        private byte _landingPage;
        private int _pauseOnFocusLoss;
        private int _windowedMode;

        public void InsertCommandline(string path)
        {
            try
            {
                using (var file = new StreamWriter(File.OpenWrite(path + "\\commandline.txt")))
                {
                    file.WriteLine("-scOfflineOnly");
                }
            }catch { }
        }

        public void ReadStartupSettings()
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "\\Rockstar Games\\GTA V\\Profiles";

            var dirs = Directory.GetDirectories(filePath);

            foreach (var dir in dirs)
            {
                var absPath = dir + "\\pc_settings.bin";

                if (!File.Exists(absPath)) continue;

                using (Stream stream = new FileStream(absPath, FileMode.Open))
                {
                    stream.Seek(0xE4, SeekOrigin.Begin); // Startup Flow
                    _startupFlow = (byte)stream.ReadByte();

                    stream.Seek(0xEC, SeekOrigin.Begin); // Landing Page
                    _landingPage = (byte)stream.ReadByte();
                }
            }
        }

        public void PatchStartup(byte startupFlow = 0x00, byte landingPage = 0x00)
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "\\Rockstar Games\\GTA V\\Profiles";

            var dirs = Directory.GetDirectories(filePath);

            foreach (var dir in dirs)
            {
                var absPath = dir + "\\pc_settings.bin";

                if (!File.Exists(absPath)) continue;

                using (Stream stream = new FileStream(absPath, FileMode.Open))
                {
                    stream.Seek(0xE4, SeekOrigin.Begin); // Startup Flow
                    stream.Write(new byte[] { startupFlow }, 0, 1);

                    stream.Seek(0xEC, SeekOrigin.Begin); // Landing Page
                    stream.Write(new byte[] { landingPage }, 0, 1);
                }
            }
        }

        public void MoveAuxilliaryStuffIn()
        {
            string[] aux = new[]
            { "ClearScriptV8-32.dll", "ClearScriptV8-64.dll", "v8-ia32.dll", "v8-x64.dll", "EasyHook64.dll", "sharpdx_direct3d11_effects_x64.dll"};

            foreach (var path in aux)
            {
                NoReadonly(InstallFolder + "\\" + path);
                File.Copy("bin\\" + path, InstallFolder + "\\" + path, true);
                OurFiles.Add(InstallFolder + "\\" + path);
            }

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

            /*
            foreach (var path in Directory.GetFiles("cef"))
            {
                NoReadonly(InstallFolder + "\\" + Path.GetFileName(path));
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }

            foreach (var path in Directory.GetDirectories("cef"))
            {
                CopyFolder(path, InstallFolder + "\\" + Path.GetFileName(path));
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }
            */
        }

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
                NoReadonly(InstallFolder + "\\" + Path.GetFileName(path));
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }

            Directory.CreateDirectory(InstallFolder + "\\scripts");

            foreach (var path in Directory.GetFiles("bin\\scripts"))
            {
                NoReadonly(InstallFolder + "\\scripts\\" + Path.GetFileName(path));
                File.Copy(path, InstallFolder + "\\scripts\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\scripts\\" + Path.GetFileName(path));
            }
            
            foreach (var path in Directory.GetFiles("cef"))
            {
                NoReadonly(InstallFolder + "\\" + Path.GetFileName(path));
                File.Copy(path, InstallFolder + "\\" + Path.GetFileName(path), true);
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }

            foreach (var path in Directory.GetDirectories("cef"))
            {
                CopyFolder(path, InstallFolder + "\\" + Path.GetFileName(path));
                OurFiles.Add(InstallFolder + "\\" + Path.GetFileName(path));
            }
        }

        public void MoveStuffOut(string installFolder)
        {
            if (File.Exists(installFolder + "\\commandline.txt"))
            {
                try
                {
                    NoReadonly(installFolder + "\\commandline.txt");
                    File.Delete(installFolder + "\\commandline.txt");
                }catch { }
            }

            foreach (var file in OurFiles)
            {
                try
                {
                    NoReadonly(file);
                    File.Delete(file);
                }
                catch
                { }
            }

            if (Directory.Exists("tempstorage"))
            {
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
                NoReadonly(file);
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
                NoReadonly(dest);
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
            NoReadonly(destFile);
            NoReadonly(sourceFile);
            File.Copy(sourceFile, destFile);
            File.SetAttributes(sourceFile, FileAttributes.Normal);
            File.Delete(sourceFile);
        }

        public static void MoveDirectory(string sourceDir, string destDir)
        {
            CopyFolder(sourceDir, destDir);
            DeleteDirectory(sourceDir);
        }

        public static void NoReadonly(string path)
        {
            if (File.Exists(path))
                new FileInfo(path).IsReadOnly = false;
        }

        public static void InjectOurselves(Process gta)
        {
            Inject(gta, Path.GetFullPath("bin\\scripthookv.dll"));
            Inject(gta, Path.GetFullPath("bin\\ScriptHookVDotNet.dll"));

            foreach (var file in Directory.GetFiles("bin", "*.asi"))
            {
                if (file.ToLower().StartsWith("scripthookv")) continue;

                try
                {
                    Inject(gta, file);
                } catch { }
            }
        }

        public static void Inject(Process target, string path)
        {
            DllInjector.GetInstance.Inject(target, Path.GetFullPath(path));
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
