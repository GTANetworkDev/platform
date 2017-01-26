using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GTANetwork.Util
{
    public class LogManager
    {
        public static string LogDirectory = Main.GTANInstallDir + "\\logs";

        public static void CreateLogDirectory()
        {
            if (!Directory.Exists(Main.GTANInstallDir + "\\logs"))
                Directory.CreateDirectory(Main.GTANInstallDir + "\\logs");
        }

        public static void SimpleLog(string filename, string text)
        {
            CreateLogDirectory();
            try
            {
                lock (errorLogLock)
                    File.AppendAllText(LogDirectory + "\\" + filename + ".log", text + "\r\n");
            }
            catch{}
        }
        public static void CefLog(string text)
        {
            CreateLogDirectory();
            try
            {
                lock (errorLogLock)
                    File.AppendAllText(LogDirectory + "\\" + "CEF.log", text + "\r\n");
            }
            catch { }
        }
        public static void CefLog(Exception ex, string source)
        {
            CreateLogDirectory();
            lock (errorLogLock)
            {
                File.AppendAllText(LogDirectory + "\\CEF.log", ">> EXCEPTION OCCURED AT " + DateTime.Now + " FROM " + source + "\r\n" + ex.ToString() + "\r\n\r\n");
            }
        }

        class ThreadInfo
        {
            public string text { get; set; }
        }

        public static void DebugLog(string text)
        {
            if (Main.PlayerSettings.DebugMode || Main.SaveDebugToFile)
            {
                ThreadInfo threadInfo = new ThreadInfo();
                threadInfo.text = text;
                ThreadPool.QueueUserWorkItem(new WaitCallback(Work), threadInfo);
            }
        }

        public static void Work(object a)
        {
            ThreadInfo threadInfo = a as ThreadInfo;
            if (Main.SaveDebugToFile)
            {
                CreateLogDirectory();
                lock (errorLogLock)
                {
                    File.AppendAllText(LogDirectory + "\\Debug.log" + Environment.NewLine, threadInfo.text);
                }
            }
            if (Main.PlayerSettings.DebugMode)
            {
                byte[] bytes = new byte[1024];
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000);
                    using (Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        if (!sender.Connected)
                        {
                            sender.Connect(remoteEP);
                        }
                        byte[] msg = Encoding.ASCII.GetBytes(threadInfo.text + "<EOL>");
                        int bytesSent = sender.Send(msg);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public static void RuntimeLog(string text)
        {
            try
            {
                Debug.WriteLine(text);
                CreateLogDirectory();
                lock (errorLogLock)
                {
                    File.AppendAllText(LogDirectory + "\\Runtime.log", ">> " + text + "\r\n\r\n");
                }
            }
            catch (Exception) { }
        }

        public static object errorLogLock = new object();
        public static void LogException(Exception ex, string source)
        {
            CreateLogDirectory();
            lock (errorLogLock)
            {
                File.AppendAllText(LogDirectory + "\\Error.log", ">> EXCEPTION OCCURED AT " + DateTime.Now + " FROM " + source + "\r\n" + ex.ToString() + "\r\n\r\n");
            }
        }
    }
}