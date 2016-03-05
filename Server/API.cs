using System;
using System.Collections.Generic;
using System.Linq;
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
            var ourResource =
                Program.ServerInstance.RunningResources.FirstOrDefault(k => k.DirectoryName == resourceName);
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

        public void setVehicleMod(NetHandle vehicle, int modType, int mod)
        {
            if (Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(vehicle.Value))
            {
                ((VehicleProperties) Program.ServerInstance.NetEntityHandler.ToDict()[vehicle.Value]).Mods[modType] = mod;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x6AF0636DDEDCB6DD, new EntityArgument(vehicle.Value), modType, mod, false);
            }
        }

        public int vehicleNameToModel(string modelName)
        {
            VehicleHash output;
            if (!Enum.TryParse(modelName, out output))
            {
                return 0;
            }
            return (int) output;
        }

        public int pedNameToModel(string modelName)
        {
            PedHash output;
            if (!Enum.TryParse(modelName, out output))
            {
                return 0;
            }
            return (int)output;
        }

        public int pickupNameToModel(string modelName)
        {
            PickupHash output;
            if (!Enum.TryParse(modelName, out output))
            {
                return 0;
            }
            return (int)output;
        }

        public int weaponNameToModel(string modelName)
        {
            WeaponHash output;
            if (!Enum.TryParse(modelName, out output))
            {
                return 0;
            }
            return (int)output;
        }

        public List<Client> getAllPlayers()
        {
            return Program.ServerInstance.Clients;
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

        public void sendNativeToPlayer(Client player, string longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, ulong.Parse(longHash), args);
        }

        public void sendNativeToAllPlayers(string longHash, params object[] args)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(ulong.Parse(longHash), args);
        }

        public  void givePlayerWeapon(Client player, int weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }

        public  void kickPlayer(Client player, string reason)
        {
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void setEntityPosition(NetHandle netHandle, Vector3 newPosition)
        {
            Program.ServerInstance.SendNativeCallToAllPlayers(0x06843DA7060A026B, new EntityArgument(netHandle.Value), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
        }

        public Vector3 getPlayerPosition(Client player)
        {
            /*Program.ServerInstance.GetNativeCallFromPlayer(player,
                salt,
                0x3FEF770D40960D5A, new Vector3Argument(), callback, new LocalPlayerArgument(), 0);*/
            return player.Position;
        }

        public void setPlayerIntoVehicle(Client player, NetHandle vehicle, int seat)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF75B0D629E1C063D, new LocalPlayerArgument(), new EntityArgument(vehicle.Value), seat);
        }

        public  void setPlayerHealth(Client player, int health)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
        }

        public  void sendNotificationToPlayer(Client player, string message, bool flashing = false)
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

        public NetHandle createPickup(int pickupHash, Vector3 pos, Vector3 rot, int amount)
        {
            return new NetHandle(Program.ServerInstance.NetEntityHandler.CreatePickup(pickupHash, pos, rot, amount));
        }

        public void deleteEntity(NetHandle netHandle)
        {
            Program.ServerInstance.NetEntityHandler.DeleteEntity(netHandle.Value);
        }

#endregion
    }
}