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
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript.Windows;
using NativeUI;
using SharpDX;
using SharpDX.Diagnostics;
using Point = System.Drawing.Point;


namespace GTANetwork.GUI
{
    public class CefController : Script
    {
        public static bool ShowCursor;
        public static PointF _lastMousePoint;
        private Keys _lastKey;
        public CefController()
        {
            Tick += (sender, args) =>
            {
                if (Game.IsKeyPressed(Keys.F11)) CEFManager.StopRender = true;
                if (Game.IsKeyPressed(Keys.F12))
                {
                    LogManager.SimpleLog("directx", ObjectTracker.ReportActiveObjects());
                }

                if (ShowCursor)
                {
                    Game.DisableAllControlsThisFrame(0);
                }
                else
                {
                    return;
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
                if (!ShowCursor) return;
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
                    
                    var key = args.KeyCode;

                    if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) ||
                        (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                    {
                        ClassicChat.ActivateKeyboardLayout(1, 0);
                        return;
                    }

                    _lastKey = key;

                    if (key == Keys.Escape)
                    {
                        return;
                    }

                    var keyChar = ClassicChat.GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), false);

                    if (keyChar.Length == 0) return;

                    if (keyChar[0] == (char)8)
                    {
                        charEvent.WindowsKeyCode = (int) args.KeyCode;
                        charEvent.Modifiers = mod;
                        browser._browser.GetBrowser().GetHost().SendKeyEvent(charEvent);
                        return;
                    }
                    if (keyChar[0] == (char)13)
                    {
                        charEvent.WindowsKeyCode = (int)args.KeyCode;
                        charEvent.Modifiers = mod;
                        browser._browser.GetBrowser().GetHost().SendKeyEvent(charEvent);
                        return;
                    }
                    else if (keyChar[0] == 27)
                    {
                        return;
                    }

                    charEvent.WindowsKeyCode = keyChar[0];
                    charEvent.Modifiers = mod;
                    browser._browser.GetBrowser().GetHost().SendKeyEvent(charEvent);
                }
            };

            KeyUp += (sender, args) =>
            {
                if (!ShowCursor) return;
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
            
            settings.RegisterScheme(new CefCustomScheme()
            {
                SchemeHandlerFactory = new ResourceFilePathHandler(),
                SchemeName = "http",
            });

            settings.RegisterScheme(new CefCustomScheme()
            {
                SchemeHandlerFactory = new ResourceFilePathHandler(),
                SchemeName = "https",
            });

            settings.RegisterScheme(new CefCustomScheme()
            {
                SchemeHandlerFactory = new ResourceFilePathHandler(),
                SchemeName = "resource",
            });

            LogManager.DebugLog("WAITING FOR INITIALIZATION...");
            if (!Cef.IsInitialized)
                Cef.Initialize(settings);
            LogManager.DebugLog("STARTING MAIN LOOP");

            SharpDX.Configuration.EnableObjectTracking = true;

            var cursor = new Bitmap(Main.GTANInstallDir + "\\images\\cef\\cursor.png");

            while (!StopRender)
            {
                if (Main.MainMenu.Visible) continue;

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

                            if (CefController.ShowCursor)
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

    public class ResourceFilePathHandler : ISchemeHandlerFactory
    {
        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            var uri = new Uri(request.Url);
            var path = Main.GTANInstallDir + "\\resources\\";

            var requestedFile = path + uri.Host + uri.LocalPath;

            if (!File.Exists(requestedFile)) return null;
            return ResourceHandler.FromFileName(requestedFile, Path.GetExtension(requestedFile));
        }
    }

    public class BrowserJavascriptCallback
    {
        private V8ScriptEngine _parent;

        public BrowserJavascriptCallback(V8ScriptEngine parent)
        {
            _parent = parent;
        }

        public BrowserJavascriptCallback() { }

        public object call(string functionName, params object[] arguments)
        {
            var method = ((object) _parent.Script).GetType().GetMethod(functionName);

            if (method != null)
            {
                return method.Invoke(_parent.Script, arguments);
            }

            return null;
        }

        public object eval(string code)
        {
            return _parent.Evaluate(code);
        }

        public void addEventHandler(string eventName, Action<object[]> action)
        {
            _eventHandlers.Add(new Tuple<string, Action<object[]>>(eventName, action));
        }

        internal void TriggerEvent(string eventName, params object[] arguments)
        {
            foreach (var handler in _eventHandlers)
            {
                if (handler.Item1 == eventName)
                    handler.Item2.Invoke(arguments);
            }
        }

        private List<Tuple<string, Action<object[]>>> _eventHandlers = new List<Tuple<string, Action<object[]>>>();
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

        private V8ScriptEngine Father;

        public object eval(string code)
        {
            var task = _browser.EvaluateScriptAsync(code);

            task.RunSynchronously();

            if (task.Result.Success)
                return task.Result.Result;
            return null;
        }

        public object call(string method, params object[] arguments)
        {
            //string callString = string.Format("{0}({1});", method, arguments.Select(a => a.ToString()).Aggregate((prev, next) => prev + ", " + next));
            string callString = method + "(";

            for (int i = 0; i < arguments.Length; i++)
            {
                string comma = ", ";

                if (i == arguments.Length - 1)
                    comma = "";

                if (arguments[i] is string)
                {
                    callString += "\"" + arguments[i] + "\"" + comma;
                }
                else
                {
                    callString += arguments[i] + comma;
                }
            }

            callString += ");";

            var task = _browser.EvaluateScriptAsync(callString);

            task.RunSynchronously();

            if (task.Result.Success)
                return task.Result.Result;
            return null;
        }

        internal Browser(V8ScriptEngine father, Size browserSize)
        {
            Father = father;

            var settings = new BrowserSettings();
            settings.LocalStorage = CefState.Disabled;
            settings.OffScreenTransparentBackground = true;
            
            _browser = new ChromiumWebBrowser(browserSettings: settings);
            _browser.RegisterJsObject("resource", new BrowserJavascriptCallback(father), false);
            Size = browserSize;
        }

        internal Browser(V8ScriptEngine father, string uri, Size browserSize)
        {
            Father = father;

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