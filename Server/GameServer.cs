using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkServer.Constant;
using GTANetworkServer.Managers;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using ProtoBuf;

namespace GTANetworkServer
{
    internal class StreamingClient
    {
        public StreamingClient(Client c)
        {
            Parent = c;
            ChunkSize = c.NetConnection.Peer.Configuration.MaximumTransmissionUnit - 20;
            Files = new List<StreamedData>();
        }

        public int ChunkSize { get; set; }
        public Client Parent { get; set; }
        public List<StreamedData> Files { get; set; }
    }

    internal class StreamedData
    {
        public StreamedData()
        {
            HasStarted = false;
            BytesSent = 0;
        }

        public bool HasStarted { get; set; }
        public bool Accepted { get; set; }

        public int Id { get; set; }
        public long BytesSent { get; set; }
        public byte[] Data { get; set; }

        public FileType Type { get; set; }
        public string Name { get; set; }
        public string Resource { get; set; }
        public string Hash { get; set; }
    }

    public delegate dynamic ExportedFunctionDelegate(params object[] parameters);
    public delegate void ExportedEvent(params dynamic[] parameters);

    internal partial class GameServer
    {
        public GameServer(ServerSettings conf)
        {
            Clients = new List<Client>();
            Downloads = new List<StreamingClient>();
            RunningResources = new List<Resource>();
            CommandHandler = new CommandHandler();
            BanManager = new BanManager();
            FileHashes = new Dictionary<string, string>();
            ExportedFunctions = new System.Dynamic.ExpandoObject();
            PickupManager = new PickupManager();
            UnoccupiedVehicleManager = new UnoccupiedVehicleManager();
            NetEntityHandler = new NetEntityHandler();

            Port = conf.Port;
            if (conf.MaxPlayers < 2) MaxPlayers = 2;
            else if (conf.MaxPlayers > 1000) MaxPlayers = 1000;
            else MaxPlayers = conf.MaxPlayers;

            ACLEnabled = conf.UseACL && File.Exists("acl.xml");
            BanManager.Initialize();

            if (ACLEnabled) ACL = new AccessControlList("acl.xml");

            ConstantVehicleDataOrganizer.Initialize();

            if (conf.Name != null) Name = conf.Name.Substring(0, Math.Min(58, conf.Name.Length)); // 46 to fill up title + additional 12 chars for colors such as ~g~.. etc..
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            var config = new NetPeerConfiguration("GTANETWORK");
            var lAdd = IPAddress.Parse(conf.LocalAddress);
            config.LocalAddress = lAdd;
            config.BroadcastAddress = lAdd;
            config.Port = conf.Port;
            config.EnableUPnP = conf.UseUPnP;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            //config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.MaxPlayers = MaxPlayers;
            config.ConnectionTimeout = 60f; // 120f = 30 second timeout
            //config.MaximumConnections = 50000;
            //config.AutoFlushSendQueue = true;
            //config.SendBufferSize = 1024 * 512;
            //config.ReceiveBufferSize = 1024 * 512;
            //config.MaximumConnections = conf.MaxPlayers + 2; // + 2 for discoveries


            Server = new NetServer(config);
            
            PasswordProtected = !string.IsNullOrWhiteSpace(conf.Password);
            Password = conf.Password;
            AnnounceSelf = conf.Announce;
            MasterServer = "http://master.gtanet.work";
            AnnounceToLAN = conf.AnnounceToLan;
            UseUPnP = conf.UseUPnP;
            MinimumClientVersion = ParseableVersion.Parse(conf.MinimumClientVersion);
            LogLevel = conf.LogLevel;
            UseHTTPFileServer = conf.UseHTTPServer;
            TrustClientProperties = conf.EnableClientsideEntityProperties;
            fqdn = conf.fqdn;
            Conntimeout = conf.Conntimeout;
            AllowCEFDevTool = conf.Allowcefdevtool;

            if (conf.whitelist != null)
            {
                ModWhitelist = conf.whitelist.Items.Select(item => item.Hash).ToList();
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var index = args.Name.IndexOf(",", StringComparison.Ordinal);
                var actualAssembly = args.Name;

                if (index != -1) actualAssembly = args.Name.Substring(0, index) + ".dll";

                return AssemblyReferences.ContainsKey(actualAssembly) ? Assembly.LoadFrom(AssemblyReferences[actualAssembly]) : null;
            };
        }

        public ParseableVersion MinimumClientVersion;
        public NetServer Server;
        public TaskFactory ConcurrentFactory;
        internal List<StreamingClient> Downloads;
        internal API PublicAPI = new API();
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public List<Client> Clients { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected { get; set; }
        public string GamemodeName { get; set; }
        public string fqdn { get; set; }
        public bool Conntimeout { get; set; }
        public bool AllowCEFDevTool { get; set; }
        public Resource Gamemode { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }
        public int LogLevel { get; set; }
        public bool AnnounceToLAN { get; set; }
        internal AccessControlList ACL { get; set; }
        public bool IsClosing { get; set; }
        public bool ReadyToClose { get; set; }
        public bool ACLEnabled { get; set; }
        public bool UseUPnP { get; set; }
        public List<string> ModWhitelist { get; set; }
        public bool UseHTTPFileServer { get; set; }
        public bool TrustClientProperties { get; set; }
        public string ErrorCmd = "~r~ERROR:~w~ Command not found.";

        public BanManager BanManager;
        public ColShapeManager ColShapeManager;
        public CommandHandler CommandHandler;
        public dynamic ExportedFunctions;

        public List<Resource> RunningResources;
        public PickupManager PickupManager;
        public UnoccupiedVehicleManager UnoccupiedVehicleManager;
        public Thread StreamerThread;
        public FileServer FileServer;

        private Dictionary<string, string> FileHashes { get; set; }
        public Dictionary<NetHandle, Dictionary<string, object>> EntityProperties = new Dictionary<NetHandle, Dictionary<string, object>>();
        public Dictionary<string, object> WorldProperties = new Dictionary<string, object>();

        public Dictionary<int, List<Client>> VehicleOccupants = new Dictionary<int, List<Client>>();

        public NetEntityHandler NetEntityHandler { get; set; }

        public bool AllowDisplayNames { get; set; }

        public List<Resource> AvailableMaps;
        public Resource CurrentMap;

        // Assembly name, Path to assembly.
        public Dictionary<string, string> AssemblyReferences = new Dictionary<string, string>();

        private Dictionary<long, DateTime> queue = new Dictionary<long, DateTime>();

        ParseableVersion _serverVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

        private DateTime _lastAnnounceDateTime;
        public void Start(IEnumerable<string> filterscripts)
        {
            try
            {
                Server.Start();
            }
            catch (SocketException ex)
            {
                Program.Output("ERROR: Socket Exception when starting server: " + ex.Message);
                Console.Read();
                Program.CloseProgram = true;
                return;
            }

            if (AnnounceSelf)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

            if (UseUPnP)
            {
                try
                {
                    Server.UPnP.ForwardPort(Port, "GTA Network Server");
                }
                catch (Exception ex)
                {
                    Program.Output("UNHANDLED EXCEPTION DURING UPNP PORT FORWARDING. YOUR ROUTER MAY NOT SUPPORT UPNP.");
                    Program.Output(ex.ToString());
                }
            }

            NetEntityHandler.CreateWorld();
            ColShapeManager = new ColShapeManager();

            if (UseHTTPFileServer)
            {
                Program.Output("Starting file server...");

                FileServer = new FileServer();
                FileServer.Start(Port);
            }

            Program.Output("Loading resources...");
            foreach (var path in filterscripts)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                StartResource(path);
            }

            StreamerThread = new Thread(Streamer.MainThread) {IsBackground = true};
            StreamerThread.Start();

            //StressTest.Init();
            // Uncomment to start a stress test
        }

        public void AnnounceSelfToMaster()
        {
            if (LogLevel > 0)
                Program.Output("Announcing self to master server...", LogCat.Debug);

            var annThread = new Thread((ThreadStart) delegate
            {
                using (var wb = new WebClient())
                {
                    try
                    {
                        var annObject = new MasterServerAnnounce
                        {
                            ServerName = Name,
                            CurrentPlayers = Clients.Count,
                            MaxPlayers = MaxPlayers,
                            Map = CurrentMap?.DirectoryName,
                            Gamemode = string.IsNullOrEmpty(GamemodeName)
                                ? Gamemode?
                                      .DirectoryName ?? "GTA Network"
                                : GamemodeName,
                            Port = Port,
                            Passworded = PasswordProtected,
                            fqdn = fqdn,
                            ServerVersion = _serverVersion.ToString()
                        };


                        wb.UploadData(MasterServer.Trim('/') + "/addserver",
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(annObject)));
                    }
                    catch (WebException)
                    {
                        Program.Output("Failed to announce self: master server is not available at this time.",
                            LogCat.Error);
                        //if (LogLevel >= 2)
                        //{
                        //    Program.Output("\n====\n" + ex.ToString() + "\n====\n");
                        //}
                    }
                }
            }) {IsBackground = true};
            annThread.Start();
        }

        private bool isIPLocal(string ipaddress)
        {
            var straryIPAddress = ipaddress.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            int[] iaryIPAddress = { int.Parse(straryIPAddress[0]), int.Parse(straryIPAddress[1]), int.Parse(straryIPAddress[2]), int.Parse(straryIPAddress[3]) };
            return iaryIPAddress[0] == 10 || (iaryIPAddress[0] == 192 && iaryIPAddress[1] == 168) || (iaryIPAddress[0] == 172 && (iaryIPAddress[1] >= 16 && iaryIPAddress[1] <= 31));
        }

        public static Dictionary<string, CustomSetting> LoadSettings(List<MetaSetting> sets)
        {
            var dict = new Dictionary<string, CustomSetting>();

            if (sets == null) return dict;
            foreach (var setting in sets)
            {
                dict.Set(setting.Name, new CustomSetting()
                {
                    Value = setting.Value,
                    DefaultValue = setting.DefaultValue,
                    Description = setting.Description,
                });
            }

            return dict;
        }

        public void LoadMap(Resource res, XmlGroup map, int dimension)
        {
            res.MapEntities = new List<NetHandle>();

            var world = map.getElementByType("world");
            if (world != null)
            {
                if (world.hasElementData("time"))
                {
                    var time = world.getElementData<TimeSpan>("time");
                    PublicAPI.setTime(time.Hours, time.Minutes);
                }

                if (world.hasElementData("weather")) // TODO: Change to integer w/ an array of possible weathers
                {
                    PublicAPI.setWeather(world.getElementData<int>("weather"));
                }
            }

            var props = map.getElementsByType("prop");
            foreach (var prop in props)
            {
                if (prop.hasElementData("quatX"))
                {
                    var ent = PublicAPI.createObject(prop.getElementData<int>("model"),
                        new Vector3(prop.getElementData<float>("posX"), prop.getElementData<float>("posY"),
                            prop.getElementData<float>("posZ")),
                        new Quaternion(prop.getElementData<float>("quatX"), prop.getElementData<float>("quatY"),
                            prop.getElementData<float>("quatZ"), prop.getElementData<float>("quatW")), dimension);
                    res.MapEntities.Add(ent);
                }
                else
                {
                    var ent = PublicAPI.createObject(prop.getElementData<int>("model"),
                        new Vector3(prop.getElementData<float>("posX"), prop.getElementData<float>("posY"),
                            prop.getElementData<float>("posZ")),
                        new Vector3(prop.getElementData<float>("rotX"), prop.getElementData<float>("rotY"),
                            prop.getElementData<float>("rotZ")), dimension);
                    res.MapEntities.Add(ent);
                }
            }

            var vehicles = map.getElementsByType("vehicle");
            foreach (var vehicle in vehicles)
            {
                var ent = PublicAPI.createVehicle((VehicleHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")), vehicle.getElementData<int>("color1"),
                    vehicle.getElementData<int>("color2"), dimension);
                res.MapEntities.Add(ent);
            }

            var pickups = map.getElementsByType("pickup");
            foreach (var vehicle in pickups)
            {
                var ent = PublicAPI.createPickup((PickupHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")), vehicle.getElementData<int>("amount"), vehicle.getElementData<uint>("respawn"), dimension);
                res.MapEntities.Add(ent);
            }

            var markers = map.getElementsByType("marker");
            foreach (var vehicle in markers)
            {
                var ent = PublicAPI.createMarker(vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("dirX"), vehicle.getElementData<float>("dirY"),
                        vehicle.getElementData<float>("dirZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")),
                    new Vector3(vehicle.getElementData<float>("scaleX"), vehicle.getElementData<float>("scaleY"),
                        vehicle.getElementData<float>("scaleZ")), vehicle.getElementData<int>("alpha"),
                    vehicle.getElementData<int>("red"), vehicle.getElementData<int>("green"), vehicle.getElementData<int>("blue"), dimension);
                res.MapEntities.Add(ent);
            }

            var blips = map.getElementsByType("blip");
            foreach (var vehicle in blips)
            {
                var ent = PublicAPI.createBlip(
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")), dimension);

                if (vehicle.hasElementData("sprite"))
                    PublicAPI.setBlipSprite(ent, vehicle.getElementData<int>("sprite"));
                if (vehicle.hasElementData("color"))
                    PublicAPI.setBlipColor(ent, vehicle.getElementData<int>("color"));
                if (vehicle.hasElementData("scale"))
                    PublicAPI.setBlipScale(ent, vehicle.getElementData<float>("scale"));
                if (vehicle.hasElementData("shortRange"))
                    PublicAPI.setBlipShortRange(ent, vehicle.getElementData<bool>("shortRange"));

                res.MapEntities.Add(ent);
            }

            var peds = map.getElementsByType("ped");
            foreach (var vehicle in peds)
            {
                var ent = PublicAPI.createPed((PedHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),vehicle.getElementData<float>("heading"), dimension);
                res.MapEntities.Add(ent);
            }

            var labels = map.getElementsByType("textlabel");
            foreach (var vehicle in labels)
            {
                var ent = PublicAPI.createTextLabel(vehicle.getElementData<string>("text"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")), vehicle.getElementData<float>("range"), vehicle.getElementData<float>("size"), dimension: dimension);
                res.MapEntities.Add(ent);
            }

            var neededInteriors = map.getElementsByType("ipl");
            foreach (var point in neededInteriors)
            {
                PublicAPI.requestIpl(point.getElementData<string>("name"));
            }

            var removedInteriors = map.getElementsByType("removeipl");
            foreach (var point in removedInteriors)
            {
                PublicAPI.removeIpl(point.getElementData<string>("name"));
            }
        }
        
        private IEnumerable<Script> InstantiateScripts(Assembly targetAssembly)
        {
            //var types = targetAssembly.GetExportedTypes();
            var types = targetAssembly.GetTypes();
            var validTypes = types.Where(t =>
                !t.IsInterface &&
                !t.IsAbstract)
                .Where(t => typeof(Script).IsAssignableFrom(t));
            var enumerable = validTypes as Type[] ?? validTypes.ToArray();

            if (!enumerable.Any()) yield break;

            foreach (var type in enumerable)
            {
                var obj = Activator.CreateInstance(type) as Script;
                if (obj != null) yield return obj;
            }
        }

        private IEnumerable<Script> CompileScript(string[] script, string[] references, bool vbBasic = false)
        {
            var provide = new CSharpCodeProvider();
            var vBasicProvider = new VBCodeProvider();

            var compParams = new CompilerParameters();

            compParams.ReferencedAssemblies.Add("System.Drawing.dll");
            compParams.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            compParams.ReferencedAssemblies.Add("System.IO.dll");
            compParams.ReferencedAssemblies.Add("System.Linq.dll");
            compParams.ReferencedAssemblies.Add("System.Core.dll");
            compParams.ReferencedAssemblies.Add("System.dll");
            compParams.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            compParams.ReferencedAssemblies.Add("GTANetworkServer.exe");
            compParams.ReferencedAssemblies.Add("GTANetworkShared.dll");

            foreach (var s in references)
            {
                compParams.ReferencedAssemblies.Add(File.Exists(AssemblyReferences[s]) ? AssemblyReferences[s] : s);
            }

            compParams.GenerateInMemory = true;
            compParams.GenerateExecutable = false;

            for (int s = 0; s < script.Length; s++)
                if (!vbBasic && script[s].TrimStart().StartsWith("public Constructor"))
                {
                    script[s] = string.Format(@"
using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;

namespace GTANResource
{{
    public class Constructor{1} : Script
    {{
        {0}
    }}
}}", script[s].Replace("Constructor(", "Constructor" + s + "("), s);
                }

            try
            {
                var results = !vbBasic
                    ? provide.CompileAssemblyFromSource(compParams, script)
                    : vBasicProvider.CompileAssemblyFromSource(compParams, script);

                if (results.Errors.HasErrors)
                {
                    var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    bool allWarns = true;
                    Program.Output("Error/warning while compiling script!", LogCat.Warn);
                    foreach (CompilerError error in results.Errors)
                    {
                        if (basePath != null)
                        {
                            Program.Output(
                                string.Format("{3} ({0}) at {4}:{2}: {1}",
                                    error.ErrorNumber,
                                    error.ErrorText,
                                    error.Line,
                                    error.IsWarning ? "Warning" : "Error",
                                    error.FileName), error.IsWarning ? LogCat.Warn : LogCat.Error);
                        }
                        Program.Output(String.Format("{3} ({0}) at {2}: {1}", error.ErrorNumber, error.ErrorText, error.Line, error.IsWarning ? "Warning" : "Error"));

                        allWarns = allWarns && error.IsWarning;
                    }


                    if (!allWarns)
                        return null;
                }

                var asm = results.CompiledAssembly;
                return InstantiateScripts(asm);
            }
            catch (Exception ex)
            {
                Program.Output("Error while compiling assembly!", LogCat.Error);
                Program.Output(ex.Message, LogCat.Error);
                Program.Output(ex.StackTrace);

                Program.Output(ex.Source);
                return null;
            }
        }

        private void UpdateAttachables(int root)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(root);

            if (prop?.Attachables == null) return;

            foreach (var attachable in prop.Attachables)
            {
                // TODO: Proper position with offsets
                var attachableProp = NetEntityHandler.NetToProp<EntityProperties>(attachable);

                if (attachableProp == null) continue;

                attachableProp.Position = prop.Position;

                UpdateAttachables(attachable);
            }
        }

        public Client GetClientFromName(string name)
        {
            //return Clients.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());
            return Clients.FirstOrDefault(c => c.Name.ToLower().StartsWith(name.ToLower()));
        }

        public void Tick()
        {
            if (IsClosing)
            {
                Streamer.Stop = true;

                try
                {
                    lock (RunningResources)
                    {
                        for (int i = RunningResources.Count - 1; i >= 0; i--)
                        {
                            StopResource(RunningResources[i].DirectoryName);
                        }
                    }

                    for (int i = Clients.Count - 1; i >= 0; i--)
                    {
                        if (!Clients[i].Fake) Clients[i].NetConnection.Disconnect("Server shutdown.");
                    }

                    ColShapeManager.Shutdown();
                    //FileServer.Dispose(); //Causes nullref on server termination
                    if (UseUPnP) Server.UPnP?.DeleteForwardingRule(Port);
                }
                catch(Exception e) { Program.Output(e.ToString()); }

                ReadyToClose = true;
                return;
            }

            if (Downloads.Count > 0)
            {
                for (int i = Downloads.Count - 1; i >= 0; i--)
                {
                    if (Downloads[i].Files.Count > 0)
                    {
                        if (!Downloads[i].Parent.NetConnection.CanSendImmediately(NetDeliveryMethod.ReliableOrdered, (int) ConnectionChannel.FileTransfer)) continue;
                        if (!Downloads[i].Files[0].HasStarted)
                        {
                            var notifyObj = new DataDownloadStart
                            {
                                FileType = (byte) Downloads[i].Files[0].Type,
                                ResourceParent = Downloads[i].Files[0].Resource,
                                FileName = Downloads[i].Files[0].Name,
                                Id = Downloads[i].Files[0].Id,
                                Length = Downloads[i].Files[0].Data.Length,
                                Md5Hash = Downloads[i].Files[0].Hash
                            };
                            SendToClient(Downloads[i].Parent, notifyObj, PacketType.FileTransferRequest, true, ConnectionChannel.FileTransfer);
                            Downloads[i].Files[0].HasStarted = true;
                        }

                        if (!Downloads[i].Files[0].Accepted) continue;

                        var remaining = Downloads[i].Files[0].Data.Length - Downloads[i].Files[0].BytesSent;
                        var sendBytes = (remaining > Downloads[i].ChunkSize
                            ? Downloads[i].ChunkSize
                            : (int) remaining);

                        var updateObj = Server.CreateMessage();
                        updateObj.Write((byte) PacketType.FileTransferTick);
                        updateObj.Write(Downloads[i].Files[0].Id);
                        updateObj.Write(sendBytes);
                        updateObj.Write(Downloads[i].Files[0].Data, (int)Downloads[i].Files[0].BytesSent, sendBytes);
                        Downloads[i].Files[0].BytesSent += sendBytes;

                        Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);

                        if (remaining - sendBytes > 0) continue;

                        var endObject = Server.CreateMessage();
                        endObject.Write((byte)PacketType.FileTransferComplete);
                        endObject.Write(Downloads[i].Files[0].Id);

                        Server.SendMessage(endObject, Downloads[i].Parent.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);
                        Downloads[i].Files.RemoveAt(0);
                    }
                    else
                    {
                        Downloads.RemoveAt(i);
                    }
                }
            }

            ProcessMessages();

            NetEntityHandler.UpdateMovements();

            PickupManager.Pulse();

            UnoccupiedVehicleManager.Pulse();

            lock (RunningResources)
            {
                //RunningResources.ForEach(fs => fs.Engines.ForEach(en => { en.InvokeUpdate(); }));

                for (int i = RunningResources.Count - 1; i >= 0; i--)
                {
                    for (int j = RunningResources[i].Engines.Count - 1; j >= 0; j--)
                    {
                        RunningResources[i].Engines[j].InvokeUpdate();
                    }

                    if (!RunningResources[i].Engines.Any(en => en.HasTerminated)) continue;
                    Program.Output("TERMINATING RESOURCE " + RunningResources[i].DirectoryName + " BECAUSE AN ENGINE HAS BEEN TERMINATED.");
                    RunningResources.RemoveAt(i);
                }
            }

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 2)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

            lock (queue)
            {
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    if (DateTime.Now.Subtract(queue.ElementAt(i).Value).TotalSeconds >= 60)
                    {
                        queue.Remove(queue.ElementAt(i).Key);
                    }
                }
            }

            lock (Clients)
            {
                for (var i = Clients.Count - 1; i >= 0; i--) // Kick AFK players
                {
                    var time = Clients[i].LastUpdate != default(DateTime) ? DateTime.Now.Subtract(Clients[i].LastUpdate).TotalSeconds : 0;

                    if (time > 70)
                    {
                        Clients.Remove(Clients[i]);
                    }
                    else if (time > 10)
                    {
                        Clients[i].NetConnection.Disconnect("Timed out.");
                        //DisconnectClient(Clients[i], "Timeout");
                    }

                }
            }
        }

        private void HandleSyncEvent(Client sender, SyncEvent data)
        {
            var args = DecodeArgumentList(data.Arguments?.ToArray()).ToList();

            switch ((SyncEventType) data.EventType)
            {
                case SyncEventType.DoorStateChange:
                {
                    var doorId = (int) args[1];
                    var newFloat = (bool) args[2];
                    if (NetEntityHandler.ToDict().ContainsKey((int) args[0]))
                    {
                        if (newFloat)
                            ((VehicleProperties) NetEntityHandler.ToDict()[(int) args[0]]).Doors |= (byte)(1 << doorId);
                        else
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Doors &= (byte)(~(1 << doorId));
                    }
                }
                    break;
                case SyncEventType.TrailerDeTach:
                {
                    var newState = (bool) args[0];
                    if (!newState)
                    {
                        if (NetEntityHandler.ToDict().ContainsKey((int) args[1]))
                        {
                            if (
                                NetEntityHandler.ToDict()
                                    .ContainsKey((NetEntityHandler.NetToProp<VehicleProperties>((int) args[1])).Trailer))
                                ((VehicleProperties)
                                    NetEntityHandler.ToDict()[
                                        NetEntityHandler.NetToProp<VehicleProperties>((int) args[1]).Trailer])
                                    .TraileredBy = 0;

                            ((VehicleProperties) NetEntityHandler.ToDict()[(int) args[1]]).Trailer = 0;
                        }
                    }
                    else
                    {
                        if (NetEntityHandler.ToDict().ContainsKey((int)args[1]))
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[1]]).Trailer = (int)args[2];

                        if (NetEntityHandler.ToDict().ContainsKey((int)args[2]))
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[2]]).TraileredBy = (int)args[1];
                    }

                    lock (RunningResources)
                        RunningResources.ForEach(
                            fs => fs.Engines.ForEach(en =>
                            {
                                en.InvokeVehicleTrailerChange(new NetHandle((int)args[1]), (bool)args[0] ? new NetHandle((int)args[2]) : new NetHandle());
                            }));
                        break;
                }
                case SyncEventType.TireBurst:
                {
                    var veh = (int)args[0];
                    var tireId = (int)args[1];
                    var isBursted = (bool)args[2];
                    if (NetEntityHandler.ToDict().ContainsKey(veh))
                    {
                        var oldValue = (((VehicleProperties) NetEntityHandler.ToDict()[(int) args[0]]).Tires &
                                        (byte) (1 << tireId)) != 0;

                        if (isBursted)
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Tires |= (byte)(1 << tireId);
                        else
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Tires &= (byte)(~(1 << tireId));

                        if (oldValue ^ isBursted)
                        {
                            lock (RunningResources)
                            RunningResources.ForEach(
                                fs => fs.Engines.ForEach(en =>
                                {
                                    en.InvokeVehicleTyreBurst(new NetHandle((int) args[0]), tireId);
                                }));
                        }
                    }
                    break;
                }
                case SyncEventType.PickupPickedUp:
                {
                    var pickupId = (int) args[0];

                    if (NetEntityHandler.ToDict().ContainsKey(pickupId))
                    {
                        if (!((PickupProperties) NetEntityHandler.ToDict()[pickupId]).PickedUp)
                        {
                            ((PickupProperties) NetEntityHandler.ToDict()[pickupId]).PickedUp = true;
                            lock (RunningResources)
                            {
                                RunningResources.ForEach(res => res.Engines.ForEach(en => en.InvokePlayerPickup(sender, new NetHandle(pickupId))));
                            }
                            if (((PickupProperties)NetEntityHandler.ToDict()[pickupId]).RespawnTime > 0)
                                PickupManager.Add(pickupId);

                            if (
                                PickupToWeapon.Translate(
                                    ((PickupProperties) NetEntityHandler.ToDict()[pickupId]).ModelHash) != 0)
                            {
                                    var wh = (WeaponHash)PickupToWeapon.Translate(((PickupProperties)NetEntityHandler.ToDict()[pickupId]).ModelHash);
                                    if (!sender.Weapons.ContainsKey(wh))
                                    {
                                        sender.Weapons.Add(wh, ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).Amount);
                                    }
                                    else
                                    {
                                        PublicAPI.setPlayerWeaponAmmo(sender, wh, sender.getWeaponAmmo(wh) + ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).Amount);
                                    }
                            }
                        }
                    }
                    break;
                }
                case SyncEventType.StickyBombDetonation:
                {
                    var playerId = (int) args[0];
                    var c = PublicAPI.getPlayerFromHandle(new NetHandle(playerId));

                    lock (RunningResources)
                            RunningResources.ForEach(
                                fs => fs.Engines.ForEach(en =>
                                {
                                    en.InvokePlayerDetonateStickies(c);
                                }));
                }
                    break;
            }
        }

        public static IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                var argument = arg as IntArgument;
                if (argument != null)
                {
                    list.Add(argument.Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                var argument = arg as IntArgument;
                if (argument != null)
                {
                    list.Add(argument.Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new GTANetworkShared.Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg is EntityArgument)
                {
                    list.Add(new NetHandle(((EntityArgument) arg).NetHandle));
                }
                else if (arg is ListArgument)
                {
                    var output = new List<object>();
                    var larg = (ListArgument)arg;
                    output.AddRange(DecodeArgumentListPure(larg.Data.ToArray()));
                    list.Add(output);
                }
            }

            return list;
        }

        public object DeserializeBinary<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    return Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException e)
                {
                    if(LogLevel > 0) Program.Output("WARN: Deserialization failed: " + e.Message);
                    return null;
                }
            }
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        //public byte GetChannelIdForConnection(Client conn)
        //{
            //lock (Clients) return (byte)(((Clients.IndexOf(conn)) % 31) + 1);
        //}

        public static NativeArgument ParseReturnType(Type t)
        {
            if (t == typeof(int))
            {
                return new IntArgument();
            }
            else if (t == typeof(uint))
            {
                return new UIntArgument();
            }
            else if (t == typeof(string))
            {
                return new StringArgument();
            }
            else if (t == typeof(float))
            {
                return new FloatArgument();
            }
            else if (t == typeof(double))
            {
                return new FloatArgument();
            }
            else if (t == typeof(bool))
            {
                return new BooleanArgument();
            }
            else if (t == typeof(Vector3))
            {
                return new Vector3Argument();
            }
            else
            {
                return null;
            }
        }

        public static List<NativeArgument> ParseNativeArguments(params object[] args)
        {
            var list = new List<NativeArgument>();
            foreach (var o in args)
            {
                if (o is int)
                {
                    list.Add(new IntArgument() { Data = ((int)o) });
                }
                else if (o is uint)
                {
                    list.Add(new UIntArgument() { Data = ((uint)o) });
                }
                else if (o is string)
                {
                    list.Add(new StringArgument() { Data = ((string)o) });
                }
                else if (o is float)
                {
                    list.Add(new FloatArgument() { Data = ((float)o) });
                }
                else if (o is double)
                {
                    list.Add(new FloatArgument() { Data = ((float)(double)o) });
                }
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is Vector3)
                {
                    var tmp = (Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
                }
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
                }
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
                else if (o is EntityArgument)
                {
                    list.Add((EntityArgument)o);
                }
                else if (o is EntityPointerArgument)
                {
                    list.Add((EntityPointerArgument) o);
                }
                else if (o is NetHandle)
                {
                    list.Add(new EntityArgument(((NetHandle) o).Value));
                }
                else if (o is Entity)
                {
                    list.Add(new EntityArgument(((Entity)o).Value));
                }
                else if (o is Client)
                {
                    list.Add(new EntityArgument(((Client) o).handle.Value));
                }
                else if (o is IList)
                {
                    var larg = new ListArgument();
                    var l = ((IList) o);
                    var array = new object[l.Count];
                    l.CopyTo(array, 0);
                    larg.Data = new List<NativeArgument>(ParseNativeArguments(array));
                    list.Add(larg);
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        private uint _nativeCount = 0;
        public object ReturnNativeCallFromPlayer(Client player, bool safe, ulong hash, NativeArgument returnType, params object[] args)
        {
            _nativeCount++;
            object output = null;
            GetNativeCallFromPlayer(player, safe, _nativeCount, hash, returnType, o =>
            {
                output = o;
            }, args);

            var start = DateTime.Now;
            while (output == null && DateTime.Now.Subtract(start).Milliseconds < 10000)
            {}
            
            return output;
        }


        public void TransferLargeString(Client target, string data, string resourceSource)
        {
            if (target == null || string.IsNullOrEmpty(data)) return;
            var bytes = Encoding.UTF8.GetBytes(data);

            var r = new Random();

            var mapData = new StreamedData
            {
                Id = r.Next(int.MaxValue),
                Data = bytes,
                Type = FileType.CustomData,
                Resource = resourceSource,
                Name = "data"
            };

            lock (Downloads)
            {
                StreamingClient cl;
                if ((cl = Downloads.FirstOrDefault(c => c.Parent == target)) != null)
                {
                    cl.Files.Add(mapData);
                }
                else
                {
                    var downloader = new StreamingClient(target);
                    downloader.Files.Add(mapData);

                    Downloads.Add(downloader);
                }
            }
        }

        public void CreatePositionInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement
            {
                ServerStartTime = Program.GetTicks(),
                Duration = duration,
                StartVector = prop.Position,
                EndVector = target,
                Start = 0
            };
            prop.PositionMovement = mov;

            var delta = new Delta_EntityProperties {PositionMovement = mov};
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public void CreateRotationInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement
            {
                ServerStartTime = Program.GetTicks(),
                Duration = duration,
                StartVector = prop.Position,
                EndVector = target,
                Start = 0
            };
            prop.RotationMovement = mov;

            var delta = new Delta_EntityProperties {RotationMovement = mov};
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public bool SetEntityProperty(int entity, string key, object value, bool world = false)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return false;

            if (prop.SyncedProperties == null) prop.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            var oldValue = DecodeArgumentListPure(prop.SyncedProperties.Get(key)).FirstOrDefault();

            prop.SyncedProperties.Set(key, nativeArg);

            var delta = new Delta_EntityProperties
            {
                SyncedProperties = new Dictionary<string, NativeArgument> {{key, nativeArg}}
            };
            UpdateEntityInfo(entity, world ? EntityType.World : EntityType.Prop, delta);

            var ent = new NetHandle(entity);

            lock (RunningResources)
                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                {
                    en.InvokeEntityDataChange(ent, key, oldValue);
                }));

            return true;
        }

        public void ResetEntityProperty(int entity, string key, bool world = false)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return;

            prop.SyncedProperties.Remove(key);

            var delta = new Delta_EntityProperties
            {
                SyncedProperties = new Dictionary<string, NativeArgument> {{key, new LocalGamePlayerArgument()}}
            };
            UpdateEntityInfo(entity, world ? EntityType.World : EntityType.Prop, delta);
        }

        public bool HasEntityProperty(int entity, string key)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key) || prop.SyncedProperties == null) return false;

            return prop.SyncedProperties.ContainsKey(key);
        }

        public dynamic GetEntityProperty(int entity, string key)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return null;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return null;

            var natArg = prop.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public string[] GetEntityAllProperties(int entity)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return new string[0];

            if (prop.SyncedProperties == null || prop.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            //return prop.SyncedProperties.Select(pair => DecodeArgumentListPure(pair.Value).Single().ToString()).ToArray(); //Returns all the values
            return prop.SyncedProperties.Select(pair => pair.Key).ToArray();
        }

       
    }
}
