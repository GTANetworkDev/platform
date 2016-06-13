using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CEFInjector.DirectXHook;
using CEFInjector.DirectXHook.Hook;
using CEFInjector.DirectXHook.Interface;

namespace CEFInjector
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            AttachProcess("GTA5.exe", Direct3DVersion.Direct3D11);

            Console.WriteLine("Waiting on bitmap...");

            Thread.Sleep(3000);

            _captureProcess.CaptureInterface.UpdateMainBitmap(new Bitmap(@"A:\Dropbox\stuff\Reaction Images\gamingjournalism.png"));

            Console.WriteLine("Bitmap written.");

            Console.ReadLine();

            Console.WriteLine("Detaching...");
            HookManager.RemoveHookedProcess(_captureProcess.Process.Id);
            _captureProcess.CaptureInterface.Disconnect();
            _captureProcess = null;
        }

        static int processId = 0;
        static Process _process;
        static CaptureProcess _captureProcess;
        static void AttachProcess(string exe, Direct3DVersion direct3DVersion)
        {
            string exeName = Path.GetFileNameWithoutExtension(exe);

            Process[] processes = Process.GetProcessesByName(exeName);
            foreach (Process process in processes)
            {
                // Simply attach to the first one found.

                // If the process doesn't have a mainwindowhandle yet, skip it (we need to be able to get the hwnd to set foreground etc)
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                // Skip if the process is already hooked (and we want to hook multiple applications)
                if (HookManager.IsHooked(process.Id))
                {
                    continue;
                }

                

                CaptureConfig cc = new CaptureConfig()
                {
                    Direct3DVersion = direct3DVersion,
                    ShowOverlay = true,
                };

                processId = process.Id;
                _process = process;

                var captureInterface = new CaptureInterface();
                captureInterface.RemoteMessage += new MessageReceivedEvent(CaptureInterface_RemoteMessage);
                _captureProcess = new CaptureProcess(process, cc, captureInterface);

                break;
            }
        }

        static void CaptureInterface_RemoteMessage(MessageReceivedEventArgs message)
        {
            Console.WriteLine(message);
        }
    }
}
