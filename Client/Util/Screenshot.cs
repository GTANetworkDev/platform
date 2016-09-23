using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace GTANetwork.Util
{
    public class Screenshot
    {
        public static void TakeScreenshot()
        {
            var t = new Thread((ThreadStart) delegate
            {
                var destinationFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                    Environment.SpecialFolderOption.Create) + "\\Rockstar Games\\GTA V\\GTA Network\\Screenshots";
                if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);

                var gta5Proc = Process.GetProcessesByName("GTA5")[0];
                var rect = new User32.Rect();
                User32.GetWindowRect(gta5Proc.MainWindowHandle, ref rect);

                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                Graphics graphics = Graphics.FromImage(bmp);
                try
                {
                    graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }
                finally
                {
                   graphics.Dispose();
                }

                var filename = "gtanetwork-" + (Directory.GetFiles(destinationFolder, "*.png").Count()+1).ToString("000") + ".png";

                bmp.Save(destinationFolder + Path.DirectorySeparatorChar + filename);

                Main.Chat.AddMessage(null, "~b~Screenshot saved as " + filename);
            });

            t.IsBackground = true;
            t.Start();
        }
    }

    public class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint nFlags);
    }
}