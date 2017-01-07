using System;
using System.Diagnostics;
using System.IO;

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

        public static void DebugLog(string text)
        {
            if (!Main.WriteDebugLog) return;
            CreateLogDirectory();
            try
            {
                File.AppendAllText(LogDirectory + "\\debug.log", text + "\r\n");
                Debug.WriteLine(text);
            }
            catch (Exception) { }
        }

        public static void AlwaysDebugLog(string text)
        {
            try
            {
                Debug.WriteLine(text);
            }
            catch (Exception) { }
        }

        public static object errorLogLock = new object();
        public static void LogException(Exception ex, string source)
        {
            CreateLogDirectory();
            lock (errorLogLock)
            {
                File.AppendAllText(LogDirectory + "\\error.log", ">> EXCEPTION OCCURED AT " + DateTime.Now + " FROM " + source + "\r\n" + ex.ToString() + "\r\n\r\n");
            }
        }
    }
}