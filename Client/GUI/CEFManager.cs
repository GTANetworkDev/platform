#if true
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;
using GTA;
using GTA.Native;
using GTANetwork.GUI.DirectXHook.Hook;
using NativeUI;
using SharpDX;
using SharpDX.Diagnostics;
using Point = System.Drawing.Point;


namespace GTANetwork.GUI
{
    public class CefController : Script
    {
        public static PointF _lastMousePoint;

        public CefController()
        {
            Tick += (sender, args) =>
            {
                if (Game.IsKeyPressed(Keys.F11)) CEFManager.StopRender = true;
                if (Game.IsKeyPressed(Keys.F12))
                {
                    LogManager.SimpleLog("directx", ObjectTracker.ReportActiveObjects());
                }
                
                var res = Game.ScreenResolution;
                var mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorX) * res.Width;
                var mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorY) * res.Height;

                _lastMousePoint = new PointF(mouseX, mouseY);

                var mouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorAccept);
                var mouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorAccept);

                var rmouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorCancel);
                var rmouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorCancel);

                var wumouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorScrollUp);
                var wumouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorScrollUp);

                var wdmouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorScrollDown);
                var wdmouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorScrollDown);

                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    if (mouseX > browser.Position.X && mouseY > browser.Position.Y &&
                        mouseX < browser.Position.X + browser.Size.Width &&
                        mouseY < browser.Position.Y + browser.Size.Height)
                    {
                        browser._browser.GetBrowser().GetHost().SetFocus(true);
                        browser._browser.GetBrowser()
                            .GetHost()
                            .SendMouseMoveEvent((int)(mouseX - browser.Position.X), (int)(mouseY - browser.Position.Y),
                                false, CefEventFlags.None);

                        if (mouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Left, false, 1, CefEventFlags.None);

                        if (mouseUp)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Left, true, 1, CefEventFlags.None);

                        if (rmouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Right, false, 1, CefEventFlags.None);

                        if (rmouseUp)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Right, true, 1, CefEventFlags.None);

                        if (wdmouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseWheelEvent((int) (mouseX - browser.Position.X),
                                    (int) (mouseY - browser.Position.Y), 0, -30, CefEventFlags.None);

                        if (wumouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseWheelEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), 0, 30, CefEventFlags.None);
                    }
                    else
                    {
                        browser._browser.GetBrowser().GetHost().SetFocus(false);
                    }
                }
            };

            KeyDown += (sender, args) =>
            {
                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    CefEventFlags mod = CefEventFlags.None;
                    if (args.Control) mod |= CefEventFlags.ControlDown;
                    if (args.Shift) mod |= CefEventFlags.ShiftDown;
                    if (args.Alt) mod |= CefEventFlags.AltDown;
                    
                    KeyEvent kEvent = new KeyEvent();
                    kEvent.Type = KeyEventType.KeyDown;
                    kEvent.Modifiers = mod;
                    kEvent.WindowsKeyCode = (int) args.KeyCode;
                    kEvent.NativeKeyCode = (int)args.KeyValue;
                    browser._browser.GetBrowser().GetHost().SendKeyEvent(kEvent);

                    KeyEvent charEvent = new KeyEvent();
                    charEvent.Type = KeyEventType.Char;
                    charEvent.WindowsKeyCode = (int)args.KeyCode;
                    charEvent.Modifiers = mod;
                    charEvent.NativeKeyCode = (int) args.KeyValue;
                    browser._browser.GetBrowser().GetHost().SendKeyEvent(charEvent);
                }
            };

            KeyUp += (sender, args) =>
            {
                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    KeyEvent kEvent = new KeyEvent();
                    kEvent.Type = KeyEventType.KeyUp;
                    kEvent.WindowsKeyCode = (int)args.KeyCode;
                    browser._browser.GetBrowser().GetHost().SendKeyEvent(kEvent);
                }
            };
        }
    }


    public static class CEFManager
    {
        public static void Initialize(Size screenSize)
        {
            ScreenSize = screenSize;
            SharpDX.Configuration.EnableObjectTracking = true;
            Configuration.EnableReleaseOnFinalizer = true;
            Configuration.EnableTrackingReleaseOnFinalizer = true;
            
            DirectXHook = new DXHookD3D11(screenSize.Width, screenSize.Height);
            DirectXHook.Hook();

            RenderThread = new Thread(RenderLoop);
            RenderThread.IsBackground = true;
            RenderThread.Start();
        }

        public static List<Browser> Browsers = new List<Browser>();
        public static int FPS = 15;
        public static Thread RenderThread;
        public static bool StopRender;
        public static Size ScreenSize;

        internal static DXHookD3D11 DirectXHook;
        

        public static void RenderLoop()
        {
            var settings = new CefSharp.CefSettings();
            settings.SetOffScreenRenderingBestPerformanceArgs();
            LogManager.DebugLog("WAITING FOR INITIALIZATION...");
            if (!Cef.IsInitialized)
                Cef.Initialize(settings);
            LogManager.DebugLog("STARTING MAIN LOOP");

            SharpDX.Configuration.EnableObjectTracking = true;

            var cursor = new Bitmap(Main.GTANInstallDir + "\\images\\cef\\cursor.png");

            while (!StopRender)
            {
                try
                {
                    using (
                        Bitmap doubleBuffer = new Bitmap(ScreenSize.Width, ScreenSize.Height,
                            PixelFormat.Format32bppArgb))
                    {

                        using (var graphics = Graphics.FromImage(doubleBuffer))
                        {
                            lock (Browsers)
                                foreach (var browser in Browsers)
                                {
                                    if (browser.Headless) continue;
                                    var bitmap = browser.GetRawBitmap();

                                    if (bitmap == null) continue;

                                    graphics.DrawImage(bitmap, browser.Position);
                                    bitmap.Dispose();
                                }

                            if (Browsers.Count > 0)
                                graphics.DrawImage(cursor, CefController._lastMousePoint);
                        }

                        DirectXHook.SetBitmap(doubleBuffer);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "DIRECTX HOOK");
                }
                finally
                {
                    Thread.Sleep(1000 / FPS);
                }
            }

            cursor.Dispose();

            lock (Browsers)
            foreach (var browser in Browsers)
            {
                browser.Dispose();
            }

            DirectXHook.Dispose();
            //Cef.Shutdown();
        }
    }

    public class Browser : IDisposable
    {
        internal ChromiumWebBrowser _browser;

        public bool Headless = false;

        public Point Position { get; set; }

        private Size _size;
        public Size Size
        {
            get { return _size; }
            set
            {
                _size = value;
                //_browser.Size = value;
            }
        }

        internal Browser(Size browserSize)
        {
            var settings = new BrowserSettings();
            settings.LocalStorage = CefState.Disabled;
            settings.OffScreenTransparentBackground = true;

            _browser = new ChromiumWebBrowser(browserSettings: settings);
            Size = browserSize;
        }

        internal Browser(string uri, Size browserSize)
        {
            var settings = new BrowserSettings();
            settings.LocalStorage = CefState.Disabled;
            settings.OffScreenTransparentBackground = true;

            _browser = new ChromiumWebBrowser(uri, browserSettings: settings);
            Size = browserSize;
        }

        internal void GoToPage(string page)
        {
            //if (!_browser.IsBrowserInitialized) Thread.Sleep(0);
            _browser.Load(page);
        }

        internal string GetAddress()
        {
            if (!_browser.IsBrowserInitialized) Thread.Sleep(0);
            return _browser.Address;
        }

        internal bool IsLoading()
        {
            if (!_browser.IsBrowserInitialized) Thread.Sleep(0);
            return _browser.IsLoading;
        }

        internal bool IsInitialized()
        {
            return _browser.IsBrowserInitialized;
        }

        internal Bitmap GetRawBitmap()
        {
            if (!_browser.IsBrowserInitialized) return null;

            if (_browser.Size.Width != Size.Width && _browser.Size.Height != Size.Height)
                _browser.Size = Size;

            Bitmap output = _browser.ScreenshotOrNull();
            _browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
            return output;
        }

        internal Bitmap GetBitmap()
        {
            var bmp = GetRawBitmap();

            if (bmp == null) return null;
            
            Bitmap doubleBuffer = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(doubleBuffer))
            {
                graphics.DrawImage(bmp, new Point(0, 0));
            }

            _browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));

            return doubleBuffer;
        }

        public void Dispose()
        {
            _browser?.Dispose();
            _browser = null;
        }
    }
    //*/
}
#endif