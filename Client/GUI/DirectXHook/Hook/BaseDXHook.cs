using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Threading;
using EasyHook;
using GTANetwork.Util;

namespace GTANetwork.GUI.DirectXHook.Hook
{
    internal abstract class BaseDXHook: SharpDX.Component, IDXHook
    {
        public BaseDXHook()
        {
            this.Timer = new Stopwatch();
            this.Timer.Start();
        }
        ~BaseDXHook()
        {
            Dispose(false);
        }

        protected Stopwatch Timer { get; set; }

        /// <summary>
        /// Frames Per second counter, FPS.Frame() must be called each frame
        /// </summary>
        int _processId = 0;
        protected int ProcessId
        {
            get
            {
                if (_processId == 0)
                {
                    _processId = RemoteHooking.GetCurrentProcessId();
                }
                return _processId;
            }
        }

        protected virtual string HookName
        {
            get
            {
                return "BaseDXHook";
            }
        }

        protected void DebugMessage(string message)
        {
            try
            {
                //Debug.WriteLine(message);
                //File.AppendAllText(Main.GTANInstallDir + "\\logs" + "\\Hook.log", ">> " + message + "\r\n\r\n");
             
            }
            catch (Exception) { }
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int startIndex, int numberOfMethods)
        {
            List<IntPtr> vtblAddresses = new List<IntPtr>();

            IntPtr vTable = Marshal.ReadIntPtr(pointer);
            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size)); // using IntPtr.Size allows us to support both 32 and 64-bit processes

            return vtblAddresses.ToArray();
        }

        protected static void CopyStream(Stream input, Stream output)
        {
            int bufferSize = 32768;
            byte[] buffer = new byte[bufferSize];
            while (true)
            {
                int read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }
                output.Write(buffer, 0, read);
            }
        }

        /// <summary>
        /// Reads data from a stream until the end is reached. The
        /// data is returned as a byte array. An IOException is
        /// thrown if any of the underlying IO calls fail.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        protected static byte[] ReadFullStream(Stream stream)
        {
            if (stream is MemoryStream)
            {
                return ((MemoryStream)stream).ToArray();
            }
            else
            {
                byte[] buffer = new byte[32768];
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read > 0)
                            ms.Write(buffer, 0, read);
                        if (read < buffer.Length)
                        {
                            return ms.ToArray();
                        }
                    }
                }
            }
        }
        /*
        /// <summary>
        /// Process the capture based on the requested format.
        /// </summary>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <param name="pitch">data pitch (bytes per row)</param>
        /// <param name="format">target format</param>
        /// <param name="pBits">IntPtr to the image data</param>
        /// <param name="request">The original requets</param>
        protected void ProcessCapture(int width, int height, int pitch, PixelFormat format, IntPtr pBits, ScreenshotRequest request)
        {
            if (request == null)
                return;

            if (format == PixelFormat.Undefined)
            {
                DebugMessage("Unsupported render target format");
                return;
            }

            // Copy the image data from the buffer
            int size = height * pitch;
            var data = new byte[size];
            Marshal.Copy(pBits, data, 0, size);

            // Prepare the response
            Interface.Screenshot response = null;

            if (request.Format == ImageFormat.PixelData)
            {
                // Return the raw data
                response = new Interface.Screenshot(request.RequestId, data)
                {
                    Format = request.Format,
                    PixelFormat = format,
                    Height = height,
                    Width = width,
                    Stride = pitch
                };
            }
            else 
            {
                // Return an image
                using (var bm = data.ToBitmap(width, height, pitch, format))
                {
                    System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                    switch (request.Format)
                    {
                        case ImageFormat.Jpeg:
                            imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                            break;
                        case ImageFormat.Png:
                            imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                            break;
                    }

                    response = new Interface.Screenshot(request.RequestId, bm.ToByteArray(imgFormat))
                    {
                        Format = request.Format,
                        Height = bm.Height,
                        Width = bm.Width
                    };
                }
            }

            // Send the response
            SendResponse(response);
        }

        protected void ProcessCapture(Stream stream, ScreenshotRequest request)
        {
            ProcessCapture(ReadFullStream(stream), request);
        }

        protected void ProcessCapture(byte[] bitmapData, ScreenshotRequest request)
        {
            try
            {
                if (request != null)
                {
                }
                LastCaptureTime = Timer.Elapsed;
            }
            catch (RemotingException)
            {
                // Ignore remoting exceptions
                // .NET Remoting will throw an exception if the host application is unreachable
            }
            catch (Exception e)
            {
                DebugMessage(e.ToString());
            }
        }

        */
        private ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private Bitmap BitmapFromBytes(byte[] bitmapData)
        {
            using (MemoryStream ms = new MemoryStream(bitmapData))
            {
                return (Bitmap)Image.FromStream(ms);
            }
        }

        protected TimeSpan LastCaptureTime
        {
            get;
            set;
        }

        protected bool CaptureThisFrame
        {
            get
            {
                return ((Timer.Elapsed - LastCaptureTime) > CaptureDelay);
            }
        }
        protected TimeSpan CaptureDelay { get; set; }

        #region IDXHook Members

        protected List<Hook> Hooks = new List<Hook>();
        public abstract void Hook();

        public abstract void Cleanup();

        #endregion

        #region IDispose Implementation

        protected override void Dispose(bool disposeManagedResources)
        {
            DebugMessage("DisposeBase");
            // Only clean up managed objects if disposing (i.e. not called from destructor)
            if (disposeManagedResources)
            {
                try
                {
                    DebugMessage("CleanupBase");
                    Cleanup();
                }
                catch { }

                try
                {
                    // Uninstall Hooks
                    if (Hooks.Count > 0)
                    {
                        // First disable the hook (by excluding all threads) and wait long enough to ensure that all hooks are not active
                        foreach (var hook in Hooks)
                        {
                            // Lets ensure that no threads will be intercepted again
                            hook.Deactivate();
                            DebugMessage("DisactiveHook");
                        }

                        System.Threading.Thread.Sleep(100);

                        // Now we can dispose of the hooks (which triggers the removal of the hook)
                        foreach (var hook in Hooks)
                        {
                            hook.Dispose();
                            DebugMessage("Dispose");
                        }
                        DebugMessage("HooksClear");
                        Hooks.Clear();

                    }

                    try
                    {
                        // Remove the event handlers
                    }
                    catch (RemotingException) { } // Ignore remoting exceptions (host process may have been closed)
                }
                catch
                {
                }
            }

            try
            {
                base.Dispose(disposeManagedResources);
            }
            catch (Exception ex)
            {
                DebugMessage("DIRECTX DISPOSE" + ex);
            }
        }

        #endregion
    }
}
