#define PROD

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ionic.Zip;
using Microsoft.Owin.Extensions;
using Owin;
using Nancy;
using Microsoft.Owin.Hosting;
using Nancy.Extensions;
using Newtonsoft.Json;
using System.Xml.Linq;
using Lidgren.Network;

namespace GTANMasterServer
{
    public class Program
    {

        public static MasterServerWorker GtanServerWorker;
        public static Dictionary<string, VersioningUpdaterWorker> UpdateChannels;
        public static Dictionary<string, string> Queue;

        public static object GlobalLock = new object();

        public static void Main(string[] args)
        {
            int port = (int)XML.Config("Port");

            if (args.Any() && int.TryParse(args.First(), out port))
            {
            }

            var url = "http://+:" + port;

            if (!File.Exists(Database._onlinePath))
                File.AppendAllText(Database._onlinePath, JsonConvert.SerializeObject(new Dictionary<string, int>()));

            GtanServerWorker = new MasterServerWorker();
            //if ((bool)XML.Config("CI")) UpdateChannels = new Dictionary<string, VersioningUpdaterWorker>();

            //if ((bool)XML.Config("CI")) UpdateChannels.Add("stable", new VersioningUpdaterWorker());

            PingerWorker.Start();
            
            using (WebApp.Start<Startup>(url))
            {
                Debug.Log("Running list on: " + url);
                WelcomeMessageWorker.UpdateWelcomeMessage();

                //if ((bool)XML.Config("CI")) ContinuousIntegration.GetLastVersion();

                while (true)
                {
                    try
                    {
                        //if ((bool)XML.Config("CI"))
                        //{
                        //    ContinuousIntegration.Work();
                        //    foreach (var pair in UpdateChannels) pair.Value.Work();
                        //}
                        GtanServerWorker.Work();
                        PingerWorker.ProcessMessages();
                        WelcomeMessageWorker.Work();
                        Database.Work();
                    }
                    catch (Exception e) { Debug.Log("Exception: " + e.ToString()); }
                    finally
                    {
                        Thread.Sleep(100);
                    }
                }
            }

        }

        public static VersioningUpdaterWorker GetChannelWorker(string channelName)
        {
            if (UpdateChannels.ContainsKey(channelName)) return UpdateChannels[channelName];
            if (Directory.Exists("updater" + Path.DirectorySeparatorChar + channelName))
            {
                VersioningUpdaterWorker output;
                UpdateChannels.Add(channelName, output = new VersioningUpdaterWorker(channelName));
                return output;
            }

            return null;
        }
    }

    #region Pinger
    public static class PingerWorker
    {

        public static NetClient Client;

        public static void Start()
        {
            Program.Queue = new Dictionary<string, string>();
            NetPeerConfiguration config = new NetPeerConfiguration("Pinger") { Port = (int)XML.Config("PingPort") };
            config.SetMessageTypeEnabled(NetIncomingMessageType.DiscoveryResponse, true);
            Client = new NetClient(config);
            Client.Start();
            Debug.Log("Running pinger on: " + config.Port);
        }

        public static void ProcessMessages()
        {
            try
            {
                NetIncomingMessage msg;
                if ((msg = Client.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.DiscoveryResponse:
                            var Server = msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port.ToString();
                            //Debug.Log("Pong <- " + Server);
                            lock (Program.GlobalLock)
                            {
                                if (Program.Queue.ContainsKey(Server))
                                {
                                    Program.GtanServerWorker.AddServer(msg.SenderEndPoint.Address.ToString(), Program.Queue[Server]);
                                }
                            }
                            break;

                        default:
                            break;
                    }
                    Client.Recycle(msg);
                }
            }
            catch (Exception e) {
                Debug.Log("Exception: " + e);
            }
        }
  
        public static void Ping(string IP, int Port)
        {
            Client.DiscoverKnownPeer(IP, Port);
        }
    }
    #endregion


    public static class Debug
    {
        public static void Log(string text) { Console.WriteLine("[" + DateTime.UtcNow.ToString("dd'/'MM'/'yyyy HH:mm:ss UTC") + "] " + text); }

        public static void LogCI(string text)
        {
            if ((bool)XML.Config("DebugCI"))
                Console.WriteLine("[" + DateTime.UtcNow.ToString("dd'/'MM'/'yyyy HH:mm:ss UTC") + "] " + text);
        }
    }

    #region CI

    public static class ContinuousIntegration
    {
        private static DateTime _lastUpdate = DateTime.Now;
        public static object GitLock = new object();


        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 10)
            {
                try
                {
                    GetLastVersion();
                }
                catch (Exception ex)
                {
                    Debug.Log("CI ERROR: " + ex.ToString());
                }
                _lastUpdate = DateTime.Now;
            }
        }

        public static void GetLastVersion()
        {
            Dictionary<string, MemoryStream> zipFile = null;

            Debug.LogCI("Getting last CI build.");
            
            try
            {
                Debug.LogCI("Fetching last build...");
                
                string basedir = "updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "temp";

                if (Directory.Exists(basedir + "" + Path.DirectorySeparatorChar + "scripts"))
                {
                    DeleteDirectory(basedir + "" + Path.DirectorySeparatorChar + "scripts");
                }

                Directory.CreateDirectory(basedir + "" + Path.DirectorySeparatorChar + "scripts");

                Debug.LogCI("Fetching last build...");

                // Download the zip to download.zip
                FetchLastBuild("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "download.zip");

                Debug.LogCI("Fetch complete. Zipping up...");

                zipFile = UnzipFile("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "download.zip");

                foreach (var memoryStream in zipFile)
                {
                    if (memoryStream.Key.ToLower() == "scripthookvdotnet.dll")
                    {
                        continue;
                    }
                    else
                    {
                        using (var fileStream = File.Create(basedir + "" + Path.DirectorySeparatorChar + "scripts" + Path.DirectorySeparatorChar + "" + memoryStream.Key))
                        {
                            memoryStream.Value.CopyTo(fileStream);
                        }
                    }
                }


                var subprocessVersionText =
                System.Diagnostics.FileVersionInfo.GetVersionInfo(basedir + "" + Path.DirectorySeparatorChar + "scripts" + Path.DirectorySeparatorChar + "GTANetwork.dll").FileVersion.ToString();
                var gtanVersion = ParseableVersion.Parse(subprocessVersionText);

                lock (GitLock)
                {
                    using (ZipFile filesZip = new ZipFile())
                    {
                        
                        filesZip.AddDirectory(basedir + "" + Path.DirectorySeparatorChar + "scripts", "scripts");
                        filesZip.AddFiles(Directory.GetFiles(basedir), "" + Path.DirectorySeparatorChar + "");
                        if (Directory.Exists(basedir + Path.DirectorySeparatorChar + "scripts_auto"))
                        {
                            filesZip.AddFiles(Directory.GetFiles(basedir + Path.DirectorySeparatorChar + "scripts_auto"), "scripts");
                            foreach (
                                var dir in
                                    Directory.GetDirectories(basedir + Path.DirectorySeparatorChar + "scripts_auto"))
                                filesZip.AddDirectory(dir,
                                    "scripts" + Path.DirectorySeparatorChar + Path.GetDirectoryName(dir));
                        }

                        if (Directory.Exists(basedir + Path.DirectorySeparatorChar + "root_auto"))
                        {
                            filesZip.AddFiles(Directory.GetFiles(basedir + Path.DirectorySeparatorChar + "root_auto"), Path.DirectorySeparatorChar+"");
                            foreach (
                                var dir in
                                    Directory.GetDirectories(basedir + Path.DirectorySeparatorChar + "root_auto"))
                                filesZip.AddDirectory(dir,
                                        Path.DirectorySeparatorChar + Path.GetFileName(dir));
                        }

                        if (File.Exists("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "files.zip"))
                            File.Delete("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "files.zip");
                        filesZip.Save("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "files.zip");
                    }

                    if (File.Exists("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "version.txt"))
                        File.Delete("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "version.txt");
                    File.WriteAllText("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "version.txt", gtanVersion.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.Log("{0} CONTINUOUS INTEGRATION ERROR: " + ex);
                return;
            }
            finally
            {
                if (zipFile != null)
                foreach (var stream in zipFile)
                {
                    stream.Value.Dispose();
                }

                if (File.Exists("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "download.zip")) File.Delete("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "download.zip");
            }
            
            Debug.Log("Git CI build updated!");
        }


        public static void FetchLastBuild(string destination)
        {
            var formParams = "email={0}&password={1}";

            var creds = File.ReadAllText("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "credentials.txt").Split('=');

            Debug.LogCI("Logging into appveyor...");

            var form = string.Format(formParams, creds[0], creds[1]);
            var url = @"http://ci.appveyor.com/api/user/login";
            string cookieHeader;

            WebRequest req = WebRequest.Create(url);

            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(form);
            req.ContentLength = bytes.Length;
            Debug.LogCI("Writing to stream...");
            using (Stream os = req.GetRequestStream())
            {
                os.Write(bytes, 0, bytes.Length);
            }
            Debug.LogCI("Getting response...");
            WebResponse resp = req.GetResponse();
            cookieHeader = resp.Headers["Set-cookie"];

            Debug.LogCI("Getting last build...");

            string pageSource;
            string getUrl = @"http://ci.appveyor.com/api/projects/Guad/mtav";
            WebRequest getRequest = WebRequest.Create(getUrl);
            Debug.LogCI("Adding cookies...");

            getRequest.Headers.Add("Cookie", cookieHeader);

            Debug.LogCI("Getting response...");
            WebResponse getResponse = getRequest.GetResponse();
            using (StreamReader sr = new StreamReader(getResponse.GetResponseStream()))
            {
                pageSource = sr.ReadToEnd();
            }
            
            var match = Regex.Match(pageSource, "\"jobId\":\"([0-9a-zA-Z]+)");
            var buildId = match.Groups[1].Captures[0].Value;


            var buildFileUri = $"http://ci.appveyor.com/api/buildjobs/{buildId}/artifacts/Client/bin/Client%20Folder.zip";

            Debug.LogCI("Downloading client folder...");
            WebRequest fileRequest = WebRequest.Create(buildFileUri);
            fileRequest.Headers.Add("Cookie", cookieHeader);

            Debug.LogCI("Getting stream response...");
            WebResponse fileResponse = fileRequest.GetResponse();
            if (File.Exists(destination)) File.Delete(destination);
            Debug.LogCI("Downloading file...");
            using (var fileDest = File.Create(destination))
            {
                fileResponse.GetResponseStream().CopyTo(fileDest);
            }
        }

        public static Dictionary<string, MemoryStream> UnzipFile(string filename)
        {
            var result = new Dictionary<string, MemoryStream>();
            using (ZipFile zip = ZipFile.Read(filename))
            {
                foreach (ZipEntry e in zip)
                {
                    MemoryStream data = new MemoryStream();
                    e.Extract(data);
                    data.Position = 0;
                    result.Add(e.FileName, data);
                }
            }
            return result;
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }
    }



    public static class MiscFilesServer
    {
        private static DateTime _lastUpdate;

        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 30)
            {
                UpdateManifest();
            }
        }

        public static string WorkDir = Path.GetFullPath("misc" + Path.DirectorySeparatorChar + "files");

        public static string LastJson { get; set; }

        public static void UpdateManifest()
        {
            _lastUpdate = DateTime.Now;

            var files = GetFilesInFolder(WorkDir);

            var obj = new FileManifest();
            obj.Files = files;

            LastJson = JsonConvert.SerializeObject(obj);
        }

        public static string MakeRelative(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }

        private static List<string> GetFilesInFolder(string folder)
        {
            var output = new List<string>();

            foreach (var file in Directory.GetFiles(folder))
            {
                output.Add(MakeRelative(file, WorkDir));
            }

            foreach (var directory in Directory.GetDirectories(folder))
            {
                output.AddRange(GetFilesInFolder(directory));
            }

            return output;
        }

        public class FileManifest
        {
            public List<string> Files { get; set; }
        }
    }


    public class VersioningUpdaterWorker
    {
        public ParseableVersion LastClientVersion;
        public ParseableVersion LastSubprocessVersion;
        public string Channel;
        private DateTime _lastUpdate = DateTime.Now;

        public VersioningUpdaterWorker(string channel = "stable")
        {
            Channel = channel;
            GetVersion();
        }


        public void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 30)
            {
                GetVersion();
            }
        }

        public void GetVersion()
        {
            _lastUpdate = DateTime.Now;

            var baseDir = "updater" + Path.DirectorySeparatorChar + Channel + Path.DirectorySeparatorChar;

            lock (ContinuousIntegration.GitLock)
            {
                if (!File.Exists(baseDir + "version.txt") ||
                    !File.Exists(baseDir + "files.zip") ||
                    !File.Exists(baseDir + "GTANetwork.dll"))
                {
                    Debug.Log("ERROR: version.txt, files.zip or GTANetwork.dll were not found for channel " +
                                      Channel);
                    return;
                }

                var versionText = File.ReadAllText(baseDir + "version.txt");
                LastClientVersion = ParseableVersion.Parse(versionText);

                var subprocessVersionText =
                    System.Diagnostics.FileVersionInfo.GetVersionInfo(baseDir + "GTANetwork.dll").FileVersion.ToString();
                LastSubprocessVersion = ParseableVersion.Parse(subprocessVersionText);

                Debug.Log("Updated last version for channel " + Channel);
            }
        }

        public string FilesPath()
        {
            return "updater" + Path.DirectorySeparatorChar + Channel + Path.DirectorySeparatorChar + "files.zip";
        }

        public string LauncherPath()
        {
            return "updater" + Path.DirectorySeparatorChar + Channel + Path.DirectorySeparatorChar + "launcher" + Path.DirectorySeparatorChar + "files.zip";
        }

        public string LauncherVersionPath()
        {
            return "updater" + Path.DirectorySeparatorChar + Channel + Path.DirectorySeparatorChar + "launcher" + Path.DirectorySeparatorChar + "version.txt";
        }
    }
#endregion
    
    #region Welcome
    public static class WelcomeMessageWorker
    {
        public static string Title { get; set; }
        public static string Message { get; set; }
        public static string Picture { get; set; }

        private static DateTime _lastUpdate = DateTime.Now;

        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 30)
            {
                UpdateWelcomeMessage();
            }
        }

        public static void UpdateWelcomeMessage()
        {
            _lastUpdate = DateTime.Now;
            if (!File.Exists("welcome" + Path.DirectorySeparatorChar + "welcome.json"))
            {
                Debug.Log("ERROR: welcome.json were not found.");
                return;
            }

            var welcomeText = File.ReadAllText("welcome" + Path.DirectorySeparatorChar + "welcome.json");
            var welcomeObj = JsonConvert.DeserializeObject<WelcomeSchema>(welcomeText);

            

            Title = welcomeObj.Title;
            Message = welcomeObj.Message;
            Picture = welcomeObj.Picture;

            Debug.Log("Updated welcome message.");
        }

        public static string ToJson()
        {
            var obj = new WelcomeSchema();
            obj.Title = Title;
            obj.Message = Message;
            obj.Picture = Picture;

            return JsonConvert.SerializeObject(obj);
        }
    }

    public class WelcomeSchema
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Picture { get; set; }
    }
    #endregion


    public static class Database
    {
        private static DateTime _lastUpdate;
        private static string[] _verified = new string[0];
        private static string[] _whitelist = new string[0];

        private static object _syncList = new object();

        private const string _verifiedPath = "verified.txt";
        internal const string _whitelistPath = "whitelist.txt";
        internal const string _onlinePath = "online.json";

        public static bool IsVerified(string ip)
        {
            lock (_syncList)
            {
                return Array.IndexOf(_verified, ip) != -1;
            }
        }

        public static bool IsWhitelisted(string ip)
        {
            lock (_syncList)
            {
                return Array.IndexOf(_whitelist, ip) != -1;
            }
        }

        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 10)
            {
                _lastUpdate = DateTime.Now;

                if (File.Exists(_verifiedPath) && File.Exists(_whitelistPath))
                {
                    lock (_syncList)
                    {
                        _verified = File.ReadAllLines(_verifiedPath);
                        _whitelist = File.ReadAllLines(_whitelistPath);
                    }
                }
            }
        }
    }

    public class MasterServerWorker
    {
        public Dictionary<string, DateTime> ServersAnnounces = new Dictionary<string, DateTime>();
        public Dictionary<string, MasterServerAnnounceSchema> Servers = new Dictionary<string, MasterServerAnnounceSchema>();
        public Dictionary<string, MasterServerAnnounceSchema> ServersMasterList = new Dictionary<string, MasterServerAnnounceSchema>();
        public Dictionary<string, MasterServerAnnounceSchema> VerifiedServers = new Dictionary<string, MasterServerAnnounceSchema>();
        public Dictionary<string, int> ServersOnlineTime = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(Database._onlinePath));
        public MasterServerStats Stats = new MasterServerStats();

        public int MaxMinutes = 10;

        public void Work()
        {
            Dictionary<string, DateTime> copy;

            lock (Program.GlobalLock)
            {
                copy = new Dictionary<string, DateTime>(ServersAnnounces);
            }

            foreach (var pair in copy)
            {
                if (DateTime.Now.Subtract(pair.Value).TotalMinutes > MaxMinutes)
                {
                    lock (Program.GlobalLock)
                    {
                        ServersAnnounces.Remove(pair.Key);
                        Servers.Remove(pair.Key);
                        VerifiedServers.Remove(pair.Key);
                        ServersMasterList.Remove(pair.Key);
                        Program.Queue.Remove(pair.Key);
                    }
                }
            }

            //if (DateTime.UtcNow.TimeOfDay.ToString().Contains("00:00"))
            //{
            //    lock (Program.GlobalLock)
            //    {
            //        if (File.Exists(Database._whitelistPath))
            //            File.Delete(Database._whitelistPath);

            //        foreach (var ip in ServersAnnounces.Keys)
            //            File.AppendAllText(Database._whitelistPath, ip + Environment.NewLine);
            //    }
            //}

            if(DateTime.Now.Minute.ToString() == "0")
            {
                lock (Program.GlobalLock)
                {
                    foreach (var ip in ServersAnnounces.Keys)
                    {
                        if (ServersOnlineTime.Keys.Contains(ip))
                        {
                            ServersOnlineTime[ip]++;
                        }
                    }

                    if (File.Exists(Database._onlinePath))
                        File.Delete(Database._onlinePath);

                    File.AppendAllText(Database._onlinePath, JsonConvert.SerializeObject(ServersOnlineTime));
                }
            }
        }

        public void QueueServer(string ip, string json)
        {
            try
            {
                lock (Program.GlobalLock)
                {
                    var newServObj = JsonConvert.DeserializeObject<MasterServerAnnounceSchema>(json);
                    if (!Servers.ContainsKey(ip + ":" + newServObj.Port.ToString()) && !Program.Queue.ContainsKey(ip + ":" + newServObj.Port.ToString()))
                    {
                        //Debug.Log("Ping -> " + ip + ":" + newServObj.Port);
                        Program.Queue.Add(ip + ":" + newServObj.Port.ToString(), json);
                        PingerWorker.Ping(ip, newServObj.Port);
                    }
                    else
                    {
                        AddServer(ip, json);
                    }
                }

            }
            catch (Exception e) { Debug.Log("Exception: " + e.ToString()); }
        }


        public void AddServer(string ip, string json)
        {
            try
            {
                //Debug.Log("Processing Server: " + ip);
                var newServObj = JsonConvert.DeserializeObject<MasterServerAnnounceSchema>(json);
                var finalAddr = ip + ":" + newServObj.Port.ToString();

                if (!string.IsNullOrWhiteSpace(newServObj.fqdn) && Dns.GetHostAddresses(newServObj.fqdn)[0].ToString() == ip && newServObj.fqdn.Length < 64) finalAddr = newServObj.fqdn + ":" + newServObj.Port;
                if (!string.IsNullOrWhiteSpace(newServObj.Gamemode)) newServObj.Gamemode = newServObj.Gamemode.Substring(0, Math.Min(20, newServObj.Gamemode.Length));
                if (!string.IsNullOrWhiteSpace(newServObj.Map)) newServObj.Map = newServObj.Map.Substring(0, Math.Min(20, newServObj.Map.Length)).Replace("~n~", string.Empty).Replace("¦", string.Empty);
                if (!string.IsNullOrWhiteSpace(newServObj.ServerName)) newServObj.ServerName = newServObj.ServerName.Substring(0, Math.Min(128, newServObj.ServerName.Length));
                if (newServObj.MaxPlayers > 1000) newServObj.MaxPlayers = 1000;
                if (newServObj.MaxPlayers < 1) newServObj.MaxPlayers = 1;
                newServObj.IP = finalAddr;

                if (string.IsNullOrWhiteSpace(newServObj.ServerVersion) || ParseableVersion.Parse(newServObj.ServerVersion) < ParseableVersion.Parse((string)XML.Config("MinVersion")))
                {
                    //Debug.Log("Rejected server: " + ip + ", reason: Outdated server version.");
                    return;
                }


                lock (Program.GlobalLock)
                {
                    //if (APIServers.Values.Count(x => x.IP.Contains(ip)) > 2 || APIServers.Values.Any(x => x.ServerName == newServObj.ServerName))
                    //{
                    //    Debug.Log("Rejected server: " + finalAddr + ", reason: Duplicate.");
                    //    return;
                    //}

                    if (ServersAnnounces.ContainsKey(finalAddr))
                    {
                        ServersAnnounces[finalAddr] = DateTime.Now;
                        Servers[finalAddr] = newServObj;

                        if (ServersMasterList.ContainsKey(finalAddr))
                            ServersMasterList[finalAddr] = newServObj;

                        if (VerifiedServers.ContainsKey(finalAddr))
                            VerifiedServers[finalAddr] = newServObj;

                        return;
                    }

                    ServersAnnounces.Add(finalAddr, DateTime.Now);
                    Servers.Add(finalAddr, newServObj);

                    if (!ServersOnlineTime.Keys.Contains(finalAddr))
                        ServersOnlineTime.Add(finalAddr, 0);

                    if (ServersOnlineTime[finalAddr] >= 24 || Database.IsWhitelisted(finalAddr))
                        ServersMasterList.Add(finalAddr, newServObj);

                    if (Database.IsVerified(finalAddr))
                        VerifiedServers.Add(finalAddr, newServObj);

                    if (!string.IsNullOrWhiteSpace(newServObj.fqdn)) {
                        Debug.Log("Adding Server: " + ip + ":" + newServObj.Port.ToString() + ", FQDN: " + newServObj.fqdn + ", Match: " + (Dns.GetHostAddresses(newServObj.fqdn)[0].ToString() == ip)); 
                    }
                    else {
                        Debug.Log("Adding Server: " + finalAddr);
                    }
                }
            }
            catch (Exception e) {
                Debug.Log("Step: " + ", Exception: " + e.ToString());
            }
        }

        public string ToServersList()
        {
            lock (Program.GlobalLock)
            {
                var obj = new MasterServerSchema();
                obj.list = new List<string>(Servers.Select(s => s.Value.IP));

                return JsonConvert.SerializeObject(obj);
            }
        }

        public string ToVerifiedServersList()
        {
            lock (Program.GlobalLock)
            {
                var obj = new MasterServerSchema();
                obj.list = new List<string>(VerifiedServers.Select(s => s.Value.IP));

                return JsonConvert.SerializeObject(obj);
            }
        }

        public string ToMasterList()
        {
            lock (Program.GlobalLock)
            {
                var obj = new MasterServer2Schema();
                obj.list = new List<MasterServerAnnounceSchema>(ServersMasterList.Select(pair => pair.Value));

                return JsonConvert.SerializeObject(obj);
            }
        }

        public string ToMasterListStats()
        {
            lock (Program.GlobalLock)
            {
                var obj = new MasterServerStats();
                obj.TotalServers = ServersAnnounces.Count();
                obj.TotalPlayers = Servers.Sum(x => x.Value.CurrentPlayers);

                return JsonConvert.SerializeObject(obj);
            }
        }
    }

    public class MasterModule : NancyModule
    {
        public MasterModule()
        {
            Get["/servers"] = _ =>
            {
                var resp = (Response) Program.GtanServerWorker.ToServersList();
                resp.ContentType = "application/json";

                return resp;
            };

            Get["/verified"] = _ =>
            {
                var resp = (Response)Program.GtanServerWorker.ToVerifiedServersList();
                resp.ContentType = "application/json";

                return resp;
            };

            Get["/apiservers"] = _ =>
            {
                var resp = (Response)Program.GtanServerWorker.ToMasterList();
                resp.ContentType = "application/json";
                resp.Headers.Add("Access-Control-Allow-Origin", "*");

                return resp;
            };

            Get["/stats"] = _ =>
            {
                var resp = (Response)Program.GtanServerWorker.ToMasterListStats();
                resp.ContentType = "application/json";
                resp.Headers.Add("Access-Control-Allow-Origin", "*");

                return resp;
            };

            Get["/"] = parameters =>
            {
                return Response.AsRedirect("https://stats.gtanet.work");
            };

            //Get["/"] = _ => View["index"];

            Post["/"] = parameters => { return 403; };

            Post["/addserver"] = parameters =>
            {
                var jsonData = new StreamReader(Request.Body).ReadToEnd();
#if PROD
                var serverAddress = Request.Headers["x-real-ip"].FirstOrDefault();
                Program.GtanServerWorker.QueueServer(serverAddress, jsonData);
#else
                var serverAddress = Request.UserHostAddress;
                Program.GtanServerWorker.AddServer(serverAddress, jsonData);
#endif
                return 200;
            };

            Get["/pictures/{pic}"] = parameters => Response.AsFile("welcome" + Path.DirectorySeparatorChar + "pictures" + Path.DirectorySeparatorChar + ((string)parameters.pic));

            Get["/welcome.json"] = _ =>
            {
                var resp = (Response) WelcomeMessageWorker.ToJson();
                resp.ContentType = "application/json";

                return resp;
            };

            Get["/static.json"] = _ =>
            {
                var resp = (Response)MiscFilesServer.LastJson;

                resp.ContentType = "application/json";

                return resp;
            };

            Get["/static/{path*}"] = path =>
            {
                string fullFile = Path.Combine(MiscFilesServer.WorkDir, path);

                if (!Path.GetFullPath(fullFile).StartsWith(MiscFilesServer.WorkDir)) return 404;

                if (File.Exists(fullFile))
                {
                    return Response.AsFile("misc" + Path.DirectorySeparatorChar + "files" + Path.DirectorySeparatorChar +
                                        (string)path);
                }

                return 404;
            };

            Get["/update/{channel}/version"] = parameters =>
            {
                return File.ReadAllText("updater" + Path.DirectorySeparatorChar + (string)parameters.channel + Path.DirectorySeparatorChar + "version.txt");
            };

            Get["/update/{channel}/files"] = parameters =>
            {
                return Response.AsFile("updater" + Path.DirectorySeparatorChar + (string)parameters.channel + Path.DirectorySeparatorChar + "files.zip");
            };

            Get["/update/{channel}/launcher/version"] = parameters =>
            {
                return File.ReadAllText("updater" + Path.DirectorySeparatorChar + (string)parameters.channel + Path.DirectorySeparatorChar + "launcher" + Path.DirectorySeparatorChar + "version.txt");
            };

            Get["/update/{channel}/launcher/files"] = parameters =>
            {
                return Response.AsFile("updater" + Path.DirectorySeparatorChar + (string)parameters.channel + Path.DirectorySeparatorChar + "launcher" + Path.DirectorySeparatorChar + "files.zip");
            };

            Get["/update/{channel}/cef/version"] = parameters =>
            {
                return File.ReadAllText("updater" + Path.DirectorySeparatorChar + (string)parameters.channel + Path.DirectorySeparatorChar + "cef" + Path.DirectorySeparatorChar + "version.txt");
            };

            Get["/update/{channel}/cef/files"] = parameters =>
            {
                return 404;
            };

            Get["/update/version"] = _ =>
            {
                return File.ReadAllText("updater" + Path.DirectorySeparatorChar + "version.txt");
            };

            #region CI Integreation
            //Get["/update/{channel}/version"] = parameters =>
            //{
            //    var chan = (string)parameters.channel;

            //    var versionWorker = Program.GetChannelWorker(chan);

            //    if (versionWorker != null)
            //    {
            //        return versionWorker.LastClientVersion.ToString();
            //    }

            //    return 404;
            //};

            //Get["/update/{channel}/files"] = parameters =>
            //{
            //    var chan = (string)parameters.channel;

            //    var versionWorker = Program.GetChannelWorker(chan);

            //    if (versionWorker != null)
            //    {
            //        return Response.AsFile(versionWorker.FilesPath());
            //    }

            //    return 404;
            //};

            //Get["/update/{channel}/launcher/version"] = parameters =>
            //{
            //    var chan = (string)parameters.channel;

            //    var versionWorker = Program.GetChannelWorker(chan);

            //    if (versionWorker != null)
            //    {
            //        return File.ReadAllText(versionWorker.LauncherVersionPath()).ToString();
            //    }
            //    return 404;
            //};

            //Get["/update/{channel}/launcher/files"] = parameters =>
            //{
            //    var chan = (string)parameters.channel;

            //    var versionWorker = Program.GetChannelWorker(chan);

            //    if (versionWorker != null)
            //    {
            //        return Response.AsFile(versionWorker.LauncherPath());
            //    }

            //    return 404;
            //};

            #endregion
        }
    }

#region Schemas

    public class MasterServerSchema
    {
        public List<string> list { get; set; }
    }

    public class MasterServer2Schema
    {
        public List<MasterServerAnnounceSchema> list { get; set; }
    }

    public class MasterServerAnnounceSchema
    {
        public int Port { get; set; }
        public int MaxPlayers { get; set; }
        public string ServerName { get; set; }
        public int CurrentPlayers { get; set; }
        public string Gamemode { get; set; }
        public string Map { get; set; }
        public string IP { get; set; }
        public bool Passworded { get; set; }
        public string fqdn { get; set; }
        public string ServerVersion { get; set; }
        public string key { get; set; }
    }

    public class MasterServerStats
    {
        public int TotalPlayers { get; set; }
        public int TotalServers { get; set; }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseNancy();
            app.UseStageMarker(PipelineStage.MapHandler);
        }
    }

    public struct ParseableVersion : IComparable<ParseableVersion>
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public int Build { get; set; }

        public ParseableVersion(int major, int minor, int rev, int build)
        {
            Major = major;
            Minor = minor;
            Revision = rev;
            Build = build;
        }

        public override string ToString()
        {
            return Major + "." + Minor + "." + Build + "." + Revision;
        }

        public int CompareTo(ParseableVersion right)
        {
            return CreateComparableInteger().CompareTo(right.CreateComparableInteger());
        }

        public long CreateComparableInteger()
        {
            return (long)((Revision) + (Build * Math.Pow(10, 4)) + (Minor * Math.Pow(10, 8)) + (Major * Math.Pow(10, 12)));
        }

        public static bool operator >(ParseableVersion left, ParseableVersion right)
        {
            return left.CreateComparableInteger() > right.CreateComparableInteger();
        }

        public static bool operator <(ParseableVersion left, ParseableVersion right)
        {
            return left.CreateComparableInteger() < right.CreateComparableInteger();
        }

        public static ParseableVersion Parse(string version)
        {
            var split = version.Split('.');
            if (split.Length < 2) throw new ArgumentException("Argument version is in wrong format");

            var output = new ParseableVersion();
            output.Major = int.Parse(split[0]);
            output.Minor = int.Parse(split[1]);
            if (split.Length >= 3) output.Build = int.Parse(split[2]);
            if (split.Length >= 4) output.Revision = int.Parse(split[3]);
            return output;
        }
        
        public static ParseableVersion FromAssembly()
        {
            var ourVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return new ParseableVersion()
            {
                Major = ourVersion.Major,
                Minor = ourVersion.Minor,
                Revision = ourVersion.Revision,
                Build = ourVersion.Build,
            };
        }
    }

    public class XML
    {
        public static dynamic Config(string str)
        {
            dynamic output;
            XElement doc = XElement.Load("settings.xml");
            return output = (from el in doc.Descendants(str) select el).FirstOrDefault();

        }
    }
        #endregion

    }
