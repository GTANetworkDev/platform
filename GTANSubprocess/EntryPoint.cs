using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using PlayGTANetwork;

namespace GTANetwork
{
    public class MainBehaviour : ISubprocessBehaviour
    {
        public void Start()
        {
            /*
                Steps:
                1. Check for new update -> PARENT

            WE START HERE:

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

            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show("GTA V is already running. Please shut down the game before starting GTA Network.");
                return;
            }

            var dictPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V";
            var keyName = "InstallFolder";
            var installFolder = (string)Registry.GetValue(dictPath, keyName, "");


            try
            {
                Registry.SetValue(dictPath, "GTANetworkInstallDir", AppDomain.CurrentDomain.BaseDirectory);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("We have no access to the registry. Please start the program as Administrator.",
                    "UNAUTHORIZED ACCES");
                return;
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

            while (!GTALauncherProcess.HasExited)
            {
                Thread.Sleep(10);
            }

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
    }
}
