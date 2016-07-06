using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace GTANetwork
{
    public class Screenshot
    {
        public static void TakeScreenshot()
        {
            string destinationFolder = Main.GTANInstallDir + Path.DirectorySeparatorChar + "screenshots";
            if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);

            var gta5Proc = Process.GetProcessesByName("GTA5")[0];
            var rect = new User32.Rect();
            User32.GetWindowRect(gta5Proc.MainWindowHandle, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

            var filename = "GTANetworkScreenshot-" +
                           DateTime.Now.ToString("yyyy-MM-dd-HH-MM-ss.fff", CultureInfo.InvariantCulture) + ".png";

            bmp.Save(destinationFolder + Path.DirectorySeparatorChar + filename);

            Main.Chat.AddMessage(null, "~b~Screenshot saved as " + filename);
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
    }
}