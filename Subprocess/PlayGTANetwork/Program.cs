using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GTANetworkShared;
using Microsoft.Win32;

namespace PlayGTANetwork
{
    public static class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string GTANFolder = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "GTANetworkInstallDir", null);
            IEnumerable<Type> validTypes;
            try
            {
                try
                {
                    DeleteFile(GTANFolder + "launcher\\GTANetwork.dll:Zone.Identifier");
                }
                catch (Exception e) { MessageBox.Show("ERROR: " + e.Message, "CRITICAL ERROR"); }

                var ourAssembly = Assembly.LoadFrom(GTANFolder + "launcher\\GTANetwork.dll");

                var types = ourAssembly.GetExportedTypes();
                validTypes = types.Where(t =>
                    !t.IsInterface &&
                    !t.IsAbstract)
                    .Where(t => typeof(LauncherSettings.ISubprocessBehaviour).IsAssignableFrom(t));
            }
            catch (Exception e)
            {
                MessageBox.Show("ERROR: " + e.Message, "CRITICAL ERROR");
                goto end;
            }

            if (!validTypes.Any())
            {
                MessageBox.Show("Failed to load assembly \"GTANetwork.dll\": no assignable classes found.", "CRITICAL ERROR");
                goto end;
            }

            LauncherSettings.ISubprocessBehaviour mainBehaviour = null;
            foreach (var type in validTypes)
            {
                mainBehaviour = Activator.CreateInstance(type) as LauncherSettings.ISubprocessBehaviour;
                if (mainBehaviour != null)
                    break;
            }

            if (mainBehaviour == null)
            {
                MessageBox.Show("Failed to load assembly \"GTANetwork.dll\": assignable class is null.", "CRITICAL ERROR");
                goto end;
            }

            try
            {
                mainBehaviour.Start(args);
            }
            catch (Exception ex)
            {
                if (!Directory.Exists(GTANFolder + "logs")) Directory.CreateDirectory(GTANFolder + "logs");
                File.AppendAllText(GTANFolder + "logs\\launcher.log", "LAUNCHER EXCEPTION AT " + DateTime.Now + "\r\n" + ex.ToString() + "\r\n\r\n");
                MessageBox.Show(ex.ToString(), "FATAL ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            end:
            { }
        }
    }
}
