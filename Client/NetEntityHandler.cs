using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;

namespace MTAV
{
    public class NetEntityHandler
    {
        public NetEntityHandler()
        {
            HandleMap = new BiDictionary<int, int>();
            Blips = new List<int>();
        }

        private BiDictionary<int, int> HandleMap;
        private List<int> Blips { get; set; }

        public Entity NetToEntity(int netId)
        {
            lock (HandleMap)
            {
                if (HandleMap.ContainsKey(netId))
                {
                    return new Prop(HandleMap[netId]);
                }
            }
            return null;
        }

        public bool ContainsNethandle(int netHandle)
        {
            lock (HandleMap)
            return HandleMap.ContainsKey(netHandle);
        }

        public bool ContainsLocalHandle(int localHandle)
        {
            lock (HandleMap)
            return HandleMap.Reverse.ContainsKey(localHandle);
        }

        public int EntityToNet(int entityHandle)
        {
            lock (HandleMap)
                if (HandleMap.Reverse.ContainsKey(entityHandle))
                    return HandleMap.Reverse[entityHandle];
            return 0;
        }

        public void RemoveByNetHandle(int netHandle)
        {
            lock (HandleMap) HandleMap.Remove(netHandle);
        }

        public void RemoveByLocalHandle(int localHandle)
        {
            lock (HandleMap) HandleMap.Reverse.Remove(localHandle);
        }

        public void AddEntity(int netHandle, int localHandle)
        {
            lock (HandleMap) HandleMap.Add(netHandle, localHandle);
        }

        public void SetEntity(int netHandle, int localHandle)
        {
            lock (HandleMap)
            {
                if (HandleMap.ContainsKey(netHandle))
                    HandleMap[netHandle] = localHandle;
                else
                    HandleMap.Add(netHandle, localHandle);
            }
        }

        public Vehicle CreateVehicle(Model model, Vector3 position, Vector3 rotation, int netHash)
        {
            if (model == null) return null;

            model.Request(10000);


            var veh = World.CreateVehicle(model, position, rotation.Z);
            veh.Rotation = rotation;
            veh.IsInvincible = true;
            lock (HandleMap)
            {
                if (!HandleMap.Reverse.ContainsKey(veh.Handle))
                {
                    HandleMap.Reverse.Add(veh.Handle, netHash);
                }
            } 
            return veh;
        }

        public Prop CreateObject(Model model, Vector3 position, Vector3 rotation, bool dynamic, int netHash)
        {
            if (model == null)
            {
                DownloadManager.Log("Model was null?");
                return null;
            }

            DownloadManager.Log("Requesting...");
            model.Request(10000);
            DownloadManager.Log("Available: " + model.IsLoaded);

            DownloadManager.Log("Pos: " + position);
            DownloadManager.Log("NetHash: " + netHash);

            var veh = World.CreateProp(model, position, rotation, dynamic, false);
            DownloadManager.Log("Prop null: " + (veh == null) + " exists: " + veh?.Exists());
            veh.Rotation = rotation;
            veh.Position = position;
            if (!dynamic)
                veh.FreezePosition = true;

            lock (HandleMap)
                if (!HandleMap.Reverse.ContainsKey(veh.Handle))
                    HandleMap.Reverse.Add(veh.Handle, netHash);

            model.MarkAsNoLongerNeeded();
            return veh;
        }

        public Blip CreateBlip(Vector3 pos, int netHandle)
        {
            var blip = World.CreateBlip(pos);
            lock (HandleMap) HandleMap.Add(netHandle, blip.Handle);
            lock (Blips) Blips.Add(blip.Handle);
            return blip;
        }
        
        public void ClearAll()
        {
            lock (HandleMap)
            {
                foreach (var pair in HandleMap)
                {
                    if (Blips.Contains(pair.Value))
                        new Blip(pair.Value).Remove();
                    else
                        new Prop(pair.Value).Delete();
                }

                HandleMap.Clear();
            }
        }
    }
}