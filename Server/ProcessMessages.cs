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
    internal partial class GameServer
    {
        public void ProcessMessages()
        {
            var messages = new List<NetIncomingMessage>();
            var msgsRead = Server.ReadMessages(messages);
            if (msgsRead <= 0) return;

            var count = messages.Count;
            for (var index1 = 0; index1 < count; index1++)
            {
                var msg = messages[index1];
                Client client = null;
                lock (Clients)
                {
                    for (var index = Clients.Count - 1; index >= 0; index--)
                    {
                        var c = Clients[index];
                        if (c == null || c.NetConnection == null || c.NetConnection.RemoteUniqueIdentifier == 0 || msg.SenderConnection == null
                            || c.NetConnection.RemoteUniqueIdentifier != msg.SenderConnection.RemoteUniqueIdentifier) continue;
                        client = c;
                        break;
                    }
                }

                if (client == null) client = new Client(msg.SenderConnection);
                var packetType = PacketType.ConnectionPacket;

                try
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.DiscoveryRequest:
                        {
                            //Program.Output("Discovery Request from: [" + msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port + "] " + msg.SenderConnection.RemoteUniqueIdentifier);
                            var response = Server.CreateMessage();
                            var obj = new DiscoveryResponse
                            {
                                ServerName = Name,
                                MaxPlayers = (short)MaxPlayers,
                                PasswordProtected = PasswordProtected,
                                Gamemode = string.IsNullOrEmpty(GamemodeName)
                                    ? Gamemode?
                                          .DirectoryName ?? "GTA Network"
                                    : GamemodeName,
                                PlayerCount = (short)
                                    Clients.Count,
                                Port = Port,
                                LAN = isIPLocal(msg.SenderEndPoint.Address.ToString())
                            };

                            if (obj.LAN && AnnounceToLAN || !obj.LAN)
                            {
                                var bin = SerializeBinary(obj);

                                response.Write((byte)PacketType.DiscoveryResponse);
                                response.Write(bin.Length);
                                response.Write(bin);

                                Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                            }
                        }
                            break;

                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            client.Latency = msg.ReadFloat();
                            break;

                        case NetIncomingMessageType.ConnectionApproval:
                        {
                            if (Conntimeout)
                            {
                                lock (queue)
                                {
                                    if (queue.ContainsKey(client.NetConnection.RemoteUniqueIdentifier))
                                    {
                                        client.NetConnection.Deny("Wait atleast 60 seconds before reconnecting..");
                                        continue;
                                    }
                                    else
                                    {
                                        queue.Add(client.NetConnection.RemoteUniqueIdentifier, DateTime.Now);
                                    }
                                }
                            }
                            Program.Output("Initiating connection: [" + client.NetConnection.RemoteEndPoint.Address + ":" + client.NetConnection.RemoteEndPoint.Port + "]");
                            msg.ReadByte();
                            var leng = msg.ReadInt32();
                            ConnectionRequest connReq;
                            try
                            {
                                connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                            }
                            catch (EndOfStreamException)
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(connReq?.DisplayName) ||
                                string.IsNullOrWhiteSpace(connReq.SocialClubName) ||
                                string.IsNullOrWhiteSpace(connReq.ScriptVersion))
                            {
                                client.NetConnection.Deny("Outdated version!\nPlease update your client.");
                                continue;
                            }

                            var cVersion = ParseableVersion.Parse(connReq.ScriptVersion);
                            if (cVersion < MinimumClientVersion ||
                                cVersion < VersionCompatibility.LastCompatibleClientVersion)
                            {
                                client.NetConnection.Deny("Outdated version!\nPlease update your client.");
                                continue;
                            }

                            if (BanManager.IsClientBanned(client))
                            {
                                client.NetConnection.Deny("You are banned from the server.");
                                continue;
                            }

                            if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                            {
                                if (Password != connReq.Password)
                                {
                                    client.NetConnection.Deny("Wrong password!");
                                    Program.Output("Player connection refused: wrong password. (" + client.NetConnection.RemoteEndPoint.Address + ")");
                                    continue;
                                }
                            }

                            lock (Clients)
                            {
                                var duplicate = 0;
                                var displayname = connReq.DisplayName;

                                //SocialClubName checker
                                if (Clients.Any(c => c.SocialClubName == connReq.SocialClubName))
                                {
                                    client.NetConnection.Deny("Duplicate RGSC handle.");
                                     Program.Output("Player connection refused: duplicate RGSC.");
                                     continue;
                                }

                                while (AllowDisplayNames && Clients.Any(c => c.Name == connReq.DisplayName))
                                {
                                    duplicate++;
                                    connReq.DisplayName = displayname + " (" + duplicate + ")";
                                }
                            }

                            client.CommitConnection(); //Create PedHandle
                            client.SocialClubName = connReq.SocialClubName;
                            client.CEF = connReq.CEF;
                            client.MediaStream = connReq.MediaStream;
                            client.Name = AllowDisplayNames ? connReq.DisplayName : connReq.SocialClubName;
                            client.RemoteScriptVersion = ParseableVersion.Parse(connReq.ScriptVersion);
                            client.GameVersion = connReq.GameVersion;
                            client.ConnectionConfirmed = false;
                            ((PlayerProperties)NetEntityHandler.ToDict()[client.handle.Value]).Name = client.Name;

                            if (!AllowCEFDevTool && connReq.CEFDevtool)
                            {
                                client.NetConnection.Deny("CEF DevTool is not allowed.");
                                Program.Output("Player connection refused: CEF Devtool enabled. (" + client.NetConnection.RemoteEndPoint.Address + ")");
                                continue;
                            }

                            var respObj = new ConnectionResponse
                            {
                                ServerVersion = _serverVersion.ToString(),
                                CharacterHandle = client.handle.Value,
                                Settings = new SharedSettings
                                {
                                    ModWhitelist = ModWhitelist,
                                    UseHttpServer = UseHTTPFileServer,
                                }
                            };

                            var channelHail = Server.CreateMessage();
                            var respBin = SerializeBinary(respObj);

                            channelHail.Write(respBin.Length);
                            channelHail.Write(respBin);

                            var cancelArgs = new CancelEventArgs();

                            lock (RunningResources)
                            {
                                RunningResources.ForEach(fs => fs.Engines.ForEach(en => en.InvokePlayerBeginConnect(client, cancelArgs)));
                            }

                            Clients.Add(client);
                            Server.Configuration.CurrentPlayers = Clients.Count;
                            client.NetConnection.Approve(channelHail);

                            Program.Output("Processing connection: " + client.SocialClubName + " (" + client.Name + ") [" + client.NetConnection.RemoteEndPoint.Address + "]");
                        }
                            break;

                        case NetIncomingMessageType.StatusChanged:
                        {

                            var newStatus = (NetConnectionStatus)msg.ReadByte();

                            switch (newStatus)
                            {
                                case NetConnectionStatus.Connected:

                                    break;

                                case NetConnectionStatus.Disconnected:
                                {
                                    var reason = msg.ReadString();
                                    if (Clients.Contains(client))
                                    {
                                        lock (RunningResources)
                                        {
                                            RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                            {
                                                en.InvokePlayerDisconnected(client, reason);
                                            }));
                                        }

                                        UnoccupiedVehicleManager.UnsyncAllFrom(client);

                                        lock (Clients)
                                        {
                                            var dcObj = new PlayerDisconnect() { Id = client.handle.Value };

                                            SendToAll(dcObj, PacketType.PlayerDisconnect, true, ConnectionChannel.SyncEvent);

                                            Program.Output("Player disconnected: " + client.SocialClubName + " (" +
                                                           client.Name + ") [" +
                                                           client.NetConnection.RemoteEndPoint.Address + "], reason: " +
                                                           reason);

                                            int vehValue = client.CurrentVehicle.Value;

                                            if (vehValue != 0 &&
                                                VehicleOccupants.ContainsKey(vehValue) &&
                                                VehicleOccupants[vehValue].Contains(client))
                                                VehicleOccupants[vehValue].Remove(client);

                                            Clients.Remove(client);
                                            Server.Configuration.CurrentPlayers = Clients.Count;
                                            NetEntityHandler.DeleteEntityQuiet(client.handle.Value);
                                            if (ACLEnabled) ACL.LogOutClient(client);

                                            Downloads.RemoveAll(d => d.Parent == client);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                            break;

                        case NetIncomingMessageType.Data:

                            packetType = (PacketType)msg.ReadByte();
                            //Console.WriteLine("Called... " + packetType);
                            switch (packetType)
                            {
                                case PacketType.ConnectionConfirmed:
                                {
                                    var state = msg.ReadBoolean();
                                    if (!state)
                                    {
                                        var delta = new Delta_PlayerProperties { Name = client.Name };
                                        UpdateEntityInfo(client.handle.Value, EntityType.Player, delta, client);

                                        var mapObj = new ServerMap
                                        {
                                            World =
                                                Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1)
                                        };

                                        foreach (var pair in NetEntityHandler.ToCopy())
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
                                            else if (pair.Value.EntityType == (byte)EntityType.TextLabel)
                                            {
                                                mapObj.TextLabels.Add(pair.Key, (TextLabelProperties)pair.Value);
                                            }
                                            else if (pair.Value.EntityType == (byte)EntityType.Ped)
                                            {
                                                mapObj.Peds.Add(pair.Key, (PedProperties)pair.Value);
                                            }
                                            else if (pair.Value.EntityType == (byte)EntityType.Particle)
                                            {
                                                mapObj.Particles.Add(pair.Key, (ParticleProperties)pair.Value);
                                            }
                                        }

                                        // TODO: replace this filth
                                        var r = new Random();

                                        var mapData = new StreamedData
                                        {
                                            Id = r.Next(int.MaxValue),
                                            Data = SerializeBinary(mapObj),
                                            Type = FileType.Map
                                        };

                                        var downloader = new StreamingClient(client);
                                        downloader.Files.Add(mapData);

                                        if (!UseHTTPFileServer)
                                        {
                                            lock (RunningResources)
                                            {
                                                foreach (var resource in RunningResources)
                                                {
                                                    foreach (var file in resource.Info.Files)
                                                    {
                                                        var fileData = new StreamedData
                                                        {
                                                            Id = r.Next(int.MaxValue),
                                                            Type = FileType.Normal,
                                                            Data = File.ReadAllBytes("resources" +
                                                                                     Path.DirectorySeparatorChar +
                                                                                     resource.DirectoryName +
                                                                                     Path.DirectorySeparatorChar +
                                                                                     file.Path),
                                                            Name = file.Path,
                                                            Resource = resource.DirectoryName,
                                                            Hash = FileHashes.ContainsKey(resource.DirectoryName + "_" +
                                                                                          file.Path)
                                                                ? FileHashes[
                                                                    resource.DirectoryName + "_" + file.Path]
                                                                : null
                                                        };
                                                        downloader.Files.Add(fileData);
                                                    }
                                                }
                                            }
                                        }

                                        foreach (var script in GetAllClientsideScripts())
                                        {
                                            var scriptData = new StreamedData
                                            {
                                                Id = r.Next(int.MaxValue),
                                                Data = Encoding.UTF8.GetBytes(script.Script),
                                                Type = FileType.Script,
                                                Resource = script.ResourceParent,
                                                Hash = script.MD5Hash,
                                                Name = script.Filename
                                            };
                                            downloader.Files.Add(scriptData);
                                        }

                                        var endStream = new StreamedData
                                        {
                                            Id = r.Next(int.MaxValue),
                                            Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D },
                                            Type = FileType.EndOfTransfer
                                        };
                                        downloader.Files.Add(endStream);


                                        Downloads.Add(downloader);

                                        lock (RunningResources)
                                            RunningResources.ForEach(
                                                fs => fs.Engines.ForEach(en => { en.InvokePlayerConnected(client); }));

                                        client.ConnectionConfirmed = true;
                                        Program.Output("Connection established: " + client.SocialClubName + " (" +
                                                       client.Name + ") [" +
                                                       client.NetConnection.RemoteEndPoint.Address + "]");
                                    }
                                    else
                                    {
                                        var length = msg.ReadInt32();
                                        string[] resources = new string[length];

                                        for (int i = 0; i < length; i++)
                                        {
                                            resources[i] = msg.ReadString();
                                        }

                                        lock (RunningResources)
                                        {
                                            RunningResources.ForEach(fs =>
                                            {
                                                if (Array.IndexOf(resources, fs.DirectoryName) == -1) return;
                                                fs.Engines.ForEach(en => { en.InvokePlayerDownloadFinished(client); });
                                            });
                                        }
                                        StressTest.HasPlayers = true;
                                    }
                                }
                                    break;

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
                                                    pass =
                                                        ACL.DoesUserHaveAccessToCommand(client,
                                                            data.Message.Split()[0].TrimStart('/'));
                                                }

                                                if (pass)
                                                {
                                                    ThreadPool.QueueUserWorkItem(delegate
                                                    {
                                                        var cancelArg = new CancelEventArgs();

                                                        lock (RunningResources)
                                                        {
                                                            RunningResources.ForEach(
                                                                fs =>
                                                                    fs.Engines.ForEach(
                                                                        en =>
                                                                            en.InvokeChatCommand(client,
                                                                                data.Message, cancelArg)));
                                                        }

                                                        if (!cancelArg.Cancel)
                                                        {
                                                            if (!CommandHandler.Parse(client, data.Message))
                                                                PublicAPI.sendChatMessageToPlayer(client, ErrorCmd);
                                                        }
                                                    });
                                                }
                                                else
                                                {
                                                    var chatObj = new ChatData()
                                                    {
                                                        Sender = "",
                                                        Message = "You don't have access to this command!",
                                                    };

                                                    var binData = SerializeBinary(chatObj);

                                                    var respMsg = Program.ServerInstance.Server.CreateMessage();
                                                    respMsg.Write((byte)PacketType.ChatData);
                                                    respMsg.Write(binData.Length);
                                                    respMsg.Write(binData);
                                                    client.NetConnection.SendMessage(respMsg,
                                                        NetDeliveryMethod.ReliableOrdered, 0);
                                                }

                                                continue;
                                            }

                                            ThreadPool.QueueUserWorkItem(delegate
                                            {
                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        fs =>
                                                            fs.Engines.ForEach(
                                                                en =>
                                                                    pass =
                                                                        pass &&
                                                                        en.InvokeChatMessage(client,
                                                                            data.Message)));

                                                if (pass)
                                                {
                                                    data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                    data.Sender = client.Name;
                                                    SendToAll(data, PacketType.ChatData, true,
                                                        ConnectionChannel.Chat);
                                                    Program.Output(data.Sender + ": " + data.Message);
                                                }
                                            });
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    {
                                    }
                                }
                                    break;

                                case PacketType.VehiclePureSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        var fullPacket = PacketOptimization.ReadPureVehicleSync(bin);

                                        fullPacket.NetHandle = client.handle.Value;

                                        if (fullPacket.PlayerHealth.Value != client.Health)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerHealthChange(client, client.Health);
                                                }));
                                        }

                                        if (fullPacket.PedArmor.Value != client.Armor)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerArmorChange(client, client.Armor);
                                                }));
                                        }

                                        if (fullPacket.WeaponHash != null &&
                                            fullPacket.WeaponHash.Value != (int)client.CurrentWeapon)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerWeaponChange(client, (int)client.CurrentWeapon);
                                                }));
                                        }

                                        client.Health = fullPacket.PlayerHealth.Value;
                                        client.Armor = fullPacket.PedArmor.Value;
                                        client.LastVehicleFlag = fullPacket.Flag.Value;
                                        client.LastUpdate = DateTime.Now;

                                        if (fullPacket.WeaponHash != null)
                                            client.CurrentWeapon = (WeaponHash)fullPacket.WeaponHash.Value;

                                        if (PacketOptimization.CheckBit(fullPacket.Flag.Value,
                                                VehicleDataFlags.HasAimData) && fullPacket.AimCoords != null)
                                        {
                                            client.LastAimPos = fullPacket.AimCoords;
                                        }
                                        else
                                        {
                                            client.LastAimPos = null;
                                        }

                                        if (PacketOptimization.CheckBit(fullPacket.Flag.Value,
                                            VehicleDataFlags.Driver))
                                        {
                                            client.Position = fullPacket.Position;
                                            client.Rotation = fullPacket.Quaternion;
                                            client.Velocity = fullPacket.Velocity;

                                            var handlerDict = NetEntityHandler.ToDict();
                                            int vehValue = client.CurrentVehicle.Value;

                                            if (!client.CurrentVehicle.IsNull &&
                                                handlerDict
                                                    .ContainsKey(vehValue))
                                            {
                                                var props = handlerDict[vehValue];

                                                props.Position
                                                    = fullPacket.Position;
                                                props.Rotation
                                                    = fullPacket.Quaternion;
                                                props.Velocity
                                                    = fullPacket.Velocity;
                                                if (fullPacket.Flag.HasValue)
                                                {
                                                    var newDead = (fullPacket.Flag &
                                                                   (byte)VehicleDataFlags.VehicleDead) > 0;
                                                    if (!((VehicleProperties)
                                                                props)
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
                                                            props)
                                                        .IsDead = newDead;
                                                }

                                                if (fullPacket.VehicleHealth.HasValue)
                                                {
                                                    if (fullPacket.VehicleHealth.Value != ((VehicleProperties)
                                                            props)
                                                        .Health)
                                                    {
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                            {
                                                                en.InvokeVehicleHealthChange(client,
                                                                    ((VehicleProperties)
                                                                        props)
                                                                    .Health);
                                                            }));
                                                    }

                                                    ((VehicleProperties)
                                                            props)
                                                        .Health = fullPacket.VehicleHealth.Value;
                                                }

                                                if (fullPacket.Flag.HasValue)
                                                {
                                                    if ((fullPacket.Flag &
                                                         (byte)VehicleDataFlags.SirenActive) != 0 ^
                                                        ((VehicleProperties)
                                                            props)
                                                        .Siren)
                                                    {
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                            {
                                                                en.InvokeVehicleSirenToggle(client, ((VehicleProperties)
                                                                        NetEntityHandler.ToDict()[
                                                                            client.CurrentVehicle.Value])
                                                                    .Siren);
                                                            }));
                                                    }

                                                    ((VehicleProperties)
                                                            props)
                                                        .Siren = (fullPacket.Flag &
                                                                  (byte)VehicleDataFlags.SirenActive) > 0;
                                                }
                                            }

                                            int netHandleValue = fullPacket.NetHandle.Value;

                                            if (handlerDict
                                                .ContainsKey(netHandleValue))
                                            {
                                                var props = handlerDict[netHandleValue];

                                                props.Position =
                                                    fullPacket.Position;
                                                props.Rotation =
                                                    fullPacket.Quaternion;
                                                props.Velocity =
                                                    fullPacket.Velocity;
                                            }
                                        }
                                        else if (!client.CurrentVehicle.IsNull &&
                                                 NetEntityHandler.ToDict().ContainsKey(client.CurrentVehicle.Value))
                                         {
                                            var props = NetEntityHandler.ToDict()[client.CurrentVehicle.Value];

                                            var carPos =
                                                props.Position;
                                            var carRot =
                                                props.Rotation;
                                            var carVel =
                                                props.Velocity;

                                            client.Position = carPos;
                                            client.Rotation = carRot;
                                            client.Velocity = carVel;

                                            if (NetEntityHandler.ToDict()
                                                .ContainsKey(fullPacket.NetHandle.Value))
                                            {
                                                var playerProps = NetEntityHandler.ToDict()[fullPacket.NetHandle.Value];

                                                playerProps.Position =
                                                    carPos;
                                                playerProps.Rotation =
                                                    carRot;
                                                playerProps.Velocity =
                                                    carVel;
                                            }
                                        }
                                        client.IsInVehicle = true;

                                        ResendPacket(fullPacket, client, true);

                                        UpdateAttachables(client.handle.Value);
                                        UpdateAttachables(client.CurrentVehicle.Value);
                                    }
                                    catch (IndexOutOfRangeException)
                                    {
                                    }
                                }
                                    break;

                                case PacketType.VehicleLightSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        var fullPacket = PacketOptimization.ReadLightVehicleSync(bin);

                                        fullPacket.NetHandle = client.handle.Value;
                                        fullPacket.Latency = client.Latency;

                                        client.IsInVehicle = true;
                                        client.VehicleSeat = fullPacket.VehicleSeat.Value;

                                        var car = new NetHandle(fullPacket.VehicleHandle.Value);

                                        if (!client.IsInVehicleInternal || client.VehicleHandleInternal != car.Value)
                                        {
                                            if (!VehicleOccupants.ContainsKey(car.Value))
                                            {
                                                VehicleOccupants.Add(car.Value, new List<Client>());
                                            }

                                            if (!VehicleOccupants[car.Value].Contains(client))
                                                VehicleOccupants[car.Value].Add(client);

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


                                        if (fullPacket.DamageModel != null)
                                        {
                                            if (((VehicleProperties)
                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                .DamageModel ==
                                                null)
                                            {
                                                ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel = new VehicleDamageModel();
                                            }

                                            var oldDoors = ((VehicleProperties)
                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                .DamageModel.BrokenDoors;

                                            var oldWindows = ((VehicleProperties)
                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                .DamageModel.BrokenWindows;

                                            ((VehicleProperties)
                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                .DamageModel = fullPacket.DamageModel;

                                            if ((oldDoors ^ fullPacket.DamageModel.BrokenDoors) != 0)
                                            {
                                                lock (RunningResources)
                                                {
                                                    for (int k = 0; k < 8; k++)
                                                    {
                                                        if (((oldDoors ^ fullPacket.DamageModel.BrokenDoors) &
                                                             1 << k) == 0) continue;
                                                        var localCopy = fullPacket.VehicleHandle.Value;
                                                        RunningResources.ForEach(
                                                            fs => fs.Engines.ForEach(en =>
                                                            {
                                                                en.InvokeVehicleDoorBreak(
                                                                    new NetHandle(localCopy),
                                                                    k);
                                                            }));
                                                    }
                                                }
                                            }


                                            if ((oldWindows ^ fullPacket.DamageModel.BrokenWindows) != 0)
                                            {
                                                lock (RunningResources)
                                                {
                                                    for (int k = 0; k < 8; k++)
                                                    {
                                                        if (((oldDoors ^ fullPacket.DamageModel.BrokenWindows) &
                                                             1 << k) == 0) continue;
                                                        var localCopy = fullPacket.VehicleHandle.Value;

                                                        RunningResources.ForEach(
                                                            fs => fs.Engines.ForEach(en =>
                                                            {
                                                                en.InvokeVehicleWindowBreak(
                                                                    new NetHandle(localCopy),
                                                                    k);
                                                            }));
                                                    }
                                                }
                                            }
                                        }

                                        ResendPacket(fullPacket, client, false);
                                    }
                                    catch (IndexOutOfRangeException)
                                    {
                                    }
                                    catch (KeyNotFoundException)
                                    {
                                    } //Proper fix is needed but this isn't very problematic
                                }
                                    break;

                                case PacketType.PedPureSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        var fullPacket = PacketOptimization.ReadPurePedSync(bin);

                                        fullPacket.NetHandle = client.handle.Value;

                                        var oldHealth = client.Health;
                                        var oldArmor = client.Armor;
                                        var oldWeap = client.CurrentWeapon;
                                        var oldAmmo = client.Ammo;

                                        client.Health = fullPacket.PlayerHealth.Value;
                                        client.Armor = fullPacket.PedArmor.Value;
                                        client.Position = fullPacket.Position;
                                        client.LastUpdate = DateTime.Now;
                                        client.Rotation = fullPacket.Quaternion;
                                        client.Velocity = fullPacket.Velocity;
                                        client.CurrentWeapon = (WeaponHash)fullPacket.WeaponHash.Value;
                                        client.Ammo = fullPacket.WeaponAmmo.Value;
                                        client.Weapons[client.CurrentWeapon] = client.Ammo;
                                        if (fullPacket.Flag != null) client.LastPedFlag = fullPacket.Flag.Value;

                                        if (fullPacket.PlayerHealth.Value != oldHealth)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerHealthChange(client, oldHealth);
                                                }));
                                        }

                                        if (fullPacket.PedArmor.Value != oldArmor)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerArmorChange(client, oldArmor);
                                                }));
                                        }

                                        if (fullPacket.WeaponHash.Value != (int)oldWeap)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerWeaponChange(client, (int)oldWeap);
                                                }));
                                        }

                                        if (fullPacket.WeaponAmmo.Value != oldAmmo &&
                                            fullPacket.WeaponHash.Value == (int)oldWeap)
                                        {
                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerWeaponAmmoChange(client, (int)oldWeap, oldAmmo);
                                                }));
                                        }

                                        if (client.IsInVehicleInternal && !client.CurrentVehicle.IsNull)
                                        {
                                            if (client.CurrentVehicle.Value != 0 &&
                                                VehicleOccupants.ContainsKey(client.CurrentVehicle.Value) &&
                                                VehicleOccupants[client.CurrentVehicle.Value].Contains(client))
                                                VehicleOccupants[client.CurrentVehicle.Value].Remove(client);


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
                                            NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Position =
                                                fullPacket.Position;
                                            NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Rotation =
                                                fullPacket.Quaternion;
                                        }

                                        ResendPacket(fullPacket, client, true);
                                        UpdateAttachables(client.handle.Value);
                                        //SendToAll(data, PacketType.PedPositionData, false, client, ConnectionChannel.PositionData);
                                    }
                                    catch (IndexOutOfRangeException)
                                    {
                                    }
                                }
                                    break;

                                case PacketType.PedLightSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        var fullPacket = PacketOptimization.ReadLightPedSync(bin);

                                        fullPacket.NetHandle = client.handle.Value;
                                        fullPacket.Latency = client.Latency;

                                        if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                        {
                                            if (client.ModelHash == 0) client.ModelHash = fullPacket.PedModelHash.Value;

                                            var oldValue = client.ModelHash;
                                            if (oldValue != fullPacket.PedModelHash.Value)
                                            {
                                                client.ModelHash = fullPacket.PedModelHash.Value;
                                                NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash =
                                                    fullPacket.PedModelHash.Value;
                                                lock (RunningResources)
                                                    RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                    {
                                                        en.InvokePlayerModelChange(client, oldValue);
                                                    }));
                                            }
                                        }
                                        ResendPacket(fullPacket, client, false);
                                    }
                                    catch (IndexOutOfRangeException)
                                    {
                                        // ignored
                                    }
                                }
                                    break;

                                case PacketType.BulletSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        int netHandle;
                                        Vector3 aimPoint;

                                        var shooting =
                                            PacketOptimization.ReadBulletSync(bin, out netHandle, out aimPoint);

                                        netHandle = client.handle.Value;

                                        ResendBulletPacket(netHandle, aimPoint, shooting, client);
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                                    break;

                                case PacketType.BulletPlayerSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        int netHandle;
                                        int netHandleTarget;

                                        var shooting =
                                            PacketOptimization.ReadBulletSync(bin, out netHandle, out netHandleTarget);

                                        netHandle = client.handle.Value;

                                        ResendBulletPacket(netHandle, netHandleTarget, shooting, client);
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                                    break;

                                case PacketType.UnoccupiedVehSync:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    try
                                    {
                                        var len = msg.ReadInt32();
                                        var bin = msg.ReadBytes(len);

                                        for (int i = 0; i < bin[0]; i++)
                                        {
                                            var cVehBin = bin.Skip(1 + 46 * i).Take(46).ToArray();

                                            var fullPacket = PacketOptimization.ReadUnoccupiedVehicleSync(cVehBin);

                                            if (NetEntityHandler.ToDict()
                                                .ContainsKey(fullPacket.VehicleHandle.Value))
                                            {
                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Position
                                                    = fullPacket.Position;
                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Rotation
                                                    = fullPacket.Quaternion;
                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Velocity
                                                    = fullPacket.Velocity;

                                                ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .Tires = fullPacket.PlayerHealth.Value;

                                                if (((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel == null)
                                                    ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .DamageModel = new VehicleDamageModel();

                                                var oldDoors = ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel.BrokenWindows;
                                                var oldWindows = ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel.BrokenDoors;

                                                ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel.BrokenWindows =
                                                    fullPacket.DamageModel.BrokenWindows;
                                                ((VehicleProperties)
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                    .DamageModel.BrokenDoors =
                                                    fullPacket.DamageModel.BrokenDoors;

                                                if ((oldDoors ^ fullPacket.DamageModel.BrokenDoors) != 0)
                                                {
                                                    lock (RunningResources)
                                                    {
                                                        for (int k = 0; k < 8; k++)
                                                        {
                                                            if (((oldDoors ^ fullPacket.DamageModel.BrokenDoors) &
                                                                 1 << k) == 0) continue;

                                                            RunningResources.ForEach(
                                                                fs => fs.Engines.ForEach(en =>
                                                                {
                                                                    if (fullPacket.VehicleHandle != null)
                                                                        en.InvokeVehicleDoorBreak(
                                                                            new NetHandle(
                                                                                fullPacket.VehicleHandle.Value), k);
                                                                }));
                                                        }
                                                    }
                                                }


                                                if ((oldWindows ^ fullPacket.DamageModel.BrokenWindows) != 0)
                                                {
                                                    lock (RunningResources)
                                                    {
                                                        for (int k = 0; k < 8; k++)
                                                        {
                                                            if (((oldDoors ^ fullPacket.DamageModel.BrokenWindows) &
                                                                 1 << k) == 0) continue;

                                                            RunningResources.ForEach(
                                                                fs => fs.Engines.ForEach(en =>
                                                                {
                                                                    if (fullPacket.VehicleHandle != null)
                                                                        en.InvokeVehicleWindowBreak(
                                                                            new NetHandle(
                                                                                fullPacket.VehicleHandle.Value), k);
                                                                }));
                                                        }
                                                    }
                                                }

                                                if (fullPacket.Flag.HasValue)
                                                {
                                                    var newDead = (fullPacket.Flag &
                                                                   (byte)VehicleDataFlags.VehicleDead) > 0;
                                                    var oldDead = ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                            ])
                                                        .IsDead;

                                                    ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .IsDead = newDead;

                                                    if (!oldDead && newDead)
                                                    {
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(
                                                                fs => fs.Engines.ForEach(en =>
                                                                {
                                                                    if (fullPacket.VehicleHandle != null)
                                                                        en.InvokeVehicleDeath(new NetHandle(fullPacket
                                                                            .VehicleHandle.Value));
                                                                }));
                                                    }
                                                }

                                                if (fullPacket.VehicleHealth.HasValue)
                                                {
                                                    var oldValue = ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                            ])
                                                        .Health;

                                                    ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                            ])
                                                        .Health = fullPacket.VehicleHealth.Value;

                                                    if (fullPacket.VehicleHealth.Value != oldValue)
                                                    {
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(
                                                                fs => fs.Engines.ForEach(en =>
                                                                {
                                                                    en.InvokeVehicleHealthChange(client,
                                                                        oldValue);
                                                                }));
                                                    }
                                                }
                                            }

                                            ResendUnoccupiedPacket(fullPacket, client);

                                            UpdateAttachables(fullPacket.VehicleHandle.Value);
                                        }
                                    }
                                    catch (IndexOutOfRangeException ex)
                                    {
                                        if (LogLevel > 0) Program.Output(ex.ToString());
                                    }
                                }
                                    break;

                                case PacketType.SyncEvent:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    var len = msg.ReadInt32();
                                    var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                                    if (data != null)
                                    {
                                        SendToAll(data, PacketType.SyncEvent, true, client, ConnectionChannel.SyncEvent);
                                        HandleSyncEvent(client, data);
                                    }
                                }
                                    break;

                                case PacketType.ScriptEventTrigger:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    var len = msg.ReadInt32();
                                    var data =
                                        DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                                    if (data != null)
                                    {
                                        lock (RunningResources)
                                            RunningResources.ForEach(
                                                en =>
                                                {
                                                    if (en.DirectoryName != data.Resource) return;

                                                    en.Engines.ForEach(fs =>
                                                    {
                                                        fs.InvokeClientEvent(client, data.EventName,
                                                            DecodeArgumentListPure(data.Arguments?.ToArray() ??
                                                                                   new NativeArgument[0])
                                                                .ToArray());
                                                    });
                                                });
                                    }
                                }
                                    break;

                                case PacketType.NativeResponse:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    var len = msg.ReadInt32();
                                    var data = DeserializeBinary<NativeResponse>(msg.ReadBytes(len)) as NativeResponse;

                                    if (data == null || !_callbacks.ContainsKey(data.Id)) continue;
                                    object resp = null;
                                    var argument = data.Response as IntArgument;
                                    if (argument != null)
                                    {
                                        resp = argument.Data;
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
                                        {
                                            ourD.Files.RemoveAt(0);
                                        }
                                        else
                                        {
                                            ourD.Files[0].Accepted = true;
                                        }
                                    }
                                }
                                    break;

                                case PacketType.PlayerKilled:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    var reason = msg.ReadInt32();
                                    var weapon = msg.ReadInt32();

                                    lock (RunningResources)
                                    {
                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                        {
                                            en.InvokePlayerDeath(client, reason, weapon);
                                        }));
                                    }

                                    PublicAPI.setEntityData(client, "__LAST_PLAYER_DEATH", PublicAPI.TickCount);
                                }
                                    break;

                                case PacketType.PlayerRespawned:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    PublicAPI.removeAllPlayerWeapons(client);
                                    PublicAPI.stopPlayerAnimation(client);

                                    lock (RunningResources)
                                    {
                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                        {
                                            en.InvokePlayerRespawn(client);
                                        }));
                                    }

                                    PublicAPI.setEntityData(client, "__LAST_PLAYER_RESPAWN", PublicAPI.TickCount);
                                }
                                    break;

                                case PacketType.UpdateEntityProperties:
                                {
                                    if (!client.ConnectionConfirmed) continue;
                                    if (TrustClientProperties)
                                    {
                                        var len = msg.ReadInt32();
                                        var data = DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                                        if (data?.Properties != null)
                                        {
                                            var item = NetEntityHandler.NetToProp<EntityProperties>(data.NetHandle);

                                            if (item != null)
                                            {
                                                if (data.Properties.SyncedProperties != null)
                                                {
                                                    if (item.SyncedProperties == null)
                                                        item.SyncedProperties =
                                                            new Dictionary<string, NativeArgument>();
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
                                                            var ent = new NetHandle(data.NetHandle);
                                                            lock (RunningResources)
                                                            {
                                                                RunningResources.ForEach(
                                                                    fs => fs.Engines.ForEach(en =>
                                                                    {
                                                                        en.InvokeEntityDataChange(ent, pair.Key,
                                                                            oldValue);
                                                                    }));
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            UpdateEntityInfo(data.NetHandle, (EntityType)data.EntityType,
                                                data.Properties, client);
                                        }
                                    }
                                }
                                    break;

                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            break;

                        case NetIncomingMessageType.VerboseDebugMessage:
                            if (LogLevel > 2) Program.Output("[VERBOSE] " + msg.ReadString());
                            break;

                        case NetIncomingMessageType.DebugMessage:
                            if (LogLevel > 1) Program.Output("[DEBUG] " + msg.ReadString());
                            break;

                        case NetIncomingMessageType.ErrorMessage:
                            if (LogLevel > 0) Program.Output("[ERROR] " + msg.ReadString());
                            break;

                        case NetIncomingMessageType.WarningMessage:
                            Program.Output("[WARNING] " + msg.ReadString());
                            break;

                        default:
                            Program.Output("[LIBRARY WARNING] " + msg.MessageType + " | " + msg.DeliveryMethod);
                            break;
                    }
                }
                catch (InvalidCastException)
                {
                    //Program.ToFile("attack.log", "Suspected connection exploit [" + client.NetConnection.RemoteEndPoint.Address + "], Message type: " + msg.MessageType + " |" + " Packet type: " + packetType + " |" + " Exception: InvalidCastException");
                }
                catch (Exception ex)
                {
                    // Program.Output("EXCEPTION IN MESSAGEPUMP, MSG TYPE: " + msg.MessageType + " DATA TYPE: " + packetType);
                    // Program.Output(ex.ToString());
                    if (LogLevel > 0)
                    {
                        Program.Output("--> Exception in the Netcode.");
                        Program.Output("--> Message type: " + msg.MessageType + " |" + " Packet type: " + packetType);
                        Program.Output("\n===\n" + ex + "\n===");
                    }
                }
                finally
                {
                    Server.Recycle(msg);
                }
            }
        }
    }
}
