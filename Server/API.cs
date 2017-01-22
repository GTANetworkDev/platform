using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
using GTANetworkServer.Constant;
using GTANetworkServer.Managers;
using GTANetworkShared;
using Lidgren.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Extensions = GTANetworkShared.Extensions;

namespace GTANetworkServer
{
    public class CancelEventArgs
    {
        public bool Cancel { get; set; }
        public string Reason { get; set; }

        public CancelEventArgs() { }
        public CancelEventArgs(bool cancel)
        {
            Cancel = cancel;
        }
    }

    public class ResourceAbortedException : Exception
    {
        
    }

    public abstract class Script
    {
        public API API = new API();
    }

    public class API
    {
        #region META
        internal ScriptingEngine ResourceParent { get; set; }

        internal bool isPathSafe(string path)
        {
            if (ResourceParent == null) throw new NullReferenceException("Illegal call to isPathSafe inside constructor!");

            var absPath = Path.GetFullPath(path);
            var resourcePath = Path.GetFullPath("resources" + Path.DirectorySeparatorChar + ResourceParent.ResourceParent.DirectoryName);

            return absPath.StartsWith(resourcePath);
        }

        internal List<NetHandle> ResourceEntities = new List<NetHandle>();
        internal List<ColShape> ResourceColShapes = new List<ColShape>();
        public bool autoGarbageCollection = true;
        #endregion

        #region Delegates
        public delegate void EmptyEvent();
        public delegate void CommandEvent(Client sender, string command, CancelEventArgs cancel);
        public delegate void ChatEvent(Client sender, string message, CancelEventArgs cancel);
        public delegate void PlayerEvent(Client player);
        public delegate void PlayerConnectingEvent(Client player, CancelEventArgs cancelConnection);
        public delegate void PlayerDisconnectedEvent(Client player, string reason);
        public delegate void PlayerKilledEvent(Client player, NetHandle entityKiller, int weapon);
        public delegate void ServerEventTrigger(Client sender, string eventName, params object[] arguments);
        public delegate void PickupEvent(Client pickupee, NetHandle pickupHandle);
        public delegate void EntityEvent(NetHandle entity);
        public delegate void MapChangeEvent(string mapName, XmlGroup map);
        public delegate void VehicleChangeEvent(Client player, NetHandle vehicle);
        public delegate void GlobalColShapeEvent(ColShape colshape, NetHandle entity);
        public delegate void EntityDataChangedEvent(NetHandle entity, string key, object oldValue);
        public delegate void ResourceEvent(string resourceName);
        public delegate void DataReceivedEvent(string data);
        public delegate void PlayerIntEvent(Client player, int oldValue);
        public delegate void PlayerWeaponEvent(Client player, WeaponHash oldValue);
        public delegate void EntityHealthEvent(NetHandle entity, float oldValue);
        public delegate void EntityBooleanEvent(NetHandle entity, bool oldValue);
        public delegate void EntityIntEvent(NetHandle entity, int index);
        public delegate void TrailerEvent(NetHandle tower, NetHandle trailer);
        #endregion

        #region Events
        public event EmptyEvent onResourceStart;
        public event EmptyEvent onResourceStop;
        public event EmptyEvent onUpdate;
        public event ChatEvent onChatMessage;
        public event CommandEvent onChatCommand;
        public event PlayerConnectingEvent onPlayerBeginConnect;
        public event PlayerEvent onPlayerConnected;
        public event PlayerEvent onPlayerFinishedDownload;
        public event PlayerDisconnectedEvent onPlayerDisconnected;
        public event PlayerKilledEvent onPlayerDeath;
        public event PlayerEvent onPlayerRespawn;
        public event ServerEventTrigger onClientEventTrigger;
        public event PickupEvent onPlayerPickup;
        public event EntityEvent onPickupRespawn;
        public event MapChangeEvent onMapChange;
        public event VehicleChangeEvent onPlayerEnterVehicle;
        public event VehicleChangeEvent onPlayerExitVehicle;
        public event EntityEvent onVehicleDeath;
        public event GlobalColShapeEvent onEntityEnterColShape;
        public event GlobalColShapeEvent onEntityExitColShape;
        public event EntityDataChangedEvent onEntityDataChange;
        public event ResourceEvent onServerResourceStart;
        public event ResourceEvent onServerResourceStop;
        public event EntityHealthEvent onVehicleHealthChange;
        public event PlayerIntEvent onPlayerHealthChange;
        public event PlayerIntEvent onPlayerArmorChange;
        public event PlayerWeaponEvent onPlayerWeaponSwitch;
        public event EntityBooleanEvent onVehicleSirenToggle;
        public event EntityIntEvent onVehicleDoorBreak;
        public event EntityIntEvent onVehicleWindowSmash;
        public event EntityIntEvent onVehicleTyreBurst;
        public event TrailerEvent onVehicleTrailerChange;
        public event PlayerIntEvent onPlayerModelChange;
        public event PlayerEvent onPlayerDetonateStickies;

        internal void invokePlayerDetonateStickies(Client player)
        {
            onPlayerDetonateStickies?.Invoke(player);
        }

        internal void invokePlayerModelChange(Client player, int oldModel)
        {
            onPlayerModelChange?.Invoke(player, oldModel);
        }

        internal void invokeVehicleTrailerChange(NetHandle veh1, NetHandle veh2)
        {
            onVehicleTrailerChange?.Invoke(veh1, veh2);
        }

        internal void invokeVehicleDoorBreak(NetHandle vehicle, int index)
        {
            onVehicleDoorBreak?.Invoke(vehicle, index);
        }

        internal void invokeVehicleWindowBreak(NetHandle vehicle, int index)
        {
            onVehicleWindowSmash?.Invoke(vehicle, index);
        }

        internal void invokeVehicleTyreBurst(NetHandle vehicle, int index)
        {
            onVehicleTyreBurst?.Invoke(vehicle, index);
        }

        internal void invokeVehicleSirenToggle(NetHandle entity, bool oldValue)
        {
            onVehicleSirenToggle?.Invoke(entity, oldValue);
        }

        internal void invokeVehicleHealthChange(NetHandle entity, float oldValue)
        {
            onVehicleHealthChange?.Invoke(entity, oldValue);
        }

        internal void invokePlayerWeaponSwitch(Client entity, int oldValue)
        {
            onPlayerWeaponSwitch?.Invoke(entity, (WeaponHash) oldValue);
        }

        internal void invokePlayerArmorChange(Client entity, int oldValue)
        {
            onPlayerArmorChange?.Invoke(entity, oldValue);
        }

        internal void invokePlayerHealthChange(Client entity, int oldValue)
        {
            onPlayerHealthChange?.Invoke(entity, oldValue);
        }

        internal void invokeOnEntityDataChange(NetHandle entity, string key, object oldValue)
        {
            onEntityDataChange?.Invoke(entity, key, oldValue);
        }

        internal void invokeColShapeEnter(ColShape shape, NetHandle vehicle)
        {
            onEntityEnterColShape?.Invoke(shape, vehicle);
        }

        internal void invokeColShapeExit(ColShape shape, NetHandle vehicle)
        {
            onEntityExitColShape?.Invoke(shape, vehicle);
        }

        internal void invokeVehicleDeath(NetHandle vehicle)
        {
            onVehicleDeath?.Invoke(vehicle);
        }

        internal void invokeMapChange(string mapName, XmlGroup map)
        {
            onMapChange?.Invoke(mapName, map);
        }

        internal void invokePlayerEnterVeh(Client player, NetHandle veh)
        {
            onPlayerEnterVehicle?.Invoke(player, veh);
        }

        internal void invokePlayerExitVeh(Client player, NetHandle veh)
        {
            onPlayerExitVehicle?.Invoke(player, veh);
        }

        internal void invokeClientEvent(Client sender, string eventName, params object[] arsg)
        {
            onClientEventTrigger?.Invoke(sender, eventName, arsg);
        }

        internal void invokeFinishedDownload(Client sender)
        {
            onPlayerFinishedDownload?.Invoke(sender);
        }

        internal void invokePickupRespawn(NetHandle pickup)
        {
            onPickupRespawn?.Invoke(pickup);
        }

        internal void invokeResourceStart()
        {
            onResourceStart?.Invoke();
        }

        internal void invokeUpdate()
        {
            onUpdate?.Invoke();
        }

        internal void invokeServerResourceStart(string resource)
        {
            onServerResourceStart?.Invoke(resource);
        }

        internal void invokeCustomDataReceive(string data)
        {
            //onCustomDataReceived?.Invoke(data);
        }

        internal void invokeServerResourceStop(string resource)
        {
            onServerResourceStop?.Invoke(resource);
        }

        internal void invokeResourceStop()
        {
            onResourceStop?.Invoke();

            lock (ResourceEntities)
            {
                for (int i = ResourceEntities.Count - 1; i >= 0; i--)
                {
                    deleteEntityInternal(ResourceEntities[i]);
                }
                ResourceEntities.Clear();
            }

            lock (ResourceColShapes)
            {
                for (int i = ResourceColShapes.Count - 1; i >= 0; i--)
                {
                    Program.ServerInstance.ColShapeManager.Remove(ResourceColShapes[i]);
                }
                ResourceColShapes.Clear();
            }
        }

        internal bool invokeChatMessage(Client sender, string msg)
        {
            var args = new CancelEventArgs(false);
            onChatMessage?.Invoke(sender, msg, args);
            return !args.Cancel;
        }

        internal void invokePlayerPickup(Client pickupee, NetHandle pickup)
        {
            onPlayerPickup?.Invoke(pickupee, pickup);
        }

        internal void invokeChatCommand(Client sender, string msg, CancelEventArgs ce)
        {
            onChatCommand?.Invoke(sender, msg, ce);
        }

        internal void invokePlayerBeginConnect(Client player, CancelEventArgs e)
        {            
            onPlayerBeginConnect?.Invoke(player, e);
        }

        internal void invokePlayerConnected(Client player)
        {
            onPlayerConnected?.Invoke(player);
        }

        internal void invokePlayerDisconnected(Client player, string reason)
        {
            onPlayerDisconnected?.Invoke(player, reason);
        }

        internal void invokePlayerDeath(Client player, NetHandle netHandle, int weapon)
        {
            onPlayerDeath?.Invoke(player, netHandle, weapon);
        }

        internal void invokePlayerRespawn(Client player)
        {
            onPlayerRespawn?.Invoke(player);
        }

        #endregion

        #region Functions

        public static API shared
        {
            get { return Program.ServerInstance.PublicAPI; }
        }

        public long TickCount
        {
            get { return DateTime.Now.Ticks/10000; }
        }

        public string getResourceFolder()
        {
            if (ResourceParent == null)
            {
                throw new NullReferenceException("Illegal call to function inside constructor.");
            }

            return Path.GetFullPath("resources" + Path.DirectorySeparatorChar + ResourceParent.ResourceParent.DirectoryName);
        }

        public string getResourceName(string resource)
        {
            if (doesResourceExist(resource))
            {
                return Program.ServerInstance.GetResourceInfo(resource)?.Info?.Name;
            }

            return null;
        }

        public string getResourceDescription(string resource)
        {
            if (doesResourceExist(resource))
            {
                return Program.ServerInstance.GetResourceInfo(resource)?.Info?.Description;
            }

            return null;
        }

        public string getResourceAuthor(string resource)
        {
            if (doesResourceExist(resource))
            {
                return Program.ServerInstance.GetResourceInfo(resource)?.Info?.Author;
            }

            return null;
        }

        public string getResourceVersion(string resource)
        {
            if (doesResourceExist(resource))
            {
                return Program.ServerInstance.GetResourceInfo(resource)?.Info?.Version;
            }

            return null;
        }

        public ResourceType getResourceType(string resource)
        {
            if (doesResourceExist(resource))
            {
                return Program.ServerInstance.GetResourceInfo(resource)?.Info?.Type ?? ResourceType.script;
            }

            return ResourceType.script;
        }

        public string[] getResourceCommands(string resource)
        {
            if (isResourceRunning(resource))
            {
                return Program.ServerInstance.CommandHandler.GetResourceCommands(resource);
            }

            return new string[0];
        }

        public CommandInfo[] getResourceCommandInfos(string resource)
        {
            if (isResourceRunning(resource))
            {
                return Program.ServerInstance.CommandHandler.GetResourceCommandInfos(resource);
            }

            return new CommandInfo[0];
        }

        public CommandInfo getResourceCommandInfo(string resource, string command)
        {
            if (isResourceRunning(resource))
            {
                return Program.ServerInstance.CommandHandler.GetCommandInfo(resource, command);
            }

            return default(CommandInfo);
        }

        public string[] getMapGamemodes(string resource)
        {
            if (doesResourceExist(resource))
            {
                var gms = Program.ServerInstance.GetResourceInfo(resource)?.Info;

                if (gms == null || gms.Type != ResourceType.map) return new string[0];

                if (string.IsNullOrEmpty(gms.Gamemodes)) return new string[0];

                return gms.Gamemodes.Split(',');
            }

            return null;
        }

        public string getCurrentGamemode()
        {
            return Program.ServerInstance.RunningResources.FirstOrDefault(res => res.Info.Info.Type == ResourceType.gamemode)?.DirectoryName;
        }

        public void setServerName(string serverName)
        {
            if (!string.IsNullOrWhiteSpace(serverName))
                Program.ServerInstance.Name = serverName;
        }

        public IEnumerable<string> getMapsForGamemode(string gamemode)
        {
            foreach (var map in getAllResources())
            {
                if (getMapGamemodes(map).Contains(gamemode)) yield return map;
            }
        }

        public bool hasSetting(string settingName)
        {
            if (ResourceParent == null) throw new AccessViolationException("Illegal call to getSetting inside the constructor!");

            return ResourceParent.ResourceParent.Settings != null &&
                   ResourceParent.ResourceParent.Settings.ContainsKey(settingName);
        }

        public T getSetting<T>(string settingName)
        {
            if (ResourceParent == null) throw new AccessViolationException("Illegal call to getSetting inside the constructor!");

            if (ResourceParent.ResourceParent.Settings != null &&
                ResourceParent.ResourceParent.Settings.ContainsKey(settingName))
            {
                var val = ResourceParent.ResourceParent.Settings[settingName];

                T output;

                if (!val.HasValue)
                {
                    if (string.IsNullOrWhiteSpace(val.Value))
                        val.Value = val.DefaultValue;

                    try
                    {
                        output = (T) Convert.ChangeType(val.Value, typeof (T), CultureInfo.InvariantCulture);
                    }
                    catch (InvalidCastException)
                    {
                        output = (T) Convert.ChangeType(val.DefaultValue, typeof (T), CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        output = (T) Convert.ChangeType(val.DefaultValue, typeof (T), CultureInfo.InvariantCulture);
                    }

                    val.CastObject = output;
                    val.HasValue = true;

                    ResourceParent.ResourceParent.Settings[settingName] = val;
                }
                else
                {
                    output = (T) val.CastObject;
                }

                return output;
            }

            return default(T);
        }

        public void setSetting(string settingName, object value)
        {
            if (ResourceParent == null) throw new AccessViolationException("Illegal call to getSetting inside the constructor!");

            if (ResourceParent.ResourceParent.Settings != null &&
                ResourceParent.ResourceParent.Settings.ContainsKey(settingName))
            {
                var ourObj = ResourceParent.ResourceParent.Settings[settingName];

                ourObj.CastObject = value;
                ourObj.HasValue = true;

                ResourceParent.ResourceParent.Settings[settingName] = ourObj;
            }
        }

        public bool hasResourceSetting(string resource, string settingName)
        {
            var res = Program.ServerInstance.RunningResources.FirstOrDefault(r => r.DirectoryName == resource);

            if (res == null) return false;

            return res.Settings != null &&
                   res.Settings.ContainsKey(settingName);
        }

        public T getResourceSetting<T>(string resource, string setting)
        {
            var res = Program.ServerInstance.RunningResources.FirstOrDefault(r => r.DirectoryName == resource);

            if (res == null) return default(T);

            if (res.Settings != null &&
                res.Settings.ContainsKey(setting))
            {
                var val = res.Settings[setting];

                T output;

                if (!val.HasValue)
                {
                    if (string.IsNullOrWhiteSpace(val.Value))
                        val.Value = val.DefaultValue;

                    try
                    {
                        output = (T)Convert.ChangeType(val.Value, typeof(T), CultureInfo.InvariantCulture);
                    }
                    catch (InvalidCastException)
                    {
                        output = (T)Convert.ChangeType(val.DefaultValue, typeof(T), CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        output = (T)Convert.ChangeType(val.DefaultValue, typeof(T), CultureInfo.InvariantCulture);
                    }

                    val.CastObject = output;
                    val.HasValue = true;

                    res.Settings[setting] = val;
                }
                else
                {
                    output = (T)val.CastObject;
                }

                return output;
            }

            return default(T);
        }

        public void setResourceSetting(string resource, string setting, object value)
        {
            var res = Program.ServerInstance.RunningResources.FirstOrDefault(r => r.DirectoryName == resource);

            if (res == null) return;

            if (res.Settings != null &&
                res.Settings.ContainsKey(setting))
            {
                var ourObj = res.Settings[setting];

                ourObj.CastObject = value;
                ourObj.HasValue = true;

                res.Settings[setting] = ourObj;
            }
        }

        public string[] getResourceSettings(string resource)
        {
            var res = Program.ServerInstance.RunningResources.FirstOrDefault(r => r.DirectoryName == resource);

            if (res == null) return new string[0];

            return res.Settings.Select(r => r.Key).ToArray();
        }

        public bool doesConfigExist(string configName)
        {
            if (ResourceParent == null) throw new AccessViolationException("Illegal call to doesConfigExist inside the constructor!");

            return ResourceParent.ResourceParent.Info.ConfigFiles != null &&
                   ResourceParent.ResourceParent.Info.ConfigFiles.Any(
                       cfg => cfg.Type == ScriptType.server && cfg.Path == configName);
        }

        public XmlGroup loadConfig(string configName)
        {
            if (ResourceParent == null) throw new AccessViolationException("Illegal call to loadConfig inside the constructor!");

            if (doesConfigExist(configName) &&
                File.Exists("resources" + Path.DirectorySeparatorChar + ResourceParent.ResourceParent.DirectoryName +
                            Path.DirectorySeparatorChar + configName))
            {
                var xml = new XmlGroup();
                xml.Load("resources" + Path.DirectorySeparatorChar + ResourceParent.ResourceParent.DirectoryName +
                         Path.DirectorySeparatorChar + configName);
                return xml;
            }

            return null;
        }

        public XmlGroup loadXml(string path)
        {
            if (!isPathSafe(path)) throw new AccessViolationException("Illegal path for XML!");
            if (!File.Exists(path)) throw new FileNotFoundException("File not found!");
            var xml = new XmlGroup();
            xml.Load(path);
            return xml;
        }

        public dynamic fromJson(string json)
        {
            return JObject.Parse(json);
        }

        public string toJson(object data)
        {
            return JsonConvert.SerializeObject(data);
        }

        public object call(string className, string methodName, params object[] arguments)
        {
            var ourResource = ResourceParent.ResourceParent;

            var ourScriptName = ourResource.Engines.FirstOrDefault(en => en.Filename == className);
            if (ourScriptName == null)
            {
                Program.Output("ERROR: call() - No class named '" + className + "' was found.");
                return null;
            }

            return ourScriptName.InvokeMethod(methodName, arguments);
        }

        public dynamic exported
        {
            get { return Program.ServerInstance.ExportedFunctions; }
        }
        
        public bool startResource(string resourceName)
        {
            return Program.ServerInstance.StartResource(resourceName);
        }

        public bool stopResource(string name)
        {
            return Program.ServerInstance.StopResource(name);
        }

        public bool isResourceRunning(string resource)
        {
            return Program.ServerInstance.RunningResources.Any(r => r.DirectoryName == resource);
        }

        public bool doesResourceExist(string resource)
        {
            if (resource.Contains("..")) return false;
            return Directory.Exists("resources" + Path.DirectorySeparatorChar + resource);
        }

        public string[] getRunningResources()
        {
            return Program.ServerInstance.RunningResources.Select(r => r.DirectoryName).ToArray();
        }

        public string[] getAllResources()
        {
            return Directory.GetDirectories("resources").Select(s => Path.GetFileName(s)).ToArray();
        }

        public void playSoundFrontEnd(Client target, string SoundName, string SoundSetName)
        {
            Program.ServerInstance.SendNativeCallToPlayer(target, true, 0x2F844A8B08D76685, SoundSetName, true);
            Program.ServerInstance.SendNativeCallToPlayer(target, true, 0x67C540AA08E4A6F5, -1, SoundName, SoundSetName);
        }

        public void setGamemodeName(string newName)
        {
            if (newName == null) throw new ArgumentNullException(nameof(newName));
            Program.ServerInstance.GamemodeName = newName;
        }

        public EntityType getEntityType(NetHandle handle)
        {
            if (!doesEntityExist(handle)) return (EntityType)0;
            return (EntityType)Program.ServerInstance.NetEntityHandler.ToDict()[handle.Value].EntityType;
        }

        public Client getPlayerFromHandle(NetHandle handle)
        {
            return Program.ServerInstance.Clients.FirstOrDefault(c => c.handle == handle);
        }

        public bool isPlayerConnected(Client player)
        {
            return player.NetConnection.Status == NetConnectionStatus.Connected;
        }

        public bool isPlayerOnFire(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.OnFire) != 0;
        }

        public bool isPlayerParachuting(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.ParachuteOpen) != 0;
        }

        public bool isPlayerInFreefall(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.InFreefall) != 0;
        }

        public bool isPlayerAiming(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.Aiming) != 0;
        }

        public bool isPlayerShooting(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.Shooting) != 0;
        }

        public bool isPlayerReloading(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.IsReloading) != 0;
        }

        public bool isPlayerInCover(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.IsInCover) != 0;
        }

        public bool isPlayerOnLadder(Client player)
        {
            return (player.LastPedFlag & (int)PedDataFlags.IsOnLadder) != 0;
        }

        public Vector3 getPlayerAimingPoint(Client player)
        {
            return player.LastAimPos;
        }

        public bool isPlayerInAnyVehicle(Client player)
        {
            return player.IsInVehicle;
        }

        public bool isPlayerDead(Client player)
        {
            if (player.IsInVehicle)
                return (player.LastVehicleFlag & (int) VehicleDataFlags.PlayerDead) != 0;
            else return (player.LastPedFlag & (int)PedDataFlags.PlayerDead) != 0;
        }

        public bool isPlayerRespawning(Client player)
        {
            var lastLegitDeath = getEntityData(player, "__LAST_PLAYER_DEATH") ?? 0;
            var lastLegitRespawn = getEntityData(player, "__LAST_PLAYER_RESPAWN") ?? 0;
            return lastLegitRespawn < lastLegitDeath;
        }

        public void requestIpl(string iplName)
        {
            var world = Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1);

            if (!world.LoadedIpl.Contains(iplName))
                world.LoadedIpl.Add(iplName);
            if (world.RemovedIpl.Contains(iplName))
                world.RemovedIpl.Remove(iplName);
            sendNativeToAllPlayers(0x41B4893843BBDB74, iplName);
        }

        public void removeIpl(string iplName)
        {
            var world = Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1);

            if (!world.RemovedIpl.Contains(iplName))
                world.RemovedIpl.Add(iplName);
            if (world.LoadedIpl.Contains(iplName))
                world.LoadedIpl.Remove(iplName);
            sendNativeToAllPlayers(0xEE6C5AD3ECE0A82D, iplName);
        }

        public void resetIplList()
        {
            Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).RemovedIpl.Clear();
            Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).LoadedIpl.Clear();
        }

        public void sleep(int ms)
        {
            if (ResourceParent != null && !ResourceParent.Async)
            {
                Program.Output("WARN: using API.sleep in a non-async environment is not recommended!");
            }

            var start = DateTime.Now;
            do
            {
                if (ResourceParent?.HasTerminated == true) throw new ResourceAbortedException();
                Thread.Sleep(10);
            } while (DateTime.Now.Subtract(start).TotalMilliseconds < ms);
        }

        /// <summary>
        /// Notice: not available in the Script constructor. Use onResourceStart even instead.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public Thread startThread(ThreadStart target)
        {
            if (ResourceParent == null)
            {
                throw new NullReferenceException("Illegal call to function inside constructor.");
            }

            var t = new Thread(target);
            t.IsBackground = true;
            t.Start();
            ResourceParent.AddTrackedThread(t);
            return t;
        }

        public void delay(int ms, bool once, Action target)
        {
            var t = new System.Timers.Timer(ms);
            t.AutoReset = !once;
            t.Elapsed += (sender, args) =>
            {
                if (ResourceParent.HasTerminated) return;

                try
                {
                    target?.Invoke();
                }
                catch (ThreadAbortException) { }
                catch (ResourceAbortedException) { }
                catch (Exception ex)
                {
                    Program.Output("EXCEPTION IN RESOURCE " + ResourceParent.ResourceParent.DirectoryName + " INSIDE SCRIPTENGINE " + ResourceParent.Filename);
                    Program.Output(ex.ToString());
                }
            };
            t.Start();
        }
        
        /// <summary>
        /// Notice: not available in the Script constructor. Use onResourceStart even instead.
        /// </summary>
        /// <returns></returns>
        public string getThisResource()
        {
            if (ResourceParent == null)
            {
                throw new NullReferenceException("Illegal call to function inside constructor.");
            }

            return ResourceParent.ResourceParent.DirectoryName;
        }

        public int getHashKey(string input)
        {
            return Program.GetHash(input);
        }

        public string getHashSHA256(string input)
        {
            return Program.GetHashSHA256(input);
        }

        public int loginPlayer(Client player, string password)
        {
            if (!Program.ServerInstance.ACLEnabled) return (int) AccessControlList.LoginResult.ACLDisabled;
            return (int) Program.ServerInstance.ACL.TryLoginPlayer(player, password);
        }

        public void logoutPlayer(Client player)
        {
            Program.ServerInstance.ACL?.LogOutClient(player);
        }

        public bool doesPlayerHaveAccessToCommand(Client player, string cmd)
        {
            cmd = cmd.TrimStart('/');

            return (!Program.ServerInstance.ACLEnabled ||
                    Program.ServerInstance.ACL.DoesUserHaveAccessToCommand(player, cmd));
        }

        public bool isPlayerLoggedIn(Client player)
        {
            return Program.ServerInstance.ACL.IsPlayerLoggedIn(player);
        }

        public bool isAclEnabled()
        {
            return Program.ServerInstance.ACLEnabled;
        }

        public string getPlayerAclGroup(Client player)
        {
            return Program.ServerInstance.ACL.GetPlayerGroup(player);
        }

        public void consoleOutput(string text)
        {
            Program.Output(text);
        }

        public void consoleOutput(string text, params object[] args)
        {
            Program.Output(string.Format(text, args));
        }

        public void downloadData(Client target, string data)
        {
            if (ResourceParent == null || ResourceParent.ResourceParent == null) throw new NullReferenceException("Illegal call to sendCustomData inside constructor!");
            Program.ServerInstance.TransferLargeString(target, data, ResourceParent.ResourceParent.DirectoryName);
        }

        public bool doesEntityExistForPlayer(Client player, NetHandle entity)
        {
            return
                (bool)
                    Program.ServerInstance.ReturnNativeCallFromPlayer(player, false, 0x7239B21A38F536BA, new BooleanArgument(),
                        new EntityArgument(entity.Value));
        }

        public void setEntityTransparency(NetHandle entity, int newAlpha)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Alpha = (byte) newAlpha;
                if (newAlpha < 255)
                    Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x44A0870B7E92D7C0, new EntityArgument(entity.Value), newAlpha, false);
                else
                    Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x9B1E824FFBB7027A, new EntityArgument(entity.Value));

                var delta = new Delta_EntityProperties();
                delta.Alpha = (byte) newAlpha;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, delta);
            }
        }

        public byte getEntityTransparency(NetHandle entity)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Alpha;
            }

            return 0;
        }

        public void setEntityDimension(NetHandle entity, int dimension)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Dimension = dimension;

                var delta = new Delta_EntityProperties();
                delta.Dimension = dimension;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, delta);

                KeyValuePair<int, EntityProperties> pair;
                if (
                    (pair =
                        Program.ServerInstance.NetEntityHandler.ToCopy()
                            .FirstOrDefault(
                                p => p.Value is BlipProperties && ((BlipProperties) p.Value).AttachedNetEntity ==
                        entity.Value)).Key != 0)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[pair.Key].Dimension = dimension;

                    var deltaBlip = new Delta_EntityProperties();
                    deltaBlip.Dimension = dimension;
                    Program.ServerInstance.UpdateEntityInfo(pair.Key, EntityType.Prop, deltaBlip);
                }

                if (
                   (pair =
                       Program.ServerInstance.NetEntityHandler.ToCopy()
                           .FirstOrDefault(
                               p => p.Value is ParticleProperties && ((ParticleProperties)p.Value).EntityAttached ==
                       entity.Value)).Key != 0)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[pair.Key].Dimension = dimension;

                    var deltaBlip = new Delta_EntityProperties();
                    deltaBlip.Dimension = dimension;
                    Program.ServerInstance.UpdateEntityInfo(pair.Key, EntityType.Prop, deltaBlip);
                }

                if (Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Attachables != null)
                    foreach (var attached in Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Attachables)
                    {
                        setEntityDimension(new NetHandle(attached), dimension);
                    }
            }
        }

        public int getEntityDimension(NetHandle entity)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Dimension;
            }

            return 0;
        }

        public void setEntityInvincible(NetHandle entity, bool invincible)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].IsInvincible = invincible;

                var delta = new Delta_EntityProperties();
                delta.IsInvincible = invincible;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, delta);

                sendNativeToAllPlayers(0x3882114BDE571AD4, entity, invincible);
            }
        }

        public bool getEntityInvincible(NetHandle entity)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].IsInvincible;
            }

            return false;
        }

        public void createParticleEffectOnPosition(string ptfxLibrary, string ptfxName, Vector3 position, Vector3 rotation, float scale, int dimension = 0)
        {
            sendNativeToPlayersInRangeInDimension(position, 40, dimension, 0x25129531F77B9ED3, ptfxLibrary, ptfxName,
                position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z,
                scale, 0, 0, 0);
        }

        public void createParticleEffectOnEntity(string ptfxLibrary, string ptfxName, NetHandle entity, Vector3 offset, Vector3 rotation, float scale, int boneIndex = -1, int dimension = 0)
        {
            if (boneIndex <= 0)
            {
                sendNativeToPlayersInRangeInDimension(getEntityPosition(entity), 40, dimension, 0x0D53A3B8DA0809D2, ptfxLibrary, ptfxName, entity,
                    offset.X, offset.Y, offset.Z, rotation.X, rotation.Y, rotation.Z,
                    scale, 0, 0, 0);
            }
            else
            {
                sendNativeToPlayersInRangeInDimension(getEntityPosition(entity), 40, dimension, 0x0E7E72961BA18619, ptfxLibrary, ptfxName, entity,
                    offset.X, offset.Y, offset.Z, rotation.X, rotation.Y, rotation.Z,
                    boneIndex, scale, 0, 0, 0);
            }
        }

        public void createExplosion(ExplosionType explosionType, Vector3 position, float damageScale = 1f, int dimension = 0)
        {
            sendNativeToPlayersInRangeInDimension(position, 50f, dimension, 0xE3AD2BDBAEE269AC, position.X, position.Y, position.Z, (int)explosionType, damageScale, true, false, 1f);
        }

        public void createOwnedExplosion(Client owner, ExplosionType explosionType, Vector3 position, float damageScale = 1f, int dimension = 0)
        {
            sendNativeToPlayersInRangeInDimension(position, 50f, dimension, 0x172AA1B624FA1013, owner.handle, position.X, position.Y, position.Z, (int)explosionType, damageScale, true, false, 1f);
        }

        public void createProjectile(WeaponHash weapon, Vector3 start, Vector3 target, int damage, float speed = -1, int dimension = 0)
        {
            sendNativeToPlayersInDimension(dimension, Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, target.X, target.Y, target.Z, damage, 1, (int)weapon, null, true, false, speed);
        }

        public void createOwnedProjectile(Client owner, WeaponHash weapon, Vector3 start, Vector3 target, int damage, float speed = -1, int dimension = 0)
        {
            sendNativeToPlayersInDimension(dimension, Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, target.X, target.Y, target.Z, damage, 1, (int)weapon, owner.handle.Value, true, false, speed);
        }

        public void setPlayerNametag(Client player, string text)
        {
            player.Properties.NametagText = text;

            var delta = new Delta_PlayerProperties();
            delta.NametagText = text;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
        }

        public string getPlayerNametag(Client player)
        {
            return player.Properties.NametagText;
        }

        public void resetPlayerNametag(Client player)
        {
            player.Properties.NametagText = " ";

            var delta = new Delta_PlayerProperties();
            delta.NametagText = " ";
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
        }

        public void setPlayerNametagVisible(Client player, bool visible)
        {
            if (visible)
                player.Properties.NametagSettings = PacketOptimization.ResetBit(player.Properties.NametagSettings, 1);
            else
                player.Properties.NametagSettings = PacketOptimization.SetBit(player.Properties.NametagSettings, 1);

            var delta = new Delta_PlayerProperties();
            delta.NametagSettings = player.Properties.NametagSettings;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
        }

        public bool getPlayerNametagVisible(Client player)
        {
            return !PacketOptimization.CheckBit(player.Properties.NametagSettings, 1);
        }

        public void setPlayerNametagColor(Client player, byte r, byte g, byte b)
        {
            player.Properties.NametagSettings = PacketOptimization.SetBit(player.Properties.NametagSettings, 2);

            var col = Extensions.FromArgb(0, r, g, b) << 8;
            player.Properties.NametagSettings |= col;

            var delta = new Delta_PlayerProperties();
            delta.NametagSettings = player.Properties.NametagSettings;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
        }

        public Color getPlayerNametagColor(Client player)
        {
            var output = new Color();

            byte a, r, g, b;

            var col = player.Properties.NametagSettings >> 8;
            Extensions.ToArgb(col, out a, out r, out g, out b);

            output.alpha = a;
            output.red = r;
            output.green = g;
            output.blue = b;

            return output;
        }

        public void resetPlayerNametagColor(Client player)
        {
            player.Properties.NametagSettings = PacketOptimization.ResetBit(player.Properties.NametagSettings, 2);

            player.Properties.NametagSettings &= 255;

            var delta = new Delta_PlayerProperties();
            delta.NametagSettings = player.Properties.NametagSettings;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
        }

        public void setPlayerToSpectator(Client player)
        {
            Program.ServerInstance.SetPlayerOnSpectate(player, true);
            player.Properties.Flag |= (byte) EntityFlag.PlayerSpectating;

            var delta = new Delta_PlayerProperties();
            delta.Flag = player.Properties.Flag;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public void setPlayerToSpectatePlayer(Client player, Client target)
        {
            Program.ServerInstance.SetPlayerOnSpectatePlayer(player, target);
            player.Properties.Flag |= (byte)EntityFlag.PlayerSpectating;

            var delta = new Delta_PlayerProperties();
            delta.Flag = player.Properties.Flag;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public void unspectatePlayer(Client player)
        {
            Program.ServerInstance.SetPlayerOnSpectate(player, false);
            player.Properties.Flag = (byte)PacketOptimization.ResetBit(player.Properties.Flag, EntityFlag.PlayerSpectating);

            var delta = new Delta_PlayerProperties();
            delta.Flag = player.Properties.Flag;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public bool isPlayerSpectating(Client player)
        {
            return PacketOptimization.CheckBit(player.Properties.Flag, EntityFlag.PlayerSpectating);
        }

        public void explodeVehicle(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).IsDead = true;
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health = -100;

                sendNativeToAllPlayers(Hash.EXPLODE_VEHICLE, vehicle, true, false);

                var delta = new Delta_VehicleProperties();
                delta.IsDead = true;
                delta.Health = -100;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleLivery(NetHandle vehicle, int livery)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Livery = livery;
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x60BF608F1B8CD1B6, new EntityArgument(vehicle.Value), livery);

                var delta = new Delta_VehicleProperties();
                delta.Livery = livery;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public Client[] getVehicleOccupants(NetHandle vehicle)
        {
            if (Program.ServerInstance.VehicleOccupants.ContainsKey(vehicle.Value))
            {
                return Program.ServerInstance.VehicleOccupants[vehicle.Value].ToArray();
            }

            return new Client[0];
        }

        public int getVehicleLivery(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Livery;
            }
            return 0;
        }

        public void setVehicleLocked(NetHandle vehicle, bool locked)
        {
            if (doesEntityExist(vehicle))
            {
                if (locked)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag =
                        (byte)PacketOptimization.SetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.VehicleLocked);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag =
                        (byte)PacketOptimization.ResetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.VehicleLocked);
                }
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xB664292EAECF7FA6, new EntityArgument(vehicle.Value), locked ? 10 : 1);

                var delta = new Delta_VehicleProperties();
                delta.Flag = Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public bool getVehicleLocked(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return PacketOptimization.CheckBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.VehicleLocked);
            }
            return false;
        }

        public NetHandle getVehicleTrailer(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return
                    new NetHandle(((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Trailer);
            }

            return new NetHandle();
        }

        public NetHandle getVehicleTraileredBy(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return
                    new NetHandle(((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).TraileredBy);
            }

            return new NetHandle();
        }

        public bool getVehicleSirenState(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).Siren;
            }

            return false;
        }

        public bool isVehicleTyrePopped(NetHandle vehicle, int tyre)
        {
            if (doesEntityExist(vehicle))
            {
                return (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).Tires &
                        1 << tyre) != 0;
            }

            return false;
        }

        public void popVehicleTyre(NetHandle vehicle, int tyre, bool pop)
        {
            if (doesEntityExist(vehicle))
            {
                if (pop)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).Tires |= (byte)(1 << tyre);
                    sendNativeToAllPlayers(0xEC6A202EE4960385, vehicle, tyre, false, 1000);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).Tires &= (byte)~(1 << tyre);
                    sendNativeToAllPlayers(0x6E13FC662B882D1D, vehicle, tyre);
                }

                var delta = new Delta_VehicleProperties();
                delta.Tires = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).Tires;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public bool isVehicleDoorBroken(NetHandle vehicle, int door)
        {
            if (doesEntityExist(vehicle))
            {
                if (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel ==
                    null) return false;

                return (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel.BrokenDoors & 1 << door) != 0;
            }

            return false;
        }

        public void breakVehicleDoor(NetHandle vehicle, int door, bool breakDoor)
        {
            if (doesEntityExist(vehicle))
            {
                if (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel ==
                    null)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel = new VehicleDamageModel();
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value)
                        .DamageModel.BrokenDoors = 0;
                }

                if (breakDoor)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel.BrokenDoors |= (byte)(1 << door);
                    sendNativeToAllPlayers(0xD4D4F6A4AB575A33, vehicle, door, false);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel.BrokenDoors &= (byte)~(1 << door);
                }

                var delta = new Delta_VehicleProperties();
                delta.DamageModel = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleDoorState(NetHandle vehicle, int door, bool open)
        {
            if (doesEntityExist(vehicle))
            {
                var prop = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value);

                if (open) prop.Doors |= (byte) (1 << door);
                else prop.Doors &= (byte) ~(1 << door);

                if (open)
                {
                    sendNativeToAllPlayers(Hash.SET_VEHICLE_DOOR_OPEN, vehicle, door, false, false);
                }
                else
                {
                    sendNativeToAllPlayers(Hash.SET_VEHICLE_DOOR_SHUT, vehicle, door, false);
                }
            }
        }

        public bool getVehicleDoorState(NetHandle vehicle, int door)
        {
            if (doesEntityExist(vehicle))
            {
                var prop = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value);

                return (prop.Doors & (1 << door)) != 0;
            }

            return false;
        }

        public bool isVehicleWindowBroken(NetHandle vehicle, int window)
        {
            if (doesEntityExist(vehicle))
            {
                if (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel ==
                    null) return false;

                return (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel.BrokenWindows & 1 << window) != 0;
            }

            return false;
        }

        public void breakVehicleWindow(NetHandle vehicle, int window, bool breakWindow)
        {
            if (doesEntityExist(vehicle))
            {
                if (Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel ==
                    null)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel = new VehicleDamageModel();
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value)
                        .DamageModel.BrokenWindows = 0;
                }

                if (breakWindow)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value)
                        .DamageModel.BrokenDoors |= (byte)(1 << window);
                    sendNativeToAllPlayers(0x9E5B5E4D2CCD2259, vehicle, window);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value)
                        .DamageModel.BrokenDoors &= (byte)~(1 << window);
                    sendNativeToAllPlayers(0x772282EBEB95E682, vehicle, window);
                }

                var delta = new Delta_VehicleProperties();
                delta.DamageModel = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value).DamageModel;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleExtra(NetHandle vehicle, int slot, bool enabled)
        {
            if (doesEntityExist(vehicle))
            {
                if (enabled)
                {
                    ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value])
                        .VehicleComponents |= (short)(1 << slot);
                }
                else
                {
                    ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value])
                        .VehicleComponents &= (short)(~(1 << slot));
                }

                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x7EE3A3C5E4A40CC9, new EntityArgument(vehicle.Value), slot, enabled ? 0 : -1);

                var delta = new Delta_VehicleProperties();
                delta.VehicleComponents = ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value])
                        .VehicleComponents;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public bool getVehicleExtra(NetHandle vehicle, int slot)
        {
            if (doesEntityExist(vehicle))
            {
                return (((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value])
                    .VehicleComponents & 1 << slot) != 0;
            }
            return false;
        }

        public void setVehicleNumberPlate(NetHandle vehicle, string plate)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).NumberPlate = plate;
                //Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x95A88F0B409CDA47, new EntityArgument(vehicle.Value), plate);

                var delta = new Delta_VehicleProperties();
                delta.NumberPlate = plate;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public string getVehicleNumberPlate(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).NumberPlate;
            }
            return null;
        }

        public void setVehicleEngineStatus(NetHandle vehicle, bool turnedOn)
        {
            if (doesEntityExist(vehicle))
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x2497C4717C8B881E, vehicle, turnedOn, true, true);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x8ABA6AF54B942B95, vehicle, !turnedOn);

                if (turnedOn)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag = (byte)
                        PacketOptimization.ResetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.EngineOff);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag = (byte)
                        PacketOptimization.SetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.EngineOff);

                }

                var delta = new Delta_EntityProperties();
                delta.Flag = Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Prop, delta);
            }
        }

        public bool getVehicleEngineStatus(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return !PacketOptimization.CheckBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag, EntityFlag.EngineOff);
            }

            return false;
        }

        public void setVehicleSpecialLightStatus(NetHandle vehicle, bool turnedOn)
        {
            if (doesEntityExist(vehicle))
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x14E85C5EE7A4D542, vehicle, turnedOn, true);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x598803E85E8448D9, vehicle, turnedOn);

                if (turnedOn)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag = (byte)
                        PacketOptimization.SetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.SpecialLight);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag = (byte)
                        PacketOptimization.ResetBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag,
                            EntityFlag.SpecialLight);

                }

                var delta = new Delta_EntityProperties();
                delta.Flag = Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Prop, delta);
            }
        }

        public bool getVehicleSpecialLightStatus(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return !PacketOptimization.CheckBit(Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value].Flag, EntityFlag.SpecialLight);
            }

            return false;
        }

        public void setEntityCollisionless(NetHandle entity, bool collisionless)
        {
            if (doesEntityExist(entity))
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x1A9205C1B9EE827F, entity, !collisionless, true);

                if (!collisionless)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag = (byte)
                        PacketOptimization.ResetBit(Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag,
                            EntityFlag.Collisionless);
                }
                else
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag = (byte)
                        PacketOptimization.SetBit(Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag,
                            EntityFlag.Collisionless);

                }

                var delta = new Delta_EntityProperties();
                delta.Flag = Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, delta);
            }
        }

        public bool getEntityCollisionless(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return PacketOptimization.CheckBit(Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Flag, EntityFlag.Collisionless);
            }

            return false;
        }
        
        public int getEntityModel(NetHandle ent)
        {
            if (doesEntityExist(ent))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[ent.Value].ModelHash;
            }
            return 0;
        }

        public bool setEntitySyncedData(NetHandle entity, string key, object value)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.SetEntityProperty(entity.Value, key, value);
            }
            return false;
        }

        public dynamic getEntitySyncedData(NetHandle entity, string key)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.GetEntityProperty(entity.Value, key);
            }
            return null;
        }

        public string[] getAllEntitySyncedData(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.GetEntityAllProperties(entity.Value);
            }
            return new string[0];
        }

        public void resetEntitySyncedData(NetHandle entity, string key)
        {
            if (doesEntityExist(entity))
            {
                Program.ServerInstance.ResetEntityProperty(entity.Value, key);
            }
        }

        public bool hasEntitySyncedData(NetHandle entity, string key)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.HasEntityProperty(entity.Value, key);
            }
            return false;
        }
        
        public bool setWorldSyncedData(string key, object value)
        {
            return Program.ServerInstance.SetEntityProperty(1, key, value, true);
        }

        public dynamic getWorldSyncedData(string key)
        {
            return Program.ServerInstance.GetEntityProperty(1, key);
        }

        public string[] getAllWorldSyncedData()
        {
            return Program.ServerInstance.GetEntityAllProperties(1);
        }

        public void resetWorldSyncedData(string key)
        {
            Program.ServerInstance.ResetEntityProperty(1, key, true);
        }

        public bool hasWorldSyncedData(string key)
        {
            return Program.ServerInstance.HasEntityProperty(1, key);
        }

        public bool setEntityData(NetHandle entity, string key, object value)
        {
            lock (Program.ServerInstance.EntityProperties)
            {
                if (Program.ServerInstance.EntityProperties.ContainsKey(entity))
                {
                    Program.ServerInstance.EntityProperties[entity].Set(key, value);
                }
                else
                {
                    Program.ServerInstance.EntityProperties.Add(entity, new Dictionary<string, object>());
                    Program.ServerInstance.EntityProperties[entity].Set(key, value);
                }
            }
            return false;
        }

        public dynamic getEntityData(NetHandle entity, string key)
        {
            lock (Program.ServerInstance.EntityProperties)
            {
                if (Program.ServerInstance.EntityProperties.ContainsKey(entity))
                {
                    return Program.ServerInstance.EntityProperties[entity].Get(key);
                }
            }
            return null;
        }

        public void resetEntityData(NetHandle entity, string key)
        {
            lock (Program.ServerInstance.EntityProperties)
            {
                if (Program.ServerInstance.EntityProperties.ContainsKey(entity) && Program.ServerInstance.EntityProperties[entity].ContainsKey(key))
                {
                    Program.ServerInstance.EntityProperties[entity].Remove(key);
                }
            }
        }

        public bool hasEntityData(NetHandle entity, string key)
        {
            lock (Program.ServerInstance.EntityProperties)
            {
                if (Program.ServerInstance.EntityProperties.ContainsKey(entity))
                {
                    return Program.ServerInstance.EntityProperties[entity].ContainsKey(key);
                }
            }

            return false;
        }

        public string[] getAllEntityData(NetHandle entity)
        {
            if (doesEntityExist(entity) && Program.ServerInstance.EntityProperties.ContainsKey(entity))
            {
                return Program.ServerInstance.EntityProperties[entity].Select(pair => pair.Key).ToArray();
            }

            return new string[0];
        }

        public bool setWorldData(string key, object value)
        {
            lock (Program.ServerInstance.WorldProperties)
            {
                Program.ServerInstance.WorldProperties.Set(key, value);
            }

            return true;
        }

        public dynamic getWorldData(string key)
        {
            lock (Program.ServerInstance.WorldProperties)
            {
                return Program.ServerInstance.WorldProperties.Get(key);
            }
        }

        public string[] getAllWorldData()
        {
            lock (Program.ServerInstance.WorldProperties)
            {
                return Program.ServerInstance.WorldProperties.Select(pair => pair.Key).ToArray();
            }
        }

        public void resetWorldData(string key)
        {
            lock (Program.ServerInstance.WorldProperties)
            {
                Program.ServerInstance.WorldProperties.Remove(key);
            }
        }

        public bool hasWorldData(string key)
        {
            lock (Program.ServerInstance.WorldProperties)
            {
                return Program.ServerInstance.WorldProperties.ContainsKey(key);
            }
        }

        public void setVehicleMod(NetHandle vehicle, int modType, int mod)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(vehicle.Value))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods.Set((byte)modType, mod);

                var delta = new Delta_VehicleProperties();
                delta.Mods = ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public int getVehicleMod(NetHandle vehicle, int slot)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods.Get((byte)slot);
            }

            return 0;
        }

        public void removeVehicleMod(NetHandle vehicle, int modType)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods.Remove((byte)modType);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x92D619E420858204, vehicle, modType);

            var delta = new Delta_VehicleProperties();
            delta.Mods = ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods;
            Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
        }

        public void setVehicleBulletproofTyres(NetHandle vehicle, bool bulletproof)
        {
            setVehicleMod(vehicle, 61, bulletproof ? 0x01 : 0x00);
        }

        public bool getVehicleBulletproofTyres(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 61) != 0;
        }

        public void setVehicleNumberPlateStyle(NetHandle vehicle, int style)
        {
            setVehicleMod(vehicle, 62, style);
        }

        public int getVehicleNumberPlateStyle(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 62);
        }

        public void setVehiclePearlescentColor(NetHandle vehicle, int color)
        {
            setVehicleMod(vehicle, 63, color);
        }

        public int getVehiclePearlescentColor(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 63);
        }

        public void setVehicleWheelColor(NetHandle vehicle, int color)
        {
            setVehicleMod(vehicle, 64, color);
        }

        public int getVehicleWheelColor(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 64);
        }

        public void setVehicleWheelType(NetHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 65, type);
        }

        public int getVehicleWheelType(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 65);
        }

        public void setVehicleModColor1(NetHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 66, Extensions.FromArgb(0, (byte)r, (byte)g, (byte)b));
        }

        public Color getVehicleModColor1(NetHandle vehicle)
        {
            var val = getVehicleMod(vehicle, 66);
            byte a,red,green,blue;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
            return new Color(red,green,blue);
        }

        public void setVehicleModColor2(NetHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 67, Extensions.FromArgb(0, (byte)r, (byte)g, (byte)b));
        }

        public Color getVehicleModColor2(NetHandle vehicle)
        {
            var val = getVehicleMod(vehicle, 67);
            byte a, red, green, blue;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
            return new Color(red, green, blue);
        }

        public void setVehicleTyreSmokeColor(NetHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 68, Extensions.FromArgb(0, (byte)r, (byte)g, (byte)b));
        }

        public Color getVehicleTyreSmokeColor(NetHandle vehicle)
        {
            var val = getVehicleMod(vehicle, 68);
            byte a, red, green, blue;
            Extensions.ToArgb(val, out a, out red, out green, out blue);

            return new Color(red, green, blue);
        }

        public void setVehicleWindowTint(NetHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 69, type);
        }

        public int getVehicleWindowTint(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 69);
        }

        public void setVehicleEnginePowerMultiplier(NetHandle vehicle, float mult)
        {
            setVehicleMod(vehicle, 70, BitConverter.ToInt32(BitConverter.GetBytes(mult), 0));
        }

        public float getVehicleEnginePowerMultiplier(NetHandle vehicle)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(getVehicleMod(vehicle, 70)), 0);
        }

        public void setVehicleEngineTorqueMultiplier(NetHandle vehicle, float mult)
        {
            setVehicleMod(vehicle, 71, BitConverter.ToInt32(BitConverter.GetBytes(mult), 0));
        }

        public float getVehicleEngineTorqueMultiplier(NetHandle vehicle)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(getVehicleMod(vehicle, 71)), 0);
        }

        public void setVehicleNeonState(NetHandle vehicle, int slot, bool turnedOn)
        {
            var currentState = getVehicleMod(vehicle, 72);

            if (turnedOn)
                setVehicleMod(vehicle, 72, currentState | 1 << slot);
            else
                setVehicleMod(vehicle, 72, currentState & ~(1 << slot));
        }

        public bool getVehicleNeonState(NetHandle vehicle, int slot)
        {
            return (getVehicleMod(vehicle, 72) & (1 << slot)) != 0;
        }

        public void setVehicleNeonColor(NetHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 73, Extensions.FromArgb(0, (byte)r, (byte)g, (byte)b));
        }

        public Color getVehicleNeonColor(NetHandle vehicle)
        {
            var val = getVehicleMod(vehicle, 73);
            byte a, red, green, blue;
            Extensions.ToArgb(val, out a, out red, out green, out blue);

            return new Color(red, green, blue);
        }

        public void setVehicleDashboardColor(NetHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 74, type);
        }

        public int getVehicleDashboardColor(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 74);
        }

        public void setVehicleTrimColor(NetHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 75, type);
        }

        public int getVehicleTrimColor(NetHandle vehicle)
        {
            return getVehicleMod(vehicle, 75);
        }

        public string getVehicleDisplayName(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).DisplayName;
        }

        public float getVehicleMaxSpeed(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).MaxSpeed;
        }

        public float getVehicleMaxBraking(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).MaxBraking;
        }

        public float getVehicleMaxTraction(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).MaxTraction;
        }

        public float getVehicleMaxAcceleration(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).MaxAcceleration;
        }

        public float getVehicleMaxOccupants(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).MaxNumberOfPassengers;
        }

        public int getVehicleClass(VehicleHash model)
        {
            return ConstantVehicleDataOrganizer.Get(model).VehicleClass;
        }

        public string getVehicleClassName(int classId)
        {
            if (classId < 0 || classId >= ConstantVehicleDataOrganizer.VehicleClasses.Length) return "";

            return ConstantVehicleDataOrganizer.VehicleClasses[classId];
        }

        public void detonatePlayerStickies(Client player)
        {
            sendNativeToAllPlayers(0xFC4BD125DE7611E4, player.handle, (int)WeaponHash.StickyBomb, true);
        }

        public void setPlayerSkin(Client player, PedHash modelHash)
        {
            removeAllPlayerWeapons(player);

            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x00A1CADD00108836, new LocalGamePlayerArgument(), (int)modelHash);
            if (doesEntityExist(player.handle))
            {
                ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                    .ModelHash = (int)modelHash;
                
                var delta = new Delta_PlayerProperties();
                delta.ModelHash = (int)modelHash;
                Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
            }

            setPlayerDefaultClothes(player);
        }

        public void setPlayerDefaultClothes(Client player)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x45EEE61580806D63, player.handle);
            if (doesEntityExist(player.handle))
            {
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Accessories.Clear();
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Textures.Clear();
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Props.Clear();

                var delta = new Delta_PlayerProperties();
                delta.Textures = new Dictionary<byte, byte>();
                delta.Accessories = new Dictionary<byte, Tuple<byte, byte>>();
                delta.Props = new Dictionary<byte, byte>();
                Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
            }
        }

        public void setWeather(int weather)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xED712CA327900C8A, weather);
            Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).Weather = weather;
        }

        public int getWeather()
        {
            return Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).Weather;
        }

        public int getMaxPlayers()
        {
            return Program.ServerInstance.MaxPlayers;
        }

        public int getServerPort()
        {
            return Program.ServerInstance.Port;
        }

        public string getServerName()
        {
            return Program.ServerInstance.Name;
        }

        public bool isPasswordProtected()
        {
            return Program.ServerInstance.PasswordProtected;
        }

        public void setServerPassword(string password)
        {
            Program.ServerInstance.Password = password;
            Program.ServerInstance.PasswordProtected = !string.IsNullOrWhiteSpace(password);
        }

        public void setPlayerTeam(Client player, int team)
        {
            Program.ServerInstance.ChangePlayerTeam(player, team);
        }

        public int getPlayerTeam(Client player)
        {
            return player.Properties.Team;
        }

        public void playPlayerAnimation(Client player, int flag, string animDict, string animName)
        {
            Program.ServerInstance.PlayCustomPlayerAnimation(player, flag, animDict, animName);
        }

        public void playPlayerScenario(Client player, string scenarioName)
        {
            Program.ServerInstance.PlayCustomPlayerAnimation(player, 0, null, scenarioName);
        }

        public void stopPlayerAnimation(Client player)
        {
            Program.ServerInstance.PlayCustomPlayerAnimationStop(player);
        }

        public void playPedAnimation(NetHandle ped, bool looped, string animDict, string animName)
        {
            PedProperties prop;
            if ((prop = Program.ServerInstance.NetEntityHandler.NetToProp<PedProperties>(ped.Value)) != null)
            {
                if (looped)
                {
                    prop.LoopingAnimation = animDict + " " + animName;

                    var delta = new Delta_PedProperties();
                    delta.LoopingAnimation = prop.LoopingAnimation;
                    Program.ServerInstance.UpdateEntityInfo(ped.Value, EntityType.Ped, delta);
                }

                sendNativeToAllPlayers(0xEA47FE3719165B94, ped, animDict, animName, -8f, -10f, -1, looped ? 1 : 0, 8f, false, false, false);
            }
        }

        public void playPedScenario(NetHandle ped, string scenario)
        {
            PedProperties prop;
            if ((prop = Program.ServerInstance.NetEntityHandler.NetToProp<PedProperties>(ped.Value)) != null)
            {
                prop.LoopingAnimation = scenario;

                var delta = new Delta_PedProperties();
                delta.LoopingAnimation = prop.LoopingAnimation;
                Program.ServerInstance.UpdateEntityInfo(ped.Value, EntityType.Ped, delta);

                sendNativeToAllPlayers(0x142A02425FF02BD9, ped, scenario, 0, false);
            }
        }

        public void stopPedAnimation(NetHandle ped)
        {
            PedProperties prop;
            if ((prop = Program.ServerInstance.NetEntityHandler.NetToProp<PedProperties>(ped.Value)) != null)
            {
                prop.LoopingAnimation = "";

                var delta = new Delta_PedProperties();
                delta.LoopingAnimation = prop.LoopingAnimation;
                Program.ServerInstance.UpdateEntityInfo(ped.Value, EntityType.Ped, delta);

                sendNativeToAllPlayers(0xE1EF3C1216AFF2CD, ped);
            }
        }

        public void setTime(int hours, int minutes)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x47C3B5848C3E45D8, hours, minutes, 0);
            Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).Hours = (byte)hours;
            Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1).Minutes = (byte) minutes;
        }

        public TimeSpan getTime()
        {
            var prop = Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1);

            return new TimeSpan(prop.Hours, prop.Minutes, 0);
        }

        public void freezePlayerTime(Client client, bool freeze)
        {
            Program.ServerInstance.SendNativeCallToPlayer(client, false, 0x4055E40BD2DBEC1D, freeze);
        }

        public void setVehiclePrimaryColor(NetHandle vehicle, int color)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor = color;
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x55E1D2758F34E437, vehicle);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x4F1D4BE3A7F24601, vehicle, color, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor);

                var delta = new Delta_VehicleProperties();
                delta.PrimaryColor = color;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleCustomPrimaryColor(NetHandle vehicle, int red, int green, int blue)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor = Extensions.FromArgb(1, (byte)red, (byte)green, (byte)blue);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x7141766F91D15BEA, vehicle, red, green, blue);

                var delta = new Delta_VehicleProperties();
                delta.PrimaryColor = Extensions.FromArgb(1, (byte)red, (byte)green, (byte)blue);
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleSecondaryColor(NetHandle vehicle, int color)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor = color;
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x5FFBDEEC3E8E2009, vehicle);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x4F1D4BE3A7F24601, vehicle, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor, color);

                var delta = new Delta_VehicleProperties();
                delta.SecondaryColor = color;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void setVehicleCustomSecondaryColor(NetHandle vehicle, int red, int green, int blue)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor = Extensions.FromArgb(1, (byte)red, (byte)green, (byte)blue);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x36CED73BFED89754, vehicle, red, green, blue);

                var delta = new Delta_VehicleProperties();
                delta.SecondaryColor = Extensions.FromArgb(1, (byte)red, (byte)green, (byte)blue);
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public int getVehiclePrimaryColor(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor;
            }
            return 0;
        }

        public Color getVehicleCustomPrimaryColor(NetHandle vehicle)
        {
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            byte a;

            if (doesEntityExist(vehicle))
            {
                Extensions.ToArgb(((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor,
                    out a, out red, out green, out blue);
            }

            return new Color(red, green, blue, 255);
        }

        public int getVehicleSecondaryColor(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor;
            }
            return 0;
        }

        public Color getVehicleCustomSecondaryColor(NetHandle vehicle)
        {
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            byte a;

            if (doesEntityExist(vehicle))
            {
                Extensions.ToArgb(((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor,
                    out a, out red, out green, out blue);
            }

            return new Color(red, green, blue, 255);
        }

        public Client getPlayerFromName(string name)
        {
            return getAllPlayers().FirstOrDefault(c => c.Name == name);
        }

        public int getPlayerPing(Client player)
        {
            return (int)(player.Latency*1000f);
        }

        public List<Client> getPlayersInRadiusOfPlayer(float radius, Client player)
        {
            return getPlayersInRadiusOfPosition(radius, player.Position);
        }

        public List<Client> getPlayersInRadiusOfPosition(float radius, Vector3 position)
        {
            Func<Client, bool> filterByRadius = player => 
                Math.Pow(position.X - player.Position.X, 2) + 
                Math.Pow(position.Y - player.Position.Y, 2) + 
                Math.Pow(position.Z - player.Position.Z, 2) < Math.Pow(radius, 2);
            return getAllPlayers().Where(filterByRadius).ToList();
        }

        public void setPlayerClothes(Client player, int slot, int drawable, int texture)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Props.Set((byte)slot, (byte)drawable);
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Textures.Set((byte)slot, (byte)texture);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x262B14F48D29DE80, new EntityArgument(player.handle.Value), slot, drawable, texture, 2);

                var delta = new Delta_PlayerProperties();
                delta.Textures =
                    ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Textures;
                delta.Props =
                    ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Props;
                Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
            }
        }

        public int getPlayerClothesDrawable(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                return
                    ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Props.Get((byte) slot);
            }

            return 0;
        }

        public int getPlayerClothesTexture(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                return
                    ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Textures.Get((byte)slot);
            }

            return 0;
        }

        public void setPlayerAccessory(Client player, int slot, int drawable, int texture)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Accessories.Set((byte)slot, new Tuple<byte, byte>((byte)drawable, (byte) texture));
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x93376B65A266EB5F, new EntityArgument(player.handle.Value), slot, drawable, texture, true);

                var delta = new Delta_PlayerProperties();
                delta.Accessories =
                    ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Accessories;
                Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
            }
        }

        public void clearPlayerAccessory(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                ((PlayerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value]).Accessories.Remove((byte) slot);
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x0943E5B8E078E76E, new EntityArgument(player.handle.Value), slot);

                var delta = new Delta_PlayerProperties();
                delta.Accessories =
                    ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Accessories;
                Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);
            }
        }

        public int getPlayerAccessoryDrawable(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                return
                    ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Accessories.Get((byte)slot).Item1;
            }

            return 0;
        }

        public int getPlayerAccessoryTexture(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.handle.Value))
            {
                return
                    ((PlayerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.handle.Value])
                        .Accessories.Get((byte)slot).Item2;
            }

            return 0;
        }

        public void clearPlayerTasks(Client player)
        {
            sendNativeToPlayer(player, 0xE1EF3C1216AFF2CD, player.handle);
            sendNativeToPlayer(player, 0x176CECF6F920D707, player.handle);
        }

        public VehicleHash vehicleNameToModel(string modelName)
		{
			return (from object value in Enum.GetValues(typeof (VehicleHash)) where modelName.ToLower() == ((VehicleHash) value).ToString().ToLower() select ((VehicleHash) value)).FirstOrDefault();
		}

	    public PedHash pedNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(PedHash)) where modelName.ToLower() == ((PedHash)value).ToString().ToLower() select ((PedHash)value)).FirstOrDefault();
		}

        public PickupHash pickupNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(PickupHash)) where modelName.ToLower() == ((PickupHash)value).ToString().ToLower() select ((PickupHash)value)).FirstOrDefault();
		}

        public WeaponHash weaponNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(WeaponHash)) where modelName.ToLower() == ((WeaponHash)value).ToString().ToLower() select ((WeaponHash)value)).FirstOrDefault();
		}

        public List<Client> getAllPlayers()
        {
            return new List<Client>(Program.ServerInstance.Clients);
        }

        public List<NetHandle> getAllVehicles()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte) EntityType.Vehicle)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllObjects()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Prop)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllMarkers()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Marker)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllBlips()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Blip)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllPickups()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Pickup)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllLabels()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.TextLabel)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllPeds()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToCopy()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Ped)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public void setEntityPositionFrozen(Client player, NetHandle entity, bool frozen)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x428CA6DBD1094446, new EntityArgument(entity.Value), frozen);
        }

        public void setEntityPositionFrozen(NetHandle entity, bool frozen)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x428CA6DBD1094446, new EntityArgument(entity.Value), frozen);
        }

        public void triggerClientEventForAll(string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            if (ResourceParent == null)
                packet.Resource = "*";
            else
                packet.Resource = ResourceParent.ResourceParent.DirectoryName;

            packet.Arguments = Program.ServerInstance.ParseNativeArguments(args);

            Program.ServerInstance.SendToAll(packet, PacketType.ScriptEventTrigger, true, ConnectionChannel.NativeCall);
        }

        public void triggerClientEvent(Client player, string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            if (ResourceParent == null)
                packet.Resource = "*";
            else
                packet.Resource = ResourceParent.ResourceParent.DirectoryName;
            packet.Arguments = Program.ServerInstance.ParseNativeArguments(args);

            Program.ServerInstance.SendToClient(player, packet, PacketType.ScriptEventTrigger, true, ConnectionChannel.NativeCall);
        }

        public void sendChatMessageToAll(string message)
        {
            foreach (var msg in message.Split('\n'))
                sendChatMessageToAll("", msg);
        }

        public void sendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            Program.ServerInstance.SendToAll(chatObj, PacketType.ChatData, true, ConnectionChannel.Chat);
        }

        public void sendChatMessageToPlayer(Client player, string message)
        {
            foreach (var msg in message.Split('\n'))
                sendChatMessageToPlayer(player, "", msg);
        }

        public void sendChatMessageToPlayer(Client player, string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            var data = Program.ServerInstance.SerializeBinary(chatObj);

            NetOutgoingMessage msg = Program.ServerInstance.Server.CreateMessage();
            msg.Write((byte)PacketType.ChatData);
            msg.Write(data.Length);
            msg.Write(data);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.Chat);
        }

        public void setPlayerWantedLevel(Client player, int wantedLevel)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x1454F2448DE30163, wantedLevel);
        }

        public int getPlayerWantedLevel(Client player)
        {
            return fetchNativeFromPlayer<int>(player, 0x4C9296CBCD1B971E);
        }

        public bool setPlayerName(Client player, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;
            if (getAllPlayers().Any(p => p.Name.ToLower() == newName.ToLower())) return false;

            player.Name = newName;

            Program.ServerInstance.NetEntityHandler.NetToProp<PlayerProperties>(player.handle.Value).Name =
                newName;

            var delta = new Delta_PlayerProperties();
            delta.Name = newName;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta);

            return true;
        }

        public string getPlayerName(Client player)
        {
            return player.Name;
        }

        public Vector3 getPlayerVelocity(Client player)
        {
            return player.Velocity ?? new Vector3();
        }

        public void setPlayerVelocity(Client player, Vector3 velocity)
        {
            if (player.IsInVehicle)
            {
                sendNativeToPlayer(player, 0x1C99BB7B6E96D16F, player.CurrentVehicle, velocity.X, velocity.Y,
                    velocity.Z);
            }
            else
            {
                sendNativeToPlayer(player, 0x1C99BB7B6E96D16F, player.handle, velocity.X, velocity.Y,
                    velocity.Z);
            }
        }

        public int getPlayerVehicleSeat(Client player)
        {
            if (!player.IsInVehicle || player.CurrentVehicle.IsNull) return -3;
            return player.VehicleSeat;
        }

        internal void safeSendNativeToPlayer(Client player, ulong longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, true, longHash, args);
        }

        internal void safeSendNativeToPlayer(Client player, Hash native, params object[] args)
        {
            safeSendNativeToPlayer(player, (ulong)native, args);
        }

        public void sendNativeToPlayer(Client player, ulong longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, longHash, args);
        }

        public void sendNativeToPlayer(Client player, Hash native, params object[] args)
        {
            sendNativeToPlayer(player, (ulong) native, args);
        }

        public void sendNativeToAllPlayers(ulong longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, longHash, args);
        }

        public void sendNativeToAllPlayers(Hash native, params object[] args)
        {
            sendNativeToAllPlayers((ulong) native, args);
        }

        public void sendNativeToPlayersInRange(Vector3 pos, float range, ulong hash, params object[] args)
        {
            foreach (var client in getAllPlayers())
            {
                if (pos.DistanceToSquared(client.Position) < range*range)
                {
                    sendNativeToPlayer(client, hash, args);
                }
            }
        }

        public void sendNativeToPlayersInRange(Vector3 pos, float range, Hash native, params object[] args)
        {
            sendNativeToPlayersInRange(pos, range, (ulong) native, args);
        }

        public void sendNativeToPlayersInRangeInDimension(Vector3 pos, float range, int dimension, ulong hash, params object[] args)
        {
            if (dimension == 0)
            {
                sendNativeToPlayersInRange(pos, range, hash, args);
                return;
            }

            foreach (var client in getAllPlayers())
            {
                if (client.Properties.Dimension == dimension && pos.DistanceToSquared(client.Position) < range * range)
                {
                    sendNativeToPlayer(client, hash, args);
                }
            }
        }

        public void sendNativeToPlayersInRangeInDimension(Vector3 pos, float range, int dimension, Hash native,
            params object[] args)
        {
            sendNativeToPlayersInRangeInDimension(pos, range, dimension, native, args);
        }

        public void sendNativeToPlayersInDimension(int dimension, ulong hash, params object[] args)
        {
            if (dimension == 0)
            {
                sendNativeToAllPlayers(hash, args);
                return;
            }

            foreach (var client in getAllPlayers())
            {
                if (client.Properties.Dimension == dimension)
                {
                    sendNativeToPlayer(client, hash, args);
                }
            }
        }

        public void sendNativeToPlayersInDimension(int dimension, Hash native, params object[] args)
        {
            sendNativeToPlayersInDimension(dimension, (ulong) native, args);
        }

        public T fetchNativeFromPlayer<T>(Client player, ulong longHash, params object[] args)
        {
            var returnType = Program.ServerInstance.ParseReturnType(typeof (T));

            if (returnType == null)
            {
                throw new ArgumentException("Type \"" + typeof(T) + "\" is not a valid return type.");
            }

            return (T) Program.ServerInstance.ReturnNativeCallFromPlayer(player, false, longHash,
                returnType, args);
        }

        internal T safeFetchNativeFromPlayer<T>(Client player, ulong longHash, params object[] args)
        {
            var returnType = Program.ServerInstance.ParseReturnType(typeof(T));

            if (returnType == null)
            {
                throw new ArgumentException("Type \"" + typeof(T) + "\" is not a valid return type.");
            }

            return (T)Program.ServerInstance.ReturnNativeCallFromPlayer(player, true, longHash,
                returnType, args);
        }

        public T fetchNativeFromPlayer<T>(Client player, Hash native, params object[] args)
        {
            return fetchNativeFromPlayer<T>(player, (ulong) native, args);
        }

        internal T safeFetchNativeFromPlayer<T>(Client player, Hash native, params object[] args)
        {
            return safeFetchNativeFromPlayer<T>(player, (ulong)native, args);
        }

        public void givePlayerWeapon(Client player, WeaponHash weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            if (!player.Weapons.ContainsKey(weaponHash))
            {
               lock (player.Weapons) player.Weapons.Add(weaponHash, ammo);
            }
            lock (player.Weapons) player.Weapons[weaponHash] = ammo;
            Program.ServerInstance.SendServerEventToPlayer(player, ServerEventType.WeaponPermissionChange, true, (int)weaponHash, true);

            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), (int)weaponHash, 0, equipNow, ammo);
            Program.ServerInstance.SendNativeCallToPlayer(player, false, (ulong)Hash.SET_PED_AMMO, new LocalPlayerArgument(), (int)weaponHash, ammo); //SET_PED_AMMO
        }

        public int getPlayerWeaponAmmo(Client player, WeaponHash weaponHash)
        {
            if (!player.Weapons.ContainsKey(weaponHash)) return 0;
            return player.Weapons[weaponHash];
            //return fetchNativeFromPlayer<int>(player, (ulong)Hash.GET_AMMO_IN_PED_WEAPON, new LocalPlayerArgument(), (int)weaponHash);
        }

        public void setPlayerWeaponAmmo(Client player, WeaponHash weaponHash, int ammo)
        {
            if (!player.Weapons.ContainsKey(weaponHash)) return;
            lock (player.Weapons) player.Weapons[weaponHash] = ammo;
            Program.ServerInstance.SendNativeCallToPlayer(player, false, (ulong)Hash.SET_PED_AMMO, new LocalPlayerArgument(), (int)weaponHash, ammo); //SET_PED_AMMO
        }

        public void removePlayerWeapon(Client player, WeaponHash weapon)
        {
            if (player.Weapons.ContainsKey(weapon)) lock (player.Weapons) player.Weapons.Remove(weapon);

            if (player.Properties.WeaponComponents.ContainsKey((int)weapon)) player.Properties.WeaponComponents.Remove((int)weapon);
            if (player.Properties.WeaponTints.ContainsKey((int)weapon)) player.Properties.WeaponTints.Remove((int)weapon);

            Program.ServerInstance.SendServerEventToPlayer(player, ServerEventType.WeaponPermissionChange, true, (int)weapon, false);
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x4899CB088EDF59B8, new LocalPlayerArgument(), (int)weapon);

            var delta = new Delta_PlayerProperties();
            delta.WeaponTints = player.Properties.WeaponTints;
            delta.WeaponComponents = player.Properties.WeaponComponents;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public void removeAllPlayerWeapons(Client player)
        {
            lock (player.Weapons) player.Weapons.Clear();
            player.Properties.WeaponTints.Clear();
            player.Properties.WeaponComponents.Clear();

            player.Properties.WeaponTints.Add((int)WeaponHash.Unarmed, 0);
            player.Properties.WeaponComponents.Add((int)WeaponHash.Unarmed, new List<int>());

            Program.ServerInstance.SendServerEventToPlayer(player, ServerEventType.WeaponPermissionChange, false);
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xF25DF915FA38C5F3, new LocalPlayerArgument(), true);

            var delta = new Delta_PlayerProperties();
            delta.WeaponTints = player.Properties.WeaponTints;
            delta.WeaponComponents = player.Properties.WeaponComponents;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public void setPlayerWeaponTint(Client player, WeaponHash weapon, WeaponTint tint)
        {
            player.Properties.WeaponTints.Set((int) weapon, (byte) tint);

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x50969B9B89ED5738, player.handle, (int) weapon, (int) tint);

            var delta = new Delta_PlayerProperties();
            delta.WeaponTints = player.Properties.WeaponTints;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public WeaponTint getPlayerWeaponTint(Client player, WeaponHash weapon)
        {
            return (WeaponTint) player.Properties.WeaponTints.Get((int) weapon);
        }

        public WeaponComponent[] getPlayerWeaponComponents(Client player, WeaponHash weapon)
        {
            if (player.Properties.WeaponComponents.ContainsKey((int)weapon))
            {
                return player.Properties.WeaponComponents[(int) weapon].Select(k => (WeaponComponent)k).ToArray();
            }
            
            return new WeaponComponent[0];
        }

        public void givePlayerWeaponComponent(Client player, WeaponHash weapon, WeaponComponent component)
        {
            if (player.Properties.WeaponComponents.ContainsKey((int) weapon))
            {
                if (!player.Properties.WeaponComponents[(int) weapon].Contains((int) component))
                {
                    player.Properties.WeaponComponents[(int) weapon].Add((int) component);
                }
            }
            else
            {
                player.Properties.WeaponComponents.Add((int)weapon, new List<int> {(int)component});
            }

            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xD966D51AA5B28BB9, player.handle, (int)weapon, (int)component);

            var delta = new Delta_PlayerProperties();
            delta.WeaponComponents = player.Properties.WeaponComponents;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public void removePlayerWeaponComponent(Client player, WeaponHash weapon, WeaponComponent component)
        {
            if (player.Properties.WeaponComponents.ContainsKey((int)weapon))
            {
                player.Properties.WeaponComponents[(int) weapon].Remove((int) component);
            }
            
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x1E8BE90C74FB4C09, player.handle, (int)weapon, (int)component);

            var delta = new Delta_PlayerProperties();
            delta.WeaponComponents = player.Properties.WeaponComponents;
            Program.ServerInstance.UpdateEntityInfo(player.handle.Value, EntityType.Player, delta, player);
        }

        public WeaponComponent[] getAllWeaponComponents(WeaponHash weapon)
        {
            switch (weapon)
            {
                default:
                    return new WeaponComponent[0];
                case WeaponHash.Pistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.PistolClip01,
                        WeaponComponent.PistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp02,
                        WeaponComponent.PistolVarmodLuxe,
                    };
                case WeaponHash.CombatPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatPistolClip01,
                        WeaponComponent.CombatPistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.CombatPistolVarmodLowrider,
                    };
                case WeaponHash.APPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.APPistolClip01,
                        WeaponComponent.APPistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.APPistolVarmodLuxe,
                    };
                case WeaponHash.MicroSMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MicroSMGClip01,
                        WeaponComponent.MicroSMGClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.MicroSMGVarmodLuxe,
                    };
                case WeaponHash.SMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SMGClip01,
                        WeaponComponent.SMGClip02,
                        WeaponComponent.SMGClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.AtScopeMacro02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.SMGVarmodLuxe,
                    };
                case WeaponHash.AssaultRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultRifleClip01,
                        WeaponComponent.AssaultRifleClip02,
                        WeaponComponent.AssaultRifleClip03,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AssaultRifleVarmodLuxe,
                    };
                case WeaponHash.CarbineRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CarbineRifleClip01,
                        WeaponComponent.CarbineRifleClip02,
                        WeaponComponent.CarbineRifleClip03,
                        WeaponComponent.AtRailCover01,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.CarbineRifleVarmodLuxe,
                    };
                case WeaponHash.AdvancedRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AdvancedRifleClip01,
                        WeaponComponent.AdvancedRifleClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.AdvancedRifleVarmodLuxe,
                    };
                case WeaponHash.MG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MGClip01,
                        WeaponComponent.MGClip02,
                        WeaponComponent.AtScopeSmall02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.MGVarmodLowrider,
                    };
                case WeaponHash.CombatMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatMGClip01,
                        WeaponComponent.CombatMGClip02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.CombatMGVarmodLowrider,
                    };
                case WeaponHash.PumpShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtSrSupp,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.PumpShotgunVarmodLowrider,
                    };
                case WeaponHash.AssaultShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultShotgunClip01,
                        WeaponComponent.AssaultShotgunClip02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtArSupp,
                    };
                case WeaponHash.SniperRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SniperRifleClip01,
                        WeaponComponent.AtScopeLarge,
                        WeaponComponent.AtScopeMax,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.SniperRifleVarmodLuxe,
                    };
                case WeaponHash.HeavySniper:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.HeavySniperClip01,
                        WeaponComponent.AtScopeLarge,
                        WeaponComponent.AtScopeMax,
                    };
                case WeaponHash.GrenadeLauncher:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                    };
                case WeaponHash.Minigun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MinigunClip01,
                    };
                case WeaponHash.AssaultSMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultSMGClip01,
                        WeaponComponent.AssaultSMGClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AssaultSMGVarmodLowrider,
                    };
                case WeaponHash.BullpupShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtArSupp02,
                    };
                case WeaponHash.Pistol50:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.Pistol50Clip01,
                        WeaponComponent.Pistol50Clip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.Pistol50VarmodLuxe,
                    };
                case WeaponHash.CombatPDW:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatPDWClip01,
                        WeaponComponent.CombatPDWClip02,
                        WeaponComponent.CombatPDWClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArAfGrip,
                    };
                case WeaponHash.SawnoffShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SawnoffShotgunVarmodLuxe,
                    };
                case WeaponHash.BullpupRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.BullpupRifleClip01,
                        WeaponComponent.BullpupRifleClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.BullpupRifleVarmodLow,
                    };
                case WeaponHash.SNSPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SNSPistolClip01,
                        WeaponComponent.SNSPistolClip02,
                        WeaponComponent.SNSPistolVarmodLowrider,
                    };
                case WeaponHash.SpecialCarbine:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SpecialCarbineClip01,
                        WeaponComponent.SpecialCarbineClip02,
                        WeaponComponent.SpecialCarbineClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.SpecialCarbineVarmodLowrider,
                    };
                case WeaponHash.KnuckleDuster:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.KnuckleVarmodPimp,
                        WeaponComponent.KnuckleVarmodBallas,
                        WeaponComponent.KnuckleVarmodDollar,
                        WeaponComponent.KnuckleVarmodDiamond,
                        WeaponComponent.KnuckleVarmodHate,
                        WeaponComponent.KnuckleVarmodLove,
                        WeaponComponent.KnuckleVarmodPlayer,
                        WeaponComponent.KnuckleVarmodKing,
                        WeaponComponent.KnuckleVarmodVagos,
                    };
                case WeaponHash.MachinePistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MachinePistolClip01,
                        WeaponComponent.MachinePistolClip02,
                        WeaponComponent.MachinePistolClip03,
                        WeaponComponent.AtPiSupp,
                    };
                case WeaponHash.SwitchBlade:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SwitchbladeVarmodVar1,
                        WeaponComponent.SwitchbladeVarmodVar2,
                    };
                case WeaponHash.Revolver:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.RevolverClip01,
                        WeaponComponent.RevolverVarmodBoss,
                        WeaponComponent.RevolverVarmodGoon,
                    };
            }
        }

        public bool hasPlayerGotWeaponComponent(Client player, WeaponHash weapon, WeaponComponent component)
        {
            return player.Properties.WeaponComponents.ContainsKey((int) weapon) &&
                   player.Properties.WeaponComponents[(int) weapon].Contains((int) component);
        }

        public WeaponHash[] getPlayerWeapons(Client player)
        {
            return player.Weapons.Keys.ToArray();
        }

        public WeaponHash getPlayerCurrentWeapon(Client player)
        {
            return player.CurrentWeapon;
        }

        public string getPlayerAddress(Client player)
        {
            return player.NetConnection.RemoteEndPoint.Address.ToString();
        }

        public void kickPlayer(Client player, string reason)
        {
            player.NetConnection.Disconnect(reason);
        }

        public void kickPlayer(Client player)
        {
            player.NetConnection.Disconnect("You have been kicked.");
        }

        public void banPlayer(Client player, string reason)
        {
            Program.ServerInstance.BanManager.BanPlayer(player, reason);
            kickPlayer(player, reason);
        }

        public void banPlayer(Client player)
        {
            Program.ServerInstance.BanManager.BanPlayer(player);
            kickPlayer(player);
        }

        public void unbanPlayer(string socialClubHandle)
        {
            Program.ServerInstance.BanManager.UnbanPlayer(socialClubHandle);
        }
        
        private Random r = new Random();
        public double random()
        {
            return r.NextDouble();
        }

        public void setEntityPosition(NetHandle netHandle, Vector3 newPosition)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x239A3351AC1DA385, new EntityArgument(netHandle.Value), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0);
	        if (doesEntityExist(netHandle))
	        {
		        Program.ServerInstance.NetEntityHandler.ToDict()[netHandle.Value].Position = newPosition;

                var delta = new Delta_EntityProperties();
	            delta.Position = newPosition;
                Program.ServerInstance.UpdateEntityInfo(netHandle.Value, EntityType.Prop, delta);

	            setEntityData(netHandle, "__LAST_POSITION_SET", TickCount);
	        }
        }

        public void moveEntityPosition(NetHandle netHandle, Vector3 target, int duration)
        {
            if (doesEntityExist(netHandle))
            {
                Program.ServerInstance.CreatePositionInterpolation(netHandle.Value, target, duration);
            }
        }

        public void moveEntityRotation(NetHandle netHandle, Vector3 target, int duration)
        {
            if (doesEntityExist(netHandle))
            {
                Program.ServerInstance.CreateRotationInterpolation(netHandle.Value, target, duration);
            }
        }

        public void attachEntityToEntity(NetHandle entity, NetHandle entityTarget, string bone, Vector3 positionOffset, Vector3 rotationOffset)
        {
            if (doesEntityExist(entity) && doesEntityExist(entityTarget) && entity != entityTarget)
            {
                if (Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].AttachedTo != null)
                {
                    detachEntity(entity, true);
                }

                Attachment info = new Attachment();

                info.NetHandle = entityTarget.Value;
                info.Bone = bone;
                info.PositionOffset = positionOffset;
                info.RotationOffset = rotationOffset;

                if (
                    Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entityTarget.Value).Attachables ==
                    null)
                {
                    Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entityTarget.Value).Attachables
                        = new List<int>();
                }

                Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entity.Value).AttachedTo = info;
                Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entityTarget.Value).Attachables.Add(entity.Value);

                var ent1 = new Delta_EntityProperties();
                ent1.AttachedTo = info;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, ent1);

                var ent2 = new Delta_EntityProperties();
                ent2.Attachables = Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entityTarget.Value).Attachables;
                Program.ServerInstance.UpdateEntityInfo(entityTarget.Value, EntityType.Prop, ent2);
            }
        }

        public bool isEntityAttachedToAnything(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entity.Value).AttachedTo != null;
            }
            return false;
        }

        public bool isEntityAttachedToEntity(NetHandle entity, NetHandle attachedTo)
        {
            if (doesEntityExist(entity))
            {
                if (!isEntityAttachedToAnything(entity)) return false;
                return Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entity.Value).AttachedTo.NetHandle == attachedTo.Value;
            }

            return false;
        }

        public void detachEntity(NetHandle entity, bool resetCollision = true)
        {
            if (doesEntityExist(entity))
            {
                Program.ServerInstance.DetachEntity(entity.Value, resetCollision);
                var prop = Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(entity.Value);
                if (prop != null && prop.AttachedTo != null)
                {
                    var target =
                        Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(prop.AttachedTo.NetHandle);

                    if (target != null && target.Attachables != null)
                    {
                        target.Attachables.Remove(entity.Value);
                    }
                }
                prop.AttachedTo = null;
            }
        }

        public void setPlayerSeatbelt(Client player, bool seatbelt)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x1913FE4CBF41C463,
                new EntityArgument(player.handle.Value), 32, !seatbelt);
        }

        public bool getPlayerSeatbelt(Client player)
        {
            return !fetchNativeFromPlayer<bool>(player, 0x7EE53118C892B513, new EntityArgument(player.handle.Value), 32, true);
        }

        public void freezePlayer(Client player, bool freeze)
        {
            if (player.IsInVehicle)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x428CA6DBD1094446, new EntityArgument(player.CurrentVehicle.Value), freeze);
            }

            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x428CA6DBD1094446, new EntityArgument(player.handle.Value), freeze);
        }

	    public void setEntityRotation(NetHandle netHandle, Vector3 newRotation)
	    {
			Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x8524A8B0171D5E07, new EntityArgument(netHandle.Value), newRotation.X, newRotation.Y, newRotation.Z, 2, 1);
			if (doesEntityExist(netHandle))
			{
				Program.ServerInstance.NetEntityHandler.ToDict()[netHandle.Value].Rotation = newRotation;

                var delta = new Delta_EntityProperties();
                delta.Rotation = newRotation;
                Program.ServerInstance.UpdateEntityInfo(netHandle.Value, EntityType.Prop, delta);
            }
		}

        public Vector3 getEntityPosition(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Position ?? new Vector3();
            }
            return new Vector3();
        }

        public Vector3 getEntityRotation(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Rotation ?? new Vector3(0, 0, 0);
            }
            return new Vector3();
        }
        

        public void setPlayerIntoVehicle(Client player, NetHandle vehicle, int seat)
        {
            //var start = Environment.TickCount;
            //while (!doesEntityExistForPlayer(player, vehicle) && Environment.TickCount - start < 1000) { }
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xF75B0D629E1C063D, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), seat);

            player.IsInVehicle = true;
            player.CurrentVehicle = vehicle;

            setEntityData(player, "__LAST_POSITION_SET", TickCount);
        }

        public void warpPlayerOutOfVehicle(Client player, NetHandle vehicle)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xD3DBCE61A490BE02, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), 16);
        }

        public bool doesEntityExist(NetHandle entity)
        {
            return Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value);
        }

        public void setVehicleHealth(NetHandle vehicle, float health)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(vehicle.Value))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health = health;

                var delta = new Delta_VehicleProperties();
                delta.Health = health;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x45F6D8EEF34ABEF1, vehicle, health);
        }

        public float getVehicleHealth(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health;
            }
            return 0f;
        }

        public int getPickupAmount(NetHandle pickup)
        {
            if (doesEntityExist(pickup))
            {
                return ((PickupProperties)Program.ServerInstance.NetEntityHandler.ToDict()[pickup.Value]).Amount;
            }
            return 0;
        }

        public int getPickupCustomModel(NetHandle pickup)
        {
            PickupProperties p;
            if ((p = Program.ServerInstance.NetEntityHandler.NetToProp<PickupProperties>(pickup.Value)) != null)
            {
                return p.CustomModel;
            }

            return 0;
        }

        public bool getPickupPickedUp(NetHandle pickup)
        {
            PickupProperties p;
            if ((p = Program.ServerInstance.NetEntityHandler.NetToProp<PickupProperties>(pickup.Value)) != null)
            {
                return p.PickedUp;
            }

            return false;
        }

        public void repairVehicle(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health = 1000f;
                var delta = new Delta_VehicleProperties();
                delta.Health = 1000f;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x115722B1B9C14C1C, vehicle);
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x953DA1E1B12C0491, vehicle);
        }

        public void setPlayerHealth(Client player, int health)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
            player.Health = health;
        }

        public int getPlayerHealth(Client player)
        {
            return player.Health;
        }

        public void setPlayerArmor(Client player, int armor)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xCEA04D83135264CC, new LocalPlayerArgument(), armor);
        }

        public int getPlayerArmor(Client player)
        {
            return player.Armor;
        }

        public void setBlipColor(NetHandle blip, int color)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Color = color;
                var delta = new Delta_BlipProperties();
                delta.Color = color;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x03D7FB09E75D6B7E, blip, color);
        }

        public int getBlipColor(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Color;
            }
            return 0;
        }

        public void setBlipName(NetHandle blip, string name)
        {
            if (doesEntityExist(blip))
            {
                if (name == null) name = "";

                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Name = name;
                var delta = new Delta_BlipProperties();
                delta.Name = name;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }
        }

        public string getBlipName(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Name;
            }
            return null;
        }

        public void setBlipTransparency(NetHandle blip, int alpha)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Alpha = (byte)alpha;
                var delta = new Delta_BlipProperties();
                delta.Alpha = (byte)alpha;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x45FF974EEE1C8734, blip, alpha);
        }

        public int getBlipTransparency(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Alpha;
            }
            return 0;
        }

        public void setBlipShortRange(NetHandle blip, bool range)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties) Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).IsShortRange = range;

                var delta = new Delta_BlipProperties();
                delta.IsShortRange = range;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xBE8BE4FE60E27B72, blip, range);
        }

        public bool getBlipShortRange(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).IsShortRange;
            }
            return false;
        }

        public void setBlipPosition(NetHandle blip, Vector3 newPos)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Position = newPos;

                var delta = new Delta_BlipProperties();
                delta.Position = newPos;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xAE2AF67E9D9AF65D, blip, newPos.X, newPos.Y, newPos.Z);
        }

        public Vector3 getBlipPosition(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Position;
            }

            return null;
        }

        public void setBlipSprite(NetHandle blip, int sprite)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Sprite = sprite;

                var delta = new Delta_BlipProperties();
                delta.Sprite = sprite;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xDF735600A4696DAF, blip, sprite);
        }

        public int getBlipSprite(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Sprite;
            }

            return 0;
        }

        public void setBlipScale(NetHandle blip, float scale)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Scale = scale;

                var delta = new Delta_BlipProperties();
                delta.Scale = scale;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xD38744167B2FA257, blip, scale);
        }

        public float getBlipScale(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Scale;
            }
            
            return 0;
        }

        public void sendNotificationToPlayer(Client player, string message, bool flashing = false)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x202709F4C58A0424, "STRING");
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
            }
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xF020C96915705B3A, flashing, true);
        }

        public void sendNotificationToAll(string message, bool flashing = false)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x202709F4C58A0424, "STRING");
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xF020C96915705B3A, flashing, true);
        }
        
        public  void sendPictureNotificationToPlayer(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToPlayer(player, false, 0xF020C96915705B3A, false, true);
        }
        
        public  void sendPictureNotificationToAll(string body, string pic, int flash, int iconType, string sender, string subject)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToAllPlayers(false, 0xF020C96915705B3A, false, true);
        }
        
        public void setMarkerType(NetHandle marker, int type)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).MarkerType = type;

                var delta = new Delta_MarkerProperties();
                delta.MarkerType = type;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
        }

        public int getMarkerType(NetHandle marker)
        {
            if (doesEntityExist(marker))
            {
                return ((MarkerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).MarkerType;
            }

            return 0;
        }

        public void setMarkerScale(NetHandle marker, Vector3 scale)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Scale = scale;

                var delta = new Delta_MarkerProperties();
                delta.Scale = scale;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
        }

        public Vector3 getMarkerScale(NetHandle marker)
        {
            if (doesEntityExist(marker))
            {
                return ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Scale;
            }

            return null;
        }

        public void setMarkerDirection(NetHandle marker, Vector3 dir)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Direction = dir;

                var delta = new Delta_MarkerProperties();
                delta.Direction = dir;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
        }

        public Vector3 getMarkerDirection(NetHandle marker)
        {
            if (doesEntityExist(marker))
            {
                return ((MarkerProperties) Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Direction;
            }

            return null;
        }

        public void setMarkerColor(NetHandle marker, int alpha, int red, int green, int blue)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Alpha = (byte) alpha;
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Red = (byte) red;
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Green = (byte) green;
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Blue = (byte) blue;

                var delta = new Delta_MarkerProperties();
                delta.Alpha = (byte) alpha;
                delta.Red = (byte)red;
                delta.Green = (byte)green;
                delta.Blue = (byte)blue;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
        }

        public Color getMarkerColor(NetHandle marker)
        {
            Color output = new Color();

            if (doesEntityExist(marker))
            {
                output.alpha = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Alpha;
                output.red = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Red;
                output.green = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Green;
                output.blue = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Blue;
            }

            return output;
        }

        public NetHandle getPlayerVehicle(Client player)
        {
            return player.CurrentVehicle;
        }

        public void setTextLabelText(NetHandle label, string newText)
        {
            if (doesEntityExist(label))
            {
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Text = newText;

                var delta = new Delta_TextLabelProperties();
                delta.Text = newText;
                Program.ServerInstance.UpdateEntityInfo(label.Value, EntityType.TextLabel, delta);
            }
        }

        public string getTextLabelText(NetHandle label)
        {
            if (doesEntityExist(label))
            {
                return ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Text;
            }

            return null;
        }

        public void setTextLabelRange(NetHandle label, float newRange)
        {
            if (doesEntityExist(label))
            {
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Range = newRange;

                var delta = new Delta_TextLabelProperties();
                delta.Range = newRange;
                Program.ServerInstance.UpdateEntityInfo(label.Value, EntityType.TextLabel, delta);
            }
        }

        public float getTextLabelRange(NetHandle label)
        {
            if (doesEntityExist(label))
            {
                return ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Range;
            }

            return 0;
        }

        public void setTextLabelColor(NetHandle label, int red, int green, int blue, int alpha)
        {
            if (doesEntityExist(label))
            {
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Alpha = (byte)alpha;
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Red = red;
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Green = green;
                ((TextLabelProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Blue = blue;

                var delta = new Delta_TextLabelProperties();
                delta.Alpha = (byte)alpha;
                delta.Red = red;
                delta.Green = green;
                delta.Blue = blue;
                Program.ServerInstance.UpdateEntityInfo(label.Value, EntityType.TextLabel, delta);
            }
        }

        public Color getTextLabelColor(NetHandle label)
        {
            Color output = new Color();

            if (doesEntityExist(label))
            {
                output.alpha = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Alpha;
                output.red = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Red;
                output.green = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Green;
                output.blue = ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).Blue;
            }

            return output;
        }

        public void setTextLabelSeethrough(NetHandle label, bool seethrough)
        {
            if (doesEntityExist(label))
            {
                ((TextLabelProperties) Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).EntitySeethrough =
                    seethrough;

                var delta = new Delta_TextLabelProperties();
                delta.EntitySeethrough = seethrough;
                Program.ServerInstance.UpdateEntityInfo(label.Value, EntityType.TextLabel, delta);
            }
        }

        public bool getTextLabelSeethrough(NetHandle label)
        {
            if (doesEntityExist(label))
            {
                return ((TextLabelProperties) Program.ServerInstance.NetEntityHandler.ToDict()[label.Value]).EntitySeethrough;
            }
            return false;
        }

        public void registerCustomColShape(ColShape shape)
        {
            Program.ServerInstance.ColShapeManager.Add(shape);
            lock (ResourceColShapes) ResourceColShapes.Add(shape);
        }

        public SphereColShape createSphereColShape(Vector3 position, float range)
        {
            var shape = new SphereColShape(position, range);
            Program.ServerInstance.ColShapeManager.Add(shape);
            lock (ResourceColShapes) ResourceColShapes.Add(shape);
            return shape;
        }

        public CylinderColShape createCylinderColShape(Vector3 position, float range, float height)
        {
            var shape = new CylinderColShape(position, range, height);
            Program.ServerInstance.ColShapeManager.Add(shape);
            lock (ResourceColShapes) ResourceColShapes.Add(shape);
            return shape;
        }

        public Rectangle2DColShape create2DColShape(float x, float y, float width, float height)
        {
            var shape = new Rectangle2DColShape(x, y, width, height);
            Program.ServerInstance.ColShapeManager.Add(shape);
            lock (ResourceColShapes) ResourceColShapes.Add(shape);
            return shape;
        }

        public Rectangle3DColShape create3DColShape(Vector3 start, Vector3 end)
        {
            var shape = new Rectangle3DColShape(start, end);
            Program.ServerInstance.ColShapeManager.Add(shape);
            lock (ResourceColShapes) ResourceColShapes.Add(shape);
            return shape;
        }

        public void deleteColShape(ColShape shape)
        {
            lock (ResourceColShapes) ResourceColShapes.Remove(shape);
            Program.ServerInstance.ColShapeManager.Remove(shape);
        }

        public Vehicle createVehicle(VehicleHash model, Vector3 pos, Vector3 rot, int color1, int color2, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateVehicle((int)model, pos, rot, color1, color2, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Vehicle(this, ent);
        }

        public Object createObject(int model, Vector3 pos, Vector3 rot, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Object(this, ent);
        }

        public Object createObject(int model, Vector3 pos, Quaternion rot, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Object(this, ent);
        }

        public Blip createBlip(Vector3 pos, float range, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(pos, range, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Blip(this, ent);
        }

        public Blip createBlip(Vector3 pos, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(pos, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Blip(this, ent);
        }

        public Blip createBlip(NetHandle entity)
        {
            if (entity.IsNull || !entity.Exists()) throw new ArgumentNullException(nameof(entity));
            //var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(entity));
            //lock (ResourceEntities) ResourceEntities.Add(ent);
            var ent = createBlip(getEntityPosition(entity));
            attachEntityToEntity(ent, entity, null, new Vector3(), new Vector3());
            return new Blip(this, ent);
        }

        public Pickup createPickup(PickupHash pickupHash, Vector3 pos, Vector3 rot, int amount, uint respawnTime, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreatePickup((int)pickupHash, pos, rot, amount, respawnTime, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Pickup(this, ent);
        }

        public Pickup createPickup(int model, Vector3 pos, Vector3 rot, bool vehicle, uint respawnTime, int dimension = 0)
        {
            var ent =
                new NetHandle(
                    Program.ServerInstance.NetEntityHandler.CreatePickup(
                        (int) (vehicle ? PickupHash.VehicleCustomScript : PickupHash.CustomScript), pos,
                        rot, -1, respawnTime, dimension, model));

            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Pickup(this, ent);
        }

        public void respawnPickup(NetHandle pickup)
        {
            var pic = Program.ServerInstance.NetEntityHandler.NetToProp<PickupProperties>(pickup.Value);
            if (pic != null && pic.PickedUp)
            {
                Program.ServerInstance.PickupManager.RespawnPickup(pickup.Value, pic);
            }
        }

        public Marker createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha,
            int r, int g, int b, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateMarker(markerType, pos, dir, rot, scale, alpha, r, g, b, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Marker(this, ent);
        }

        public TextLabel createTextLabel(string text, Vector3 pos, float range, float size, bool entitySeethrough = false, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateTextLabel(text, size, range, 255, 255, 255, pos, entitySeethrough, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new TextLabel(this, ent);
        }

        public Ped createPed(PedHash model, Vector3 pos, float heading, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateStaticPed((int) model, pos, heading, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new Ped(this, ent);
        }

        public ParticleEffect createLoopedParticleEffectOnPosition(string ptfxLib, string ptfxName, Vector3 position, Vector3 rotation, float scale, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateParticleEffect(ptfxLib, ptfxName, position, rotation, scale, 0, 0, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new ParticleEffect(this, ent);
        }

        public ParticleEffect createLoopedParticleEffectOnEntity(string ptfxLib, string ptfxName, NetHandle entity, Vector3 offset, Vector3 rotation, float scale, int bone = -1, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateParticleEffect(ptfxLib, ptfxName, offset, rotation, scale, entity.Value, bone, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return new ParticleEffect(this, ent);
        }

        private void deleteEntityInternal(NetHandle netHandle)
        {
            Program.ServerInstance.NetEntityHandler.DeleteEntity(netHandle.Value);
            if (Program.ServerInstance.EntityProperties.ContainsKey(netHandle))
                Program.ServerInstance.EntityProperties.Remove(netHandle);
        }

        public void deleteEntity(NetHandle netHandle)
        {
            if (!netHandle.IsNull &&
                Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(netHandle.Value)?.EntityType ==
                (byte) EntityType.Player)
                return;

            deleteEntityInternal(netHandle);
            lock (ResourceEntities) ResourceEntities.Remove(netHandle);
        }

        public void deleteObject(Client client, Vector3 position, int modelHash)
        {
            Program.ServerInstance.SendDeleteObject(client, position, 1f, modelHash);
        }

#endregion
    }
}