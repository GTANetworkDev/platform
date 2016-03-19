using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public class NetEntityHandler
    {
        public NetEntityHandler()
        {
            HandleMap = new BiDictionary<int, int>();
            Blips = new List<int>();
            Markers = new Dictionary<int, MarkerProperties>();
            Pickups = new List<int>();
            _localMarkers = new Dictionary<int, MarkerProperties>();
        }

        public void DrawMarkers()
        {
            lock (Markers)
            {
                foreach (var marker in Markers)
                {
                    World.DrawMarker((MarkerType) marker.Value.MarkerType, marker.Value.Position.ToVector(),
                        marker.Value.Direction.ToVector(), marker.Value.Rotation.ToVector(),
                        marker.Value.Scale.ToVector(),
                        Color.FromArgb(marker.Value.Alpha, marker.Value.Red, marker.Value.Green, marker.Value.Blue));
                }
            }

            lock (_localMarkers)
            {
                foreach (var marker in _localMarkers)
                {
                    World.DrawMarker((MarkerType)marker.Value.MarkerType, marker.Value.Position.ToVector(),
                        marker.Value.Direction.ToVector(), marker.Value.Rotation.ToVector(),
                        marker.Value.Scale.ToVector(),
                        Color.FromArgb(marker.Value.Alpha, marker.Value.Red, marker.Value.Green, marker.Value.Blue));
                }
            }
        }

        private BiDictionary<int, int> HandleMap;
        public List<int> Blips { get; set; }
        public List<int> Pickups { get; set; } 
        public Dictionary<int, MarkerProperties> Markers { get; set; }
        private Dictionary<int, MarkerProperties> _localMarkers { get; set; }
        private int _markerCount;

        public int CreateLocalMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b)
        {
            var newId = ++_markerCount;
            _localMarkers.Add(newId, new MarkerProperties()
            {
                MarkerType = markerType,
                Position = pos.ToLVector(),
                Direction = dir.ToLVector(),
                Rotation = rot.ToLVector(),
                Scale = scale.ToLVector(),
                Alpha = (byte)alpha,
                Red = r,
                Green = g,
                Blue = b,
                EntityType = (byte)EntityType.Marker,
            });
            return newId;
        }
        

        public void DeleteLocalMarker(int handle)
        {
            if (_localMarkers.ContainsKey(handle)) _localMarkers.Remove(handle);
        }

        public Entity NetToEntity(int netId)
        {
            lock (HandleMap)
            {
                if (HandleMap.ContainsKey(netId))
                {
                    if (HandleMap[netId] == -2) return Game.Player.Character;
                    return new Prop(HandleMap[netId]);
                }
            }
            return null;
        }

        public bool IsBlip(int localHandle)
        {
            return Blips.Contains(localHandle);
        }

        public bool IsPickup(int localHandle)
        {
            return Pickups.Contains(localHandle);
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
            DownloadManager.Log("CREATING VEHICLE FOR NETHASH " + netHash);
            model.Request(10000);
            DownloadManager.Log("LOAD COMPLETE. AVAILABLE: " + model.IsLoaded);

            var veh = World.CreateVehicle(model, position, rotation.Z);
            DownloadManager.Log("VEHICLE CREATED. NULL? " + (veh == null));
            veh.Rotation = rotation;
            veh.IsInvincible = true;
            DownloadManager.Log("PROPERTIES SET");
            lock (HandleMap)
            {
                if (!HandleMap.Reverse.ContainsKey(veh.Handle))
                {
                    HandleMap.Reverse.Add(veh.Handle, netHash);
                }
            }
            DownloadManager.Log("DISCARDING MODEL");
            model.MarkAsNoLongerNeeded();
            DownloadManager.Log("CREATEVEHICLE COMPLETE");
            return veh;
        }

        public Prop CreateObject(Model model, Vector3 position, Vector3 rotation, bool dynamic, int netHash)
        {
            if (model == null)
            {
                DownloadManager.Log("Model was null?");
                return null;
            }

            int counter = 0;
            var sc = new Scaleform(0);
            sc.Load("instructional_buttons");
            sc.CallFunction("CLEAR_ALL");
            sc.CallFunction("TOGGLE_MOUSE_BUTTONS", 0);
            sc.CallFunction("CREATE_CONTAINER");
            sc.CallFunction("SET_DATA_SLOT", 0, "b_50", "Loading Model");
            sc.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
            while (!model.IsLoaded && counter < 500)
            {
                model.Request();
                Script.Yield();
                counter++;
                sc.Render2D();
            }

            var veh = World.CreateProp(model, position, rotation, dynamic, false);
            veh.Rotation = rotation;
            veh.Position = position;
            veh.LodDistance = 3000;
            
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

        public void CreateMarker(int type, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, GTANetworkShared.Vector3 dir, GTANetworkShared.Vector3 scale, int r, int g, int b, int a,
            int netHandle)
        {
            if (!Markers.ContainsKey(netHandle))
            {
                Markers.Add(netHandle, new MarkerProperties()
                {
                    MarkerType = type,
                    Position = position,
                    Rotation =  rotation,
                    Direction = dir,
                    Scale = scale,
                    Red = r,
                    Green = g,
                    Blue = b,
                    Alpha = (byte)a,
                });
            }
        }

        public int CreatePickup(Vector3 pos, Vector3 rot, int pickupHash, int amount, int netHandle)
        {
            var newPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, pickupHash, pos.X, pos.Y, pos.Z, rot.X, rot.Y, rot.Z, 512, amount, 0, true, 0);
            lock (HandleMap) HandleMap.Add(netHandle, newPickup);
            lock (Pickups) Pickups.Add(newPickup);
            var start = 0;
            while (Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup) == -1 && start < 20)
            {
                start++;
                Script.Yield();
            }

            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).FreezePosition = true;
            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).IsPersistent = true;

            return newPickup;
        }

        public void ClearAll()
        {
            lock (HandleMap)
            {
                foreach (var pair in HandleMap)
                {
                    if (Blips.Contains(pair.Value))
                        new Blip(pair.Value).Remove();
                    else if(Pickups.Contains(pair.Value))
                        Function.Call(Hash.REMOVE_PICKUP, pair.Value);
                    else
                        new Prop(pair.Value).Delete();
                }

                HandleMap.Clear();
                Markers.Clear();
                Blips.Clear();
                Pickups.Clear();
                _localMarkers.Clear();
                _markerCount = 0;
            }

        }
    }
}