using System;
using System.CodeDom.Compiler;
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
using Microsoft.ClearScript;
using Microsoft.ClearScript.Windows;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using ProtoBuf;

namespace GTANetworkServer
{
    public class Client
    {
        public NetConnection NetConnection { get; private set; }
        public string SocialClubName { get; set; }
        public string Name { get; set; }
        public float Latency { get; set; }
        public ScriptVersion RemoteScriptVersion { get; set; }
        public int GameVersion { get; set; }

        public NetHandle CurrentVehicle { get; set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Rotation { get; internal set; }
        public int Health { get; internal set; }
        public int Armor { get; internal set; }
        public bool IsInVehicle { get; internal set; }

        public DateTime LastUpdate { get; internal set; }

        public NetHandle CharacterHandle { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
            CharacterHandle = new NetHandle(Program.ServerInstance.NetEntityHandler.GeneratePedHandle());
        }
    }

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
    /*
    public struct ServerConfig
    {
        public int Port;
        public string Name;
        public bool PasswordProtected;
        public string Password;
        public string MasterServer;
        public bool AnnounceSelf;
        public bool AnnounceToLan;
        public int MaxPlayers;
        public bool ACLEnabled;

    }*/

    public class GameServer
    {
        public GameServer(ServerSettings conf)
        {
            Clients = new List<Client>();
            Downloads = new List<StreamingClient>();
            RunningResources = new List<Resource>();
            FileHashes = new Dictionary<string, string>();

            MaxPlayers = 32;
            Port = conf.Port;
            
            NetEntityHandler = new NetEntityHandler();

            ACLEnabled = conf.UseACL;

            if (conf.UseACL)
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
        }
        
        public NetServer Server;
        public TaskFactory ConcurrentFactory;
        internal List<StreamingClient> Downloads;
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public List<Client> Clients { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected { get; set; }
        public string GamemodeName { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }
        public bool AnnounceToLAN { get; set; }
        public AccessControlList ACL { get; set; }
        public bool IsClosing { get; set; }
        public bool ReadyToClose { get; set; }
        public bool ACLEnabled { get; set; }
        public bool UseUPnP { get; set; }

        public List<string> LoadedIPL = new List<string>();
        public List<string> RemovedIPL = new List<string>();

        public string Weather { get; set; } = "CLEAR";
        public DateTime TimeOfDay { get; set; } = DateTime.Now;

        public List<Resource> RunningResources;

        private Dictionary<string, string> FileHashes { get; set; }

        public NetEntityHandler NetEntityHandler { get; set; }

        public bool AllowDisplayNames { get; set; }

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
            

            Program.Output("Loading resources...");
            var list = new List<JScriptEngine>();
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
        }

        public void AnnounceSelfToMaster()
        {
            Program.Output("Announcing self to master server...");
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

        public void StartResource(string resourceName, string father = null)
        {
            try
            {
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

                        if (FileHashes.ContainsKey(filePath.Path))
                            FileHashes[filePath.Path] = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
                        else
                            FileHashes.Add(filePath.Path, myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right));
                    }
                }

                var csScripts = new List<ClientsideScript>();

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
                                Filename = Path.GetFileNameWithoutExtension(script.Path)?.Replace('.', '_'),
                            };

                            ourResource.ClientsideScripts.Add(csScript);
                            csScripts.Add(csScript);
                            continue;
                        }

                        Program.Output("WARNING: Javascript resources will not run on GNU/Linux servers, and thus, they are deprecated.");
                        ourResource.Engines.Add(new ScriptingEngine(scrTxt, script.Path, ourResource, currentResInfo.Referenceses.Select(r => r.Name).ToArray()));
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
                        ourResource.Engines.AddRange(instances.Select(sss => new ScriptingEngine(sss, script.Path, ourResource)));
                    }
                    else if (script.Language == ScriptingEngineLanguage.csharp)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);

                        var ass = CompileScript(scrTxt, currentResInfo.Referenceses.Select(r => r.Name).ToArray(), false);
                        ourResource.Engines.AddRange(ass.Select(sss => new ScriptingEngine(sss, script.Path, ourResource)));
                    }
                    else if (script.Language == ScriptingEngineLanguage.vbasic)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        var ass = CompileScript(scrTxt, currentResInfo.Referenceses.Select(r => r.Name).ToArray(), true);
                        ourResource.Engines.AddRange(ass.Select(sss => new ScriptingEngine(sss, script.Path, ourResource)));
                    }
                }

                foreach (var engine in ourResource.Engines)
                {
                    engine.InvokeResourceStart();
                }
                

                var randGen = new Random();
                // TODO: Send new files to everyone
                foreach (var client in Clients)
                {
                    var clientScripts = new ScriptCollection();
                    clientScripts.ClientsideScripts = new List<ClientsideScript>(ourResource.ClientsideScripts);

                    var scriptData = new StreamedData();
                    scriptData.Id = randGen.Next(int.MaxValue);
                    scriptData.Data = SerializeBinary(clientScripts);
                    scriptData.Type = FileType.Script;

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
                        fileData.Hash = FileHashes.ContainsKey(file.Path)
                            ? FileHashes[file.Path]
                            : null;

                        downloader.Files.Add(fileData);
                    }

                    downloader.Files.Add(scriptData);
                    Downloads.Add(downloader);
                }

                lock (RunningResources) RunningResources.Add(ourResource);
            }
            catch (Exception ex)
            {
                Program.Output("ERROR STARTING RESOURCE " + resourceName);
                Program.Output(ex.ToString());
            }
        }

        public void StopResource(string resourceName)
        {
            lock (RunningResources)
            {
                var ourRes = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName);
                if (ourRes == null) return;

                Program.Output("Stopping " + resourceName);

                ourRes.Engines.ForEach(en => en.InvokeResourceStop());

                var msg = Server.CreateMessage();
                msg.Write((int) PacketType.StopResource);
                msg.Write(resourceName);
                Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

                RunningResources.Remove(ourRes);

                Program.Output("Stopped " + resourceName + "!");
            }
        }

        private JScriptEngine InstantiateScripts(string script, string resourceName, string[] refs)
        {
            var scriptEngine = new JScriptEngine();

            var collect = new HostTypeCollection(refs);

            scriptEngine.AddHostObject("clr", collect);
            scriptEngine.AddHostObject("API", new API());
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostType("Dictionary", typeof(Dictionary<,>));
            scriptEngine.AddHostType("xmlParser", typeof(RetardedXMLParser));
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
            scriptEngine.AddHostType("String", typeof(string));
            scriptEngine.AddHostType("List", typeof (List<>));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("Vector3", typeof(Vector3));
            scriptEngine.AddHostType("Quaternion", typeof(Vector3));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("LocalPlayerArgument", typeof(LocalPlayerArgument));
            scriptEngine.AddHostType("LocalGamePlayerArgument", typeof(LocalGamePlayerArgument));
            scriptEngine.AddHostType("EntityArgument", typeof(EntityArgument));
            scriptEngine.AddHostType("EntityPointerArgument", typeof(EntityPointerArgument));
            scriptEngine.AddHostType("console", typeof(Console));
            scriptEngine.AddHostType("VehicleHash", typeof(VehicleHash));
            scriptEngine.AddHostType("Int32", typeof(int));
            scriptEngine.AddHostType("EntityArgument", typeof(EntityArgument));
            scriptEngine.AddHostType("EntityPtrArgument", typeof(EntityPointerArgument));
            try
            {
                scriptEngine.Execute(script);
            }
            catch (ScriptEngineException ex)
            {
                LogException(ex, resourceName);
            }

            return scriptEngine;
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

        private IEnumerable<Script> CompileScript(string script, string[] references, bool vbBasic = false)
        {
            var provide = new CSharpCodeProvider();
            var vBasicProvider = new VBCodeProvider();

            var compParams = new CompilerParameters();

            compParams.ReferencedAssemblies.Add("System.Drawing.dll");
            compParams.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            compParams.ReferencedAssemblies.Add("System.IO.dll");
            compParams.ReferencedAssemblies.Add("System.Linq.dll");
            compParams.ReferencedAssemblies.Add("System.Core.dll");
            compParams.ReferencedAssemblies.Add("GTANetworkServer.exe");
            compParams.ReferencedAssemblies.Add("GTANetworkShared.dll");

            foreach (var s in references)
            {
                compParams.ReferencedAssemblies.Add(s);
            }

            compParams.GenerateInMemory = true;
            compParams.GenerateExecutable = false;

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

        public void UpdateMarkerInfo(int netId, MarkerProperties newInfo)
        {
            var packet = new CreateEntity();
            packet.EntityType = (byte)EntityType.Marker;
            packet.Properties = newInfo;
            Program.ServerInstance.SendToAll(packet, PacketType.UpdateMarkerProperties, true, ConnectionChannel.EntityBackend);
        }
        
        private void LogException(Exception ex, string resourceName)
        {
            Program.Output("RESOURCE EXCEPTION FROM " + resourceName + ": " + ex.Message);
            Program.Output(ex.StackTrace);
        }

        public void ProcessMessages()
        {
            NetIncomingMessage msg;
            while ((msg = Server.ReadMessage()) != null)
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
                        var type = msg.ReadInt32();
                        var leng = msg.ReadInt32();
                        var connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                        if (connReq == null)
                        {
                            client.NetConnection.Deny("Connection Object is null");
                            Server.Recycle(msg);
                            continue;
                        }

                        if ((ScriptVersion) connReq.ScriptVersion == ScriptVersion.Unknown)
                        {
                            client.NetConnection.Deny("Unknown version. Please update your client.");
                            Server.Recycle(msg);
                            continue;
                        }

                        int clients = 0;
                        lock (Clients) clients = Clients.Count;
                        if (clients < MaxPlayers)
                        {
                            if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                            {
                                if (Password != connReq.Password)
                                {
                                    client.NetConnection.Deny("Wrong password.");
                                    Program.Output("Player connection refused: wrong password.");

                                    Server.Recycle(msg);

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

                            client.SocialClubName = connReq.SocialClubName;
                            client.Name = AllowDisplayNames ? connReq.DisplayName : connReq.SocialClubName;

                            if (client.RemoteScriptVersion != (ScriptVersion) connReq.ScriptVersion)
                                client.RemoteScriptVersion = (ScriptVersion) connReq.ScriptVersion;
                            if (client.GameVersion != connReq.GameVersion) client.GameVersion = connReq.GameVersion;

                            var respObj = new ConnectionResponse();
                            respObj.CharacterHandle = client.CharacterHandle.Value;
                            //respObj.AssignedChannel = GetChannelIdForConnection(client);

                            // TODO: Transfer map.



                            var channelHail = Server.CreateMessage();
                            var respBin = SerializeBinary(respObj);

                            channelHail.Write(respBin.Length);
                            channelHail.Write(respBin);

                            client.NetConnection.Approve(channelHail);

                            lock (RunningResources)
                                RunningResources.ForEach(
                                    fs => fs.Engines.ForEach(en => en.InvokePlayerBeginConnect(client)));

                            Program.Output("New incoming connection: " + client.SocialClubName + " (" + client.Name +
                                            ")");
                        }
                        else
                        {
                            client.NetConnection.Deny("No available player slots.");
                            Program.Output("Player connection refused: server full.");
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var newStatus = (NetConnectionStatus) msg.ReadByte();

                        if (newStatus == NetConnectionStatus.Connected)
                        {
                        }
                        else if (newStatus == NetConnectionStatus.Disconnected)
                        {
                            var reason = msg.ReadString();

                            lock (Clients)
                            {
                                if (Clients.Contains(client))
                                {
                                    lock (RunningResources)
                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                        {
                                            en.InvokePlayerDisconnected(client, reason);
                                        }));

                                    var dcObj = new PlayerDisconnect()
                                    {
                                        Id = client.CharacterHandle.Value,
                                    };

                                    SendToAll(dcObj, PacketType.PlayerDisconnect, true, ConnectionChannel.EntityBackend);

                                    Program.Output("Player disconnected: " + client.SocialClubName + " (" +
                                                    client.Name + ")");

                                    Clients.Remove(client);
                                }
                            }
                        }
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        NetOutgoingMessage response = Server.CreateMessage();
                        var obj = new DiscoveryResponse();
                        obj.ServerName = Name;
                        obj.MaxPlayers = (short) MaxPlayers;
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
                                    Clients.Count(c => DateTime.Now.Subtract(c.LastUpdate).TotalMilliseconds < 60000);
                        obj.Port = Port;
                        obj.LAN = isIPLocal(msg.SenderEndPoint.Address.ToString());

                        if ((obj.LAN && AnnounceToLAN) || !obj.LAN)
                        {
                            var bin = SerializeBinary(obj);

                            response.Write((int) PacketType.DiscoveryResponse);
                            response.Write(bin.Length);
                            response.Write(bin);

                            Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        var packetType = (PacketType) msg.ReadInt32();

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
                                                respMsg.Write((int)PacketType.ChatData);
                                                respMsg.Write(binData.Length);
                                                respMsg.Write(binData);
                                                client.NetConnection.SendMessage(respMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                                }

                                            return;
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
                            case PacketType.VehiclePositionData:
                            {
                                try
                                {
                                    var len = msg.ReadInt32();
                                    var data =
                                        DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as
                                            VehicleData;
                                    if (data != null)
                                    {
                                        data.Name = client.Name;
                                        data.Latency = client.Latency;
                                        data.NetHandle = client.CharacterHandle.Value;

                                        client.Health = data.PlayerHealth;
                                        client.Armor = data.PedArmor;
                                        client.Position = data.Position;
                                        client.IsInVehicle = true;
                                        client.CurrentVehicle = new NetHandle(data.VehicleHandle);
                                        client.Rotation = data.Quaternion;
                                        client.LastUpdate = DateTime.Now;

                                        if (NetEntityHandler.ToDict().ContainsKey(data.VehicleHandle))
                                        {
                                            NetEntityHandler.ToDict()[data.VehicleHandle].Position = data.Position;
                                            NetEntityHandler.ToDict()[data.VehicleHandle].Rotation = data.Quaternion;
                                            ((VehicleProperties) NetEntityHandler.ToDict()[data.VehicleHandle]).IsDead = (data.Flag & (byte)VehicleDataFlags.VehicleDead) > 0;
                                            ((VehicleProperties) NetEntityHandler.ToDict()[data.VehicleHandle]).Health = data.VehicleHealth;
                                            ((VehicleProperties) NetEntityHandler.ToDict()[data.VehicleHandle]).Siren = (data.Flag & (byte)VehicleDataFlags.SirenActive) > 0;

                                            if (data.Trailer != null)
                                            {
                                                var trailer = ((VehicleProperties) NetEntityHandler.ToDict()[data.VehicleHandle]).Trailer;
                                                if (NetEntityHandler.ToDict().ContainsKey(trailer))
                                                {
                                                    NetEntityHandler.ToDict()[trailer].Position = data.Trailer;
                                                }
                                            }
                                        }

                                        if (NetEntityHandler.ToDict().ContainsKey(data.NetHandle))
                                        {
                                            NetEntityHandler.ToDict()[data.NetHandle].Position = data.Position;
                                            NetEntityHandler.ToDict()[data.NetHandle].Rotation = data.Quaternion;
                                            NetEntityHandler.ToDict()[data.NetHandle].ModelHash = data.PedModelHash;
                                        }


                                        SendToAll(data, PacketType.VehiclePositionData, false, client, ConnectionChannel.PositionData);
                                    }
                                }
                                catch (IndexOutOfRangeException)
                                {
                                }
                            }
                                break;
                            case PacketType.PedPositionData:
                            {
                                try
                                {
                                    var len = msg.ReadInt32();
                                    var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                    if (data != null)
                                    {
                                        data.Name = client.Name;
                                        data.Latency = client.Latency;
                                        data.NetHandle = client.CharacterHandle.Value;

                                        client.Health = data.PlayerHealth;
                                        client.Armor = data.PedArmor;
                                        client.Position = data.Position;
                                        client.IsInVehicle = false;
                                        client.LastUpdate = DateTime.Now;
                                        client.Rotation = data.Quaternion;
                                        client.CurrentVehicle = new NetHandle(0);

                                        client.Rotation = data.Quaternion;

                                        if (NetEntityHandler.ToDict().ContainsKey(data.NetHandle))
                                        {
                                            NetEntityHandler.ToDict()[data.NetHandle].Position = data.Position;
                                            NetEntityHandler.ToDict()[data.NetHandle].Rotation = data.Quaternion;
                                            NetEntityHandler.ToDict()[data.NetHandle].ModelHash = data.PedModelHash;
                                        }

                                        SendToAll(data, PacketType.PedPositionData, false, client, ConnectionChannel.PositionData);
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
                                    resp = ((IntArgument) data.Response).Data;
                                }
                                else if (data.Response is UIntArgument)
                                {
                                    resp = ((UIntArgument) data.Response).Data;
                                }
                                else if (data.Response is StringArgument)
                                {
                                    resp = ((StringArgument) data.Response).Data;
                                }
                                else if (data.Response is FloatArgument)
                                {
                                    resp = ((FloatArgument) data.Response).Data;
                                }
                                else if (data.Response is BooleanArgument)
                                {
                                    resp = ((BooleanArgument) data.Response).Data;
                                }
                                else if (data.Response is Vector3Argument)
                                {
                                    var tmp = (Vector3Argument) data.Response;
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
                                    var mapObj = new ServerMap();
                                    mapObj.Hours = (byte)TimeOfDay.Hour;
                                    mapObj.Minutes = (byte)TimeOfDay.Minute;
                                    mapObj.Weather = Weather;
                                    mapObj.LoadedIpl = LoadedIPL;
                                    mapObj.RemovedIpl = RemovedIPL;
                                    foreach (var pair in NetEntityHandler.ToDict())
                                    {
                                        if (pair.Value.EntityType == (byte) EntityType.Vehicle)
                                        {
                                            mapObj.Vehicles.Add(pair.Key, (VehicleProperties) pair.Value);
                                        }
                                        else if (pair.Value.EntityType == (byte) EntityType.Prop)
                                        {
                                            mapObj.Objects.Add(pair.Key, pair.Value);
                                        }
                                        else if (pair.Value.EntityType == (byte) EntityType.Blip)
                                        {
                                            mapObj.Blips.Add(pair.Key, (BlipProperties) pair.Value);
                                        }
                                        else if (pair.Value.EntityType == (byte) EntityType.Marker)
                                        {
                                            mapObj.Markers.Add(pair.Key, (MarkerProperties) pair.Value);
                                        }
                                        else if (pair.Value.EntityType == (byte) EntityType.Pickup)
                                        {
                                            if (!((PickupProperties)pair.Value).PickedUp)
                                                mapObj.Pickups.Add(pair.Key, (PickupProperties) pair.Value);
                                        }
                                        else if (pair.Value.EntityType == (byte) EntityType.Ped)
                                        {
                                            mapObj.Players.Add(pair.Key, (PedProperties) pair.Value);
                                        }
                                    }

                                    // TODO: replace this filth
                                    var r = new Random();

                                    var mapData = new StreamedData();
                                    mapData.Id = r.Next(int.MaxValue);
                                    mapData.Data = SerializeBinary(mapObj);
                                    mapData.Type = FileType.Map;

                                    var clientScripts = new ScriptCollection();
                                    clientScripts.ClientsideScripts = new List<ClientsideScript>(GetAllClientsideScripts());

                                    var scriptData = new StreamedData();
                                    scriptData.Id = r.Next(int.MaxValue);
                                    scriptData.Data = SerializeBinary(clientScripts);
                                    scriptData.Type = FileType.Script;

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
                                            fileData.Hash = FileHashes.ContainsKey(file.Path)
                                                ? FileHashes[file.Path]
                                                : null;
                                            downloader.Files.Add(fileData);
                                        }
                                    }

                                    downloader.Files.Add(scriptData);
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
                        }
                        break;
                    default:
                        Program.Output("WARN: Unhandled type: " + msg.MessageType);
                        break;
                }
                Server.Recycle(msg);
            }
        }

        public void Tick()
        {
            if (IsClosing)
            {
                for (int i = RunningResources.Count - 1; i >= 0; i--)
                {
                    StopResource(RunningResources[i].DirectoryName);
                }

                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    Clients[i].NetConnection.Disconnect("Server is shutting down");
                }

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
                            updateObj.Write((int) PacketType.FileTransferTick);
                            updateObj.Write(Downloads[i].Files[0].Id);
                            updateObj.Write(sendBytes);
                            updateObj.Write(Downloads[i].Files[0].Data, (int)Downloads[i].Files[0].BytesSent, sendBytes);
                            Downloads[i].Files[0].BytesSent += sendBytes;

                            Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection,
                                NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);

                            if (remaining - sendBytes <= 0)
                            {
                                var endObject = Server.CreateMessage();
                                endObject.Write((int)PacketType.FileTransferComplete);
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

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

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
                            ((VehicleProperties) NetEntityHandler.ToDict()[(int) args[1]]).Trailer = 0;
                    }
                    else
                    {
                        if (NetEntityHandler.ToDict().ContainsKey((int)args[1]))
                            ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[1]]).Trailer = (int)args[2];
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
                        ((PickupProperties) NetEntityHandler.ToDict()[pickupId]).PickedUp = true;
                        RunningResources.ForEach(res => res.Engines.ForEach(en => en.InvokePlayerPickup(sender, new NetHandle(pickupId))));
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
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendMessage(msg, c.NetConnection,
                important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                (int)channel);
        }

        public void SendToAll(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            lock (Clients)
            foreach (var client in Clients)
            {
                var data = SerializeBinary(newData);
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((int)packetType);
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
                msg.Write((int)packetType);
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
            msg.Write((int)PacketType.NativeCall);
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

                msg.Write((int) PacketType.NativeCall);
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

            msg.Write((int)PacketType.NativeTick);
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

            msg.Write((int)PacketType.NativeTick);
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
            msg.Write((int)PacketType.NativeTickRecall);
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
            msg.Write((int)PacketType.NativeTickRecall);
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

            msg.Write((int)PacketType.NativeOnDisconnect);
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

            msg.Write((int)PacketType.NativeOnDisconnect);
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
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
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
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
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

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void ChangePlayerTeam(Client target, int newTeam)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PedProperties) NetEntityHandler.ToDict()[target.CharacterHandle.Value]).Team = newTeam;
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
                ((PedProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipColor = newColor;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipColorChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newColor);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSprite(Client target, int newSprite)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PedProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipSprite = newSprite;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipSpriteChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newSprite);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlpha(Client target, int newAlpha)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.CharacterHandle.Value))
            {
                ((PedProperties)NetEntityHandler.ToDict()[target.CharacterHandle.Value]).BlipAlpha = (byte)newAlpha;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipAlphaChange;
            obj.Arguments = ParseNativeArguments(target.CharacterHandle.Value, newAlpha);

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
    }
}
