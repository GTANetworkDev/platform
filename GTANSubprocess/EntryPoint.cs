using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

            REWORK:

                1. Check for new update
                2. Check Game directory
                3. Check Game version
                4. Start GTAVLauncher.exe
                5. Start the game
                6. Inject ourselves into GTA5.exe
                7. Restore old settings on GTA5.exe termination.
                8. Terminate
            */

            

            #region Create splash screen
            SplashScreenThread splashScreen = new SplashScreenThread();
            #endregion

            PlayerSettings settings = null;

            settings = ReadSettings("settings.xml");

            splashScreen.SetPercent(10);

            #region Check if running
            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show(splashScreen.SplashScreen, "GTA V is already running. Please close the game before starting GTA Network.");
                return;
            }
            #endregion

            #region Check for dependencies
            if(!Environment.Is64BitOperatingSystem)
            {
                MessageBox.Show(splashScreen.SplashScreen, "GTA Network does not work on 32bit machines.", "Incompatible");
                return;
            }

            if (Environment.OSVersion.ToString().Contains("Windows NT 6.1"))
            {
                MessageBox.Show(splashScreen.SplashScreen, "You may run into loading to Singleplayer issue using Windows 7", "Just a little reminder :)");
            }

            var NetPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
            if ((int)Registry.GetValue(NetPath, "Release", null) < 379893) //379893 == .NET Framework v4.5.2
            {
                MessageBox.Show(splashScreen.SplashScreen, "Missing or outdated .NET Framework, required version: 4.5.2 or newer.", "Missing Dependency");
                return;
            }

            var Redist2013x86 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x86";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2013x86, "Version", null)))
            {
                MessageBox.Show(splashScreen.SplashScreen, "Microsoft Visual C++ 2013 Redistributable (x86) is missing.", "Missing Dependency");
                return;
            }

            var Redist2013x64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x64";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2013x64, "Version", null)))
            {
                MessageBox.Show(splashScreen.SplashScreen, "Microsoft Visual C++ 2013 Redistributable (x64) is missing.", "Missing Dependency");
                return;
            }

            var Redist2015x86 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2015x86, "Version", null)))
            {
                MessageBox.Show(splashScreen.SplashScreen, "Microsoft Visual C++ 2015 Redistributable (x86) is missing.", "Missing Dependency");
                return;
            }

            var Redist2015x64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
            if (string.IsNullOrEmpty((string)Registry.GetValue(Redist2015x64, "Version", null)))
            {
                MessageBox.Show(splashScreen.SplashScreen, "Microsoft Visual C++ 2015 Redistributable (x64) is missing.", "Missing Dependency");
                return;
            }
            #endregion

            splashScreen.SetPercent(20);

            #region Check for new version

            ParseableVersion fileVersion = new ParseableVersion(0, 0, 0, 0);
            if (File.Exists("bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll"))
            {
                fileVersion = ParseableVersion.Parse(FileVersionInfo.GetVersionInfo(Path.GetFullPath("bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll")).FileVersion);
            }

            using (var wc = new ImpatientWebClient())
            {
                try
                {
                    var lastVersion = ParseableVersion.Parse(wc.DownloadString(settings.MasterServerAddress.Trim('/') + $"/update/{settings.UpdateChannel}/version"));
                    if (lastVersion > fileVersion)
                    {
                        var updateResult =
                            MessageBox.Show(splashScreen.SplashScreen,
                                "New GTA Network update is available! Download now?\n\nInternet Version: " +
                                lastVersion + "\nLocal Version: " + fileVersion, "Update Available",
                                MessageBoxButtons.YesNo);

                        if (updateResult == DialogResult.Yes)
                        {
                            // Download latest version.
                            if (!Directory.Exists("tempstorage")) Directory.CreateDirectory("tempstorage");
                            wc.Timeout = Int32.MaxValue;
                            wc.DownloadFile(settings.MasterServerAddress.Trim('/') + $"/update/{settings.UpdateChannel}/files", "tempstorage" + "\\" + "files.zip");
                            using (var zipfile = ZipFile.Read("tempstorage" + "\\" + "files.zip"))
                            {
                                zipfile.ParallelDeflateThreshold = -1; // http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract
                                foreach (var entry in zipfile)
                                {
                                    entry.Extract("bin", ExtractExistingFileAction.OverwriteSilently);
                                }
                            }

                            File.Delete("tempstorage" + "\\" + "files.zip");
                        }
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show(splashScreen.SplashScreen, "Unable to contact master server, Please check your internet connection and try again.", "Warning");
                    File.AppendAllText("logs" + "\\" + "launcher.log", "MASTER SERVER LOOKUP EXCEPTION AT " + DateTime.Now + "\n\n" + ex);
                }
            }
            #endregion

            splashScreen.SetPercent(30);

            #region Check GamePath directory
            if (string.IsNullOrWhiteSpace(settings.GamePath) || !File.Exists(settings.GamePath + "\\" + "GTA5.exe"))
            {
                var diag = new OpenFileDialog();
                diag.Filter = "GTA5 Executable|GTA5.exe";
                diag.FileName = "GTA5.exe";
                diag.DefaultExt = ".exe";
                diag.RestoreDirectory = true;
                diag.CheckFileExists = true;
                diag.CheckPathExists = true;
                diag.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (diag.ShowDialog() == DialogResult.OK)
                {
                    settings.GamePath = Path.GetDirectoryName(diag.FileName);
                    try
                    {
                        SaveSettings("settings.xml", settings);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (2)", "Unauthorized access");
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            #endregion

            splashScreen.SetPercent(35);
          
            #region Check GTAN Folder Registry entry
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
                    MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues.(6)", "Unauthorized access");
                    return;
                }
            }
            #endregion

            splashScreen.SetPercent(40);

            #region Check required folders and clean up
            string Profiles = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Rockstar Games" + "\\GTA V" + "\\Profiles";

            if (!Directory.Exists(Profiles))
            {
                MessageBox.Show(splashScreen.SplashScreen, "Missing Path: " + Profiles + ", Make sure to have run the game atleast once.", "Missing files");
                return;
            }
            if (!Directory.GetFiles(Profiles, "pc_settings.bin", SearchOption.AllDirectories).Any())
            {
                MessageBox.Show(splashScreen.SplashScreen, "Missing Profile, Make sure to have run the game atleast once.", "Missing files");
                return;
            }
            try
            {
                if (Directory.Exists(settings.GamePath + "\\" + "scripts"))
                {
                    if (!Directory.Exists(settings.GamePath + "\\" + "Disabled")) Directory.CreateDirectory(settings.GamePath + "\\" + "Disabled");
                    Directory.Move(settings.GamePath + "\\" + "scripts", settings.GamePath + "\\" + "Disabled" + "\\" + "scripts");
                }

                foreach (var file in Directory.GetFiles(settings.GamePath, "*.asi", SearchOption.TopDirectoryOnly))
                {
                    if (!Directory.Exists(settings.GamePath + "\\" + "Disabled")) Directory.CreateDirectory(settings.GamePath + "\\" + "Disabled");
                    File.Move(file, settings.GamePath + "\\" + "Disabled" + "\\" + Path.GetFileName(file));
                }

                string[] Files = { "ClearScript.dll", "ClearScriptV8-32.dll", "ClearScriptV8-64.dll", "EasyHook64.dll", "scripthookv.dll", "ScriptHookVDotNet.dll", "v8-ia32.dll", "d3d11.dll", "d3d10.dll", "d3d9.dll", "dxgi.dll" };
                foreach (var file in Files)
                {
                    if (!File.Exists(settings.GamePath + "\\" + file)) continue;
                    if (!Directory.Exists(settings.GamePath + "\\" + "Disabled")) Directory.CreateDirectory(settings.GamePath + "\\" + "Disabled");
                    if (!File.Exists(settings.GamePath + "\\" + "Disabled" + "\\" + file)) File.Delete(settings.GamePath + "\\" + "Disabled" + "\\" + file);
                    if (File.Exists(settings.GamePath + "\\" + "Disabled" + "\\" + file)) File.Delete(settings.GamePath + "\\" + "Disabled" + "\\" + file);
                    File.Move(settings.GamePath + "\\" + file, settings.GamePath + "\\" + "Disabled" + "\\" + file);
                }

                foreach (var file in Directory.GetFiles(Profiles, "pc_settings.bin", SearchOption.AllDirectories))
                {
                    if (!File.Exists((Path.GetDirectoryName(file) + "\\" + "SGTA50000.bak"))) continue;
                    if (File.Exists(Path.GetDirectoryName(file) + "\\" + "SGTA50000")) File.Delete(Path.GetDirectoryName(file) + "\\" + "SGTA50000");
                    File.Move(Path.GetDirectoryName(file) + "\\" + "SGTA50000.bak", Path.GetDirectoryName(file) + "\\" + "SGTA50000");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (1)", "Unauthorized access");
                MessageBox.Show(splashScreen.SplashScreen, e.ToString(), "Unauthorized access");
                return;
            }

            #endregion
            splashScreen.SetPercent(50);

            #region Copy over required files to GamePath
            try
            {
                if (!File.Exists(settings.GamePath + "\\" + "sharpdx_direct3d11_effects_x64.dll"))
                    File.Copy(GTANFolder + "bin" + "\\" + "sharpdx_direct3d11_effects_x64.dll", settings.GamePath + "\\" + "sharpdx_direct3d11_effects_x64.dll");

                if (!File.Exists(settings.GamePath + "\\" + "dinput8.dll")) {
                    File.Copy(GTANFolder + "bin" + "\\" + "dinput8.dll", settings.GamePath + "\\" + "dinput8.dll");
                }
                if (!File.Exists(settings.GamePath + "\\" + "v8-x64.dll"))
                    File.Copy(GTANFolder + "bin" + "\\" + "v8-x64.dll", settings.GamePath + "\\" + "v8-x64.dll");

            }
            catch (Exception e)
            {
                MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (3)", "Unauthorized access");
                return;
            }
            #endregion

            splashScreen.SetPercent(60);

            #region Check if CEF is enabled
            //try
            //{
            //    if (File.Exists(GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll")) File.Delete(GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll");
            //    if (settings.CEF)
            //    {
            //        File.Copy(GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll.CEF", GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll");
            //    }
            //    else
            //    {
            //        File.Copy(GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll.Non-CEF", GTANFolder + "bin" + "\\" + "scripts" + "\\" + "GTANetwork.dll");
            //    }
            //}
            //catch (Exception)
            //{
            //    MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (9)", "Unauthorized access");
            //    return;
            //}

            //splashScreen.SetPercent(65);
            #endregion

            #region Patching Game Settings

            var mySettings = GameSettings.LoadGameSettings();
            if (mySettings.Video != null)
            {
                if (mySettings.Video.PauseOnFocusLoss != null)
                {
                    _pauseOnFocusLoss = mySettings.Video.PauseOnFocusLoss.Value;
                    mySettings.Video.PauseOnFocusLoss.Value = 0;
                }
            }
            else
            {
                mySettings.Video = new GameSettings.Video();
                mySettings.Video.PauseOnFocusLoss = new GameSettings.PauseOnFocusLoss();
                mySettings.Video.PauseOnFocusLoss.Value = 0;
            }
            try
            {
                GameSettings.SaveSettings(mySettings);
            }
            catch
            {
                MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues.(8)", "Unauthorized access");
                return;
            }
            #endregion

            splashScreen.SetPercent(70);

            #region Patch Startup Settings
            //string filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.Create) + "\\" + "Rockstar Games" + "\\" + "GTA V" + "\\" + "Profiles";

            //if (!Directory.Exists(filePath))
            //{
            //    MessageBox.Show(splashScreen.SplashScreen, "Missing GTA V Profile folder, Make sure to have run the game atleast once.", "Missing files");
            //    return;
            //}

            //foreach (var dir in Directory.GetDirectories(filePath))
            //{
            //    var Path = dir + "\\" + "pc_settings.bin";
            //    if (!File.Exists(Path)) continue;

            //    using (Stream stream = new FileStream(Path, FileMode.Open))
            //    {
            //        stream.Seek(0xE4, SeekOrigin.Begin); // Startup Flow
            //        _startupFlow = (byte)stream.ReadByte();

            //        stream.Seek(0xEC, SeekOrigin.Begin); // Landing Page
            //        _landingPage = (byte)stream.ReadByte();
            //    }
            //}

            PatchStartup();
            #endregion

            splashScreen.SetPercent(80);

            #region Copy over the savegame
            foreach (var file in Directory.GetFiles(Profiles, "pc_settings.bin", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.Exists((Path.GetDirectoryName(file) + "\\" + "SGTA50000")))
                        File.Move(Path.GetDirectoryName(file) + "\\" + "SGTA50000", Path.GetDirectoryName(file) + "\\" + "SGTA50000.bak");

                    if (File.Exists("savegame" + "\\" + "SGTA50000"))
                        File.Copy("savegame" + "\\" + "SGTA50000", Path.GetDirectoryName(file) + "\\" + "SGTA50000");
                }
                catch (Exception e)
                {
                    MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (4)", "Unauthorized access");
                    return;
                }
            }
            #endregion

            splashScreen.SetPercent(85);

            #region Launch the Game
            BinaryReader br = new BinaryReader(new MemoryStream(File.ReadAllBytes(settings.GamePath + "\\" + "GTA5.exe")));
            br.BaseStream.Position = 0x01500000;
            byte[] array = br.ReadBytes(0x35F757);
            string value = BitConverter.ToString(array).Replace("-", string.Empty);

            if (value.Contains("737465616D")) { Process.Start("steam://run/271590"); } else { Process.Start(settings.GamePath + "\\" + "GTAVLauncher.exe"); }
            #endregion

            splashScreen.SetPercent(90);

            #region Wait for the Game to launch
            Process gta5Process;
            while ((gta5Process = Process.GetProcessesByName("GTA5").FirstOrDefault(p => p != null)) == null) { Thread.Sleep(100); }
            #endregion

            splashScreen.SetPercent(100);
            splashScreen.Stop();

            //Injection
            Thread.Sleep(15000);
            InjectOurselves(gta5Process);

            // Wait for GTA5 to exit
            var launcherProcess = Process.GetProcessesByName("GTAVLauncher").FirstOrDefault(p => p != null);
            while (!gta5Process.HasExited || (launcherProcess != null && !launcherProcess.HasExited)) { Thread.Sleep(1000); }
            Thread.Sleep(1000);

            #region remove that commandline.txt mistake we've made 
            try
            {
                if (File.Exists(settings.GamePath + "\\" + "commandline.txt")) File.Delete(settings.GamePath + "\\" + "commandline.txt");
            }
            catch (Exception)
            {
                MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (7)", "Unauthorized access");
            }
            #endregion

            #region Restore save game
            foreach (var file in Directory.GetFiles(Profiles, "pc_settings.bin", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.Exists((Path.GetDirectoryName(file) + "\\" + "SGTA50000")))
                        File.Delete(Path.GetDirectoryName(file) + "\\" + "SGTA50000");

                    if (File.Exists((Path.GetDirectoryName(file) + "\\" + "SGTA50000.bak")))
                        File.Move(Path.GetDirectoryName(file) + "\\" + "SGTA50000.bak", Path.GetDirectoryName(file) + "\\" + "SGTA50000"); 
                }
                catch (Exception)
                {
                    MessageBox.Show(splashScreen.SplashScreen, "Insufficient permissions, Please run as Admin to avoid permission issues. (5)", "Unauthorized access");
                    return;
                }
            }
            #endregion
        }

        private int _pauseOnFocusLoss;

        public void PatchStartup(byte startupFlow = 0x00, byte landingPage = 0x00)
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "\\" + "Rockstar Games" + "\\" + "GTA V" + "\\" + "Profiles";

            var dirs = Directory.GetDirectories(filePath);

            foreach (var dir in dirs)
            {
                var absPath = dir + "\\" + "pc_settings.bin";

                if (!File.Exists(absPath)) continue;

                using (Stream stream = new FileStream(absPath, FileMode.Open))
                {
                    stream.Seek(0xF4, SeekOrigin.Begin); // Startup Flow, why was it 0xE4?
                    stream.Write(new byte[] { startupFlow }, 0, 1);

                    stream.Seek(0xEC, SeekOrigin.Begin); // Landing Page
                    stream.Write(new byte[] { landingPage }, 0, 1);
                }
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

        public static void SaveSettings(string path, PlayerSettings set)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Truncate)) ser.Serialize(stream, set);
            }
            else 
            {
                using (var stream = new FileStream(path, FileMode.Create)) ser.Serialize(stream, set);
            }

        }

        public static void InjectOurselves(Process gta)
        {
            Inject(gta, Path.GetFullPath("bin" + "\\" + "scripthookv.dll"));
            Inject(gta, Path.GetFullPath("bin" + "\\" + "ScriptHookVDotNet.dll"));

            foreach (var file in Directory.GetFiles("bin", "*.asi"))
            {
                if (string.IsNullOrWhiteSpace(file)) continue;

                if (Path.GetFileName(file).ToLower().StartsWith("scripthookv")) continue;

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
