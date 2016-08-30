//#define DISABLE_HOOK
//#define DISABLE_CEF

#if true
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;
using GTA;
using GTA.Native;
using GTANetwork.GUI.DirectXHook.Hook;
using Microsoft.ClearScript.V8;
using SharpDX;
using SharpDX.Diagnostics;
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
                    _justShownCursor = true;
                _showCursor = value;
            }
        }

        private static bool _justShownCursor;
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
                if (Game.IsKeyPressed(Keys.F11)) CEFManager.StopRender = true;
                if (Game.IsKeyPressed(Keys.F12))
                {
                    LogManager.SimpleLog("directx", ObjectTracker.ReportActiveObjects());
                }

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
                
                var res = Game.ScreenResolution;
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
                        browser._browser.GetBrowser().GetHost().SetFocus(true);
                        browser._browser.GetBrowser().GetHost().SendFocusEvent(true);
                        browser._hasFocused = true;
                    }

                    if (mouseX > browser.Position.X && mouseY > browser.Position.Y &&
                        mouseX < browser.Position.X + browser.Size.Width &&
                        mouseY < browser.Position.Y + browser.Size.Height)
                    {
                        browser._browser.GetBrowser()
                            .GetHost()
                            .SendMouseMoveEvent((int)(mouseX - browser.Position.X), (int)(mouseY - browser.Position.Y),
                                false, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (mouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Left, false, 1, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (mouseUp)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Left, true, 1, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (rmouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Right, false, 1, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (rmouseUp)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseClickEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), MouseButtonType.Right, true, 1, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (wdmouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseWheelEvent((int) (mouseX - browser.Position.X),
                                    (int) (mouseY - browser.Position.Y), 0, -30, GetMouseModifiers(mouseDownRN, rmouseDownRN));

                        if (wumouseDown)
                            browser._browser.GetBrowser()
                                .GetHost()
                                .SendMouseWheelEvent((int)(mouseX - browser.Position.X),
                                    (int)(mouseY - browser.Position.Y), 0, 30, GetMouseModifiers(mouseDownRN, rmouseDownRN));
                    }
                }

                #endif
            };

            KeyDown += (sender, args) =>
            {
                if (!ShowCursor) return;

                if (_justShownCursor)
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
                        //charEvent.WindowsKeyCode = (int) args.KeyCode;
                        charEvent.WindowsKeyCode = 8;
                        charEvent.Modifiers = mod;
                        browser._browser.GetBrowser().GetHost().SendKeyEvent(charEvent);
                        return;
                    }
                    if (keyChar[0] == (char)13)
                    {
                        //charEvent.WindowsKeyCode = (int)args.KeyCode;
                        charEvent.WindowsKeyCode = 13;
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

                #endif
            };

            KeyUp += (sender, args) =>
            {
                #if !DISABLE_CEF
                if (!ShowCursor) return;
                foreach (var browser in CEFManager.Browsers)
                {
                    if (!browser.IsInitialized()) continue;

                    KeyEvent kEvent = new KeyEvent();
                    kEvent.Type = KeyEventType.KeyUp;
                    kEvent.WindowsKeyCode = (int)args.KeyCode;
                    browser._browser.GetBrowser().GetHost().SendKeyEvent(kEvent);
                }
                #endif
            };
        }
    }
    
    public static class CEFManager
    {
        #if DISABLE_HOOK
        public const bool D3D11_DISABLED = true;
        #else
        public const bool D3D11_DISABLED = false;
        #endif


        public static void InitializeCef()
        {
#if !DISABLE_CEF
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

            settings.CefCommandLineArgs.Add("--off-screen-frame-rate", "60");

            LogManager.DebugLog("WAITING FOR INITIALIZATION...");
            try
            {
                Cef.Initialize(settings);
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "CEF INIT");
            }
#endif
        }

        public static void DisposeCef()
        {
#if !DISABLE_CEF
            Cef.Shutdown();
#endif
        }

        public static void Initialize(Size screenSize)
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
                DirectXHook.Hook();
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

        public static List<Browser> Browsers = new List<Browser>();
        public static int FPS = 30;
        public static Thread RenderThread;
        public static bool StopRender;
        public static Size ScreenSize;
        public static bool Disposed = true;

        internal static DXHookD3D11 DirectXHook;
        
        public static void RenderLoop()
        {
            Application.ThreadException += ApplicationOnThreadException;
            AppDomain.CurrentDomain.UnhandledException += AppDomainException;
            
            LogManager.DebugLog("STARTING MAIN LOOP");


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
                                lock (Browsers)
                                    foreach (var browser in Browsers)
                                    {
                                        if (browser.Headless) continue;
                                        var bitmap = browser.GetRawBitmap();

                                        if (bitmap == null) continue;

                                        graphics.DrawImage(bitmap, browser.Position);
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
    
    public class ResourceFilePathHandler : ISchemeHandlerFactory
    {
        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            var browserWrapper = CEFManager.Browsers.FirstOrDefault(b => b._browser?.GetBrowser().Identifier == browser.Identifier);
            
            if (browserWrapper == null || browserWrapper._localMode)
            {
                var uri = new Uri(request.Url);
                var path = Main.GTANInstallDir + "\\resources\\";

                var requestedFile = path + uri.Host + uri.LocalPath;

                if (!File.Exists(requestedFile))
                {
                    browser.StopLoad();
                    return ResourceHandler.FromString(@"<!DOCTYPE html><html><body><h1>404</h1></body></html>", ".html");
                }

                return ResourceHandler.FromFileName(requestedFile, Path.GetExtension(requestedFile));
            }

            return null;
        }
    }

    public class GoBackForwardCanceller : IRequestHandler
    {
        bool IRequestHandler.OnResourceResponse(IWebBrowser browser, IBrowser b, IFrame frame, IRequest req, IResponse resp)
        {
            return false;
        }

        void IRequestHandler.OnResourceLoadComplete(IWebBrowser browser, IBrowser b, IFrame frame, IRequest req,
            IResponse resp, UrlRequestStatus status, long unk)
        {
        }

        bool IRequestHandler.OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect)
        {
            if ((request.TransitionType & TransitionType.ForwardBack) != 0)
            {
                return true;
            }

            return false;
        }
        bool IRequestHandler.OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
        {
            return false;
        }
        bool IRequestHandler.OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
        {
            return false;
        }
        void IRequestHandler.OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath)
        {
        }
        CefReturnValue IRequestHandler.OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            return CefReturnValue.Continue;
        }

        bool IRequestHandler.GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        {
            //NOTE: If you do not wish to implement this method returning false is the default behaviour
            // We also suggest you explicitly Dispose of the callback as it wraps an unmanaged resource.

            callback.Dispose();
            return false;
        }

        void IRequestHandler.OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status)
        {
        }

        bool IRequestHandler.OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
        {
            return false;
        }

        void IRequestHandler.OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, ref string newUrl)
        {
        }

        bool IRequestHandler.OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url)
        {
            return false;
        }

        void IRequestHandler.OnRenderViewReady(IWebBrowser browserControl, IBrowser browser)
        {

        }
    }

    public class BrowserJavascriptCallback
    {
        private V8ScriptEngine _parent;
        private Browser _wrapper;

        public BrowserJavascriptCallback(V8ScriptEngine parent, Browser wrapper)
        {
            _parent = parent;
            _wrapper = wrapper;
        }

        public BrowserJavascriptCallback() { }

        public object call(string functionName, params object[] arguments)
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
        }

        public object eval(string code)
        {
            if (!_wrapper._localMode) return null;

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

        public void addEventHandler(string eventName, Action<object[]> action)
        {
            if (!_wrapper._localMode) return;
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
        internal readonly bool _localMode;
        internal bool _hasFocused;
        
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
            if (!_localMode) return null;

            var task = _browser.EvaluateScriptAsync(code);

            task.Wait();

            if (task.Result.Success)
                return task.Result.Result;
            LogManager.LogException(new Exception(task.Result.Message), "CLIENTSIDESCRIPT -> CEF COMMUNICATION");
            return null;
        }

        public object call(string method, params object[] arguments)
        {
            if (!_localMode) return null;

            string callString = method + "(";

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

            callString += ");";

            var task = _browser.EvaluateScriptAsync(callString);

            task.Wait();

            if (task.Result.Success)
                return task.Result.Result;

            LogManager.LogException(new Exception(task.Result.Message), "CLIENTSIDESCRIPT -> CEF COMMUNICATION");
            return null;
        }

        internal Browser(V8ScriptEngine father, Size browserSize, bool localMode)
        {
            Father = father;

            var settings = new BrowserSettings();
            settings.LocalStorage = CefState.Disabled;
            settings.OffScreenTransparentBackground = true;
            settings.JavascriptAccessClipboard = CefState.Disabled;
            settings.JavascriptOpenWindows = CefState.Disabled;
            
            _browser = new ChromiumWebBrowser(browserSettings: settings);
            _browser.RegisterJsObject("resource", new BrowserJavascriptCallback(father, this), false);
            //_browser.RequestHandler = new GoBackForwardCanceller(); // disabled for now, giving problems
            Size = browserSize;

            _localMode = localMode;
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
            _browser = null;
        }
    }
    //*/
}
#endif