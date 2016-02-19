using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Lidgren.Network;
using Microsoft.ClearScript;
using Microsoft.ClearScript.Windows;
using ProtoBuf;
using MultiTheftAutoShared;

namespace GTAServer
{
    public class Client
    {
        public NetConnection NetConnection { get; private set; }
        public string SocialClubName { get; set; }
        public string Name { get; set; }
        public float Latency { get; set; }
        public ScriptVersion RemoteScriptVersion { get; set; }
        public int GameVersion { get; set; }

        public int CurrentVehicle { get; set; }
        public LVector3 Position { get; internal set; }
        public LVector3 Rotation { get; internal set; }
        public int Health { get; internal set; }
        public int VehicleHealth { get; internal set; }
        public bool IsInVehicle { get; internal set; }

        public DateTime LastUpdate { get; internal set; }

        public int CharacterHandle { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
            CharacterHandle = Program.ServerInstance.NetEntityHandler.GenerateHandle();
        }
    }

    public class StreamingClient
    {
        public StreamingClient(Client c)
        {
            Parent = c;
            ChunkSize = c.NetConnection.Peer.Configuration.MaximumTransmissionUnit - 20;
            Files = new Queue<StreamedData>();
        }

        public int ChunkSize { get; set; }
        public Client Parent { get; set; }
        public Queue<StreamedData> Files { get; set; }
    }

    public class StreamedData
    {
        public StreamedData()
        {
            HasStarted = false;
            BytesSent = 0;
        }

        public bool HasStarted { get; set; }
        public int Id { get; set; }
        public long BytesSent { get; set; }
        public byte[] Data { get; set; }

        public FileType Type { get; set; }
        public string Name { get; set; }
    }

    public enum NotificationIconType
    {
        Chatbox = 1,
        Email = 2,
        AddFriendRequest = 3,
        Nothing = 4,
        RightJumpingArrow = 7,
        RP_Icon = 8,
        DollarIcon = 9,
    }

    public enum NotificationPicType
    {
        CHAR_DEFAULT, // : Default profile pic
        CHAR_FACEBOOK, // Facebook
        CHAR_SOCIAL_CLUB, // Social Club Star
        CHAR_CARSITE2, // Super Auto San Andreas Car Site
        CHAR_BOATSITE, // Boat Site Anchor
        CHAR_BANK_MAZE, // Maze Bank Logo
        CHAR_BANK_FLEECA, // Fleeca Bank
        CHAR_BANK_BOL, // Bank Bell Icon
        CHAR_MINOTAUR, // Minotaur Icon
        CHAR_EPSILON, // Epsilon E
        CHAR_MILSITE, // Warstock W
        CHAR_CARSITE, // Legendary Motorsports Icon
        CHAR_DR_FRIEDLANDER, // Dr Freidlander Face
        CHAR_BIKESITE, // P&M Logo
        CHAR_LIFEINVADER, // Liveinvader
        CHAR_PLANESITE, // Plane Site E
        CHAR_MICHAEL, // Michael's Face
        CHAR_FRANKLIN, // Franklin's Face
        CHAR_TREVOR, // Trevor's Face
        CHAR_SIMEON, // Simeon's Face
        CHAR_RON, // Ron's Face
        CHAR_JIMMY, // Jimmy's Face
        CHAR_LESTER, // Lester's Shadowed Face
        CHAR_DAVE, // Dave Norton's Face
        CHAR_LAMAR, // Chop's Face
        CHAR_DEVIN, // Devin Weston's Face
        CHAR_AMANDA, // Amanda's Face
        CHAR_TRACEY, // Tracey's Face
        CHAR_STRETCH, // Stretch's Face
        CHAR_WADE, // Wade's Face
        CHAR_MARTIN, // Martin Madrazo's Face

    }

    public class GameServer
    {
        public GameServer(int port, string name, string gamemodeName)
        {
            Clients = new List<Client>();
            Downloads = new List<StreamingClient>();
            MaxPlayers = 32;
            Port = port;
            GamemodeName = gamemodeName;

            _resources = new List<JScriptEngine>();
            _clientScripts = new List<string>();
            NetEntityHandler = new NetEntityHandler();

            Name = name;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            NetPeerConfiguration config = new NetPeerConfiguration("GTAVOnlineRaces");
            config.Port = port;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(config);
        }

        public NetServer Server;

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

        public NetEntityHandler NetEntityHandler { get; set; }

        public bool AllowDisplayNames { get; set; }

        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9;

        private List<JScriptEngine> _resources;
        private List<string> _clientScripts;
        private DateTime _lastAnnounceDateTime;

        public void Start(string[] filterscripts)
        {
            Server.Start();

            if (AnnounceSelf)
            {
                _lastAnnounceDateTime = DateTime.Now;
                Console.WriteLine("Announcing to master server...");
                AnnounceSelfToMaster();
            }
            

            Console.WriteLine("Loading resources...");
            var list = new List<JScriptEngine>();
            foreach (var path in filterscripts)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                try
                {
                    var script = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "resources" + Path.DirectorySeparatorChar + path + ".js");
                    if (script.StartsWith("//local"))
                    {
                        _clientScripts.Add(script);
                        continue;
                    }
                    var fsObj = InstantiateScripts(script, path);
                    list.Add(fsObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load resource \"" + path + "\", error: " + ex.Message);
                }
            }

            Console.WriteLine("Starting resources...");
            list.ForEach(fs =>
            {
                fs.Script.API.invokeResourceStart();
            });
            lock (_resources)
            {
                _resources = list;
            }
        }

        public void AnnounceSelfToMaster()
        {
            using (var wb = new WebClient())
            {
                try
                {
                    wb.UploadData(MasterServer, Encoding.UTF8.GetBytes(Port.ToString()));
                }
                catch (WebException)
                {
                    Console.WriteLine("Failed to announce self: master server is not available at this time.");
                }
            }
        }

        private JScriptEngine InstantiateScripts(string script, string resourceName)
        {
            var scriptEngine = new JScriptEngine();
            scriptEngine.AddHostObject("API", new API());
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
            scriptEngine.AddHostType("String", typeof(string));
            scriptEngine.AddHostType("List", typeof (IList));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("Vector3", typeof(LVector3));
            scriptEngine.AddHostType("Quaternion", typeof(LVector3));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("LocalPlayerArgument", typeof(LocalPlayerArgument));
            scriptEngine.AddHostType("LocalGamePlayerArgument", typeof(LocalGamePlayerArgument));
            scriptEngine.AddHostType("EntityArgument", typeof(EntityArgument));
            scriptEngine.AddHostType("EntityPointerArgument", typeof(EntityPointerArgument));
            scriptEngine.AddHostType("console", typeof(Console));
            scriptEngine.AddHostType("VehicleHash", typeof(VehicleHash));

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

        private void LogException(Exception ex, string resourceName)
        {
            Console.WriteLine("RESOURCE EXCEPTION FROM " + resourceName + ": " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }

        public void Tick()
        {
            if (Downloads.Count > 0)
            {
                for (int i = Downloads.Count - 1; i >= 0; i--)
                {
                    if (Downloads[i].Files.Count > 0)
                    {
                        if (Downloads[i].Parent.NetConnection.CanSendImmediately(NetDeliveryMethod.ReliableOrdered,
                            GetChannelIdForConnection(Downloads[i].Parent)))
                        {
                            var ourObj = Downloads[i].Files.Peek();
                            if (!ourObj.HasStarted)
                            {
                                var notifyObj = new DataDownloadStart();
                                notifyObj.FileType = (byte) ourObj.Type;
                                notifyObj.ResourceParent = null;
                                notifyObj.FileName = null;
                                notifyObj.Length = ourObj.Data.Length;
                                SendToClient(Downloads[i].Parent, notifyObj, PacketType.FileTransferRequest, true);
                                Downloads[i].Files.Peek().HasStarted = true;
                            }

                            var remaining = ourObj.Data.Length - ourObj.BytesSent;
                            int sendBytes = (remaining > Downloads[i].ChunkSize
                                ? Downloads[i].ChunkSize
                                : (int) remaining);

                            var updateObj = Server.CreateMessage();
                            updateObj.Write((int) PacketType.FileTransferTick);
                            updateObj.Write(ourObj.Id);
                            updateObj.Write(sendBytes);
                            updateObj.Write(ourObj.Data, (int) ourObj.BytesSent, sendBytes);
                            Downloads[i].Files.Peek().BytesSent += sendBytes;

                            Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection,
                                NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(Downloads[i].Parent));

                            if (remaining - sendBytes <= 0)
                            {
                                var endObject = Server.CreateMessage();
                                endObject.Write((int)PacketType.FileTransferComplete);
                                endObject.Write(ourObj.Id);

                                Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection,
                                    NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(Downloads[i].Parent));
                                Downloads[i].Files.Dequeue();
                            }
                        }
                    }
                    else
                    {
                        Downloads.RemoveAt(i);
                    }
                }
            }

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

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
                            Console.WriteLine("INFO: ping received from " + msg.SenderEndPoint.Address.ToString());
                            var pong = Server.CreateMessage();
                            pong.Write("pong");
                            Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                        }
                        if (isPing == "query")
                        {
                            int playersonline = 0;
                            lock (Clients) playersonline = Clients.Count;
                            Console.WriteLine("INFO: query received from " + msg.SenderEndPoint.Address.ToString());
                            var pong = Server.CreateMessage();
                            pong.Write(Name + "%" + PasswordProtected + "%" + playersonline + "%" + MaxPlayers + "%" + GamemodeName);
                            Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                        }
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        Console.WriteLine(msg.ReadString());
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

                        if ((ScriptVersion)connReq.ScriptVersion == ScriptVersion.Unknown)
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
                                    Console.WriteLine("Player connection refused: wrong password.");

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

                            if (client.RemoteScriptVersion != (ScriptVersion)connReq.ScriptVersion) client.RemoteScriptVersion = (ScriptVersion)connReq.ScriptVersion;
                            if (client.GameVersion != connReq.GameVersion) client.GameVersion = connReq.GameVersion;

                            var respObj = new ConnectionResponse();
                            respObj.CharacterHandle = client.CharacterHandle;
                            respObj.AssignedChannel = GetChannelIdForConnection(client);
                            
                            // TODO: Transfer map.

                            var mapObj = new ServerMap();
                            mapObj.Vehicles = new Dictionary<int, VehicleProperties>();
                            mapObj.Objects = new Dictionary<int, EntityProperties>();
                            foreach (var pair in NetEntityHandler.ToDict())
                            {
                                if (pair.Value is VehicleProperties && pair.Value.EntityType == (byte)EntityType.Vehicle)
                                {
                                    mapObj.Vehicles.Add(pair.Key, (VehicleProperties)pair.Value);
                                }
                                else if (pair.Value.EntityType == (byte)EntityType.Prop)
                                {
                                    mapObj.Objects.Add(pair.Key, pair.Value);
                                }
                            }

                            // TODO: replace this filth
                            var r = new Random();

                            var mapData = new StreamedData();
                            mapData.Id = r.Next(int.MaxValue);
                            mapData.Data = SerializeBinary(mapObj);
                            mapData.Type = FileType.Map;

                            var clientScripts = new ScriptCollection();
                            clientScripts.ClientsideScripts = new List<string>(_clientScripts);

                            var scriptData = new StreamedData();
                            scriptData.Id = r.Next(int.MaxValue);
                            scriptData.Data = SerializeBinary(clientScripts);
                            scriptData.Type = FileType.Script;
                            
                            var downloader = new StreamingClient(client);
                            downloader.Files.Enqueue(mapData);
                            downloader.Files.Enqueue(scriptData);

                            Downloads.Add(downloader);
                            
                            var channelHail = Server.CreateMessage();
                            var respBin = SerializeBinary(respObj);

                            channelHail.Write(respBin.Length);
                            channelHail.Write(respBin);
                            
                            client.NetConnection.Approve(channelHail);

                            if (_resources != null) _resources.ForEach(fs => fs.Script.API.invokePlayerBeginConnect(client));

                            /**/

                            Console.WriteLine("New incoming connection: " + client.SocialClubName + " (" + client.Name + ")");
                        }
                        else
                        {
                            client.NetConnection.Deny("No available player slots.");
                            Console.WriteLine("Player connection refused: server full.");
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var newStatus = (NetConnectionStatus)msg.ReadByte();

                        if (newStatus == NetConnectionStatus.Connected)
                        {
                            bool sendMsg = true;

                            if (_resources != null) _resources.ForEach(fs => fs.Script.API.invokePlayerConnected(client));

                            //if (sendMsg) API.sendNotificationToAll("Player ~h~" + client.Name + "~h~ has connected.");

                            Console.WriteLine("New player connected: " + client.SocialClubName + " (" + client.Name + ")");
                        }
                        else if (newStatus == NetConnectionStatus.Disconnected)
                        {
                            lock (Clients)
                            {
                                if (Clients.Contains(client))
                                {
                                    var sendMsg = true;

                                    if (_resources != null) _resources.ForEach(fs => fs.Script.API.invokePlayerDisconnected(client));

                                    //if (sendMsg) API.sendNotificationToAll("Player ~h~" + client.Name + "~h~ has disconnected.");

                                    var dcObj = new PlayerDisconnect()
                                    {
                                        Id = client.NetConnection.RemoteUniqueIdentifier,
                                    };

                                    SendToAll(dcObj, PacketType.PlayerDisconnect, true);

                                    Console.WriteLine("Player disconnected: " + client.SocialClubName + " (" + client.Name + ")");

                                    Clients.Remove(client);
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
                        obj.Gamemode = GamemodeName;
                        lock (Clients) obj.PlayerCount = (short)Clients.Count(c => DateTime.Now.Subtract(c.LastUpdate).TotalMilliseconds < 60000);
                        obj.Port = Port;

                        var bin = SerializeBinary(obj);

                        response.Write((int)PacketType.DiscoveryResponse);
                        response.Write(bin.Length);
                        response.Write(bin);

                        Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                        break;
                    case NetIncomingMessageType.Data:
                        var packetType = (PacketType)msg.ReadInt32();

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
                                                if (_resources != null) _resources.ForEach(fs => pass = pass && !fs.Script.API.invokeChatCommand(client, data.Message));
                                                break;
                                            }

                                            if (_resources != null) _resources.ForEach(fs => pass = pass && !fs.Script.API.invokeChatMessage(client, data.Message));

                                            if (pass)
                                            {
                                                data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                data.Sender = client.Name;
                                                SendToAll(data, PacketType.ChatData, true);
                                                Console.WriteLine(data.Sender + ": " + data.Message);
                                            }
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
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
                                            data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                            data.Name = client.Name;
                                            data.Latency = client.Latency;
                                            data.NetHandle = client.CharacterHandle;

                                            client.Health = data.PlayerHealth;
                                            client.Position = data.Position;
                                            client.VehicleHealth = data.VehicleHealth;
                                            client.IsInVehicle = true;
                                            client.CurrentVehicle = data.VehicleHandle;
                                            client.Rotation = data.Quaternion;
                                            client.LastUpdate = DateTime.Now;

                                            if (NetEntityHandler.ToDict().ContainsKey(data.VehicleHandle))
                                            {
                                                NetEntityHandler.ToDict()[data.VehicleHandle].Position = data.Position;
                                                NetEntityHandler.ToDict()[data.VehicleHandle].Rotation = data.Quaternion;
                                                ((VehicleProperties) NetEntityHandler.ToDict()[data.VehicleHandle])
                                                    .PrimaryColor = data.PrimaryColor;
                                                ((VehicleProperties)NetEntityHandler.ToDict()[data.VehicleHandle])
                                                    .SecondaryColor = data.SecondaryColor;
                                            }

                                            SendToAll(data, PacketType.VehiclePositionData, false, client);
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
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
                                            data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                            data.Name = client.Name;
                                            data.Latency = client.Latency;
                                            data.NetHandle = client.CharacterHandle;

                                            client.Health = data.PlayerHealth;
                                            client.Position = data.Position;
                                            client.IsInVehicle = false;
                                            client.LastUpdate = DateTime.Now;

                                            client.Rotation = data.Quaternion;

                                            SendToAll(data, PacketType.PedPositionData, false, client);
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
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
                                            data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                            SendToAll(data, PacketType.NpcVehPositionData, false, client);
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
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
                                            data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                            SendToAll(data, PacketType.NpcPedPositionData, false, client);
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
                                }
                                break;
                            case PacketType.WorldSharingStop:
                                {
                                    var dcObj = new PlayerDisconnect()
                                    {
                                        Id = client.NetConnection.RemoteUniqueIdentifier,
                                    };
                                    SendToAll(dcObj, PacketType.WorldSharingStop, true);
                                }
                                break;
                            case PacketType.ScriptEventTrigger:
                                {
                                    var len = msg.ReadInt32();
                                    var data =
                                        DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                                    if (data != null)
                                    {
                                        _resources.ForEach(
                                            en =>
                                                en.Script.invokeClientEvent(client, data.EventName,
                                                    DecodeArgumentList(data.Arguments.ToArray()).ToArray()));
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
                                        resp = new LVector3()
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
                            case PacketType.PlayerKilled:
                                {
                                    if (_resources != null) _resources.ForEach(fs => fs.Script.API.invokePlayerDeath(client));
                                }
                                break;
                        }
                        break;
                    default:
                        Console.WriteLine("WARN: Unhandled type: " + msg.MessageType);
                        break;
                }
                Server.Recycle(msg);
            }
            if (_resources != null) _resources.ForEach(fs => fs.Script.API.invokeUpdate());
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
                    list.Add(new LVector3(tmp.X, tmp.Y, tmp.Z));
                }
            }

            return list;
        }

        public void SendToClient(Client c, object newData, PacketType packetType, bool important)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendMessage(msg, c.NetConnection,
                important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                GetChannelIdForConnection(c));
        }

        public void SendToAll(object newData, PacketType packetType, bool important)
        {
            foreach (var client in Clients)
            {
                var data = SerializeBinary(newData);
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((int)packetType);
                msg.Write(data.Length);
                msg.Write(data);
                Server.SendMessage(msg, client.NetConnection,
                    important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                    GetChannelIdForConnection(client));
            }
        }

        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, exclude.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, GetChannelIdForConnection(exclude));
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
                    Console.WriteLine("WARN: Deserialization failed: " + e.Message);
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

        public byte GetChannelIdForConnection(Client conn)
        {
            lock (Clients) return (byte)(((Clients.IndexOf(conn)) % 31) + 1);
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
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is LVector3)
                {
                    var tmp = (LVector3)o;
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
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void SendNativeCallToAllPlayers(ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Arguments = ParseNativeArguments(arguments);
            obj.ReturnType = null;
            obj.Id = null;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
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

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
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

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
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

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
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

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
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
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        // SCRIPTING

        
    }
}
