using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using GTANetworkShared;
using Lidgren.Network;

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
        public bool autoGarbageCollection = true;
        #endregion

        #region Delegates
        public delegate void CommandEvent(Client sender, string command);
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
        #endregion

        #region Events
        public event EventHandler onResourceStart;
        public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event ChatEvent onChatMessage;
        public event CommandEvent onChatCommand;
        public event PlayerConnectingEvent OnPlayerBeginConnect;
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
            onResourceStart?.Invoke(this, EventArgs.Empty);
        }

        internal void invokeUpdate()
        {
            onUpdate?.Invoke(this, EventArgs.Empty);
        }

        internal void invokeResourceStop()
        {
            onResourceStop?.Invoke(this, EventArgs.Empty);

            lock (ResourceEntities)
            {
                for (int i = ResourceEntities.Count - 1; i >= 0; i--)
                {
                    deleteEntityInternal(ResourceEntities[i]);
                }
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

        internal void invokeChatCommand(Client sender, string msg)
        {
            onChatCommand?.Invoke(sender, msg);
        }

        internal void invokePlayerBeginConnect(Client player, CancelEventArgs e)
        {            
            OnPlayerBeginConnect?.Invoke(player, e);
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

        public string getResourceFolder()
        {
            if (ResourceParent == null)
            {
                throw new NullReferenceException("Illegal call to function inside constructor.");
            }

            return Path.GetFullPath("resources\\" + ResourceParent.ResourceParent.DirectoryName);
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
            foreach (var map in Program.ServerInstance.AvailableMaps)
            {
                if (map.Info.Info.Gamemodes.Split(',').Contains(gamemode)) yield return map.DirectoryName;
            }
        }

        public XmlGroup loadXml(string path)
        {
            if (!isPathSafe(path)) throw new AccessViolationException("Illegal path for XML!");
            if (!File.Exists(path)) throw new FileNotFoundException("File not found!");
            var xml = new XmlGroup();
            xml.Load(path);
            return xml;
        }

        public object call(string resourceName, string scriptName, string methodName, params object[] arguments)
        {
            var ourResource = Program.ServerInstance.RunningResources.FirstOrDefault(k => k.DirectoryName == resourceName);
            if (ourResource == null)
            {
                Program.Output("ERROR: call() - No resource named '" + resourceName + "' found.");
                return null;
            }

            var ourScriptName = ourResource.Engines.FirstOrDefault(en => en.Filename == scriptName);
            if (ourScriptName == null)
            {
                Program.Output("ERROR: call() - No script name named '" + scriptName + "' found.");
                return null;
            }

            return ourScriptName.InvokeMethod(methodName, arguments);
        }

        public dynamic exported
        {
            get { return Program.ServerInstance.ExportedFunctions; }
        }
        
        public void startResource(string resourceName)
        {
            Program.ServerInstance.StartResource(resourceName);
        }

        public void stopResource(string name)
        {
            Program.ServerInstance.StopResource(name);
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

        public void playSoundFrontEnd(Client target, string audioLib, string audioName)
        {
            Program.ServerInstance.SendNativeCallToPlayer(target, 0x2F844A8B08D76685, audioLib, true);
            Program.ServerInstance.SendNativeCallToPlayer(target, 0x67C540AA08E4A6F5, -1, audioName, audioLib);
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
            return Program.ServerInstance.Clients.FirstOrDefault(c => c.CharacterHandle == handle);
        }

        public void requestIpl(string iplName)
        {
            if (!Program.ServerInstance.LoadedIPL.Contains(iplName))
                Program.ServerInstance.LoadedIPL.Add(iplName);
            if (Program.ServerInstance.RemovedIPL.Contains(iplName))
                Program.ServerInstance.RemovedIPL.Remove(iplName);
            sendNativeToAllPlayers(0x41B4893843BBDB74, iplName);
        }

        public void removeIpl(string iplName)
        {
            if (!Program.ServerInstance.RemovedIPL.Contains(iplName))
                Program.ServerInstance.RemovedIPL.Add(iplName);
            if (Program.ServerInstance.LoadedIPL.Contains(iplName))
                Program.ServerInstance.LoadedIPL.Remove(iplName);
            sendNativeToAllPlayers(0xEE6C5AD3ECE0A82D, iplName);
        }

        public void resetIplList()
        {
            Program.ServerInstance.RemovedIPL.Clear();
            Program.ServerInstance.LoadedIPL.Clear();
        }

        public void sleep(int ms)
        {
            if (ResourceParent != null && !ResourceParent.Async)
            {
                Program.Output("WARN: using API.sleep in a non-async environment is not recommended!");
            }

            var start = DateTime.Now;
            while (DateTime.Now.Subtract(start).TotalMilliseconds < ms)
            {
                Thread.Sleep(10);
            }
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

        public int loginPlayer(Client player, string password)
        {
            if (!Program.ServerInstance.ACLEnabled) return (int) AccessControlList.LoginResult.ACLDisabled;
            return (int) Program.ServerInstance.ACL.TryLoginPlayer(player, password);
        }

        public void logoutPlayer(Client player)
        {
            Program.ServerInstance.ACL.LogOutClient(player);
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

        public bool doesEntityExistForPlayer(Client player, NetHandle entity)
        {
            return
                (bool)
                    Program.ServerInstance.ReturnNativeCallFromPlayer(player, 0x7239B21A38F536BA, new BooleanArgument(),
                        new EntityArgument(entity.Value));
        }

        public void setEntityTransparency(NetHandle entity, int newAlpha)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(entity.Value))
            {
                Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Alpha = (byte) newAlpha;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x44A0870B7E92D7C0, new EntityArgument(entity.Value), newAlpha, false);
                
                var delta = new Delta_EntityProperties();
                delta.Alpha = (byte) newAlpha;
                Program.ServerInstance.UpdateEntityInfo(entity.Value, EntityType.Prop, delta);
            }
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
                        Program.ServerInstance.NetEntityHandler.ToDict()
                            .FirstOrDefault(
                                p => p.Value is BlipProperties && ((BlipProperties) p.Value).AttachedNetEntity ==
                        entity.Value)).Key != 0)
                {
                    Program.ServerInstance.NetEntityHandler.ToDict()[pair.Key].Dimension = dimension;

                    var deltaBlip = new Delta_EntityProperties();
                    deltaBlip.Dimension = dimension;
                    Program.ServerInstance.UpdateEntityInfo(pair.Key, EntityType.Prop, deltaBlip);
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

        public void playSpecialEffectOnPosition(string ptfxLibrary, string ptfxName, Vector3 position, Vector3 rotation, float scale)
        {
            sendNativeToAllPlayers(0xB80D8756B4668AB6, ptfxLibrary);
            sendNativeToAllPlayers(0x6C38AF3693A69A91, ptfxLibrary);
            sendNativeToAllPlayers(0x25129531F77B9ED3, ptfxName, position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z, scale, 0, 0, 0);
        }

        /// <summary>
        /// WARN: Resets on reconnect.
        /// </summary>
        public void setCollisionBetweenEntities(NetHandle entity1, NetHandle entity2, bool collision)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0xA53ED5520C07654A, entity1, entity2, collision);
        }

        public void setPlayerBlipColor(Client target, int newColor)
        {
            Program.ServerInstance.ChangePlayerBlipColor(target, newColor);
        }

        public void setPlayerBlipAlpha(Client target, int newAlpha)
        {
            Program.ServerInstance.ChangePlayerBlipAlpha(target, newAlpha);
        }

        public void setPlayerBlipSprite(Client target, int newSprite)
        {
            Program.ServerInstance.ChangePlayerBlipSprite(target, newSprite);
        }

        public void setPlayerToSpectator(Client player)
        {
            Program.ServerInstance.SetPlayerOnSpectate(player, true);
        }

        public void setPlayerToSpectatePlayer(Client player, Client target)
        {
            Program.ServerInstance.SetPlayerOnSpectatePlayer(player, target);
        }

        public void unspectatePlayer(Client player)
        {
            Program.ServerInstance.SetPlayerOnSpectate(player, false);
        }

        public void setVehicleLivery(NetHandle vehicle, int livery)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Livery = livery;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x60BF608F1B8CD1B6, new EntityArgument(vehicle.Value), livery);

                var delta = new Delta_VehicleProperties();
                delta.Livery = livery;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public int getVehicleLivery(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Livery;
            }
            return 0;
        }

        public void setVehicleNumberPlate(NetHandle vehicle, string plate)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).NumberPlate = plate;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x95A88F0B409CDA47, new EntityArgument(vehicle.Value), plate);

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
                Program.ServerInstance.SendNativeCallToAllPlayers(0x2497C4717C8B881E, vehicle, turnedOn, true, true);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x8ABA6AF54B942B95, vehicle, !turnedOn);

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

        public void setEntityCollisionless(NetHandle entity, bool collisionless)
        {
            if (doesEntityExist(entity))
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(0x1A9205C1B9EE827F, entity, !collisionless, true);

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

        public void setVehicleMod(NetHandle vehicle, int modType, int mod)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(vehicle.Value))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods.Set(modType, mod);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x6AF0636DDEDCB6DD, new EntityArgument(vehicle.Value), modType, mod, false);

                var delta = new Delta_VehicleProperties();
                delta.Mods = ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }
        }

        public void removeVehicleMod(NetHandle vehicle, int modType)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods.Remove(modType);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(0x92D619E420858204, vehicle, modType);

            var delta = new Delta_VehicleProperties();
            delta.Mods = ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods;
            Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
        }

        public void setPlayerSkin(Client player, PedHash modelHash)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x00A1CADD00108836, new LocalGamePlayerArgument(), (int)modelHash);
            if (doesEntityExist(player.CharacterHandle))
            {
                ((PedProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value])
                    .ModelHash = (int)modelHash;
                
                var delta = new Delta_PedProperties();
                delta.ModelHash = (int)modelHash;
                Program.ServerInstance.UpdateEntityInfo(player.CharacterHandle.Value, EntityType.Ped, delta);
            }

            setPlayerDefaultClothes(player);
        }

        public void setPlayerDefaultClothes(Client player)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x45EEE61580806D63, player.CharacterHandle);
            if (doesEntityExist(player.CharacterHandle))
            {
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Accessories.Clear();
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Textures.Clear();
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Props.Clear();

                var delta = new Delta_PedProperties();
                delta.Textures = new Dictionary<byte, byte>();
                delta.Accessories = new Dictionary<byte, Tuple<byte, byte>>();
                delta.Props = new Dictionary<byte, byte>();
                Program.ServerInstance.UpdateEntityInfo(player.CharacterHandle.Value, EntityType.Ped, delta);
            }
        }

        public void setWeather(string weather)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0xED712CA327900C8A, weather);
            Program.ServerInstance.Weather = weather;
        }

        public void setPlayerTeam(Client player, int team)
        {
            Program.ServerInstance.ChangePlayerTeam(player, team);
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

        public void setTime(int hours, int minutes)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x47C3B5848C3E45D8, hours, minutes, 0);
            Program.ServerInstance.TimeOfDay = new DateTime(2016, 1, 1, hours, minutes, 0);
        }

        public void freezePlayerTime(Client client, bool freeze)
        {
            Program.ServerInstance.SendNativeCallToPlayer(client, 0x4055E40BD2DBEC1D, freeze);
        }

        public void setVehiclePrimaryColor(NetHandle vehicle, int color)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor = color;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x55E1D2758F34E437, vehicle);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4F1D4BE3A7F24601, vehicle, color, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor);

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
                Program.ServerInstance.SendNativeCallToAllPlayers(0x7141766F91D15BEA, vehicle, red, green, blue);

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
                Program.ServerInstance.SendNativeCallToAllPlayers(0x5FFBDEEC3E8E2009, vehicle);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4F1D4BE3A7F24601, vehicle, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor, color);

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
                Program.ServerInstance.SendNativeCallToAllPlayers(0x36CED73BFED89754, vehicle, red, green, blue);

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

        public void getVehicleCustomPrimaryColor(NetHandle vehicle, out byte red, out byte green, out byte blue)
        {
            red = 0;
            green = 0;
            blue = 0;
            byte a;

            if (doesEntityExist(vehicle))
            {
                Extensions.ToArgb(((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor,
                    out a, out red, out green, out blue);
            }
        }

        public int getVehicleSecondaryColor(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor;
            }
            return 0;
        }

        public void getVehicleCustomSecondaryColor(NetHandle vehicle, out byte red, out byte green, out byte blue)
        {
            red = 0;
            green = 0;
            blue = 0;
            byte a;

            if (doesEntityExist(vehicle))
            {
                Extensions.ToArgb(((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor,
                    out a, out red, out green, out blue);
            }
        }

        public Client getPlayerFromName(string name)
        {
            return getAllPlayers().FirstOrDefault(c => c.Name == name);
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

        public void setPlayerProp(Client player, int slot, int drawable, int texture)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.CharacterHandle.Value))
            {
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Props.Set((byte)slot, (byte)drawable);
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Textures.Set((byte)slot, (byte)texture);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x262B14F48D29DE80, new EntityArgument(player.CharacterHandle.Value), slot, drawable, texture, 2);

                var delta = new Delta_PedProperties();
                delta.Textures =
                    ((PedProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value])
                        .Textures;
                delta.Props =
                    ((PedProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value])
                        .Props;
                Program.ServerInstance.UpdateEntityInfo(player.CharacterHandle.Value, EntityType.Ped, delta);
            }
        }

        public void setPlayerAccessory(Client player, int slot, int drawable, int texture)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.CharacterHandle.Value))
            {
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Accessories.Set((byte)slot, new Tuple<byte, byte>((byte)drawable, (byte) texture));
                Program.ServerInstance.SendNativeCallToAllPlayers(0x93376B65A266EB5F, new EntityArgument(player.CharacterHandle.Value), slot, drawable, texture, true);

                var delta = new Delta_PedProperties();
                delta.Accessories =
                    ((PedProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value])
                        .Accessories;
                Program.ServerInstance.UpdateEntityInfo(player.CharacterHandle.Value, EntityType.Ped, delta);
            }
        }

        public void clearPlayerAccessory(Client player, int slot)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(player.CharacterHandle.Value))
            {
                ((PedProperties) Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Accessories.Remove((byte) slot);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x0943E5B8E078E76E, new EntityArgument(player.CharacterHandle.Value), slot);

                var delta = new Delta_PedProperties();
                delta.Accessories =
                    ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value])
                        .Accessories;
                Program.ServerInstance.UpdateEntityInfo(player.CharacterHandle.Value, EntityType.Ped, delta);
            }
        }

        

        public int vehicleNameToModel(string modelName)
		{
			return (from object value in Enum.GetValues(typeof (VehicleHash)) where modelName.ToLower() == ((VehicleHash) value).ToString().ToLower() select (int) ((VehicleHash) value)).FirstOrDefault();
		}

	    public int pedNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(PedHash)) where modelName.ToLower() == ((PedHash)value).ToString().ToLower() select (int)((PedHash)value)).FirstOrDefault();
		}

        public int pickupNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(PickupHash)) where modelName.ToLower() == ((PickupHash)value).ToString().ToLower() select (int)((PickupHash)value)).FirstOrDefault();
		}

        public int weaponNameToModel(string modelName)
        {
			return (from object value in Enum.GetValues(typeof(WeaponHash)) where modelName.ToLower() == ((WeaponHash)value).ToString().ToLower() select (int)((WeaponHash)value)).FirstOrDefault();
		}

        public List<Client> getAllPlayers()
        {
            return new List<Client>(Program.ServerInstance.Clients);
        }

        public List<NetHandle> getAllVehicles()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte) EntityType.Vehicle)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllObjects()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Prop)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllMarkers()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Marker)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllBlips()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Blip)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllPickups()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.Pickup)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public List<NetHandle> getAllLabels()
        {
            return
                new List<NetHandle>(
                    Program.ServerInstance.NetEntityHandler.ToDict()
                        .Where(pair => pair.Value.EntityType == (byte)EntityType.TextLabel)
                        .Select(pair => new NetHandle(pair.Key)));
        }

        public void setEntityPositionFrozen(Client player, NetHandle entity, bool frozen)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x428CA6DBD1094446, new EntityArgument(entity.Value), frozen);
        }

        public void triggerClientEventForAll(string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            packet.Arguments = Program.ServerInstance.ParseNativeArguments(args);

            Program.ServerInstance.SendToAll(packet, PacketType.ScriptEventTrigger, true, ConnectionChannel.NativeCall);
        }

        public void triggerClientEvent(Client player, string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
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
            msg.Write((int)PacketType.ChatData);
            msg.Write(data.Length);
            msg.Write(data);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void setPlayerWantedLevel(Client player, int wantedLevel)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1454F2448DE30163, wantedLevel);
        }

        public int getPlayerWantedLevel(Client player)
        {
            return fetchNativeFromPlayer<int>(player, 0x4C9296CBCD1B971E);
        }

        public int getPlayerVehicleSeat(Client player)
        {
            if (!player.IsInVehicle || player.CurrentVehicle.IsNull) return -3;
            return player.VehicleSeat;
        }

        public void sendNativeToPlayer(Client player, ulong longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, longHash, args);
        }

        public void sendNativeToAllPlayers(ulong longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(longHash, args);
        }

        public T fetchNativeFromPlayer<T>(Client player, ulong longHash, params object[] args)
        {
            var returnType = Program.ServerInstance.ParseReturnType(typeof (T));

            if (returnType == null)
            {
                throw new ArgumentException("Type \"" + typeof(T) + "\" is not a valid return type.");
            }

            return (T) Program.ServerInstance.ReturnNativeCallFromPlayer(player, longHash,
                returnType, args);
        }

        public  void givePlayerWeapon(Client player, int weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }

        public string getPlayerAddress(Client player)
        {
            return player.NetConnection.RemoteEndPoint.Address.ToString();
        }

        public void kickPlayer(Client player, string reason)
        {
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void kickPlayer(Client player)
        {
            player.NetConnection.Disconnect("You have been kicked.");
        }


        private Random r = new Random();
        public double random()
        {
            return r.NextDouble();
        }

        public void setEntityPosition(NetHandle netHandle, Vector3 newPosition)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x239A3351AC1DA385, new EntityArgument(netHandle.Value), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0);
	        if (doesEntityExist(netHandle))
	        {
		        Program.ServerInstance.NetEntityHandler.ToDict()[netHandle.Value].Position = newPosition;

                var delta = new Delta_EntityProperties();
	            delta.Position = newPosition;
                Program.ServerInstance.UpdateEntityInfo(netHandle.Value, EntityType.Prop, delta);
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
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1913FE4CBF41C463,
                new EntityArgument(player.CharacterHandle.Value), 32, !seatbelt);
        }

        public bool getPlayerSeatbelt(Client player)
        {
            return !fetchNativeFromPlayer<bool>(player, 0x7EE53118C892B513, new EntityArgument(player.CharacterHandle.Value), 32, true);
        }

        public void freezePlayer(Client player, bool freeze)
        {
            if (player.IsInVehicle)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x428CA6DBD1094446, new EntityArgument(player.CurrentVehicle.Value), freeze);
            }
            else
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x428CA6DBD1094446, new EntityArgument(player.CharacterHandle.Value), freeze);
            }
        }

	    public void setEntityRotation(NetHandle netHandle, Vector3 newRotation)
	    {
			Program.ServerInstance.SendNativeCallToAllPlayers(0x8524A8B0171D5E07, new EntityArgument(netHandle.Value), newRotation.X, newRotation.Y, newRotation.Z, 2, 1);
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
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF75B0D629E1C063D, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), seat);

            player.IsInVehicle = true;
            player.CurrentVehicle = vehicle;
        }

        public void warpPlayerOutOfVehicle(Client player, NetHandle vehicle)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xD3DBCE61A490BE02, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), 16);
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

            Program.ServerInstance.SendNativeCallToAllPlayers(0x45F6D8EEF34ABEF1, vehicle, health);
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

        public void repairVehicle(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health = 1000f;
                var delta = new Delta_VehicleProperties();
                delta.Health = 1000f;
                Program.ServerInstance.UpdateEntityInfo(vehicle.Value, EntityType.Vehicle, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(0x115722B1B9C14C1C, vehicle);
            Program.ServerInstance.SendNativeCallToAllPlayers(0x953DA1E1B12C0491, vehicle);
        }

        public void setPlayerHealth(Client player, float health)
        {
            var normalized = health/100f;
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), (int)(normalized*200) + 100);
        }

        public float getPlayerHealth(Client player)
        {
            return player.Health;
        }

        public void setPlayerArmor(Client player, int armor)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xCEA04D83135264CC, new LocalPlayerArgument(), armor);
        }

        public int getPlayerArmor(Client player)
        {
            return player.Armor;
        }

        public void setBlipColor(NetHandle blip, int color)
        {
            if (doesEntityExist(blip))
            {
                ((BlipProperties) Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Color = color;
                var delta = new Delta_BlipProperties();
                delta.Color = color;
                Program.ServerInstance.UpdateEntityInfo(blip.Value, EntityType.Blip, delta);
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(0x03D7FB09E75D6B7E, blip, color);
        }

        public int getBlipColor(NetHandle blip)
        {
            if (doesEntityExist(blip))
            {
                return ((BlipProperties)Program.ServerInstance.NetEntityHandler.ToDict()[blip.Value]).Color;
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
            Program.ServerInstance.SendNativeCallToAllPlayers(0xBE8BE4FE60E27B72, blip, range);
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
            Program.ServerInstance.SendNativeCallToAllPlayers(0xAE2AF67E9D9AF65D, blip, newPos.X, newPos.Y, newPos.Z);
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
            Program.ServerInstance.SendNativeCallToAllPlayers(0xDF735600A4696DAF, blip, sprite);
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
            Program.ServerInstance.SendNativeCallToAllPlayers(0xD38744167B2FA257, blip, scale);
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
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
            }
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
        }

        public void sendNotificationToAll(string message, bool flashing = false)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
            }
            Program.ServerInstance.SendNativeCallToAllPlayers(0xF020C96915705B3A, flashing, true);
        }
        
        public  void sendPictureNotificationToPlayer(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF020C96915705B3A, false, true);
        }
        
        public  void sendPictureNotificationToAll(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }

        public  void getPlayerHealth(Client player, Action<object> callback, string salt = "salt")
        {
            Program.ServerInstance.GetNativeCallFromPlayer(player, salt,
                0xEEF059FAD016D209, new IntArgument(), callback, new LocalPlayerArgument());
        }

        public  void toggleNightVisionForPlayer(Client player, bool status)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x18F621F7A5B1F85D, status);
        }

        public  void toggleNightVisionForAll(Client player, bool status)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x18F621F7A5B1F85D, status);
        }

        public  void isNightVisionActive(Client player, Action<object> callback, string salt = "salt")
        {
            Program.ServerInstance.GetNativeCallFromPlayer(player, salt, 0x2202A3F42C8E5F79, new BooleanArgument(), callback, new LocalPlayerArgument());
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

        public void setMarkerPosition(NetHandle marker, Vector3 position)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Position = position;

                var delta = new Delta_MarkerProperties();
                delta.Position = position;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
        }

        public void setMarkerRotation(NetHandle marker, Vector3 rotation)
        {
            if (doesEntityExist(marker))
            {
                ((MarkerProperties)Program.ServerInstance.NetEntityHandler.ToDict()[marker.Value]).Rotation = rotation;

                var delta = new Delta_MarkerProperties();
                delta.Rotation = rotation;
                Program.ServerInstance.UpdateEntityInfo(marker.Value, EntityType.Marker, delta);
            }
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

        public NetHandle getPlayerVehicle(Client player)
        {
            if (player.IsInVehicle)
            {
                return player.CurrentVehicle;
            }
            else
            {
                return new NetHandle(0);
            }
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

        public NetHandle createVehicle(VehicleHash model, Vector3 pos, Vector3 rot, int color1, int color2, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateVehicle((int)model, pos, rot, color1, color2, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createObject(int model, Vector3 pos, Vector3 rot, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createObject(int model, Vector3 pos, Quaternion rot, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createBlip(Vector3 pos, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(pos, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createBlip(NetHandle entity)
        {
            if (entity.IsNull || !entity.Exists()) throw new ArgumentNullException(nameof(entity));
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(entity));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createPickup(PickupHash pickupHash, Vector3 pos, Vector3 rot, int amount, uint respawnTime, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreatePickup((int)pickupHash, pos, rot, amount, respawnTime, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public void respawnPickup(NetHandle pickup)
        {
            var pic = Program.ServerInstance.NetEntityHandler.NetToProp<PickupProperties>(pickup.Value);
            if (pic != null && pic.PickedUp)
            {
                Program.ServerInstance.PickupManager.RespawnPickup(pickup.Value, pic);
            }
        }

        public NetHandle createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha,
            int r, int g, int b, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateMarker(markerType, pos, dir, rot, scale, alpha, r, g, b, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        public NetHandle createTextLabel(string text, Vector3 pos, float range, float size, bool entitySeethrough = false, int dimension = 0)
        {
            var ent = new NetHandle(Program.ServerInstance.NetEntityHandler.CreateTextLabel(text, size, range, 255, 255, 255, pos, entitySeethrough, dimension));
            lock (ResourceEntities) ResourceEntities.Add(ent);
            return ent;
        }

        private void deleteEntityInternal(NetHandle netHandle)
        {
            Program.ServerInstance.NetEntityHandler.DeleteEntity(netHandle.Value);
        }

        public void deleteEntity(NetHandle netHandle)
        {
            deleteEntityInternal(netHandle);
            lock (ResourceEntities) ResourceEntities.Remove(netHandle);
        }

#endregion
    }
}