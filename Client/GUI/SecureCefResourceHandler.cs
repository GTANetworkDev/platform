using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using GTANetwork.Util;
using Xilium.CefGlue;

namespace GTANetwork.GUI
{
    public class SecureCefResourceHandler : CefResourceHandler
    {
        public const string DefaultMimeType = "text/html";
        public string FilePath { get; private set; }
        public string MimeType { get; set; }
        public Stream Stream { get; set; }
        public int StatusCode { get; set; }
        public string StatusText { get; set; }
        public long? ResponseLength { get; set; }
        public NameValueCollection Headers { get; private set; }
        public bool AutoDisposeStream { get; set; }
        public CefErrorCode? ErrorCode { get; set; }

        public SecureCefResourceHandler() : this(DefaultMimeType)
        {}

        public SecureCefResourceHandler(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                throw new ArgumentException("mimeType", "Please provide a valid mimeType");
            }

            StatusCode = 200;
            StatusText = "OK";
            MimeType = mimeType;
            Headers = new NameValueCollection();
        }


        protected override bool ProcessRequest(CefRequest request, CefCallback callback)
        {
            callback.Continue();

            return true;
        }

        protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
        {
            callback.Dispose();

            if (Stream == null)
            {
                bytesRead = 0;
                return false;
            }

            var buffer = new byte[response.Length];
            bytesRead = Stream.Read(buffer, 0, buffer.Length);

            response.Write(buffer, 0, buffer.Length);
            
            if (Stream.Position >= Stream.Length - 1 && bytesRead == 0)
            {
                Stream.Close();
                LogManager.CefLog("Closed file stream!");
            }

            return bytesRead > 0;
        }

        protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
        {
            if (ErrorCode.HasValue)
            {
                responseLength = 0;
                redirectUrl = null;
                response.Status = 501;
            }
            else
            {
                responseLength = -1;
                redirectUrl = null;

                response.Status = StatusCode;
                response.MimeType = MimeType;
                response.StatusText = StatusText;
                response.SetHeaderMap(Headers);

                if (ResponseLength.HasValue)
                {
                    responseLength = ResponseLength.Value;
                }
                else
                {
                    var memoryStream = Stream as MemoryStream;
                    if (memoryStream != null)
                    {
                        responseLength = memoryStream.Length;
                    }
                }

                if (Stream != null && Stream.CanSeek)
                {
                    Stream.Position = 0;
                }
            }
        }


        protected override void Cancel()
        {
            Stream = null;
        }

        protected override bool CanGetCookie(CefCookie cookie)
        {
            return true;
        }

        protected override bool CanSetCookie(CefCookie cookie)
        {
            return true;
        }

        private static MemoryStream GetStream(string text, Encoding encoding, bool includePreamble)
        {
            if (includePreamble)
            {
                var preamble = encoding.GetPreamble();
                var bytes = encoding.GetBytes(text);

                var memoryStream = new MemoryStream(preamble.Length + bytes.Length);

                memoryStream.Write(preamble, 0, preamble.Length);
                memoryStream.Write(bytes, 0, bytes.Length);

                memoryStream.Position = 0;

                return memoryStream;
            }

            return new MemoryStream(encoding.GetBytes(text));
        }

        public static SecureCefResourceHandler FromFilePath(string fileName, string mimeType = null)
        {
            return new SecureCefResourceHandler(mimeType ?? DefaultMimeType)
            {
                Stream = File.OpenRead(fileName),
            };
        }

        public static SecureCefResourceHandler FromString(string text, string fileExtension)
        {
            var mimeType = GTANetworkShared.MimeType.GetMimeType(fileExtension);
            return FromString(text, Encoding.UTF8, false, mimeType);
        }

        public static SecureCefResourceHandler FromString(string text, Encoding encoding = null,
            bool includePreamble = true, string mimeType = DefaultMimeType)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return new SecureCefResourceHandler(mimeType) { Stream = GetStream(text, encoding, includePreamble)};
        }

        public static SecureCefResourceHandler FromStream(Stream stream, string mimeType = DefaultMimeType)
        {
            return new SecureCefResourceHandler(mimeType) {Stream = stream };
        }
    }
}