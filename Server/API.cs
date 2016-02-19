using System;
using System.ComponentModel;
using Lidgren.Network;

namespace GTAServer
{
    public class API
    {
        #region Delegates
        public delegate void ChatEvent(Client sender, string message, CancelEventArgs cancel);
        public delegate void PlayerEvent(Client player);
        public delegate void ServerEventTrigger(string eventName, params object[] arguments);
        #endregion

        #region Events
        public event EventHandler onResourceStart;
        //public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;
        public event PlayerEvent OnPlayerBeginConnect;
        public event PlayerEvent onPlayerConnected;
        public event PlayerEvent onPlayerDisconnected;
        public event PlayerEvent onPlayerDeath;
        public event ServerEventTrigger onClientEventTrigger;

        internal void invokeClientEvent(string eventName, params object[] arsg)
        {
            onClientEventTrigger?.Invoke(eventName, arsg);
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
            //onResourceStop?.Invoke(this, EventArgs.Empty);
        }

        internal bool invokeChatMessage(Client sender, string msg)
        {
            var args = new CancelEventArgs(false);
            onChatMessage?.Invoke(sender, msg, args);
            return args.Cancel;
        }

        internal bool invokeChatCommand(Client sender, string msg)
        {
            var args = new CancelEventArgs(false);
            onChatCommand?.Invoke(sender, msg, args);
            return args.Cancel;
        }

        internal void invokePlayerBeginConnect(Client player)
        {
            OnPlayerBeginConnect?.Invoke(player);
        }

        internal void invokePlayerConnected(Client player)
        {
            onPlayerConnected?.Invoke(player);
        }

        internal void invokePlayerDisconnected(Client player)
        {
            onPlayerDisconnected?.Invoke(player);
        }

        internal void invokePlayerDeath(Client player)
        {
            onPlayerDeath?.Invoke(player);
        }

        #endregion

        #region Functions

        public int vehicleNameToModel(string modelName)
        {
            VehicleHash output;
            if (!Enum.TryParse(modelName, out output))
            {
                return 0;
            }
            return (int) output;
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

        public  void givePlayerWeapon(Client player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }

        public  void kickPlayer(Client player, string reason)
        {
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void setEntityPosition(int netHandle, Vector3 newPosition)
        {
            //Program.ServerInstance.SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
            Program.ServerInstance.SendNativeCallToAllPlayers(0x06843DA7060A026B, new EntityArgument(netHandle), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
        }

        public  void getPlayerPosition(Client player, Action<object> callback, string salt = "salt")
        {
            Program.ServerInstance.GetNativeCallFromPlayer(player,
                salt,
                0x3FEF770D40960D5A, new Vector3Argument(), callback, new LocalPlayerArgument(), 0);
        }

        public void setPlayerIntoVehicle(Client player, int vehicle, int seat)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF75B0D629E1C063D, new LocalPlayerArgument(), new EntityArgument(vehicle), seat);
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

        public  void sendPictureNotificationToPlayer(Client player, string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF020C96915705B3A, false, true);
        }


        public  void sendPictureNotificationToPlayer(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0xF020C96915705B3A, false, true);
        }

        public  void sendPictureNotificationToAll(Client player, string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            Program.ServerInstance.SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            Program.ServerInstance.SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            Program.ServerInstance.SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            Program.ServerInstance.SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }

        public  void sendPictureNotificationToAll(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
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

        public  int createVehicle(int model, Vector3 pos, Vector3 rot, int color1, int color2)
        {
            return Program.ServerInstance.NetEntityHandler.CreateVehicle(model, pos, rot, color1, color2);
        }

        public  int createObject(int model, Vector3 pos, Vector3 rot, bool dyn)
        {
            return Program.ServerInstance.NetEntityHandler.CreateProp(model, pos, rot, dyn);
        }

        public  void deleteEntity(int netHandle)
        {
            Program.ServerInstance.NetEntityHandler.DeleteEntity(netHandle);
        }

#endregion
    }
}