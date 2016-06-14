using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetworkServer
{
    public class CancelEventArgs
    {
        public bool Cancel { get; set; }

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
        #endregion

        #region Delegates
        public delegate void ChatEvent(Client sender, string message, CancelEventArgs cancel);
        public delegate void PlayerEvent(Client player);
        public delegate void PlayerDisconnectedEvent(Client player, string reason);
        public delegate void PlayerKilledEvent(Client player, NetHandle entityKiller, int weapon);
        public delegate void ServerEventTrigger(Client sender, string eventName, params object[] arguments);
        public delegate void PickupEvent(Client pickupee, NetHandle pickupHandle);
        #endregion

        #region Events
        public event EventHandler onResourceStart;
        public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;
        public event PlayerEvent OnPlayerBeginConnect;
        public event PlayerEvent onPlayerConnected;
        public event PlayerEvent onPlayerFinishedDownload;
        public event PlayerDisconnectedEvent onPlayerDisconnected;
        public event PlayerKilledEvent onPlayerDeath;
        public event PlayerEvent onPlayerRespawn;
        public event ServerEventTrigger onClientEventTrigger;
        public event PickupEvent onPlayerPickup;

        internal void invokeClientEvent(Client sender, string eventName, params object[] arsg)
        {
            onClientEventTrigger?.Invoke(sender, eventName, arsg);
        }

        internal void invokeFinishedDownload(Client sender)
        {
            onPlayerFinishedDownload?.Invoke(sender);
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

        internal bool invokeChatCommand(Client sender, string msg)
        {
            var args = new CancelEventArgs(false);
            onChatCommand?.Invoke(sender, msg, args);
            return !args.Cancel;
        }

        internal void invokePlayerBeginConnect(Client player)
        {
            OnPlayerBeginConnect?.Invoke(player);
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

        public void sleep(int ms)
        {
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
            }
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

        public void setVehicleMod(NetHandle vehicle, int modType, int mod)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(vehicle.Value))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods[modType] = mod;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x6AF0636DDEDCB6DD, new EntityArgument(vehicle.Value), modType, mod, false);
            }
        }

        public void removeVehicleMod(NetHandle vehicle, int modType)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods[modType] = -1;
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(0x92D619E420858204, vehicle, modType);
        }

        public void setPlayerSkin(Client player, int modelHash)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x00A1CADD00108836, new LocalGamePlayerArgument(), modelHash);
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
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor =
                    color;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4F1D4BE3A7F24601, vehicle, color, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor);
            }
        }

        public void setVehicleSecondaryColor(NetHandle vehicle, int color)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor =
                    color;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4F1D4BE3A7F24601, vehicle, ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).PrimaryColor, color);
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

        public int getVehicleSecondaryColor(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                return ((VehicleProperties)Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).SecondaryColor;
            }
            return 0;
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
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Props[slot] = (ushort)drawable;
                ((PedProperties)Program.ServerInstance.NetEntityHandler.ToDict()[player.CharacterHandle.Value]).Textures[slot] = (ushort)texture;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x262B14F48D29DE80, new EntityArgument(player.CharacterHandle.Value), slot, texture, 2);
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

        public void setEntityPositionFrozen(Client player, NetHandle entity, bool frozen)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x428CA6DBD1094446, new EntityArgument(entity.Value), frozen);
        }

        public void triggerClientEventForAll(string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            packet.Arguments = Program.ServerInstance.ParseNativeArguments(args);

            Program.ServerInstance.SendToAll(packet, PacketType.ScriptEventTrigger, true);
        }

        public  void triggerClientEvent(Client player, string eventName, params object[] args)
        {
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            packet.Arguments = Program.ServerInstance.ParseNativeArguments(args);

            Program.ServerInstance.SendToClient(player, packet, PacketType.ScriptEventTrigger, true);
        }

        public  void sendChatMessageToAll(string message)
        {
            sendChatMessageToAll("", message);
            
        }

        public  void sendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            Program.ServerInstance.SendToAll(chatObj, PacketType.ChatData, true);
        }

        public  void sendChatMessageToPlayer(Client player, string message)
        {
            sendChatMessageToPlayer(player, "", message);
        }

        public  void sendChatMessageToPlayer(Client player, string sender, string message)
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

        public void setEntityPosition(NetHandle netHandle, Vector3 newPosition)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x06843DA7060A026B, new EntityArgument(netHandle.Value), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
	        if (doesEntityExist(netHandle))
	        {
		        Program.ServerInstance.NetEntityHandler.ToDict()[netHandle.Value].Position = newPosition;
	        }
        }

	    public void setEntityRotation(NetHandle netHandle, Vector3 newRotation)
	    {
			Program.ServerInstance.SendNativeCallToAllPlayers(0x8524A8B0171D5E07, new EntityArgument(netHandle.Value), newRotation.X, newRotation.Y, newRotation.Z, 2, 1);
			if (doesEntityExist(netHandle))
			{
				Program.ServerInstance.NetEntityHandler.ToDict()[netHandle.Value].Rotation = newRotation;
			}
		}

        public Vector3 getEntityPosition(NetHandle entity)
        {
            if (doesEntityExist(entity))
            {
                return Program.ServerInstance.NetEntityHandler.ToDict()[entity.Value].Position;
            }
            return null;
        }

        public void setPlayerIntoVehicle(Client player, NetHandle vehicle, int seat)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF75B0D629E1C063D, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), seat);
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

        public void repairVehicle(NetHandle vehicle)
        {
            if (doesEntityExist(vehicle))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Health = 1000f;
            }

            Program.ServerInstance.SendNativeCallToAllPlayers(0x115722B1B9C14C1C, vehicle);
        }

        public  void setPlayerHealth(Client player, int health)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
        }

        public int getPlayerHealth(Client player)
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
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                Program.ServerInstance.SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
            }
        }

        public void sendNotificationToAll(string message, bool flashing = false)
        {
            for (int i = 0; i < message.Length; i += 99)
            {
                Program.ServerInstance.SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
                Program.ServerInstance.SendNativeCallToAllPlayers(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                Program.ServerInstance.SendNativeCallToAllPlayers(0xF020C96915705B3A, flashing, true);
            }
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

        public NetHandle createVehicle(int model, Vector3 pos, Vector3 rot, int color1, int color2)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreateVehicle(model, pos, rot, color1, color2));
        }

        public NetHandle createObject(int model, Vector3 pos, Vector3 rot)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot));
        }

        public NetHandle createBlip(Vector3 pos)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(pos));
        }

        public NetHandle createBlip(NetHandle entity)
        {
            if (entity.IsNull || !entity.Exists()) throw new ArgumentNullException(nameof(entity));
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreateBlip(entity));
        }

        public NetHandle createPickup(int pickupHash, Vector3 pos, Vector3 rot, int amount)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreatePickup(pickupHash, pos, rot, amount));
        }

        public NetHandle createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha,
            int r, int g, int b)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreateMarker(markerType, pos, dir, rot, scale, alpha, r, g, b));
        }

        public void deleteEntity(NetHandle netHandle)
        {
            Program.ServerInstance.NetEntityHandler.DeleteEntity(netHandle.Value);
        }

#endregion
    }
}