using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using GTANetwork.GUI.DirectXHook.Hook.Common;
using GTANetwork.Util;
using GTANetworkShared;
using Xilium.CefGlue;

namespace GTANetwork.GUI
{
    internal static class CefUtil
    {
        public static bool DISABLE_CEF = true;
        public static bool DISABLE_HOOK = true;


        internal static Dictionary<int, Browser> _cachedReferences = new Dictionary<int, Browser>();

        internal static Browser GetBrowserFromCef(CefBrowser browser)
        {
            Browser father = null;

            if (browser == null) return null;

            if (_cachedReferences.ContainsKey(browser.Identifier))
                return _cachedReferences[browser.Identifier];
            if (!CefUtil.DISABLE_CEF)
            {
                lock (CEFManager.Browsers)
                {
                    foreach (var b in CEFManager.Browsers)
                    {
                        if (b != null && b._browser != null && b._browser.Identifier == browser.Identifier)
                        {
                            father = b;
                            _cachedReferences.Add(browser.Identifier, b);
                            break;
                        }
                    }
                }
            }
            return father;
        }
    }

    internal class MainCefApp : CefApp
    {
        private WebKitInjector _injector;

        public MainCefApp()
        {
            _injector = new WebKitInjector();
        }

        protected override CefRenderProcessHandler GetRenderProcessHandler()
        {
            return _injector;
        }
        
    }

    internal class SecureSchemeFactory : CefSchemeHandlerFactory
    {
        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request)
        {
            Browser father = null;

            LogManager.AlwaysDebugLog("Entering request w/ schemeName " + schemeName);

            try
            {
                father = CefUtil.GetBrowserFromCef(browser);

                if (father == null || father._localMode)
                {
                    LogManager.AlwaysDebugLog("Local mode detected! Uri: " + request.Url);
                    var uri = new Uri(request.Url);
                    var path = Main.GTANInstallDir + "resources\\";
                    var requestedFile = path + uri.Host + uri.LocalPath;

                    LogManager.AlwaysDebugLog("Requested file: " + requestedFile);

                    if (!File.Exists(requestedFile))
                    {
                        LogManager.AlwaysDebugLog("File doesnt exist!");
                        browser.StopLoad();
                        return SecureCefResourceHandler.FromString("404", ".txt");
                    }

                    LogManager.AlwaysDebugLog("Loading from file!");

                    return SecureCefResourceHandler.FromFilePath(requestedFile,
                        MimeType.GetMimeType(Path.GetExtension(requestedFile)));
                }
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "CEF SCHEME HANDLING");
                browser?.StopLoad();
                return SecureCefResourceHandler.FromString("error", ".txt");
            }

            return null;
        }
    }
    
    internal class MainCefLoadHandler : CefLoadHandler
    {
        protected override void OnLoadStart(CefBrowser browser, CefFrame frame)
        {
            // A single CefBrowser instance can handle multiple requests
            //   for a single URL if there are frames (i.e. <FRAME>, <IFRAME>).
            //if (frame.IsMain)
            {
                LogManager.AlwaysDebugLog("START: " + browser.GetMainFrame().Url);
            }
        }

        protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
        {
            //if (frame.IsMain)
            {
                LogManager.AlwaysDebugLog(string.Format("END: {0}, {1}", browser.GetMainFrame().Url, httpStatusCode));
            }
        }
    }

    internal class MainLifeSpanHandler : CefLifeSpanHandler
    {
        private MainCefClient bClient;

        internal MainLifeSpanHandler(MainCefClient bc)
        {
            this.bClient = bc;
        }

        protected override void OnAfterCreated(CefBrowser browser)
        {
            base.OnAfterCreated(browser);
            this.bClient.Created(browser);
        }
    }

    internal class V8Bridge : CefV8Handler
    {
        private CefBrowser _browser;

        public V8Bridge(CefBrowser browser)
        {
            _browser = browser;
        }

        protected override bool Execute(string name, CefV8Value obj, CefV8Value[] arguments, out CefV8Value returnValue, out string exception)
        {
            Browser father = null;

            LogManager.AlwaysDebugLog("Entering JS Execute. Func: " + name + " arg len: " + arguments.Length);

            father = CefUtil.GetBrowserFromCef(_browser);

            if (father == null)
            {
                LogManager.SimpleLog("cef", "NO FATHER FOUND FOR BROWSER " + _browser.Identifier);
                returnValue = CefV8Value.CreateNull();
                exception = "NO FATHER WAS FOUND.";
                return false;
            }
            if (!CefUtil.DISABLE_CEF)
            {
                LogManager.AlwaysDebugLog("Father was found!");
                try
                {
                    if (name == "resourceCall")
                    {
                        LogManager.AlwaysDebugLog("Entering resourceCall...");

                        List<object> args = new List<object>();

                        for (int i = 1; i < arguments.Length; i++)
                        {
                            args.Add(arguments[i].GetValue());
                        }

                        LogManager.AlwaysDebugLog("Executing callback...");

                        object output = father._callback.call(arguments[0].GetStringValue(), args.ToArray());

                        LogManager.AlwaysDebugLog("Callback executed!");

                        returnValue = V8Helper.CreateValue(output);
                        exception = null;
                        return true;
                    }

                    if (name == "resourceEval")
                    {
                        LogManager.AlwaysDebugLog("Entering resource eval");
                        object output = father._callback.eval(arguments[0].GetStringValue());
                        LogManager.AlwaysDebugLog("callback executed!");

                        returnValue = V8Helper.CreateValue(output);
                        exception = null;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "EXECUTE JS FUNCTION");
                }
            }
            returnValue = CefV8Value.CreateNull();
            exception = "";
            return false;
        }
    }

    internal class WebKitInjector : CefRenderProcessHandler
    {
        protected override void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context)
        {
            if (frame.IsMain)
            {
                LogManager.AlwaysDebugLog("Setting main context!");

                Browser father = CefUtil.GetBrowserFromCef(browser);
                if (father != null)
                {
                    if (!CefUtil.DISABLE_CEF)
                    {
                        father._mainContext = context;
                    }
                    LogManager.AlwaysDebugLog("Main context set!");
                }
            }

            CefV8Value global = context.GetGlobal();

            CefV8Value func = CefV8Value.CreateFunction("resourceCall", new V8Bridge(browser));
            global.SetValue("resourceCall", func, CefV8PropertyAttribute.None);

            CefV8Value func2 = CefV8Value.CreateFunction("resourceEval", new V8Bridge(browser));
            global.SetValue("resourceEval", func2, CefV8PropertyAttribute.None);

            base.OnContextCreated(browser, frame, context);
        }

        protected override bool OnBeforeNavigation(CefBrowser browser, CefFrame frame, CefRequest request, CefNavigationType navigation_type,
            bool isRedirect)
        {
            if ((request.TransitionType & CefTransitionType.ForwardBackFlag) != 0 || navigation_type == CefNavigationType.BackForwarD)
            {
                return true;
            }

            return base.OnBeforeNavigation(browser, frame, request, navigation_type, isRedirect);
        }
    }

    internal class MainCefRenderHandler : CefRenderHandler
    {
        private int _windowHeight;
        private int _windowWidth;

        private ImageElement _imageElement;

        public Bitmap LastBitmap;
        public readonly object BitmapLock = new object();

        public MainCefRenderHandler(int windowWidth, int windowHeight)
        {
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
            LogManager.AlwaysDebugLog("Instantiated Renderer");

            _imageElement = new ImageElement(null, true);

            CEFManager.DirectXHook.AddImage(_imageElement);
        }

        public void SetHidden(bool hidden)
        {
            _imageElement.Hidden = hidden;
        }

        public void SetSize(int width, int height)
        {
            _windowHeight = height;
            _windowWidth = width;
        }

        public void SetPosition(int x, int y)
        {
            _imageElement.Location = new Point(x, y);
        }

        public void Dispose()
        {
            CEFManager.DirectXHook?.RemoveImage(_imageElement);
            _imageElement?.Dispose();
            _imageElement = null;
        }

        protected override void OnCursorChange(CefBrowser browser, IntPtr cursorHandle, CefCursorType type, CefCursorInfo customCursorInfo)
        {

        }

        protected override bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
        {
            return GetViewRect(browser, ref rect);
        }

        protected override bool GetScreenPoint(CefBrowser browser, int viewX, int viewY, ref int screenX, ref int screenY)
        {
            screenX = viewX;
            screenY = viewY;
            return true;
        }

        protected override bool GetViewRect(CefBrowser browser, ref CefRectangle rect)
        {
            rect.X = 0;
            rect.Y = 0;
            rect.Width = _windowWidth;
            rect.Height = _windowHeight;
            return true;
        }

        protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
        {
            return false;
        }

        protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
        {
        }

        protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
        {
            try
            {
                if (_imageElement != null)
                    _imageElement.SetBitmap(new Bitmap(width, height, width*4, PixelFormat.Format32bppArgb, buffer));
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "CEF PAINT");
            }
        }
        
        protected override void OnScrollOffsetChanged(CefBrowser browser)
        {
        }
    }

    internal class ContextMenuRemover : CefContextMenuHandler
    {
        protected override void OnBeforeContextMenu(CefBrowser browser, CefFrame frame, CefContextMenuParams state, CefMenuModel model)
        {
            model.Clear();
        }
    }

    internal class MainCefClient : CefClient
    {
        private readonly MainCefLoadHandler _loadHandler;
        private readonly MainCefRenderHandler _renderHandler;
        private readonly MainLifeSpanHandler _lifeSpanHandler;
        private readonly ContextMenuRemover _contextMenuHandler;

        public event EventHandler OnCreated;

        public MainCefClient(int windowWidth, int windowHeight)
        {
            _renderHandler = new MainCefRenderHandler(windowWidth, windowHeight);
            _loadHandler = new MainCefLoadHandler();
            _lifeSpanHandler = new MainLifeSpanHandler(this);
            _contextMenuHandler = new ContextMenuRemover();
        }

        public void SetPosition(int x, int y)
        {
            _renderHandler.SetPosition(x, y);
        }

        public void SetSize(int w, int h)
        {
            _renderHandler.SetSize(w, h);
        }

        public void SetHidden(bool hidden)
        {
            _renderHandler.SetHidden(hidden);
        }

        public void Close()
        {
            _renderHandler.Dispose();
        }

        public Bitmap GetLastBitmap()
        {
            if (_renderHandler.LastBitmap == null) return null;

            lock (_renderHandler.BitmapLock)
                return new Bitmap(_renderHandler.LastBitmap);
        }

        public void Created(CefBrowser bs)
        {
            if (this.OnCreated != null)
            {
                this.OnCreated(bs, EventArgs.Empty);
            }
        }

        protected override CefContextMenuHandler GetContextMenuHandler()
        {
            return _contextMenuHandler;
        }

        protected override CefRenderHandler GetRenderHandler()
        {
            LogManager.AlwaysDebugLog("Requested Renderer");
            return _renderHandler;
        }

        protected override CefLoadHandler GetLoadHandler()
        {
            return _loadHandler;
        }

        protected override CefLifeSpanHandler GetLifeSpanHandler()
        {
            return _lifeSpanHandler;
        }
    }

    public static class V8Helper
    {
        public static object GetValue(this CefV8Value val)
        {
            if (val.IsNull || val.IsUndefined) return null;

            if (val.IsArray) return new V8Array(val);
            if (val.IsBool) return val.GetBoolValue();
            if (val.IsDouble) return val.GetDoubleValue();
            if (val.IsInt) return val.GetIntValue();
            if (val.IsString) return val.GetStringValue();
            if (val.IsUInt) return val.GetUIntValue();

            return null;
        }

        public static CefV8Value CreateValue(object value)
        {
            if (value == null)
                return CefV8Value.CreateNull();
            if (value is bool)
                return CefV8Value.CreateBool((bool) value);
            if (value is double)
                return CefV8Value.CreateDouble((double) value);
            if (value is float)
                return CefV8Value.CreateDouble((double)(float)value);
            if (value is int)
                return CefV8Value.CreateInt((int)value);
            if (value is string)
                return CefV8Value.CreateString((string)value);
            if (value is uint)
                return CefV8Value.CreateUInt((uint)value);
            if (value is IList)
            {
                IList val = (IList) value;

                var arr = CefV8Value.CreateArray(val.Count);

                for (int i = 0; i < val.Count; i++)
                {
                    arr.SetValue(i, CreateValue(val[i]));
                }

                return arr;
            }
            return CefV8Value.CreateUndefined();
        }
    }

    public class V8Array
    {
        private CefV8Value _value;

        internal V8Array(CefV8Value val)
        {
            _value = val;
        }

        public object this[int index]
        {
            get { return _value.GetValue(index).GetValue(); }
        }

        public int length
        {
            get { return _value.GetArrayLength(); }
        }
    }
}