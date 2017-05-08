using System.Collections.Generic;
using System.Linq;
using GTANetwork.Javascript;
using GTANetwork.Util;
using GTANetworkShared;

namespace GTANetwork
{
    internal partial class Main
    {
        public static void UpdateEntityInfo(int netId, EntityType entity, Delta_EntityProperties newInfo)
        {
            var packet = new UpdateEntity();
            packet.EntityType = (byte)entity;
            packet.Properties = newInfo;
            packet.NetHandle = netId;
            SendToServer(packet, PacketType.UpdateEntityProperties, true, ConnectionChannel.NativeCall);
        }

        public static bool SetEntityProperty(LocalHandle entity, string key, object value)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var item = NetEntityHandler.NetToStreamedItem(handle);
            var prop = item as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return false;

            if (prop.SyncedProperties == null) prop.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            NativeArgument oldValue = prop.SyncedProperties.Get(key);

            prop.SyncedProperties.Set(key, nativeArg);


            if (!item.LocalOnly)
            {
                var delta = new Delta_EntityProperties();
                delta.SyncedProperties = new Dictionary<string, NativeArgument>();
                delta.SyncedProperties.Add(key, nativeArg);
                UpdateEntityInfo(handle, EntityType.Prop, delta);
            }

            JavascriptHook.InvokeDataChangeEvent(entity, key, DecodeArgumentListPure(oldValue).FirstOrDefault());
            return true;
        }

        public static void ResetEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var item = NetEntityHandler.NetToStreamedItem(handle);
            var prop = item as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return;

            prop.SyncedProperties.Remove(key);

            if (!item.LocalOnly)
            {
                var delta = new Delta_EntityProperties();
                delta.SyncedProperties = new Dictionary<string, NativeArgument>();
                delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
                UpdateEntityInfo(handle, EntityType.Prop, delta);
            }
        }

        public static bool HasEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key) || prop.SyncedProperties == null) return false;

            return prop.SyncedProperties.ContainsKey(key);
        }

        public static object GetEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return null;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return null;

            var natArg = prop.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public static string[] GetEntityAllProperties(LocalHandle entity)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null) return new string[0];

            if (prop.SyncedProperties == null || prop.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            //return prop.SyncedProperties.Select(pair => DecodeArgumentListPure(pair.Value).Single().ToString()).ToArray(); //Returns all the values
            return prop.SyncedProperties.Select(pair => pair.Key).ToArray();
        }

        public static bool SetWorldData(string key, object value)
        {
            if (NetEntityHandler.ServerWorld.SyncedProperties == null) NetEntityHandler.ServerWorld.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            NativeArgument oldValue = NetEntityHandler.ServerWorld.SyncedProperties.Get(key);

            NetEntityHandler.ServerWorld.SyncedProperties.Set(key, nativeArg);


            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, nativeArg);
            UpdateEntityInfo(1, EntityType.Prop, delta);

            JavascriptHook.InvokeDataChangeEvent(new LocalHandle(0), key, DecodeArgumentListPure(oldValue).FirstOrDefault());
            return true;
        }

        public static void ResetWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld.SyncedProperties == null || !NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key)) return;

            NetEntityHandler.ServerWorld.SyncedProperties.Remove(key);

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
            UpdateEntityInfo(1, EntityType.Prop, delta);
        }

        public static bool HasWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld == null || string.IsNullOrEmpty(key) || NetEntityHandler.ServerWorld.SyncedProperties == null) return false;

            return NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key);
        }

        public static object GetWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld == null || string.IsNullOrEmpty(key)) return null;

            if (NetEntityHandler.ServerWorld.SyncedProperties == null || !NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key)) return null;

            var natArg = NetEntityHandler.ServerWorld.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public static string[] GetAllWorldData()
        {
            if (NetEntityHandler.ServerWorld == null) return new string[0];

            if (NetEntityHandler.ServerWorld.SyncedProperties == null || NetEntityHandler.ServerWorld.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            return NetEntityHandler.ServerWorld.SyncedProperties.Select(pair => pair.Key).ToArray();
        }
    }
}
