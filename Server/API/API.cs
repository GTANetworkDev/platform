using GTANetworkShared;

namespace GTANetworkServer
{
    public class API : ServerAPI
    {
        public static ServerAPI shared
        {
            get { return Program.ServerInstance.PublicAPI; }
        }

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
        public delegate void PlayerAmmoEvent(Client player, WeaponHash weapon, int oldValue);
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
        public event PlayerAmmoEvent onPlayerWeaponAmmoChange;
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
            onPlayerWeaponSwitch?.Invoke(entity, (WeaponHash)oldValue);
        }

        internal void invokePlayerWeaponAmmoChange(Client entity, int weapon, int oldValue)
        {
            onPlayerWeaponAmmoChange?.Invoke(entity, (WeaponHash)weapon, oldValue);
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
    }
}
