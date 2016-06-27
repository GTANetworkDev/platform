#define DEBUG

#if !DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using Xilium.CefGlue;

namespace GTANetwork.GUI
{
    public class DemoCefApp : CefApp
    {
        public DemoCefApp()
        {
            
        }
    }

    public static class CEFManager
    {
        public static void Initialize(Size screenSize)
        {
            ScreenSize = screenSize;

            _bitmapRegion = MemoryMappedFile.CreateNew("GTANETWORKBITMAPSCREEN", 1048576);

            try
            {
                _memorySharedMutex = Mutex.OpenExisting("GTANETWORKCEFMUTEX");
            }
            catch
            {
                _memorySharedMutex = new Mutex(false, "GTANETWORKCEFMUTEX");
            }

            RenderThread = new Thread(RenderLoop);
            RenderThread.IsBackground = true;
            RenderThread.Start();
        }

        public static List<Browser> Browsers = new List<Browser>();
        public static int FPS = 1;
        public static Thread RenderThread;
        public static bool StopRender;
        public static Size ScreenSize;

        public static Process DirectXHook;

        private static Mutex _memorySharedMutex;
        private static MemoryMappedFile _bitmapRegion;


        public static void RenderLoop()
        {
            CefRuntime.Load();
            
            var cefMainArgs = new CefMainArgs(new string[0]);
            var cefApp = new DemoCefApp();

            if (CefRuntime.ExecuteProcess(cefMainArgs, cefApp) != -1)
            {
                LogManager.DebugLog("Error!");
            }

            var cefSettings = new CefSettings()
            {
                SingleProcess = false,
                MultiThreadedMessageLoop = true,
            };

            CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp);
            

            DirectXHook = Process.Start(Main.GTANInstallDir + "\\cef\\CEFInjector.exe");
            
            while (!StopRender)
            {
                Bitmap doubleBuffer = new Bitmap(ScreenSize.Width, ScreenSize.Height,
                    PixelFormat.Format32bppArgb);

                using (var graphics = Graphics.FromImage(doubleBuffer))
                {
                    foreach (var browser in Browsers)
                    {
                        graphics.DrawImage(browser.GetRawBitmap(), browser.Position);
                    }
                }

                var rawBytes = BitmapToByteArray(doubleBuffer);

                if (_memorySharedMutex.WaitOne())
                {
                    using (var accessor = _bitmapRegion.CreateViewStream())
                    using (var binReader = new BinaryWriter(accessor))
                    {
                        binReader.Write(rawBytes.Length);
                        binReader.Write(doubleBuffer.Width);
                        binReader.Write(doubleBuffer.Height);
                        binReader.Write(rawBytes, 0, rawBytes.Length);
                    }

                    _memorySharedMutex.ReleaseMutex();
                }

                Thread.Sleep(1000/FPS);
            }
        }

        public static byte[] BitmapToByteArray(Bitmap bitmap)
        {
            BitmapData bmpdata = null;

            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }
        }
    }


    public class Browser
    {
        //private ChromiumWebBrowser _browser;

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


        private CefWindowInfo _cefWindowInfo;
        private DemoCefClient _cefClient;
        public Browser(Size ScreenSize)
        {
            _cefWindowInfo = CefWindowInfo.Create();
            _cefWindowInfo.SetAsOffScreen(IntPtr.Zero);

            var browserSettings = new CefBrowserSettings();
            _cefClient = new DemoCefClient(ScreenSize.Width, ScreenSize.Height);
            CefBrowserHost.CreateBrowser(_cefWindowInfo, _cefClient, browserSettings, "http://www.reddit.com/");
        }

        public void GoToPage(string page)
        {
            //_browser.Load(page);
            
        }

        public Bitmap GetRawBitmap()
        {
            Bitmap output = _browser.Bitmap;
            _browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));
            return output;
        }

        public Bitmap GetBitmap()
        {
            Bitmap doubleBuffer = new Bitmap(_browser.Bitmap.Width, _browser.Bitmap.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(doubleBuffer))
            {
                graphics.DrawImage(_browser.Bitmap, new Point(0, 0));
            }

            _browser.InvokeRenderAsync(_browser.BitmapFactory.CreateBitmap(false, 1));

            return doubleBuffer;
        }
    }

    internal class DemoCefClient : CefClient
    {
        private readonly DemoCefLoadHandler _loadHandler;
        private readonly DemoCefRenderHandler _renderHandler;

        public DemoCefClient(int windowWidth, int windowHeight)
        {
            _renderHandler = new DemoCefRenderHandler(windowWidth, windowHeight);
            _loadHandler = new DemoCefLoadHandler();
        }

        protected override CefRenderHandler GetRenderHandler()
        {
            return _renderHandler;
        }

        protected override CefLoadHandler GetLoadHandler()
        {
            return _loadHandler;
        }
    }

    internal class DemoCefLoadHandler : CefLoadHandler
    {
        protected override void OnLoadStart(CefBrowser browser, CefFrame frame)
        {
            // A single CefBrowser instance can handle multiple requests
            //   for a single URL if there are frames (i.e. <FRAME>, <IFRAME>).
            if (frame.IsMain)
            {
                Console.WriteLine("START: {0}", browser.GetMainFrame().Url);
            }
        }

        protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
        {
            if (frame.IsMain)
            {
                Console.WriteLine("END: {0}, {1}", browser.GetMainFrame().Url, httpStatusCode);
            }
        }
    }

    internal class DemoCefRenderHandler : CefRenderHandler
    {
        private readonly int _windowHeight;
        private readonly int _windowWidth;

        public DemoCefRenderHandler(int windowWidth, int windowHeight)
        {
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
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
            // Save the provided buffer (a bitmap image) as a PNG.
            var bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppRgb, buffer);
            bitmap.Save("LastOnPaint.png", ImageFormat.Png);
        }

        protected override void OnCursorChange(CefBrowser browser, IntPtr cursorHandle)
        {
        }

        protected override void OnScrollOffsetChanged(CefBrowser browser)
        {
        }
    }
}
#endif