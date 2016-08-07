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

namespace GTANMasterServer
{
    public class Program
    {
        public static MasterServerWorker GtanServerWorker;
        public static MasterServerWorker CoopServerWorker;
        public static Dictionary<string, VersioningUpdaterWorker> UpdateChannels;

        public static void Main(string[] args)
        {
            int port = 80;

            if (args.Any() && int.TryParse(args.First(), out port))
            {
            }

            var url = "http://+:" + port;

            GtanServerWorker = new MasterServerWorker();
            CoopServerWorker = new MasterServerWorker();
            UpdateChannels = new Dictionary<string, VersioningUpdaterWorker>();

            UpdateChannels.Add("stable", new VersioningUpdaterWorker());

            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine("Running on {0}", url);
                WelcomeMessageWorker.UpdateWelcomeMessage();
                ContinuousIntegration.Work();

                while (true)
                {
                    try
                    {
                        ContinuousIntegration.Work();
                        GtanServerWorker.Work();
                        CoopServerWorker.Work();
                        foreach (var pair in UpdateChannels) pair.Value.Work();
                        WelcomeMessageWorker.Work();
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
                    Console.WriteLine("[{0}] CI ERROR: " + ex.ToString(), DateTime.Now.ToString("HH:mm:ss"));
                }
                _lastUpdate = DateTime.Now;
            }
        }

        public static void GetLastVersion()
        {
            Dictionary<string, MemoryStream> zipFile = null;

            Console.WriteLine("[{0}] Getting last CI build.", DateTime.Now.ToString("HH:mm:ss"));
            
            try
            {
                Console.WriteLine("[{0}] Fetching last build...", DateTime.Now.ToString("HH:mm:ss"));
                
                string basedir = "updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "temp";

                if (Directory.Exists(basedir + "" + Path.DirectorySeparatorChar + "scripts"))
                {
                    DeleteDirectory(basedir + "" + Path.DirectorySeparatorChar + "scripts");
                }

                Directory.CreateDirectory(basedir + "" + Path.DirectorySeparatorChar + "scripts");

                FetchLastBuild("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "download.zip");
                
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
                            filesZip.AddFiles(Directory.GetFiles(basedir + Path.DirectorySeparatorChar + "scripts_auto"), "scripts");
                        if (Directory.Exists(basedir + Path.DirectorySeparatorChar + "root_auto"))
                            filesZip.AddFiles(Directory.GetFiles(basedir + Path.DirectorySeparatorChar + "root_auto"), Path.DirectorySeparatorChar+"");
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
                Console.WriteLine("{0} CONTINUOUS INTEGRATION ERROR: " + ex, DateTime.Now.ToString("HH:mm:ss"));
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
            
            Console.WriteLine("[{0}] Git CI build updated!", DateTime.Now.ToString("HH:mm:ss"));
        }


        public static void FetchLastBuild(string destination)
        {
            var formParams = "email={0}&password={1}";

            var creds = File.ReadAllText("updater" + Path.DirectorySeparatorChar + "git" + Path.DirectorySeparatorChar + "credentials.txt").Split('=');

            var form = string.Format(formParams, creds[0], creds[1]);
            var url = @"https://ci.appveyor.com/api/user/login";
            string cookieHeader;

            WebRequest req = WebRequest.Create(url);

            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(form);
            req.ContentLength = bytes.Length;
            using (Stream os = req.GetRequestStream())
            {
                os.Write(bytes, 0, bytes.Length);
            }

            WebResponse resp = req.GetResponse();
            cookieHeader = resp.Headers["Set-cookie"];


            string pageSource;
            string getUrl = @"https://ci.appveyor.com/api/projects/Guad/mtav";
            WebRequest getRequest = WebRequest.Create(getUrl);
            getRequest.Headers.Add("Cookie", cookieHeader);
            WebResponse getResponse = getRequest.GetResponse();
            using (StreamReader sr = new StreamReader(getResponse.GetResponseStream()))
            {
                pageSource = sr.ReadToEnd();
            }

            var match = Regex.Match(pageSource, "\"jobId\":\"([0-9a-zA-Z]+)");
            var buildId = match.Groups[1].Captures[0].Value;


            var buildFileUri = $"https://ci.appveyor.com/api/buildjobs/{buildId}/artifacts/Client/bin/Client%20Folder.zip";

            
            WebRequest fileRequest = WebRequest.Create(buildFileUri);
            fileRequest.Headers.Add("Cookie", cookieHeader);
            WebResponse fileResponse = fileRequest.GetResponse();
            if (File.Exists(destination)) File.Delete(destination);
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
                    Console.WriteLine("ERROR: version.txt, files.zip or GTANetwork.dll were not found for channel " +
                                      Channel);
                    return;
                }

                var versionText = File.ReadAllText(baseDir + "version.txt");
                LastClientVersion = ParseableVersion.Parse(versionText);

                var subprocessVersionText =
                    System.Diagnostics.FileVersionInfo.GetVersionInfo(baseDir + "GTANetwork.dll").FileVersion.ToString();
                LastSubprocessVersion = ParseableVersion.Parse(subprocessVersionText);

                Console.WriteLine("[{0}] Updated last version for channel {1}.", DateTime.Now.ToString("HH:mm:ss"),
                    Channel);
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
                Console.WriteLine("ERROR: welcome.json were not found.");
                return;
            }

            var welcomeText = File.ReadAllText("welcome" + Path.DirectorySeparatorChar + "welcome.json");
            var welcomeObj = JsonConvert.DeserializeObject<WelcomeSchema>(welcomeText);

            

            Title = welcomeObj.Title;
            Message = welcomeObj.Message;
            Picture = welcomeObj.Picture;

            Console.WriteLine("[{0}] Updated welcome message.", DateTime.Now.ToString("HH:mm:ss"));
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

    public class MasterServerWorker
    {
        public List<Tuple<string, DateTime>> Servers = new List<Tuple<string, DateTime>>();
        public int MaxMinutes = 10;

        public void Work()
        {
            lock (Servers)
            {
                for (int i = Servers.Count - 1; i >= 0; i--)
                {
                    if (DateTime.Now.Subtract(Servers[i].Item2).TotalMinutes > MaxMinutes)
                    {
                        Servers.RemoveAt(i);
                    }
                }
            }
        }

        public void AddServer(string ip)
        {
            var split = ip.Split(':');
            if (split.Length != 2) return;
            int port;
            if (!int.TryParse(split[1], out port)) return;
            var finalString = split[0] + ":" + port;
            lock (Servers)
            {
                if (Servers.Any(s => s.Item1 == finalString)) return;
                Servers.Add(new Tuple<string, DateTime>(split[0] + ":" + port, DateTime.Now));
            }
        }

        public string ToJson()
        {
            lock (Servers)
            {
                var obj = new MasterServerSchema();
                obj.list = new List<string>(Servers.Select(s => s.Item1));

                return JsonConvert.SerializeObject(obj);
            }
        }
    }

    public class MasterModule : NancyModule
    {
        public MasterModule()
        {
            Get["/servers"] = _ => Program.GtanServerWorker.ToJson();

            Get["/"] = _ => Program.CoopServerWorker.ToJson();
            Post["/"] = parameters =>
            {
                if (Request.IsLocal()) return 403;
                var port = new StreamReader(Request.Body).ReadToEnd();
                var serverAddress = Request.UserHostAddress + ":" + port;
                Console.WriteLine("[{1}] Adding COOP server \"{0}\".", serverAddress, DateTime.Now.ToString("HH:mm:ss"));
                Program.CoopServerWorker.AddServer(serverAddress);
                return 200;
            };


            Post["/addserver"] = parameters =>
            {
                if (Request.IsLocal()) return 403;
                var port = new StreamReader(Request.Body).ReadToEnd();
                var serverAddress = Request.UserHostAddress + ":" + port;
                Console.WriteLine("[{1}] Adding server \"{0}\".", serverAddress, DateTime.Now.ToString("HH:mm:ss"));
                Program.GtanServerWorker.AddServer(serverAddress);
                return 200;
            };

            Get["/pictures/{pic}"] = parameters => Response.AsFile("welcome" + Path.DirectorySeparatorChar + "pictures" + Path.DirectorySeparatorChar + ((string)parameters.pic));

            Get["/welcome.json"] = _ => WelcomeMessageWorker.ToJson();


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


    public class MasterServerSchema
    {
        public List<string> list { get; set; }
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
}
