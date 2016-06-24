using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

        public static void Main(string[] args)
        {
            var url = "http://+:80";

            GtanServerWorker = new MasterServerWorker();
            CoopServerWorker = new MasterServerWorker();

            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine("Running on {0}", url);
                VersioningUpdaterWorker.GetVersion();
                WelcomeMessageWorker.UpdateWelcomeMessage();

                while (true)
                {
                    GtanServerWorker.Work();
                    CoopServerWorker.Work();
                    VersioningUpdaterWorker.Work();
                    WelcomeMessageWorker.Work();
                    Thread.Sleep(100);
                }
            }
        }
    }

    public static class VersioningUpdaterWorker
    {
        public static ParseableVersion LastClientVersion;
        public static ParseableVersion LastSubprocessVersion;
        private static DateTime _lastUpdate = DateTime.Now;

        public static void Work()
        {
            if (DateTime.Now.Subtract(_lastUpdate).TotalMinutes > 30)
            {
                GetVersion();
            }
        }

        public static void GetVersion()
        {
            _lastUpdate = DateTime.Now;

            if (!File.Exists("updater" + Path.DirectorySeparatorChar + "version.txt") || !File.Exists("updater" + Path.DirectorySeparatorChar + "files.zip") || !File.Exists("updater" + Path.DirectorySeparatorChar + "GTANetwork.dll"))
            {
                Console.WriteLine("ERROR: version.txt, files.zip or GTANetwork.dll were not found.");
                return;
            }
            

            var versionText = File.ReadAllText("updater" + Path.DirectorySeparatorChar + "version.txt");
            LastClientVersion = ParseableVersion.Parse(versionText);

            var subprocessVersionText =
                System.Diagnostics.FileVersionInfo.GetVersionInfo("updater" + Path.DirectorySeparatorChar +
                                                                  "GTANetwork.dll").FileVersion.ToString();
            LastSubprocessVersion = ParseableVersion.Parse(subprocessVersionText);
            
            Console.WriteLine("[{0}] Updated last version.", DateTime.Now.ToString("HH:mm:ss"));
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

            Get["/version"] = _ => VersioningUpdaterWorker.LastClientVersion.ToString();

            Get["/launcherversion"] = _ => VersioningUpdaterWorker.LastSubprocessVersion.ToString();

            Get["/launcher"] = _ => Response.AsFile("updater" + Path.DirectorySeparatorChar + "GTANetwork.dll");

            Get["/files"] = _ => Response.AsFile("updater" + Path.DirectorySeparatorChar + "files.zip");
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
