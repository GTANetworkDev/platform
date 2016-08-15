using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using ProtoBuf;

namespace GTANetworkServer
{
    public class StreamingClient
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

    public class StreamedData
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
    
    public class GameServer
    {
        public GameServer(ServerSettings conf)
        {
            Clients = new List<Client>();
            Downloads = new List<StreamingClient>();
            RunningResources = new List<Resource>();
            CommandHandler = new CommandHandler();
            FileHashes = new Dictionary<string, string>();
            ExportedFunctions = new System.Dynamic.ExpandoObject();
            PickupManager = new PickupManager();
            UnoccupiedVehicleManager = new UnoccupiedVehicleManager();

            MaxPlayers = 32;
            Port = conf.Port;
            
            NetEntityHandler = new NetEntityHandler();

            ACLEnabled = conf.UseACL && File.Exists("acl.xml");

            if (ACLEnabled)
            {
                ACL = new AccessControlList("acl.xml");
            }
            
            Name = conf.Name;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            NetPeerConfiguration config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK");
            config.Port = conf.Port;
            config.EnableUPnP = conf.UseUPnP;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            
            Server = new NetServer(config);

            if (conf.UseUPnP)
            {
                try
                {
                    Server.UPnP.ForwardPort(conf.Port, "GTA Network Server");
                }
                catch (Exception ex)
                {
                    Program.Output("UNHANDLED EXCEPTION DURING UPNP PORT FORWARDING. YOUR ROUTER MAY NOT SUPPORT UPNP.");
                    Program.Output(ex.ToString());
                }
            }

            PasswordProtected = !string.IsNullOrWhiteSpace(conf.Password);
            Password = conf.Password;
            AnnounceSelf = conf.Announce;
            MasterServer = conf.MasterServer;
            MaxPlayers = conf.MaxPlayers;
            AnnounceToLAN = conf.AnnounceToLan;
            UseUPnP = conf.UseUPnP;
            MinimumClientVersion = ParseableVersion.Parse(conf.MinimumClientVersion);
            OnFootLagComp = conf.OnFootLagCompensation;
            VehLagComp = conf.VehicleLagCompensation;
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
        public Resource Gamemode { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }
        public bool AnnounceToLAN { get; set; }
        public AccessControlList ACL { get; set; }
        public bool IsClosing { get; set; }
        public bool ReadyToClose { get; set; }
        public bool ACLEnabled { get; set; }
        public bool UseUPnP { get; set; }
        public bool VehLagComp { get; set; }
        public bool OnFootLagComp { get; set; }

        public ColShapeManager ColShapeManager;
        public CommandHandler CommandHandler;
        public dynamic ExportedFunctions;
        public delegate dynamic ExportedFunctionDelegate(params object[] parameters);

        public List<Resource> RunningResources;
        public PickupManager PickupManager;
        public UnoccupiedVehicleManager UnoccupiedVehicleManager;
        public Thread StreamerThread;

        private Dictionary<string, string> FileHashes { get; set; }

        public NetEntityHandler NetEntityHandler { get; set; }

        public bool AllowDisplayNames { get; set; }

        public List<Resource> AvailableMaps;
        public Resource CurrentMap;

        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9;

        //private List<ClientsideScript> _clientScripts;
        private DateTime _lastAnnounceDateTime;

        public void Start(string[] filterscripts)
        {
            Server.Start();

            if (AnnounceSelf)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }
            
            Program.Output("Preloading maps...");

            AvailableMaps = new List<Resource>();

            foreach (var dir in Directory.GetDirectories("resources").Select(f => Path.GetFileName(f)))
            {
                var baseDir = "resource\\" + dir + "\\";

                if (!File.Exists(baseDir + "meta.xml"))
                    continue;

                var xmlSer = new XmlSerializer(typeof(ResourceInfo));
                ResourceInfo currentResInfo;
                using (var str = File.OpenRead(baseDir + "meta.xml"))
                    currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);

                if (currentResInfo.Info.Type != ResourceType.map) continue;
                var res = new Resource();
                res.DirectoryName = dir;
                res.Info = currentResInfo;
                AvailableMaps.Add(res);
             }

            Program.Output("Loading resources...");
            foreach (var path in filterscripts)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                try
                {
                    StartResource(path);
                }
                catch (Exception ex)
                {
                    Program.Output("Failed to load resource \"" + path + "\", error: " + ex.Message);
                }
            }

            NetEntityHandler.CreateWorld();
            ColShapeManager = new ColShapeManager();
            StreamerThread = new Thread(Streamer.MainThread);
            StreamerThread.IsBackground = true;
            StreamerThread.Start();
        }

        public void AnnounceSelfToMaster()
        {
            Program.Output("Announcing self to master server...");
            var annThread = new Thread((ThreadStart) delegate
            {
                using (var wb = new WebClient())
                {
                    try
                    {
                        wb.UploadData(MasterServer.Trim('/') + "/addserver", Encoding.UTF8.GetBytes(Port.ToString()));
                    }
                    catch (WebException)
                    {
                        Program.Output("Failed to announce self: master server is not available at this time.");
                    }
                }
            });
            annThread.IsBackground = true;
            annThread.Start();
        }

        private bool isIPLocal(string ipaddress)
        {
            String[] straryIPAddress = ipaddress.ToString().Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            int[] iaryIPAddress = new int[] { int.Parse(straryIPAddress[0]), int.Parse(straryIPAddress[1]), int.Parse(straryIPAddress[2]), int.Parse(straryIPAddress[3]) };
            if (iaryIPAddress[0] == 10 || (iaryIPAddress[0] == 192 && iaryIPAddress[1] == 168) || (iaryIPAddress[0] == 172 && (iaryIPAddress[1] >= 16 && iaryIPAddress[1] <= 31)))
            {
                return true;
            }
            else
            {
                // IP Address is "probably" public. This doesn't catch some VPN ranges like OpenVPN and Hamachi.
                return false;
            }
        }

        public List<ClientsideScript> GetAllClientsideScripts()
        {
            List<ClientsideScript> allScripts = new List<ClientsideScript>();

            lock (RunningResources)
            {
                foreach (var resource in RunningResources)
                {
                    allScripts.AddRange(resource.ClientsideScripts);
                }
            }

            return allScripts;
        }

        public bool StartResource(string resourceName, string father = null)
        {
            try
            {
                if (RunningResources.Any(res => res.DirectoryName == resourceName)) return false;

                Program.Output("Starting " + resourceName);

                if (!Directory.Exists("resources" + Path.DirectorySeparatorChar + resourceName))
                    throw new FileNotFoundException("Resource does not exist.");

                var baseDir = "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar;

                if (!File.Exists(baseDir + "meta.xml")) 
                    throw new FileNotFoundException("meta.xml has not been found.");

                var xmlSer = new XmlSerializer(typeof(ResourceInfo));
                ResourceInfo currentResInfo;
                using (var str = File.OpenRead(baseDir + "meta.xml"))
                    currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);
                
                var ourResource = new Resource();
                ourResource.Info = currentResInfo;
                ourResource.DirectoryName = resourceName;
                ourResource.Engines = new List<ScriptingEngine>();
                ourResource.ClientsideScripts = new List<ClientsideScript>();

                if (ourResource.Info.Info.Type == ResourceType.gamemode)
                {
                    if (Gamemode != null)
                        StopResource(Gamemode.DirectoryName);
                    Gamemode = ourResource;
                }

                if (currentResInfo.ResourceACL != null && ACLEnabled)
                {
                    var aclHead = AccessControlList.ParseXml("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + currentResInfo.ResourceACL.Path);
                    ACL.MergeACL(aclHead);
                }
                
                if (currentResInfo.Includes != null)
                    foreach (var resource in currentResInfo.Includes)
                    {
                        if (string.IsNullOrWhiteSpace(resource.Resource) || resource.Resource == father) continue;
                        StartResource(resource.Resource, resourceName);
                    }

                foreach (var filePath in currentResInfo.Files)
                {
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + filePath.Path))
                    {
                        var myData = md5.ComputeHash(stream);

                        var keyName = ourResource.DirectoryName + "_" + filePath.Path;

                        if (FileHashes.ContainsKey(keyName))
                            FileHashes[keyName] = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
                        else
                            FileHashes.Add(keyName, myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right));
                    }
                }

                var csScripts = new List<ClientsideScript>();

                var cSharp = new List<string>();
                var vBasic = new List<string>();

                foreach (var script in currentResInfo.Scripts)
                {
                    if (script.Language == ScriptingEngineLanguage.javascript)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        if (script.Type == ScriptType.client)
                        {
                            var csScript = new ClientsideScript()
                            {
                                ResourceParent = resourceName,
                                Script = scrTxt,
                                //Filename = Path.GetFileNameWithoutExtension(script.Path)?.Replace('.', '_'),
                                Filename = script.Path,
                            };


                            using (var md5 = MD5.Create())
                            { 
                                var myData = md5.ComputeHash(Encoding.UTF8.GetBytes(scrTxt));
                                var scriptHash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
                                csScript.MD5Hash = scriptHash;

                                if (FileHashes.ContainsKey(ourResource.DirectoryName + "_" + script.Path))
                                    FileHashes[ourResource.DirectoryName + "_" + script.Path] = scriptHash;
                                else
                                    FileHashes.Add(ourResource.DirectoryName + "_" + script.Path, scriptHash);
                            }

                            ourResource.ClientsideScripts.Add(csScript);
                            csScripts.Add(csScript);
                            continue;
                        }
                    }
                    else if (script.Language == ScriptingEngineLanguage.compiled)
                    {
                        try
                        {
                            Program.DeleteFile(baseDir + script.Path + ":Zone.Identifier");
                        }
                        catch
                        {
                        }

                        var ass = Assembly.LoadFrom(baseDir + script.Path);
                        var instances = InstantiateScripts(ass);
                        ourResource.Engines.AddRange(instances.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, ourResource.Info.Info.Multithreaded)));
                    }
                    else if (script.Language == ScriptingEngineLanguage.csharp)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        cSharp.Add(scrTxt);                        
                    }
                    else if (script.Language == ScriptingEngineLanguage.vbasic)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        vBasic.Add(scrTxt);                        
                    }
                }

                if (cSharp.Count > 0)
                {
                    var csharpAss = CompileScript(cSharp.ToArray(), currentResInfo.Referenceses.Select(r => r.Name).ToArray(), false);
                    ourResource.Engines.AddRange(csharpAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, ourResource.Info.Info.Multithreaded)));
                }

                if (vBasic.Count > 0)
                {
                    var vbasicAss = CompileScript(vBasic.ToArray(), currentResInfo.Referenceses.Select(r => r.Name).ToArray(), true);
                    ourResource.Engines.AddRange(vbasicAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, ourResource.Info.Info.Multithreaded)));
                }

                CommandHandler.Register(ourResource);

                foreach (var engine in ourResource.Engines)
                {
                    engine.InvokeResourceStart();
                }

                var randGen = new Random();
                
                if (ourResource.ClientsideScripts.Count > 0 || currentResInfo.Files.Count > 0)
                foreach (var client in Clients)
                {
                    var downloader = new StreamingClient(client);

                    foreach (var file in currentResInfo.Files)
                    {
                        var fileData = new StreamedData();
                        fileData.Id = randGen.Next(int.MaxValue);
                        fileData.Type = FileType.Normal;
                        fileData.Data =
                            File.ReadAllBytes("resources" + Path.DirectorySeparatorChar +
                                                ourResource.DirectoryName +
                                                Path.DirectorySeparatorChar +
                                                file.Path);
                        fileData.Name = file.Path;
                        fileData.Resource = ourResource.DirectoryName;
                        fileData.Hash = FileHashes.ContainsKey(ourResource.DirectoryName + "_" + file.Path)
                            ? FileHashes[ourResource.DirectoryName + "_" + file.Path]
                            : null;

                        downloader.Files.Add(fileData);
                    }

                    foreach (var script in ourResource.ClientsideScripts)
                    {
                        var scriptData = new StreamedData();
                        scriptData.Id = randGen.Next(int.MaxValue);
                        scriptData.Data = Encoding.UTF8.GetBytes(script.Script);
                        scriptData.Type = FileType.Script;
                        scriptData.Resource = script.ResourceParent;
                        scriptData.Hash = script.MD5Hash;
                        scriptData.Name = script.Filename;
                        downloader.Files.Add(scriptData);
                    }

                    var endStream = new StreamedData();
                    endStream.Id = randGen.Next(int.MaxValue);
                    endStream.Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D };
                    endStream.Type = FileType.EndOfTransfer;
                    downloader.Files.Add(endStream);

                    Downloads.Add(downloader);
                }

                if (ourResource.Info.Map != null && !string.IsNullOrWhiteSpace(ourResource.Info.Map.Path))
                {
                    ourResource.Map = new XmlGroup();
                    ourResource.Map.Load("resources\\" + ourResource.DirectoryName +"\\" + ourResource.Info.Map.Path);

                    LoadMap(ourResource, ourResource.Map, ourResource.Info.Map.Dimension);

                    if (ourResource.Info.Info.Type == ResourceType.gamemode)
                    {
                        if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                        ourResource.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                    }
                    else if (ourResource.Info.Info.Type == ResourceType.map)
                    {
                        if (string.IsNullOrWhiteSpace(ourResource.Info.Info.Gamemodes))
                        {}
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length != 1 && Gamemode == null)
                        {}
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length == 1 && (Gamemode == null || !ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName)))
                        {
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            StartResource(ourResource.Info.Info.Gamemodes?.Split(',')[0]);

                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                        else if (Gamemode != null && ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName))
                        {
                            Program.Output("Starting map " + ourResource.DirectoryName + "!");
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                    }
                }

                if (ourResource.Info.ExportedFunctions != null)
                {
                    var gPool = ExportedFunctions as IDictionary<string, object>;
                    dynamic resPool = new System.Dynamic.ExpandoObject();
                    var resPoolDict = resPool as IDictionary<string, object>;

                    foreach (var func in ourResource.Info.ExportedFunctions)
                    {
                        ScriptingEngine engine;
                        if (string.IsNullOrEmpty(func.Path))
                            engine = ourResource.Engines.SingleOrDefault();
                        else
                            engine = ourResource.Engines.FirstOrDefault(en => en.Filename == func.Path);

                        if (engine == null) continue;
                        ExportedFunctionDelegate punchthrough = parameters => engine.InvokeMethod(func.Name, parameters);
                        resPoolDict.Add(func.Name, punchthrough);
                    }

                    gPool.Add(ourResource.DirectoryName, resPool);
                }

                lock (RunningResources) RunningResources.Add(ourResource);
                Program.Output("Resource " + ourResource.DirectoryName + " started!");
                return true;
            }
            catch (Exception ex)
            {
                Program.Output("ERROR STARTING RESOURCE " + resourceName);
                Program.Output(ex.ToString());
                return false;
            }
        }

        public bool StopResource(string resourceName, Resource[] resourceParent = null)
        {
            Resource ourRes;
            lock (RunningResources)
            {
                ourRes = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName);
                if (ourRes == null) return false;

                Program.Output("Stopping " + resourceName);

                var dependencies =
                    RunningResources.Where(r => r.Info.Includes.Any(i => i.Resource == resourceName))
                        .Except(resourceParent ?? new Resource[0]);
                foreach (var res in dependencies)
                {
                    StopResource(res.DirectoryName, dependencies.ToArray());
                }

                RunningResources.Remove(ourRes);
            }

            ourRes.Engines.ForEach(en => en.InvokeResourceStop());

            var msg = Server.CreateMessage();
            msg.Write((byte) PacketType.StopResource);
            msg.Write(resourceName);
            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            if (Gamemode == ourRes)
            {
                if (CurrentMap != null && CurrentMap != ourRes)
                {
                    StopResource(CurrentMap.DirectoryName);
                    CurrentMap = null;
                }
                    
                Gamemode = null;
            }

            if (ourRes.MapEntities != null)
            foreach (var entity in ourRes.MapEntities)
            {
                PublicAPI.deleteEntity(entity);
            }

            if (CurrentMap == ourRes) CurrentMap = null;

            var gPool = ExportedFunctions as IDictionary<string, object>;
            if (gPool.ContainsKey(ourRes.DirectoryName)) gPool.Remove(ourRes.DirectoryName);
            CommandHandler.Unregister(ourRes.DirectoryName);
                

            Program.Output("Stopped " + resourceName + "!");
            return true;
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
                    PublicAPI.setWeather(world.getElementData<string>("weather"));
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
            var types = targetAssembly.GetExportedTypes();
            var validTypes = types.Where(t =>
                !t.IsInterface &&
                !t.IsAbstract)
                .Where(t => typeof(Script).IsAssignableFrom(t));
            if (!validTypes.Any())
            {
                yield break;
            }
            foreach (var type in validTypes)
            {
                var obj = Activator.CreateInstance(type) as Script;
                if (obj != null)
                    yield return obj;
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
            compParams.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            compParams.ReferencedAssemblies.Add("GTANetworkServer.exe");
            compParams.ReferencedAssemblies.Add("GTANetworkShared.dll");

            foreach (var s in references)
            {
                compParams.ReferencedAssemblies.Add(s);
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
                CompilerResults results;
                results = !vbBasic
                    ? provide.CompileAssemblyFromSource(compParams, script)
                    : vBasicProvider.CompileAssemblyFromSource(compParams, script);

                if (results.Errors.HasErrors)
                {
                    bool allWarns = true;
                    Program.Output("Error/warning while compiling script!");
                    foreach (CompilerError error in results.Errors)
                    {
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
                Program.Output("Error while compiling assembly!");
                Program.Output(ex.Message);
                Program.Output(ex.StackTrace);

                Program.Output(ex.Source);
                return null;
            }
        }

        public void UpdateEntityInfo(int netId, EntityType entity, Delta_EntityProperties newInfo, Client exclude = null)
        {
            var packet = new UpdateEntity();
            packet.EntityType = (byte)entity;
            packet.Properties = newInfo;
            packet.NetHandle = netId;
            if (exclude == null)
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, ConnectionChannel.NativeCall);
            else
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, exclude, ConnectionChannel.NativeCall);
        }

        private void ResendPacket(PedData fullPacket, Client exception, bool pure)
        {
            byte[] full = new byte[0];
            byte[] basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach(var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    if (client.Position.DistanceToSquared(fullPacket.Position) > 1000000f) // 1km
                    {
                        var lastUpdateReceived = client.LastPacketReceived.Get(exception.CharacterHandle.Value);

                        if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                        { 
                            msg.Write((byte) PacketType.BasicSync);
                            msg.Write(basic.Length);
                            msg.Write(basic);
                            Server.SendMessage(msg, client.NetConnection,
                                NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.BasicSync);

                            client.LastPacketReceived.Set(exception.CharacterHandle.Value, Program.GetTicks());
                        }
                    }
                    else
                    {
                        msg.Write((byte)PacketType.PedPureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.PureSync);
                    }
                }
                else
                {
                    msg.Write((byte)PacketType.PedLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.ReliableSequenced,
                        (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    var lastUpdateReceived = client.LastPacketReceived.Get(exception.CharacterHandle.Value);

                    if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                    {
                        msg.Write((byte)PacketType.BasicSync);
                        msg.Write(basic.Length);
                        msg.Write(basic);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.BasicSync);

                        client.LastPacketReceived.Set(exception.CharacterHandle.Value, Program.GetTicks());
                    }
                }
            }
        }

        private void ResendBulletPacket(int netHandle, Vector3 aim, bool shooting, Client exception)
        {
            byte[] full = new byte[0];

            full = PacketOptimization.WriteBulletSync(netHandle, shooting, aim);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;
                if (client.Position.DistanceToSquared(exception.Position) > 1000000f) continue; // 1km

                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((byte)PacketType.BulletSync);
                msg.Write(full.Length);
                msg.Write(full);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.ReliableSequenced,
                    (int)ConnectionChannel.BulletSync);
            }
        }

        private void ResendPacket(VehicleData fullPacket, Client exception, bool pure)
        {
            byte[] full = new byte[0];
            byte[] basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                if (PacketOptimization.CheckBit(fullPacket.Flag.Value, VehicleDataFlags.Driver))
                {
                    basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
                }
                else if (!exception.CurrentVehicle.IsNull)
                {
                    var carPos = NetEntityHandler.ToDict()[exception.CurrentVehicle.Value].Position;
                    basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, carPos);
                }
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    if (client.Position.DistanceToSquared(fullPacket.Position) > 1000000f) // 1 km
                    {
                        var lastUpdateReceived = client.LastPacketReceived.Get(exception.CharacterHandle.Value);

                        if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                        {
                            msg.Write((byte) PacketType.BasicSync);
                            msg.Write(basic.Length);
                            msg.Write(basic);
                            Server.SendMessage(msg, client.NetConnection,
                                NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.BasicSync);

                            client.LastPacketReceived.Set(exception.CharacterHandle.Value, Program.GetTicks());
                        }
                    }
                    else
                    {
                        msg.Write((byte)PacketType.VehiclePureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.PureSync);
                    }
                }
                else
                {
                    msg.Write((byte)PacketType.VehicleLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.ReliableSequenced,
                        (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    var lastUpdateReceived = client.LastPacketReceived.Get(exception.CharacterHandle.Value);

                    if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                    {
                        msg.Write((byte)PacketType.BasicSync);
                        msg.Write(basic.Length);
                        msg.Write(basic);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.BasicSync);

                        client.LastPacketReceived.Set(exception.CharacterHandle.Value, Program.GetTicks());
                    }
                }
            }
        }

        private void ResendUnoccupiedPacket(VehicleData fullPacket, Client exception)
        {
            byte[] full = new byte[0];
            byte[] basic = new byte[0];

            full = PacketOptimization.WriteUnOccupiedVehicleSync(fullPacket);
            basic = PacketOptimization.WriteBasicUnOccupiedVehicleSync(fullPacket);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (client.Position == null) continue;
                if (client.Position.DistanceToSquared(fullPacket.Position) < 250000) // 500 m
                {
                    msg.Write((byte)PacketType.UnoccupiedVehSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.UnreliableSequenced,
                        (int)ConnectionChannel.UnoccupiedVeh);
                }
                else
                {
                    msg.Write((byte)PacketType.BasicUnoccupiedVehSync);
                    msg.Write(basic.Length);
                    msg.Write(basic);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.UnreliableSequenced,
                        (int)ConnectionChannel.UnoccupiedVeh);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                NetOutgoingMessage msg = Server.CreateMessage();

                msg.Write((byte)PacketType.BasicUnoccupiedVehSync);
                msg.Write(basic.Length);
                msg.Write(basic);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.UnreliableSequenced,
                    (int)ConnectionChannel.UnoccupiedVeh);
            }
        }

        private void LogException(Exception ex, string resourceName)
        {
            Program.Output("RESOURCE EXCEPTION FROM " + resourceName + ": " + ex.Message);
            Program.Output(ex.StackTrace);
        }

        public void ProcessMessages()
        {
            List<NetIncomingMessage> messages = new List<NetIncomingMessage>();
            int msgsRead = Server.ReadMessages(messages);
            if (msgsRead > 0)
                foreach (var msg in messages)
                {
                    Client client = null;
                    lock (Clients)
                    {
                        foreach (Client c in Clients)
                        {
                            if (c != null && c.NetConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier != 0 &&
                                msg.SenderConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier == msg.SenderConnection.RemoteUniqueIdentifier)
                            {
                                client = c;
                                break;
                            }
                        }
                    }

                    if (client == null) client = new Client(msg.SenderConnection);


                    try
                    {
                        switch (msg.MessageType)
                        {
                            case NetIncomingMessageType.UnconnectedData:
                                var isPing = msg.ReadString();
                                if (isPing == "ping")
                                {
                                    Program.Output("INFO: ping received from " + msg.SenderEndPoint.Address.ToString());
                                    var pong = Server.CreateMessage();
                                    pong.Write("pong");
                                    Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                                }
                                if (isPing == "query")
                                {
                                    int playersonline = 0;
                                    lock (Clients) playersonline = Clients.Count;
                                    Program.Output("INFO: query received from " + msg.SenderEndPoint.Address.ToString());
                                    var pong = Server.CreateMessage();
                                    pong.Write(Name + "%" + PasswordProtected + "%" + playersonline + "%" + MaxPlayers + "%" +
                                                GamemodeName);
                                    Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                                }
                                break;
                            case NetIncomingMessageType.VerboseDebugMessage:
                            case NetIncomingMessageType.DebugMessage:
                            case NetIncomingMessageType.WarningMessage:
                            case NetIncomingMessageType.ErrorMessage:
                                Program.Output(msg.ReadString());
                                break;
                            case NetIncomingMessageType.ConnectionLatencyUpdated:
                                client.Latency = msg.ReadFloat();
                                break;
                            case NetIncomingMessageType.ConnectionApproval:
                                var type = msg.ReadByte();
                                var leng = msg.ReadInt32();
                                var connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                                if (connReq == null)
                                {
                                    client.NetConnection.Deny("Connection Object is null");
                                    continue;
                                }

                                var cVersion = ParseableVersion.FromLong(connReq.ScriptVersion);
                                if (cVersion < MinimumClientVersion)
                                {
                                    client.NetConnection.Deny("Outdated version. Please update your client.");
                                    continue;
                                }

                                int clients = 0;
                                lock (Clients) clients = Clients.Count;
                                if (clients <= MaxPlayers)
                                {
                                    if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                                    {
                                        if (Password != connReq.Password)
                                        {
                                            client.NetConnection.Deny("Wrong password.");
                                            Program.Output("Player connection refused: wrong password.");
                                            continue;
                                        }
                                    }

                                    lock (Clients)
                                    {
                                        int duplicate = 0;
                                        string displayname = connReq.DisplayName;
                                        while (AllowDisplayNames && Clients.Any(c => c.Name == connReq.DisplayName))
                                        {
                                            duplicate++;

                                            connReq.DisplayName = displayname + " (" + duplicate + ")";
                                        }

                                        Clients.Add(client);
                                    }

                                    client.CommitConnection();
                                    client.SocialClubName = connReq.SocialClubName;
                                    client.Name = AllowDisplayNames ? connReq.DisplayName : connReq.SocialClubName;
                                    client.RemoteScriptVersion = ParseableVersion.FromLong(connReq.ScriptVersion);
                                    client.GameVersion = connReq.GameVersion;
                                    ((PlayerProperties)NetEntityHandler.ToDict()[client.CharacterHandle.Value]).Name = client.Name;

                                    var respObj = new ConnectionResponse();

                                    respObj.CharacterHandle = client.CharacterHandle.Value;
                                    respObj.Settings = new SharedSettings()
                                    {
                                        OnFootLagCompensation = OnFootLagComp,
                                        VehicleLagCompensation = VehLagComp,
                                    };
                                    
                                    var channelHail = Server.CreateMessage();
                                    var respBin = SerializeBinary(respObj);

                                    channelHail.Write(respBin.Length);
                                    channelHail.Write(respBin);
                                    
                                    var cancelArgs = new CancelEventArgs();

                                    lock (RunningResources)
                                        RunningResources.ForEach(
                                            fs => fs.Engines.ForEach(en => en.InvokePlayerBeginConnect(client, cancelArgs)));

                                    if (cancelArgs.Cancel)
                                    {
                                        client.NetConnection.Deny(cancelArgs.Reason ?? "");
                                        Program.Output("Incoming connection denied: " + client.SocialClubName + " (" + client.Name + ")");
                                        continue;
                                    }
                                    else
                                    {
                                        client.NetConnection.Approve(channelHail);
                                        Program.Output("New incoming connection: " + client.SocialClubName + " (" + client.Name + ")");
                                    }
                                }
                                else
                                {
                                    client.NetConnection.Deny("Server is full");
                                    Program.Output("Player connection refused: server full.");
                                    continue;
                                }
                            break;
                        case NetIncomingMessageType.StatusChanged:
                                var newStatus = (NetConnectionStatus)msg.ReadByte();

                                if (newStatus == NetConnectionStatus.Connected)
                                {
                                }
                                else if (newStatus == NetConnectionStatus.Disconnected)
                                {
                                    var reason = msg.ReadString();

                                    if (Clients.Contains(client))
                                    {
                                        lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerDisconnected(client, reason);
                                                }));

                                        UnoccupiedVehicleManager.UnsyncAllFrom(client);

                                        lock (Clients)
                                        {
                                            var dcObj = new PlayerDisconnect()
                                            {
                                                Id = client.CharacterHandle.Value,
                                            };

                                            SendToAll(dcObj, PacketType.PlayerDisconnect, true, ConnectionChannel.EntityBackend);

                                            Program.Output("Player disconnected: " + client.SocialClubName + " (" +
                                                            client.Name + ")");
                                            
                                            Clients.Remove(client);
                                            NetEntityHandler.DeleteEntityQuiet(client.CharacterHandle.Value);
                                            if (ACLEnabled) ACL.LogOutClient(client);

                                            Downloads.RemoveAll(d => d.Parent == client);
                                        }
                                    }
                                }
                                break;
                            case NetIncomingMessageType.DiscoveryRequest:
                                NetOutgoingMessage response = Server.CreateMessage();
                                var obj = new DiscoveryResponse();
                                obj.ServerName = Name;
                                obj.MaxPlayers = (short)MaxPlayers;
                                obj.PasswordProtected = PasswordProtected;
                                lock (RunningResources)
                                {
                                    obj.Gamemode = string.IsNullOrEmpty(GamemodeName)
                                        ? RunningResources.FirstOrDefault(r => r.Info.Info.Type == ResourceType.gamemode)?
                                            .DirectoryName ?? "GTA Network"
                                        : GamemodeName;
                                }
                                lock (Clients)
                                    obj.PlayerCount =
                                        (short)
                                            Clients.Count;//(c => DateTime.Now.Subtract(c.LastUpdate).TotalMilliseconds < 60000);
                                obj.Port = Port;
                                obj.LAN = isIPLocal(msg.SenderEndPoint.Address.ToString());

                                if ((obj.LAN && AnnounceToLAN) || !obj.LAN)
                                {
                                    var bin = SerializeBinary(obj);

                                    response.Write((byte)PacketType.DiscoveryResponse);
                                    response.Write(bin.Length);
                                    response.Write(bin);

                                    Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                                }
                                break;
                            case NetIncomingMessageType.Data:
                                var packetType = (PacketType)msg.ReadByte();

                                switch (packetType)
                                {
                                    case PacketType.ChatData:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                                if (data != null)
                                                {
                                                    var pass = true;
                                                    var command = data.Message.StartsWith("/");

                                                    if (command)
                                                    {
                                                        if (ACLEnabled)
                                                        {
                                                            pass = ACL.DoesUserHaveAccessToCommand(client, data.Message.Split()[0].TrimStart('/'));
                                                        }

                                                        if (pass)
                                                        {
                                                            lock (RunningResources)
                                                                RunningResources.ForEach(
                                                                    fs =>
                                                                        fs.Engines.ForEach(
                                                                            en => en.InvokeChatCommand(client, data.Message)));
                                                            
                                                            if (!CommandHandler.Parse(client, data.Message))
                                                                PublicAPI.sendChatMessageToPlayer(client, "~r~ERROR:~w~ Command not found.");
                                                        }
                                                        else
                                                        {
                                                            var chatObj = new ChatData()
                                                            {
                                                                Sender = "",
                                                                Message = "You don't have access to this command!",
                                                            };

                                                            var binData = Program.ServerInstance.SerializeBinary(chatObj);

                                                            NetOutgoingMessage respMsg = Program.ServerInstance.Server.CreateMessage();
                                                            respMsg.Write((byte)PacketType.ChatData);
                                                            respMsg.Write(binData.Length);
                                                            respMsg.Write(binData);
                                                            client.NetConnection.SendMessage(respMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                                        }

                                                        continue;
                                                    }

                                                    lock (RunningResources)
                                                        RunningResources.ForEach(
                                                            fs =>
                                                                fs.Engines.ForEach(
                                                                    en =>
                                                                        pass =
                                                                            pass && en.InvokeChatMessage(client, data.Message)));

                                                    if (pass)
                                                    {
                                                        data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                        data.Sender = client.Name;
                                                        SendToAll(data, PacketType.ChatData, true, ConnectionChannel.Chat);
                                                        Program.Output(data.Sender + ": " + data.Message);
                                                    }
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.VehiclePureSync:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadPureVehicleSync(bin);
                                                
                                                fullPacket.NetHandle = client.CharacterHandle.Value;

                                                client.Health = fullPacket.PlayerHealth.Value;
                                                client.Armor = fullPacket.PedArmor.Value;
                                                client.LastUpdate = DateTime.Now;

                                                if (PacketOptimization.CheckBit(fullPacket.Flag.Value,
                                                    VehicleDataFlags.Driver))
                                                {
                                                    client.Position = fullPacket.Position;
                                                    client.Rotation = fullPacket.Quaternion;
                                                    client.Velocity = fullPacket.Velocity;

                                                    if (!client.CurrentVehicle.IsNull &&
                                                        NetEntityHandler.ToDict()
                                                            .ContainsKey(client.CurrentVehicle.Value))
                                                    {
                                                        NetEntityHandler.ToDict()[client.CurrentVehicle.Value].Position
                                                            = fullPacket.Position;
                                                        NetEntityHandler.ToDict()[client.CurrentVehicle.Value].Rotation
                                                            = fullPacket.Quaternion;
                                                        if (fullPacket.Flag.HasValue)
                                                        {
                                                            var newDead = (fullPacket.Flag &
                                                                           (byte) VehicleDataFlags.VehicleDead) > 0;
                                                            if (!((VehicleProperties)
                                                                NetEntityHandler.ToDict()[client.CurrentVehicle.Value])
                                                                .IsDead && newDead)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            en.InvokeVehicleDeath(client.CurrentVehicle);
                                                                        }));
                                                            }

                                                            ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[client.CurrentVehicle.Value])
                                                                .IsDead = newDead;
                                                        }

                                                        if (fullPacket.VehicleHealth.HasValue)
                                                            ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[client.CurrentVehicle.Value])
                                                                .Health = fullPacket.VehicleHealth.Value;
                                                        if (fullPacket.Flag.HasValue)
                                                            ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[client.CurrentVehicle.Value])
                                                                .Siren = (fullPacket.Flag &
                                                                          (byte) VehicleDataFlags.SirenActive) > 0;
                                                    }

                                                    if (NetEntityHandler.ToDict()
                                                        .ContainsKey(fullPacket.NetHandle.Value))
                                                    {
                                                        NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Position =
                                                            fullPacket.Position;
                                                        NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Rotation =
                                                            fullPacket.Quaternion;
                                                    }
                                                }
                                                else if (!client.CurrentVehicle.IsNull && NetEntityHandler.ToDict().ContainsKey(client.CurrentVehicle.Value))
                                                {
                                                    var carPos =
                                                        NetEntityHandler.ToDict()[client.CurrentVehicle.Value].Position;
                                                    var carRot =
                                                        NetEntityHandler.ToDict()[client.CurrentVehicle.Value].Rotation;

                                                    client.Position = carPos;
                                                    client.Rotation = carRot;

                                                    if (NetEntityHandler.ToDict()
                                                        .ContainsKey(fullPacket.NetHandle.Value))
                                                    {
                                                        NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Position =
                                                            carPos;
                                                        NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Rotation =
                                                            carRot;
                                                    }
                                                }
                                                client.IsInVehicle = true;

                                                ResendPacket(fullPacket, client, true);

                                                UpdateAttachables(client.CharacterHandle.Value);
                                                UpdateAttachables(client.CurrentVehicle.Value);
                                                //SendToAll(data, PacketType.VehiclePositionData, false, client, ConnectionChannel.PositionData);

                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.VehicleLightSync:
                                    {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadLightVehicleSync(bin);

                                                fullPacket.NetHandle = client.CharacterHandle.Value;
                                                fullPacket.Latency = client.Latency;

                                                client.IsInVehicle = true;
                                                client.VehicleSeat = fullPacket.VehicleSeat.Value;

                                                var car = new NetHandle(fullPacket.VehicleHandle.Value);

                                                if (!client.IsInVehicleInternal || client.VehicleHandleInternal != car.Value)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerEnterVehicle(client, car);
                                                        }));
                                                }

                                                client.IsInVehicleInternal = true;
                                                client.VehicleHandleInternal = car.Value;
                                                client.CurrentVehicle = car;


                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash =
                                                        fullPacket.PedModelHash.Value;
                                                }

                                                if (fullPacket.Trailer != null)
                                                {
                                                    var trailer =
                                                        ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .Trailer;
                                                    if (NetEntityHandler.ToDict().ContainsKey(trailer))
                                                    {
                                                        NetEntityHandler.ToDict()[trailer].Position = fullPacket.Trailer;
                                                    }
                                                }

                                                ResendPacket(fullPacket, client, false);
                                            }
                                            catch(IndexOutOfRangeException)
                                            { }
                                    }
                                        break;
                                    case PacketType.PedPureSync:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadPurePedSync(bin);

                                                fullPacket.NetHandle = client.CharacterHandle.Value;

                                                client.Health = fullPacket.PlayerHealth.Value;
                                                client.Armor = fullPacket.PedArmor.Value;
                                                client.Position = fullPacket.Position;
                                                client.LastUpdate = DateTime.Now;
                                                client.Rotation = fullPacket.Quaternion;
                                                client.Velocity = fullPacket.Velocity;

                                                if (client.IsInVehicleInternal && !client.CurrentVehicle.IsNull)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerExitVehicle(client, client.CurrentVehicle);
                                                        }));
                                                }

                                                client.IsInVehicleInternal = false;
                                                client.IsInVehicle = false;
                                                client.CurrentVehicle = new NetHandle(0);
                                                client.VehicleHandleInternal = 0;

                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Position = fullPacket.Position;
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Rotation = fullPacket.Quaternion;
                                                    //NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash = fullPacket.PedModelHash.HasValue ? fullPacket.PedModelHash.Value : 0;
                                                }

                                                ResendPacket(fullPacket, client, true);
                                                UpdateAttachables(client.CharacterHandle.Value);
                                                //SendToAll(data, PacketType.PedPositionData, false, client, ConnectionChannel.PositionData);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.PedLightSync:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadLightPedSync(bin);

                                                fullPacket.NetHandle = client.CharacterHandle.Value;
                                                fullPacket.Latency = client.Latency;

                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash =
                                                        fullPacket.PedModelHash.Value;
                                                }

                                                ResendPacket(fullPacket, client, false);
                                            }
                                            catch(IndexOutOfRangeException)
                                            { }
                                        }
                                        break;
                                    case PacketType.BulletSync:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                int netHandle;
                                                bool shooting;
                                                Vector3 aimPoint;

                                                shooting = PacketOptimization.ReadBulletSync(bin, out netHandle, out aimPoint);

                                                netHandle = client.CharacterHandle.Value;

                                                ResendBulletPacket(netHandle, aimPoint, shooting, client);
                                            }
                                            catch
                                            { }
                                        }
                                        break;
                                    case PacketType.UnoccupiedVehSync:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                for (int i = 0; i < bin[0]; i++)
                                                {
                                                    var cVehBin = bin.Skip(1 + 43*i).Take(43).ToArray();

                                                    var fullPacket = PacketOptimization.ReadUnoccupiedVehicleSync(cVehBin);

                                                    if (NetEntityHandler.ToDict()
                                                        .ContainsKey(fullPacket.VehicleHandle.Value))
                                                    {
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Position
                                                            = fullPacket.Position;
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Rotation
                                                            = fullPacket.Quaternion;

                                                        if (fullPacket.Flag.HasValue)
                                                        {
                                                            var newDead = (fullPacket.Flag &
                                                                           (byte) VehicleDataFlags.VehicleDead) > 0;
                                                            if (!((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                                .IsDead && newDead)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            en.InvokeVehicleDeath(new NetHandle(fullPacket.VehicleHandle.Value));
                                                                        }));
                                                            }

                                                            ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                                .IsDead = newDead;
                                                        }

                                                        if (fullPacket.VehicleHealth.HasValue)
                                                            ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                                .Health = fullPacket.VehicleHealth.Value;
                                                    }

                                                    ResendUnoccupiedPacket(fullPacket, client);

                                                    UpdateAttachables(fullPacket.VehicleHandle.Value);
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.NpcVehPositionData:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var data =
                                                    DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as
                                                        VehicleData;
                                                if (data != null)
                                                {
                                                    SendToAll(data, PacketType.NpcVehPositionData, false, client, ConnectionChannel.PositionData);
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.NpcPedPositionData:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var data =
                                                    DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                                if (data != null)
                                                {
                                                    SendToAll(data, PacketType.NpcPedPositionData, false, client, ConnectionChannel.PositionData);
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.SyncEvent:
                                        {
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                                            if (data != null)
                                            {
                                                SendToAll(data, PacketType.SyncEvent, true, client, ConnectionChannel.NativeCall);
                                                HandleSyncEvent(client, data);
                                            }

                                        }
                                        break;
                                    case PacketType.ScriptEventTrigger:
                                        {
                                            var len = msg.ReadInt32();
                                            var data =
                                                DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                                            if (data != null)
                                            {
                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        en =>
                                                            en.Engines.ForEach(fs =>
                                                            {
                                                                fs.InvokeClientEvent(client, data.EventName,
                                                                    DecodeArgumentListPure(data.Arguments?.ToArray() ?? new NativeArgument[0]).ToArray());
                                                            }
                                                                ));
                                            }
                                        }
                                        break;
                                    case PacketType.NativeResponse:
                                        {
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<NativeResponse>(msg.ReadBytes(len)) as NativeResponse;

                                            if (data == null || !_callbacks.ContainsKey(data.Id)) continue;
                                            object resp = null;
                                            if (data.Response is IntArgument)
                                            {
                                                resp = ((IntArgument)data.Response).Data;
                                            }
                                            else if (data.Response is UIntArgument)
                                            {
                                                resp = ((UIntArgument)data.Response).Data;
                                            }
                                            else if (data.Response is StringArgument)
                                            {
                                                resp = ((StringArgument)data.Response).Data;
                                            }
                                            else if (data.Response is FloatArgument)
                                            {
                                                resp = ((FloatArgument)data.Response).Data;
                                            }
                                            else if (data.Response is BooleanArgument)
                                            {
                                                resp = ((BooleanArgument)data.Response).Data;
                                            }
                                            else if (data.Response is Vector3Argument)
                                            {
                                                var tmp = (Vector3Argument)data.Response;
                                                resp = new Vector3()
                                                {
                                                    X = tmp.X,
                                                    Y = tmp.Y,
                                                    Z = tmp.Z,
                                                };
                                            }
                                            if (_callbacks.ContainsKey(data.Id))
                                                _callbacks[data.Id].Invoke(resp);
                                            _callbacks.Remove(data.Id);
                                        }
                                        break;
                                    case PacketType.FileAcceptDeny:
                                        {
                                            var fileId = msg.ReadInt32();
                                            var hasBeenAccepted = msg.ReadBoolean();
                                            var ourD = Downloads.FirstOrDefault(d => d.Parent == client);
                                            if (ourD != null && ourD.Files.Count > 0 && ourD.Files[0].Id == fileId &&
                                                !ourD.Files[0].Accepted)
                                            {
                                                if (!hasBeenAccepted)
                                                    ourD.Files.RemoveAt(0);
                                                else
                                                    ourD.Files[0].Accepted = true;
                                            }
                                        }
                                        break;
                                    case PacketType.ConnectionConfirmed:
                                        {
                                            var state = msg.ReadBoolean();
                                            if (!state)
                                            {
                                                var delta = new Delta_PlayerProperties();
                                                delta.Name = client.Name;
                                                UpdateEntityInfo(client.CharacterHandle.Value, EntityType.Player, delta, client);

                                                var mapObj = new ServerMap();
                                                mapObj.World =
                                                    Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1);

                                                foreach (var pair in NetEntityHandler.ToDict())
                                                {
                                                    if (pair.Value.EntityType == (byte)EntityType.Vehicle)
                                                    {
                                                        mapObj.Vehicles.Add(pair.Key, (VehicleProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Prop)
                                                    {
                                                        mapObj.Objects.Add(pair.Key, pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Blip)
                                                    {
                                                        mapObj.Blips.Add(pair.Key, (BlipProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Marker)
                                                    {
                                                        mapObj.Markers.Add(pair.Key, (MarkerProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Pickup)
                                                    {
                                                        if (!((PickupProperties)pair.Value).PickedUp)
                                                            mapObj.Pickups.Add(pair.Key, (PickupProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Player)
                                                    {
                                                        mapObj.Players.Add(pair.Key, (PlayerProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte) EntityType.TextLabel)
                                                    {
                                                        mapObj.TextLabels.Add(pair.Key, (TextLabelProperties) pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte) EntityType.Ped)
                                                    {
                                                        mapObj.Peds.Add(pair.Key, pair.Value);
                                                    }
                                                }

                                                // TODO: replace this filth
                                                var r = new Random();

                                                var mapData = new StreamedData();
                                                mapData.Id = r.Next(int.MaxValue);
                                                mapData.Data = SerializeBinary(mapObj);
                                                mapData.Type = FileType.Map;

                                                var downloader = new StreamingClient(client);
                                                downloader.Files.Add(mapData);

                                                foreach (var resource in RunningResources)
                                                {
                                                    foreach (var file in resource.Info.Files)
                                                    {
                                                        var fileData = new StreamedData();
                                                        fileData.Id = r.Next(int.MaxValue);
                                                        fileData.Type = FileType.Normal;
                                                        fileData.Data =
                                                            File.ReadAllBytes("resources" + Path.DirectorySeparatorChar +
                                                                                resource.DirectoryName +
                                                                                Path.DirectorySeparatorChar +
                                                                                file.Path);
                                                        fileData.Name = file.Path;
                                                        fileData.Resource = resource.DirectoryName;
                                                        fileData.Hash = FileHashes.ContainsKey(resource.DirectoryName + "_" + file.Path)
                                                            ? FileHashes[resource.DirectoryName + "_" + file.Path]
                                                            : null;
                                                        downloader.Files.Add(fileData);
                                                    }
                                                }

                                                foreach (var script in GetAllClientsideScripts())
                                                {
                                                    var scriptData = new StreamedData();
                                                    scriptData.Id = r.Next(int.MaxValue);
                                                    scriptData.Data = Encoding.UTF8.GetBytes(script.Script);
                                                    scriptData.Type = FileType.Script;
                                                    scriptData.Resource = script.ResourceParent;
                                                    scriptData.Hash = script.MD5Hash;
                                                    scriptData.Name = script.Filename;
                                                    downloader.Files.Add(scriptData);
                                                }


                                                var endStream = new StreamedData();
                                                endStream.Id = r.Next(int.MaxValue);
                                                endStream.Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D };
                                                endStream.Type = FileType.EndOfTransfer;
                                                downloader.Files.Add(endStream);
                                                Downloads.Add(downloader);
                                                
                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerConnected(client);
                                                        }));

                                                Program.Output("New player connected: " + client.SocialClubName + " (" +
                                                                client.Name + ")");
                                            }
                                            else
                                            {
                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerDownloadFinished(client);
                                                        }));
                                            }

                                            break;
                                        }
                                    case PacketType.PlayerKilled:
                                        {
                                            var reason = msg.ReadInt32();
                                            var weapon = msg.ReadInt32();

                                            lock (RunningResources)
                                            {
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerDeath(client, reason, weapon);
                                                }));
                                            }
                                        }
                                        break;
                                    case PacketType.PlayerRespawned:
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerRespawn(client);
                                                }));

                                        }
                                        break;
                                    case PacketType.UpdateEntityProperties:
                                        {
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                                            if (data != null && data.Properties != null)
                                            {
                                                var item = NetEntityHandler.NetToProp<EntityProperties>(data.NetHandle);

                                                if (item != null)
                                                {
                                                    if (data.Properties.SyncedProperties != null)
                                                    {
                                                        if (item.SyncedProperties == null) item.SyncedProperties = new Dictionary<string, NativeArgument>();
                                                        foreach (var pair in data.Properties.SyncedProperties)
                                                        {
                                                            if (pair.Value is LocalGamePlayerArgument)
                                                                item.SyncedProperties.Remove(pair.Key);
                                                            else
                                                            {
                                                                object oldValue =
                                                                    DecodeArgumentListPure(
                                                                        item.SyncedProperties.Get(pair.Key));
                                                                item.SyncedProperties.Set(pair.Key, pair.Value);
                                                                NetHandle ent = new NetHandle(data.NetHandle);
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                                    {
                                                                        en.InvokeEntityDataChange(ent, pair.Key, oldValue);
                                                                    }));
                                                            }
                                                        }
                                                    }
                                                }

                                                UpdateEntityInfo(data.NetHandle, (EntityType)data.EntityType, data.Properties, client);
                                            }

                                        }
                                        break;
                                }
                                break;
                            default:
                                Program.Output("WARN: Unhandled type: " + msg.MessageType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.Output("EXCEPTION IN MESSAGEPUMP");
                        Program.Output(ex.ToString());
                    }
                    finally
                    {
                        Server.Recycle(msg);
                    }
                }
        }

        private void UpdateAttachables(int root)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(root);

            if (prop == null || prop.Attachables == null) return;

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
            return Clients.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());
        }

        public void Tick()
        {
            if (IsClosing)
            {
                Streamer.Stop = true;

                for (int i = RunningResources.Count - 1; i >= 0; i--)
                {
                    StopResource(RunningResources[i].DirectoryName);
                }

                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    Clients[i].NetConnection.Disconnect("Server is shutting down");
                }

                ColShapeManager.Shutdown();

                if (UseUPnP) Server.UPnP?.DeleteForwardingRule(Port);

                ReadyToClose = true;
                return;
            }

            if (Downloads.Count > 0)
            {
                for (int i = Downloads.Count - 1; i >= 0; i--)
                {
                    if (Downloads[i].Files.Count > 0)
                    {
                        if (Downloads[i].Parent.NetConnection.CanSendImmediately(NetDeliveryMethod.ReliableOrdered,
                            (int)ConnectionChannel.FileTransfer))
                        {
                            if (!Downloads[i].Files[0].HasStarted)
                            {
                                var notifyObj = new DataDownloadStart();
                                notifyObj.FileType = (byte)Downloads[i].Files[0].Type;
                                notifyObj.ResourceParent = Downloads[i].Files[0].Resource;
                                notifyObj.FileName = Downloads[i].Files[0].Name;
                                notifyObj.Id = Downloads[i].Files[0].Id;
                                notifyObj.Length = Downloads[i].Files[0].Data.Length;
                                notifyObj.Md5Hash = Downloads[i].Files[0].Hash;
                                SendToClient(Downloads[i].Parent, notifyObj, PacketType.FileTransferRequest, true, ConnectionChannel.FileTransfer);
                                Downloads[i].Files[0].HasStarted = true;
                            }

                            if (!Downloads[i].Files[0].Accepted) continue;

                            var remaining = Downloads[i].Files[0].Data.Length - Downloads[i].Files[0].BytesSent;
                            int sendBytes = (remaining > Downloads[i].ChunkSize
                                ? Downloads[i].ChunkSize
                                : (int) remaining);

                            var updateObj = Server.CreateMessage();
                            updateObj.Write((byte) PacketType.FileTransferTick);
                            updateObj.Write(Downloads[i].Files[0].Id);
                            updateObj.Write(sendBytes);
                            updateObj.Write(Downloads[i].Files[0].Data, (int)Downloads[i].Files[0].BytesSent, sendBytes);
                            Downloads[i].Files[0].BytesSent += sendBytes;

                            Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection,
                                NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);

                            if (remaining - sendBytes <= 0)
                            {
                                var endObject = Server.CreateMessage();
                                endObject.Write((byte)PacketType.FileTransferComplete);
                                endObject.Write(Downloads[i].Files[0].Id);

                                Server.SendMessage(endObject, Downloads[i].Parent.NetConnection,
                                    NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);
                                Downloads[i].Files.RemoveAt(0);
                            }
                        }
                    }
                    else
                    {
                        Downloads.RemoveAt(i);
                    }
                }
            }

            ProcessMessages();

            NetEntityHandler.UpdateMovements();

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

            PickupManager.Pulse();

            UnoccupiedVehicleManager.Pulse();

            lock (RunningResources) RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
            {
                en.InvokeUpdate();
            }));

            lock (RunningResources)
            {
                for (int i = RunningResources.Count - 1; i >= 0; i--)
                {
                    if (RunningResources[i].Engines.Any(en => en.HasTerminated))
                    {
                        Program.Output("TERMINATING RESOURCE " + RunningResources[i].DirectoryName + " BECAUSE AN ENGINE HAS BEEN TERMINATED.");
                        RunningResources.RemoveAt(i);
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
                        ((VehicleProperties) NetEntityHandler.ToDict()[(int) args[0]]).Doors[doorId] = newFloat;
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
                    break;
                }
                case SyncEventType.TireBurst:
                {
                    var veh = (int)args[0];
                    var tireId = (int)args[1];
                    var isBursted = (bool)args[2];
                    if (NetEntityHandler.ToDict().ContainsKey(veh))
                        ((VehicleProperties)NetEntityHandler.ToDict()[veh]).Tires[tireId] = isBursted;
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
                            RunningResources.ForEach(res => res.Engines.ForEach(en => en.InvokePlayerPickup(sender, new NetHandle(pickupId))));
                            if (((PickupProperties)NetEntityHandler.ToDict()[pickupId]).RespawnTime > 0)
                                PickupManager.Add(pickupId);
                        }
                    }
                    break;
                }
            }
        }

        public IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
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

        public IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
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
                    List<object> output = new List<object>();
                    var larg = (ListArgument)arg;
                    output.AddRange(DecodeArgumentListPure(larg.Data.ToArray()));
                    list.Add(output);
                }
                else if (args == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public void SendToClient(Client c, object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendMessage(msg, c.NetConnection,
                important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.UnreliableSequenced,
                (int)channel);
        }

        public void SendToAll(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            lock (Clients)
            foreach (var client in Clients)
            {
                var data = SerializeBinary(newData);
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((byte)packetType);
                msg.Write(data.Length);
                msg.Write(data);
                Server.SendMessage(msg, client.NetConnection,
                    important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                    (int)channel);
            }
        }
        
        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude, ConnectionChannel channel)
        {
            lock (Clients)
            foreach (var client in Clients)
            {
                if (client == exclude) continue;
                var data = SerializeBinary(newData);
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((byte)packetType);
                msg.Write(data.Length);
                msg.Write(data);
                Server.SendMessage(msg, client.NetConnection,
                    important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                    (int)channel);
            }
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
                    Program.Output("WARN: Deserialization failed: " + e.Message);
                    return null;
                }
            }
        }

        public byte[] SerializeBinary(object data)
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

        public NativeArgument ParseReturnType(Type t)
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

        public List<NativeArgument> ParseNativeArguments(params object[] args)
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
                else if (o is IList)
                {
                    var larg = new ListArgument();
                    var l = ((IList) o);
                    object[] array = new object[l.Count];
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
        
        public void SendNativeCallToPlayer(Client player, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Arguments = ParseNativeArguments(arguments);
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void SendNativeCallToAllPlayers(ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Arguments = ParseNativeArguments(arguments);
            obj.ReturnType = null;
            obj.Id = null;

            var bin = SerializeBinary(obj);

            foreach (var c in Clients)
            {
                var msg = Server.CreateMessage();

                msg.Write((byte) PacketType.NativeCall);
                msg.Write(bin.Length);
                msg.Write(bin);

                Server.SendMessage(msg, c.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
            }
        }

        public void SetNativeCallOnTickForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            obj.Arguments = ParseNativeArguments(arguments);

            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;
            wrapper.Native = obj;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void SetNativeCallOnTickForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            obj.Arguments = ParseNativeArguments(arguments);

            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;
            wrapper.Native = obj;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void RecallNativeCallOnTickForPlayer(Client player, string identifier)
        {
            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void RecallNativeCallOnTickForAllPlayers(string identifier)
        {
            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void SetNativeCallOnDisconnectForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Id = identifier;
            obj.Arguments = ParseNativeArguments(arguments);

            
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void SetNativeCallOnDisconnectForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Id = identifier;
            obj.Arguments = ParseNativeArguments(arguments);

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void RecallNativeCallOnDisconnectForPlayer(Client player, string identifier)
        {
            var obj = new NativeData();
            obj.Id = identifier;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void RecallNativeCallOnDisconnectForAllPlayers(string identifier)
        {
            var obj = new NativeData();
            obj.Id = identifier;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        private ulong _nativeCount = 0;
        public object ReturnNativeCallFromPlayer(Client player, ulong hash, NativeArgument returnType, params object[] args)
        {
            _nativeCount++;
            object output = null;
            GetNativeCallFromPlayer(player, _nativeCount.ToString(), hash, returnType, (o) =>
            {
                output = o;
            }, args);

            DateTime start = DateTime.Now;
            while (output == null && DateTime.Now.Subtract(start).Milliseconds < 10000)
            {}
            
            return output;
        }

        private Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();
        public void GetNativeCallFromPlayer(Client player, string salt, ulong hash, NativeArgument returnType, Action<object> callback,
            params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.ReturnType = returnType;
            salt = Environment.TickCount.ToString() +
                   salt +
                   player.NetConnection.RemoteUniqueIdentifier.ToString() +
                   DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString();
            obj.Id = salt;
            obj.Arguments = ParseNativeArguments(arguments);

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void CreatePositionInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement();
            mov.ServerStartTime = Program.GetTicks();
            mov.Duration = duration;
            mov.StartVector = prop.Position;
            mov.EndVector = target;
            mov.Start = 0;
            prop.PositionMovement = mov;

            var delta = new Delta_EntityProperties();
            delta.PositionMovement = mov;
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public void CreateRotationInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement();
            mov.ServerStartTime = Program.GetTicks();
            mov.Duration = duration;
            mov.StartVector = prop.Position;
            mov.EndVector = target;
            mov.Start = 0;
            prop.RotationMovement = mov;

            var delta = new Delta_EntityProperties();
            delta.RotationMovement = mov;
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public bool SetEntityProperty(int entity, string key, object value, bool world = false)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return false;

            if (prop.SyncedProperties == null) prop.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            object oldValue = DecodeArgumentListPure(prop.SyncedProperties.Get(key)).FirstOrDefault();

            prop.SyncedProperties.Set(key, nativeArg);

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, nativeArg);
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

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
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

        public void ChangePlayerTeam(Client target, int newTeam)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PlayerProperties) NetEntityHandler.ToDict()[target.CharacterHandle.Value]).Team = newTeam;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte) ServerEventType.PlayerTeamChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newTeam);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColor(Client target, int newColor)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipColor = newColor;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipColorChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newColor);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColorForPlayer(Client target, int newColor, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipColorChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newColor);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSprite(Client target, int newSprite)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipSprite = newSprite;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipSpriteChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newSprite);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSpriteForPlayer(Client target, int newSprite, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipSpriteChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newSprite);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlpha(Client target, int newAlpha)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipAlpha = (byte)newAlpha;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipAlphaChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newAlpha);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlphaForPlayer(Client target, int newAlpha, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipAlphaChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newAlpha);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void DetachEntity(int nethandle, bool collision)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.EntityDetachment;
            obj.Arguments = ParseNativeArguments(nethandle, collision);
            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectate(Client target, bool spectating)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerSpectatorChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, spectating);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectatePlayer(Client spectator, Client target)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerSpectatorChange;
            obj.Arguments = ParseNativeArguments(spectator.CharacterHandle.Value, true, target.CharacterHandle.Value);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimation(Client target, int flag, string animDict, string animName)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerAnimationStart;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, flag, animDict, animName);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimationStop(Client target)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerAnimationStop;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }
    }
}
