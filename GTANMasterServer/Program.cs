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

namespace GTANMasterServer
{
    public class Program
    {

        public static MasterServerWorker GtanServerWorker;
        public static Dictionary<string, VersioningUpdaterWorker> UpdateChannels;

        public static void Main(string[] args)
        {
            int port = (int)XML.Config("Port");

            if (args.Any() && int.TryParse(args.First(), out port))
            {
            }

            var url = "http://+:" + port;

            GtanServerWorker = new MasterServerWorker();
            UpdateChannels = new Dictionary<string, VersioningUpdaterWorker>();

            UpdateChannels.Add("stable", new VersioningUpdaterWorker());

            using (WebApp.Start<Startup>(url))
            {
                Debug.Log("Running on: " + url);
                WelcomeMessageWorker.UpdateWelcomeMessage();
                ContinuousIntegration.GetLastVersion();

                while (true)
                {
                    try
                    {
                        ContinuousIntegration.Work();
                        GtanServerWorker.Work();
                        foreach (var pair in UpdateChannels) pair.Value.Work();
                        WelcomeMessageWorker.Work();

                        if((bool)XML.Config("whitelist"))
                            Whitelist.Work();
                    }
                    catch {}
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

        public string SubprocessPath()
        {
            return "updater" + Path.DirectorySeparatorChar + Channel + Path.DirectorySeparatorChar + "GTANetwork.dll";
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

    public static class Whitelist
    {
        private static DateTime _lastWhitelistUpdate;
        private static string[] _whitelist = new string[0];

        private static object _syncList = new object();

        public static bool IsWhitelisted(string ip)
        {
            lock (_syncList)
            {
                return Array.IndexOf(_whitelist, ip) != -1;
            }
        }


        private const string _whitelistPath = "whitelist.txt";

        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastWhitelistUpdate).TotalMinutes > 30)
            {
                _lastWhitelistUpdate = DateTime.Now;

                if (File.Exists(_whitelistPath))
                {
                    lock (_syncList)
                    {
                        _whitelist = File.ReadAllLines(_whitelistPath);
                    }
                }
            }
        }
    }

    public class MasterServerWorker
    {
        public Dictionary<string, DateTime> UpdatesServers = new Dictionary<string, DateTime>();
        public Dictionary<string, MasterServerAnnounceSchema> APIServers = new Dictionary<string, MasterServerAnnounceSchema>();
        public MasterServerStats Stats = new MasterServerStats();

        public int MaxMinutes = 10;

        public object GlobalLock = new object();

        public void Work()
        {
            Dictionary<string, DateTime> copy;

            lock (GlobalLock)
            {
                copy = new Dictionary<string, DateTime>(UpdatesServers);
            }

            foreach (var pair in copy)
            {
                if (DateTime.Now.Subtract(pair.Value).TotalMinutes > MaxMinutes)
                {
                    lock (GlobalLock)
                    {
                        UpdatesServers.Remove(pair.Key);
                        APIServers.Remove(pair.Key);
                    }
                }
            }
        }

        public void AddServer(string ip, string json)
        {
            try
            {
                if((bool)XML.Config("whitelist"))
                {
                    if (!Whitelist.IsWhitelisted(ip)) return;
                }
               
                var newServObj = JsonConvert.DeserializeObject<MasterServerAnnounceSchema>(json);

                var finalAddr = ip + ":" + newServObj.Port;

                if (newServObj.fqdn != null && Dns.GetHostAddresses(newServObj.fqdn)[0].ToString() == ip && newServObj.fqdn.Length < 64) finalAddr = newServObj.fqdn + ":" + newServObj.Port;
                if (newServObj.ServerName != null) newServObj.ServerName = newServObj.ServerName.Substring(0, Math.Min(55, newServObj.ServerName.Length));
                if (newServObj.Gamemode != null) newServObj.Gamemode = newServObj.Gamemode.Substring(0, Math.Min(20, newServObj.Gamemode.Length));
                if (newServObj.Map != null) newServObj.Map = newServObj.Map.Substring(0, Math.Min(20, newServObj.Map.Length));

                newServObj.IP = finalAddr;

                lock (GlobalLock)
                {
                    if (UpdatesServers.ContainsKey(finalAddr))
                    {
                        UpdatesServers[finalAddr] = DateTime.Now;
                        APIServers[finalAddr] = newServObj;
                        return;
                    }

                    UpdatesServers.Add(finalAddr, DateTime.Now);
                    APIServers.Add(finalAddr, newServObj);

                    //Will only be shown if the server does not exist in memory
                    if (newServObj.fqdn != null)
                    {
                        Debug.Log("Adding Server: " + ip + ":" + newServObj.Port + " | FQDN: " + newServObj.fqdn + " | Match: " + (Dns.GetHostAddresses(newServObj.fqdn)[0].ToString() == ip)); 
                    }
                    else
                    {
                        Debug.Log("Adding Server: " + finalAddr);
                    }
                }
            }
            catch { }
        }

        public string ToJson()
        {
            lock (GlobalLock)
            {
                var obj = new MasterServer2Schema();
                obj.list = new List<MasterServerAnnounceSchema>(APIServers.Select(pair => pair.Value));

                return JsonConvert.SerializeObject(obj);
            }
        }

        public string ToRawJson()
        {
            lock (GlobalLock)
            {
                var obj = new MasterServerSchema();
                obj.list = new List<string>(APIServers.Select(s => s.Value.IP));

                return JsonConvert.SerializeObject(obj);
            }
        }

        public string StatsJson()
        {
            lock (GlobalLock)
            {
                var obj = new MasterServerStats();
                obj.TotalServers = UpdatesServers.Count();
                obj.TotalPlayers = APIServers.Sum(x => x.Value.CurrentPlayers);

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
                var resp = (Response) Program.GtanServerWorker.ToRawJson();
                resp.ContentType = "application/json";

                return resp;
            };

            Get["/stats"] = _ =>
            {
                var resp = (Response)Program.GtanServerWorker.StatsJson();
                resp.ContentType = "application/json";
                resp.Headers.Add("Access-Control-Allow-Origin", "*");

                return resp;
            };

            Get["/apiservers"] = _ =>
            {
                var resp = (Response)Program.GtanServerWorker.ToJson();
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
                //if (Request.IsLocal()) return 403;
                var jsonData = new StreamReader(Request.Body).ReadToEnd();
                var serverAddress = Request.Headers["x-real-ip"].FirstOrDefault();
                //var serverAddress = Request.UserHostAddress;
                Program.GtanServerWorker.AddServer(serverAddress, jsonData);

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
                var chan = (string)parameters.channel;

                var versionWorker = Program.GetChannelWorker(chan);

                if (versionWorker != null)
                {
                    return versionWorker.LastClientVersion.ToString();
                }

                return 404;
            };

            Get["/update/{channel}/version/l"] = parameters =>
            {
                var chan = (string)parameters.channel;

                var versionWorker = Program.GetChannelWorker(chan);

                if (versionWorker != null)
                {
                    return versionWorker.LastSubprocessVersion.ToString();
                }

                return 404;
            };

            Get["/update/{channel}/files"] = parameters =>
            {
                var chan = (string)parameters.channel;

                var versionWorker = Program.GetChannelWorker(chan);

                if (versionWorker != null)
                {
                    return Response.AsFile(versionWorker.FilesPath());
                }

                return 404;
            };

            Get["/update/{channel}/files/l"] = parameters =>
            {
                var chan = (string)parameters.channel;

                var versionWorker = Program.GetChannelWorker(chan);

                if (versionWorker != null)
                {
                    return Response.AsFile(versionWorker.SubprocessPath());
                }

                return 404;
            };
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
