using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTANetwork.GUI.DirectXHook.Hook;
using GTANetwork.GUI.DirectXHook.Hook.Common;
using GTANetwork.Javascript;
using GTANetwork.Util;
using Microsoft.ClearScript.V8;
using SharpDX;
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

                CEFManager.SetMouseHidden(!value);
            }
        }

        private static bool _justShownCursor;
        private static long _lastShownCursor = 0;
        public static PointF _lastMousePoint;
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
                if (ShowCursor)
                {
                    Game.DisableAllControlsThisFrame(0);
                    if (CefUtil.DISABLE_CEF)
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

                if (CEFManager._cursor != null)
                {
                    CEFManager._cursor.Location = new Point((int)mouseX, (int)mouseY);
                }


                var mouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorAccept);
                var mouseDownRN = Game.IsDisabledControlPressed(0, GTA.Control.CursorAccept);
                var mouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorAccept);

                var rmouseDown = Game.IsDisabledControlJustPressed(0, GTA.Control.CursorCancel);
                var rmouseDownRN = Game.IsDisabledControlPressed(0, GTA.Control.CursorCancel);
                var rmouseUp = Game.IsDisabledControlJustReleased(0, GTA.Control.CursorCancel);

                var wumouseDown = Game.IsDisabledControlPressed(0, GTA.Control.CursorScrollUp);
                var wdmouseDown = Game.IsDisabledControlPressed(0, GTA.Control.CursorScrollDown);

                if (!CefUtil.DISABLE_CEF)
                {
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
                }
            };

            KeyDown += (sender, args) =>
            {
                if (!ShowCursor) return;

                if (_justShownCursor && Util.Util.TickCount - _lastShownCursor < 500)
                {
                    _justShownCursor = false;
                    return;
                }

                if (!CefUtil.DISABLE_CEF)
                {
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
                        kEvent.WindowsKeyCode = (int)args.KeyCode;
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
                }
            };

            KeyUp += (sender, args) =>
            {
                if (!CefUtil.DISABLE_CEF)
                {
                    if (!ShowCursor) return;
                    foreach (var browser in CEFManager.Browsers)
                    {
                        if (!browser.IsInitialized()) continue;

                        CefKeyEvent kEvent = new CefKeyEvent();
                        kEvent.EventType = CefKeyEventType.KeyUp;
                        kEvent.WindowsKeyCode = (int)args.KeyCode;
                        browser._browser.GetHost().SendKeyEvent(kEvent);
                    }
                }
            };
        }

    }

    internal static class CEFManager
    {
        internal static void InitializeCef()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                var t = new Thread((ThreadStart)delegate
                {
                    try
                    {
                        //LogManager.CefLog("--> InitilizeCef: Start");
                        CefRuntime.Load(Main.GTANInstallDir + "\\cef");
                        //LogManager.CefLog("-> InitilizeCef: 1");

                        var args = new[]
                        {
                            "--off-screen-rendering-enabled",
                            "--transparent-painting-enabled",
                            "--disable-gpu",
                            "--disable-gpu-compositing",
                            "--disable-gpu-vsync",
                            "--enable-begin-frame-scheduling",
                            "--disable-d3d11",

                        };

                        var cefMainArgs = new CefMainArgs(args);
                        var cefApp = new MainCefApp();

                        if (CefRuntime.ExecuteProcess(cefMainArgs, cefApp, IntPtr.Zero) != -1) {
                            LogManager.CefLog("CefRuntime could not execute the secondary process.");
                        }

                        //LogManager.CefLog("-> InitilizeCef: 2");
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
                            IgnoreCertificateErrors = true,
                        };
                        if(Main.PlayerSettings.CEFDevtool) cefSettings.RemoteDebuggingPort = 9222;

                        CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);
                        //LogManager.CefLog("-> InitilizeCef: 3");

                        CefRuntime.RegisterSchemeHandlerFactory("http", null, new SecureSchemeFactory());
                        CefRuntime.RegisterSchemeHandlerFactory("https", null, new SecureSchemeFactory());
                        CefRuntime.RegisterSchemeHandlerFactory("ftp", null, new SecureSchemeFactory());
                        CefRuntime.RegisterSchemeHandlerFactory("sftp", null, new SecureSchemeFactory());
                        //LogManager.CefLog("--> InitilizeCef: End");


                    }
                    catch (Exception ex)
                    {
                        LogManager.CefLog(ex, "cef initialization");
                    }
                });

                t.SetApartmentState(ApartmentState.STA);
                t.Start();

            }
        }

        internal static void DisposeCef()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                CefRuntime.Shutdown();
            }
        }

        internal static void Dispose()
        {
            _cursor?.Dispose();
            _cursor = null;

            DirectXHook?.Dispose();
            DirectXHook = null;
        }

        internal static void SetMouseHidden(bool hidden)
        {
            if (DirectXHook == null) return;

            if (_cursor == null)
            {
                var cursorPic = new Bitmap(Main.GTANInstallDir + "images\\cef\\cursor.png");
                _cursor = new ImageElement(null, true);
                _cursor.SetBitmap(cursorPic);
                _cursor.Hidden = true;
                DirectXHook.AddImage(_cursor, 1);
            }

            _cursor.Hidden = hidden;
        }

        internal static void Initialize(Size screenSize)
        {

            //LogManager.CefLog("--> Initiatlize: Start");
            ScreenSize = screenSize;
            if (!CefUtil.DISABLE_CEF && DirectXHook == null)
            {
                Configuration.EnableObjectTracking = true;
                Configuration.EnableReleaseOnFinalizer = true;
                Configuration.EnableTrackingReleaseOnFinalizer = true;

                try
                {
                    LogManager.CefLog("--> Initiatlize: Creating device");
                    DirectXHook = new DXHookD3D11(screenSize.Width, screenSize.Height);
                    //DirectXHook.Hook();
                }
                catch (Exception ex)
                {
                    LogManager.CefLog(ex, "DIRECTX START");
                }

            }

            //RenderThread = new Thread(RenderLoop);
            //RenderThread.IsBackground = true;
            //RenderThread.Start();
            //LogManager.CefLog("--> Initiatlize: End");
        }

        internal static readonly List<Browser> Browsers = new List<Browser>();
        internal static int FPS = (int)Game.FPS;
        internal static Size ScreenSize;
        internal static ImageElement _cursor;

        internal static DXHookD3D11 DirectXHook;

        private static long _lastCefRender = 0;
        private static Bitmap _lastCefBitmap = null;
    }


    public class BrowserJavascriptCallback
    {
        private V8ScriptEngine _parent;
        private Browser _wrapper;
        public BrowserJavascriptCallback(V8ScriptEngine parent, Browser wrapper)
        {
            _parent = parent;
            if (!CefUtil.DISABLE_CEF)
            {
                _wrapper = wrapper;
            }
        }

        public BrowserJavascriptCallback() { }

        public object call(string functionName, params object[] arguments)
        {
            if (!CefUtil.DISABLE_CEF)
            {
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
                                    else if (arguments[i] is bool)
                                    {
                                        callString += arguments[i].ToString().ToLower() + comma;
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
            }
            else
            {
                return null;
            }
        }

        public object eval(string code)
        {
            if (!CefUtil.DISABLE_CEF)
            {
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
            }
            else
            {
                return null;
            }
        }
        public void addEventHandler(string eventName, Action<object[]> action)
        {
            if (!CefUtil.DISABLE_CEF)
            {
                if (!_wrapper._localMode) return;
                _eventHandlers.Add(new Tuple<string, Action<object[]>>(eventName, action));
            }
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
        internal MainCefClient _client;
        internal CefBrowser _browser;
        internal BrowserJavascriptCallback _callback;

        internal CefV8Context _mainContext;

        internal readonly bool _localMode;
        internal bool _hasFocused;


        private bool _headless = false;
        public bool Headless
        {
            get { return _headless; }
            set
            {
                if (!CefUtil.DISABLE_CEF)
                {
                    _client.SetHidden(value);
                }
                _headless = value;
            }
        }
        private Point _position;

        public Point Position
        {
            get { return _position; }
            set
            {
                _position = value;
                if (!CefUtil.DISABLE_CEF)
                {
                    _client.SetPosition(value.X, value.Y);
                }
            }
        }

        public PointF[] Pinned { get; set; }

        private Size _size;
        public Size Size
        {
            get { return _size; }
            set
            {
                //_browser.Size = value;
                if (!CefUtil.DISABLE_CEF)
                {
                    _client.SetSize(value.Width, value.Height);
                }
                _size = value;
            }
        }

        private V8ScriptEngine Father;

        public void eval(string code)
        {
            if (!_localMode) return;
            if (!CefUtil.DISABLE_CEF)
            {

                _browser.GetMainFrame().ExecuteJavaScript(code, null, 0);
            }
        }

        public void call(string method, params object[] arguments)
        {
            if (!_localMode) return;
            if (!CefUtil.DISABLE_CEF)
            {
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
            }
        }

        internal Browser(V8ScriptEngine father, Size browserSize, bool localMode)
        {
            Father = father;
            if (!CefUtil.DISABLE_CEF)
            {
                LogManager.CefLog("--> Browser: Start");
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
                    _browser = (CefBrowser)sender;
                    LogManager.CefLog("-> Browser created!");
                };

                Size = browserSize;
                _localMode = localMode;
                _callback = new BrowserJavascriptCallback(father, this);
                try
                {
                    LogManager.CefLog("--> Browser: Creating Browser");
                    CefBrowserHost.CreateBrowser(cefWindowinfo, _client, browserSettings);
                }
                catch (Exception e)
                {
                    LogManager.CefLog(e, "CreateBrowser");
                }
                LogManager.CefLog("--> Browser: End");
            }
        }

        internal void GoToPage(string page)
        {
            if (!CefUtil.DISABLE_CEF)
            {
                if (_browser != null)
                {
                    LogManager.CefLog("Trying to load page " + page + "...");
                    _browser.GetMainFrame().LoadUrl(page);
                }
            }
        }

        internal void GoBack()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                if (_browser != null && _browser.CanGoBack)
                {
                    LogManager.CefLog("Trying to go back a page...");
                    _browser.GoBack();
                }
            }
        }

        internal void Close()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                _client.Close();

                if (_browser == null) return;
                var host = _browser.GetHost();
                host.CloseBrowser(true);
                host.Dispose();
                _browser.Dispose();
            }
        }

        internal void LoadHtml(string html)
        {
            if (!CefUtil.DISABLE_CEF)
            {
                if (_browser == null) return;
                _browser.GetMainFrame().LoadString(html, "localhost");
            }
        }

        internal string GetAddress()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                if (_browser == null) return null;
                return _browser.GetMainFrame().Url;
            }
            else
            {
                return null;
            }
        }

        internal bool IsLoading()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                return _browser.IsLoading;
            }
            else
            {
                return false;
            }
        }

        internal bool IsInitialized()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                return _browser != null;
            }
            else
            {
                return true;
            }
        }

        internal Bitmap GetRawBitmap()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                //if (!_browser.IsBrowserInitialized) return null;

                //if (_browser.Size.Width != Size.Width && _browser.Size.Height != Size.Height)
                //_browser.Size = Size;

                //Bitmap output = _browser.ScreenshotOrNull();
                //_browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
                //return output;
                Bitmap lbmp = _client.GetLastBitmap();

                //LogManager.CefLog("Requesting bitmap. Null? " + (lbmp == null));
                return lbmp;
            }
            else
            {
                return null;
            }
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
            if (!CefUtil.DISABLE_CEF)
            {
                //_browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
            }

            return doubleBuffer;
        }

        public void Dispose()
        {
            if (!CefUtil.DISABLE_CEF)
            {
                _browser = null;
            }
        }
    }
}