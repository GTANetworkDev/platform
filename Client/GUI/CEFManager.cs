//#define DISABLE_HOOK
//#define DISABLE_CEF

#if true
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
//using CefSharp;
//using CefSharp.OffScreen;
using GTA;
using GTA.Native;
using GTANetwork.GUI.DirectXHook.Hook;
using GTANetwork.GUI.Extern;
using GTANetwork.Javascript;
using GTANetwork.Util;
using Microsoft.ClearScript.V8;
using SharpDX;
using SharpDX.Diagnostics;
using Xilium.CefGlue;
using Point = System.Drawing.Point;


namespace GTANetwork.GUI
{
    public class CefController : Script
    {
        private static bool _showCursor;

        public static bool ShowCursor
        {
            get { return _showCursor; }
            set
            {
                if (!_showCursor && value)
                {
                    _justShownCursor = true;
                    _lastShownCursor = Util.Util.TickCount;
                }
                _showCursor = value;
            }
        }

        private static bool _justShownCursor;
        private static long _lastShownCursor = 0;
        public static PointF _lastMousePoint;
        public static int GameFPS = 1;
        private Keys _lastKey;

        public static CefEventFlags GetMouseModifiers(bool leftbutton, bool rightButton)
        {
            CefEventFlags mod = CefEventFlags.None;

            if (leftbutton) mod |= CefEventFlags.LeftMouseButton;
            if (rightButton) mod |= CefEventFlags.RightMouseButton;

            return mod;
        }
        
        public CefController()
        {
            Tick += (sender, args) =>
            {
                GameFPS = (int)Game.FPS;
                
                if (ShowCursor)
                {
                    Game.DisableAllControlsThisFrame(0);
                    if (CEFManager.D3D11_DISABLED)
                        Function.Call(Hash._SHOW_CURSOR_THIS_FRAME);
                }
                else
                {
                    return;
                }
                
                var res = GTA.UI.Screen.Resolution;
                var mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorX) * res.Width;
                var mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorY) * res.Height;

                _lastMousePoint = new PointF(mouseX, mouseY);

                var mouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorAccept);
                var mouseDownRN = Game.IsDisabledControlPressed(0, GTA.Control.CursorAccept);
                var mouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorAccept);

                var rmouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorCancel);
                var rmouseDownRN = Game.IsDisabledControlPressed(0, GTA.Control.CursorCancel);
                var rmouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorCancel);

                var wumouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorScrollUp);
                var wumouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorScrollUp);

                var wdmouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorScrollDown);
                var wdmouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorScrollDown);

                #if !DISABLE_CEF
                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    if (!browser._hasFocused)
                    {
                        browser._browser.GetHost().SetFocus(true);

                        browser._browser.GetHost().SetFocus(true);
                        browser._browser.GetHost().SendFocusEvent(true);
                        browser._hasFocused = true;
                    }

                    if (mouseX > browser.Position.X && mouseY > browser.Position.Y &&
                        mouseX < browser.Position.X + browser.Size.Width &&
                        mouseY < browser.Position.Y + browser.Size.Height)
                    {
                        var ev = new CefMouseEvent((int)(mouseX - browser.Position.X), (int)(mouseY - browser.Position.Y),
                                GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        browser._browser
                            .GetHost()
                            .SendMouseMoveEvent(ev, false);

                        if (mouseDown)
                            browser._browser
                                .GetHost()
                                .SendMouseClickEvent(ev, CefMouseButtonType.Left, false, 1);

                        if (mouseUp)
                            browser._browser
                                .GetHost()
                                .SendMouseClickEvent(ev, CefMouseButtonType.Left, true, 1);

                        if (rmouseDown)
                            browser._browser
                                .GetHost()
                                .SendMouseClickEvent(ev, CefMouseButtonType.Right, false, 1);

                        if (rmouseUp)
                            browser._browser
                                .GetHost()
                                .SendMouseClickEvent(ev, CefMouseButtonType.Right, true, 1);

                        if (wdmouseDown)
                            browser._browser
                                .GetHost()
                                .SendMouseWheelEvent(ev, 0, -30);

                        if (wumouseDown)
                            browser._browser
                                .GetHost()
                                .SendMouseWheelEvent(ev, 0, 30);
                    }
                }

                #endif
            };

            KeyDown += (sender, args) =>
            {
                if (!ShowCursor) return;

                if (_justShownCursor && Util.Util.TickCount - _lastShownCursor < 500)
                {
                    _justShownCursor = false;
                    return;
                }

#if !DISABLE_CEF

                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    CefEventFlags mod = CefEventFlags.None;
                    if (args.Control) mod |= CefEventFlags.ControlDown;
                    if (args.Shift) mod |= CefEventFlags.ShiftDown;
                    if (args.Alt) mod |= CefEventFlags.AltDown;
                    
                    CefKeyEvent kEvent = new CefKeyEvent();
                    kEvent.EventType = CefKeyEventType.KeyDown;
                    kEvent.Modifiers = mod;
                    kEvent.WindowsKeyCode = (int) args.KeyCode;
                    kEvent.NativeKeyCode = (int)args.KeyValue;
                    browser._browser.GetHost().SendKeyEvent(kEvent);

                    CefKeyEvent charEvent = new CefKeyEvent();
                    charEvent.EventType = CefKeyEventType.Char;
                    
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

                    var keyChar = ClassicChat.GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), Game.IsKeyPressed(Keys.Menu) && Game.IsKeyPressed(Keys.ControlKey));

                    if (keyChar.Length == 0 || keyChar[0] == 27) return;
                    
                    charEvent.WindowsKeyCode = keyChar[0];
                    charEvent.Modifiers = mod;
                    browser._browser.GetHost().SendKeyEvent(charEvent);
                }

#endif
            };

            KeyUp += (sender, args) =>
            {
                #if !DISABLE_CEF
                if (!ShowCursor) return;
                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    CefKeyEvent kEvent = new CefKeyEvent();
                    kEvent.EventType = CefKeyEventType.KeyUp;
                    kEvent.WindowsKeyCode = (int)args.KeyCode;
                    browser._browser.GetHost().SendKeyEvent(kEvent);
                }
                #endif
            };
        }
        
    }

    internal static class CEFManager
    {
        #if DISABLE_HOOK
        public const bool D3D11_DISABLED = true;
        #else
        public const bool D3D11_DISABLED = false;
#endif


        internal static void InitializeCef()
        {
#if !DISABLE_CEF
            CefRuntime.Load(Main.GTANInstallDir + "\\cef");

            var args = new []
            {
                "--off-screen-rendering-enabled",
                "--transparent-painting-enabled",
            };

            var cefMainArgs = new CefMainArgs(args);
            var cefApp = new MainCefApp();
                
            if (CefRuntime.ExecuteProcess(cefMainArgs, cefApp, IntPtr.Zero) != -1)
            {
                LogManager.AlwaysDebugLog("CefRuntime could not execute the secondary process.");
            }

            var cefSettings = new CefSettings()
            {
                SingleProcess = true,
                MultiThreadedMessageLoop = true,
                WindowlessRenderingEnabled = true,
                BackgroundColor = new CefColor(0, 0, 0, 0),
                    
                CachePath = Main.GTANInstallDir + "\\cef",
                ResourcesDirPath = Main.GTANInstallDir + "\\cef",
                LocalesDirPath = Main.GTANInstallDir + "\\cef\\locales",
                BrowserSubprocessPath = Main.GTANInstallDir + "\\cef",
                    
                //NoSandbox = true,
            };

            CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);

            CefRuntime.RegisterSchemeHandlerFactory("http", null, new SecureSchemeFactory());
            CefRuntime.RegisterSchemeHandlerFactory("https", null, new SecureSchemeFactory());
#endif
        }

        internal static void DisposeCef()
        {
#if !DISABLE_CEF
            CefRuntime.Shutdown();
#endif
        }

        internal static void Initialize(Size screenSize)
        {
            ScreenSize = screenSize;
#if !DISABLE_HOOK
            SharpDX.Configuration.EnableObjectTracking = true;
            Configuration.EnableReleaseOnFinalizer = true;
            Configuration.EnableTrackingReleaseOnFinalizer = true;
            StopRender = false;
            Disposed = false;

            try
            {
                DirectXHook = new DXHookD3D11(screenSize.Width, screenSize.Height);
                //DirectXHook.Hook();
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "DIRECTX START");
            }
#endif

            RenderThread = new Thread(RenderLoop);
            RenderThread.IsBackground = true;
            RenderThread.Start();
        }

        internal static readonly List<Browser> Browsers = new List<Browser>();
        internal static int FPS = 30;
        internal static Thread RenderThread;
        internal static bool StopRender;
        internal static Size ScreenSize;
        internal static bool Disposed = true;

        internal static DXHookD3D11 DirectXHook;

        private static long _lastCefRender = 0;
        private static Bitmap _lastCefBitmap = null;

        internal static void RenderLoop()
        {
            Application.ThreadException += ApplicationOnThreadException;
            AppDomain.CurrentDomain.UnhandledException += AppDomainException;
            
            LogManager.AlwaysDebugLog("STARTING MAIN LOOP");


            var cursor = new Bitmap(Main.GTANInstallDir + "\\images\\cef\\cursor.png");

#if !DISABLE_HOOK
            SharpDX.Configuration.EnableObjectTracking = true;

            while (!StopRender)
            {
                try
                {
                    using (
                        Bitmap doubleBuffer = new Bitmap(ScreenSize.Width, ScreenSize.Height,
                            PixelFormat.Format32bppArgb))
                    {
                        if (!Main.MainMenu.Visible)
                            using (var graphics = Graphics.FromImage(doubleBuffer))
                            {
#if !DISABLE_CEF
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                                lock (Browsers)
                                    foreach (var browser in Browsers)
                                    {
                                        if (browser.Headless) continue;
                                        
                                        var bitmap = browser.GetRawBitmap();

                                        if (bitmap == null) continue;

                                        if (browser.Pinned == null || browser.Pinned.Length != 4)
                                        {
                                            graphics.DrawImageUnscaled(bitmap, browser.Position);
                                        }
                                        else
                                        {
                                            var bmOut = new FastBitmap(doubleBuffer);
                                            var ourText = new FastBitmap(bitmap);

                                            QuadDistort.DrawBitmap(ourText,
                                                browser.Pinned[0].Floor(),
                                                browser.Pinned[1].Floor(),
                                                browser.Pinned[2].Floor(),
                                                browser.Pinned[3].Floor(),
                                                bmOut);

                                            graphics.DrawImageUnscaled(bmOut, 0, 0);
                                        }

                                        bitmap.Dispose();
                                    }
#endif
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
            {
                foreach (var browser in CEFManager.Browsers)
                {
                    browser.Dispose();
                }

                Browsers.Clear();
            }
            
            try
            {
                DirectXHook.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "DIRECTX DISPOSAL");
            }
#endif

            Application.ThreadException -= ApplicationOnThreadException;
            AppDomain.CurrentDomain.UnhandledException -= AppDomainException;

            Disposed = true;
        }

        private static void ApplicationOnThreadException(object sender, ThreadExceptionEventArgs threadExceptionEventArgs)
        {
            LogManager.LogException(threadExceptionEventArgs.Exception, "APPTHREAD");
        }

        private static void AppDomainException(object sender, UnhandledExceptionEventArgs threadExceptionEventArgs)
        {
            LogManager.LogException(threadExceptionEventArgs.ExceptionObject as Exception, "APPTHREAD");
        }
    }
    

    public class BrowserJavascriptCallback
    {
        private V8ScriptEngine _parent;
#if !DISABLE_CEF
        private Browser _wrapper;
#endif
        public BrowserJavascriptCallback(V8ScriptEngine parent, Browser wrapper)
        {
            _parent = parent;
#if !DISABLE_CEF
            _wrapper = wrapper;
#endif
        }

        public BrowserJavascriptCallback() { }

        public object call(string functionName, params object[] arguments)
        {
#if !DISABLE_CEF
            if (!_wrapper._localMode) return null;

            object objToReturn = null;
            bool hasValue = false;

            lock (JavascriptHook.ThreadJumper)
            JavascriptHook.ThreadJumper.Add(() =>
            {
                try
                {
                    string callString = functionName + "(";

                    if (arguments != null)
                        for (int i = 0; i < arguments.Length; i++)
                        {
                            string comma = ", ";

                            if (i == arguments.Length - 1)
                                comma = "";

                            if (arguments[i] is string)
                            {
                                callString += System.Web.HttpUtility.JavaScriptStringEncode(arguments[i].ToString(), true) + comma;
                            }
                            else
                            {
                                callString += arguments[i] + comma;
                            }
                        }

                    callString += ");";

                    objToReturn = _parent.Evaluate(callString);
                }
                finally
                {
                    hasValue = true;
                }
            });

            while (!hasValue) Thread.Sleep(10);

            return objToReturn;
#else
            return null;
#endif
        }

        public object eval(string code)
        {
#if !DISABLE_CEF
            if (!_wrapper._localMode) return null;
            // TODO: reinstate

            object objToReturn = null;
            bool hasValue = false;

            lock (JavascriptHook.ThreadJumper)
            JavascriptHook.ThreadJumper.Add(() =>
            {
                try
                {
                    objToReturn = _parent.Evaluate(code);
                }
                finally
                {
                    hasValue = true;
                }
            });

            while (!hasValue) Thread.Sleep(10);

            return objToReturn;
#else
            return null;
#endif
        }

        public void addEventHandler(string eventName, Action<object[]> action)
        {
#if !DISABLE_CEF
            if (!_wrapper._localMode) return;
            _eventHandlers.Add(new Tuple<string, Action<object[]>>(eventName, action));
#endif
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
#if !DISABLE_CEF
        internal MainCefClient _client;
        internal CefBrowser _browser;
        internal BrowserJavascriptCallback _callback;

        internal CefV8Context _mainContext;
#endif
        internal readonly bool _localMode;
        internal bool _hasFocused;
        
        public bool Headless = false;

        public Point Position { get; set; }

        public PointF[] Pinned { get; set; }
        
        private Size _size;
        public Size Size
        {
            get { return _size; }
            set
            {
                //_browser.Size = value;
                #if !DISABLE_CEF
                _client.SetSize(value.Width, value.Height);
                #endif
                _size = value;
            }
        }

        private V8ScriptEngine Father;
        
        public void eval(string code)
        {
            if (!_localMode) return;
#if !DISABLE_CEF

            _browser.GetMainFrame().ExecuteJavaScript(code, null, 0);
#endif
        }

        public void call(string method, params object[] arguments)
        {
            if (!_localMode) return;
#if !DISABLE_CEF
            string callString = method + "(";

            if (arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    string comma = ", ";

                    if (i == arguments.Length - 1)
                        comma = "";

                    if (arguments[i] is string)
                    {
                        var escaped = System.Web.HttpUtility.JavaScriptStringEncode(arguments[i].ToString(), true);
                        callString += escaped + comma;
                    }
                    else if (arguments[i] is bool)
                    {
                        callString += arguments[i].ToString().ToLower() + comma;
                    }
                    else
                    {
                        callString += arguments[i] + comma;
                    }
                }
            }
            callString += ");";
            
            _browser.GetMainFrame().ExecuteJavaScript(callString, null, 0);
#endif
        }

        internal Browser(V8ScriptEngine father, Size browserSize, bool localMode)
        {
            Father = father;
#if !DISABLE_CEF

            CefWindowInfo cefWindowinfo = CefWindowInfo.Create();
            cefWindowinfo.SetAsWindowless(IntPtr.Zero, true);
            cefWindowinfo.TransparentPaintingEnabled = true;
            cefWindowinfo.WindowlessRenderingEnabled = true;
            
            
            var browserSettings = new CefBrowserSettings()
            {
                JavaScriptCloseWindows = CefState.Disabled,
                JavaScriptOpenWindows = CefState.Disabled,
                WindowlessFrameRate = CEFManager.FPS,
                FileAccessFromFileUrls = CefState.Disabled,
            };

            _client = new MainCefClient(browserSize.Width, browserSize.Height);
            
            _client.OnCreated += (sender, args) =>
            {
                _browser = (CefBrowser) sender;
                LogManager.AlwaysDebugLog("Browser ready!");
            };

            Size = browserSize;
            _localMode = localMode;
            _callback = new BrowserJavascriptCallback(father, this);
            CefBrowserHost.CreateBrowser(cefWindowinfo, _client, browserSettings);
#endif
        }
        
        internal void GoToPage(string page)
        {
#if !DISABLE_CEF
            if (_browser != null)
            {
                LogManager.AlwaysDebugLog("Trying to load page " + page + "...");
                _browser.GetMainFrame().LoadUrl(page);
            }
#endif
        }

        internal void Close()
        {
#if !DISABLE_CEF
            if (_browser == null) return;
            var host = _browser.GetHost();
            host.CloseBrowser(true);
            host.Dispose();
            _browser.Dispose();
#endif
        }

        internal void LoadHtml(string html)
        {
#if !DISABLE_CEF
            if (_browser == null) return;
            _browser.GetMainFrame().LoadString(html, "localhost");
#endif            
        }

        internal string GetAddress()
        {
#if !DISABLE_CEF
            if (_browser == null) return null;
            return _browser.GetMainFrame().Url;
#else
            return null;
#endif
        }

        internal bool IsLoading()
        {
#if !DISABLE_CEF
            return _browser.IsLoading;
#else
            return false;
#endif
        }

        internal bool IsInitialized()
        {
#if !DISABLE_CEF
            return _browser != null;
#else
            return true;
#endif
        }

        internal Bitmap GetRawBitmap()
        {
#if !DISABLE_CEF
            //if (!_browser.IsBrowserInitialized) return null;

            //if (_browser.Size.Width != Size.Width && _browser.Size.Height != Size.Height)
                //_browser.Size = Size;

            //Bitmap output = _browser.ScreenshotOrNull();
            //_browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
            //return output;
            Bitmap lbmp = _client.GetLastBitmap();

            //LogManager.AlwaysDebugLog("Requesting bitmap. Null? " + (lbmp == null));
            return lbmp;
#else
            return null;
#endif
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
#if !DISABLE_CEF
            //_browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
#endif

            return doubleBuffer;
        }

        public void Dispose()
        {
#if !DISABLE_CEF
            _browser = null;
#endif
        }
    }
    //*/
}
#endif