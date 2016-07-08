using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public class Streamer
    {
        public Streamer()
        {
            ClientMap = new List<IStreamedItem>();
        }

        private int _localHandleCounter = 0;

        public void DrawMarkers()
        {
            lock (ClientMap)
            {
                foreach (var marker in ClientMap.Where(item => item is RemoteMarker).Cast<RemoteMarker>())
                {
                    World.DrawMarker((MarkerType)marker.MarkerType, marker.Position.ToVector(),
                        marker.Direction.ToVector(), marker.Rotation.ToVector(),
                        marker.Scale.ToVector(),
                        Color.FromArgb(marker.Alpha, marker.Red, marker.Green, marker.Blue));
                }
            }
        }

        private List<IStreamedItem> ClientMap;

        public int CreateLocalMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b)
        {
            var newId = ++_localHandleCounter;
            ClientMap.Add(new RemoteMarker()
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
                LocalOnly = true,
                RemoteHandle = newId,
            });
            return newId;
        }


        public void DeleteLocalMarker(int handle)
        {
            ClientMap.RemoveAll(item => item is RemoteMarker &&
                                ((RemoteMarker)item).LocalOnly &&
                                ((RemoteMarker)item).RemoteHandle == handle);
        }

        public IStreamedItem NetToStreamedItem(int netId, bool local = false)
        {
            lock (ClientMap)
            {
                return ClientMap.FirstOrDefault(item => item.RemoteHandle == netId && item.LocalOnly == local);
            }
        }

        public Entity NetToEntity(int netId)
        {
            lock (ClientMap)
            {
                var streamedItem = ClientMap.FirstOrDefault(item => item.RemoteHandle == netId && !item.LocalOnly && item.StreamedIn);
                var handleable = streamedItem as ILocalHandleable;
                if (streamedItem == null || handleable == null) return null;
                if (handleable.LocalHandle == -2) return Game.Player.Character;
                return new Prop(handleable.LocalHandle);
             }
        }

        public bool IsBlip(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) is RemoteBlip;
        }

        public bool IsPickup(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) is RemotePickup;
        }

        public bool ContainsNethandle(int netHandle)
        {
            return NetToStreamedItem(netHandle) != null;
        }

        public bool ContainsLocalHandle(int localHandle)
        {
            return NetToStreamedItem(localHandle, true) != null;
        }

        public int EntityToNet(int entityHandle)
        {
            lock (ClientMap)
            {
                return (ClientMap.FirstOrDefault(item => 
                        !item.LocalOnly && item.StreamedIn && item as ILocalHandleable != null && 
                        ((ILocalHandleable)item).LocalHandle == entityHandle) as ILocalHandleable)
                        ?.LocalHandle ?? 0;
            }
        }

        public void RemoveByNetHandle(int netHandle)
        {
            lock (ClientMap) ClientMap.Remove(NetToStreamedItem(netHandle));
        }

        public void RemoveByLocalHandle(int localHandle)
        {
            lock (ClientMap) ClientMap.Remove(NetToStreamedItem(localHandle));
        }
        

        public RemoteVehicle CreateVehicle(int model, Vector3 position, Vector3 rotation, int netHash)
        {
            RemoteVehicle rem;
            lock (ClientMap)
            {
                ClientMap.Add(rem = new RemoteVehicle()
                {
                    RemoteHandle = netHash,
                    ModelHash = model,
                    Position = position.ToLVector(),
                    Rotation = rotation.ToLVector(),
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteProp CreateObject(int model, Vector3 position, Vector3 rotation, bool dynamic, int netHash)
        {
            RemoteProp rem;
            lock (ClientMap)
            {
                ClientMap.Add(rem = new RemoteProp()
                {
                    RemoteHandle = netHash,
                    ModelHash = model,
                    EntityType = 2,
                    Position = position.ToLVector(),
                    Rotation = rotation.ToLVector(),
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteBlip CreateBlip(Vector3 pos, int netHandle)
        {
            RemoteBlip rem;
            lock (ClientMap)
            {
                ClientMap.Add(rem = new RemoteBlip()
                {
                    RemoteHandle = netHandle,
                    Position = pos.ToLVector(),
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public RemoteBlip CreateBlip(IStreamedItem entity, int netHandle)
        {
            RemoteBlip rem;
            lock (ClientMap)
            {
                ClientMap.Add(rem = new RemoteBlip()
                {
                    RemoteHandle = netHandle,
                    AttachedNetEntity = entity.RemoteHandle,
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public void CreateMarker(int type, GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation, GTANetworkShared.Vector3 dir, GTANetworkShared.Vector3 scale, int r, int g, int b, int a,
            int netHandle)
        {
            lock (ClientMap)
            {
                ClientMap.Add(new RemoteMarker()
                {
                    MarkerType = type,
                    Position = position,
                    Rotation = rotation,
                    Direction = dir,
                    Scale = scale,
                    Red = r,
                    Green = g,
                    Blue = b,
                    Alpha = (byte)a,
                    RemoteHandle = netHandle,
                });
            }
        }

        public RemotePickup CreatePickup(Vector3 pos, Vector3 rot, int pickupHash, int amount, int netHandle)
        {
            RemotePickup rem;
            lock (ClientMap)
            {
                ClientMap.Add(rem = new RemotePickup()
                {
                    RemoteHandle = netHandle,
                    Position = pos.ToLVector(),
                    Rotation = rot.ToLVector(),
                    ModelHash = pickupHash,
                    Amount = amount,
                    StreamedIn = false,
                    LocalOnly = false,
                });
            }
            return rem;
        }

        public void StreamIn(IStreamedItem item)
        {
            if (item.StreamedIn) return;

            switch ((EntityType)item.EntityType)
            {
                case EntityType.Vehicle:
                    StreamInVehicle((RemoteVehicle)item);
                    break;
                case EntityType.Prop:
                    StreamInProp((RemoteProp)item);
                    break;
                case EntityType.Pickup:
                    StreamInPickup((RemotePickup)item);
                    break;
            }
        }

        public void StreamOut(IStreamedItem item)
        {
            if (!item.StreamedIn) return;

            switch ((EntityType) item.EntityType)
            {
                case EntityType.Prop:
                case EntityType.Vehicle:
                    StreamOutEntity((ILocalHandleable) item);
                    break;
                case EntityType.Blip:
                    StreamOutBlip((ILocalHandleable) item);
                    break;
                case EntityType.Pickup:
                    StreamOutPickup((ILocalHandleable) item);
                    break;
            }

            item.StreamedIn = false;
        }

        private void StreamOutEntity(ILocalHandleable data)
        {
            new Prop(data.LocalHandle).Delete();
        }

        private void StreamOutBlip(ILocalHandleable blip)
        {
            new Blip(blip.LocalHandle).Remove();
        }

        private void StreamOutPickup(ILocalHandleable pickup)
        {
            Function.Call(Hash.REMOVE_PICKUP, pickup.LocalHandle);
        }

        private void StreamInVehicle(RemoteVehicle data)
        {
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage) return;
            LogManager.DebugLog("CREATING VEHICLE FOR NETHANDLE " + data.RemoteHandle);
            if (!model.IsLoaded) model.Request(10000);
            LogManager.DebugLog("LOAD COMPLETE. AVAILABLE: " + model.IsLoaded);

            var veh = World.CreateVehicle(model, data.Position.ToVector(), data.Rotation.Z);
            LogManager.DebugLog("VEHICLE CREATED. NULL? " + (veh == null));
            veh.Rotation = data.Rotation.ToVector();
            veh.IsInvincible = true;
            data.LocalHandle = veh.Handle;
            veh.Livery = data.Livery;

            if ((data.PrimaryColor & 0xFF000000) > 0)
                veh.CustomPrimaryColor = Color.FromArgb(data.PrimaryColor);
            else
                veh.PrimaryColor = (VehicleColor)data.PrimaryColor;

            if ((data.SecondaryColor & 0xFF000000) > 0)
                veh.CustomSecondaryColor = Color.FromArgb(data.SecondaryColor);
            else
                veh.SecondaryColor = (VehicleColor)data.SecondaryColor;



            veh.PearlescentColor = (VehicleColor)0;
            veh.RimColor = (VehicleColor)0;
            veh.EngineHealth = data.Health;
            veh.SirenActive = data.Siren;
            veh.NumberPlate = data.NumberPlate;
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, veh, 0, 0);

            for (int i = 0; i < data.Doors.Length; i++)
            {
                if (data.Doors[i])
                    veh.OpenDoor((VehicleDoor)i, false, true);
                else veh.CloseDoor((VehicleDoor)i, true);
            }

            for (int i = 0; i < data.Tires.Length; i++)
            {
                if (data.Tires[i])
                {
                    veh.IsInvincible = false;
                    veh.BurstTire(i);
                }
            }

            if (data.Trailer != 0)
            {
                var trailerId = NetToStreamedItem(data.Trailer);
                if (trailerId != null)
                {
                    StreamIn(trailerId);
                    var trailer = new Vehicle(((RemoteVehicle)trailerId).LocalHandle);

                    if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                                        (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    {
                        Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, veh, trailer, true, 0, 0, 0);
                    }
                    else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                             (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    {
                        veh.DropCargobobHook(CargobobHook.Hook);
                        Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, veh, 0, 0, 0, 0);
                    }
                    else
                    {
                        Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, veh, trailer, 4f);
                    }
                }
            }

            LogManager.DebugLog("PROPERTIES SET");
            data.StreamedIn = true;
            LogManager.DebugLog("DISCARDING MODEL");
            model.MarkAsNoLongerNeeded();
            LogManager.DebugLog("CREATEVEHICLE COMPLETE");
        }
    

        private void StreamInProp(RemoteProp data)
        {
            var model = new Model(data.ModelHash);
            if (model == null || !model.IsValid || !model.IsInCdImage) return;
            LogManager.DebugLog("CREATING VEHICLE FOR NETHANDLE " + data.RemoteHandle);
            if (!model.IsLoaded) model.Request(10000);
            LogManager.DebugLog("LOAD COMPLETE. AVAILABLE: " + model.IsLoaded);
            var ourVeh = new Prop(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, model.Hash, data.Position.X, data.Position.Y, data.Position.Z, false, true, false));
            ourVeh.Rotation = data.Rotation.ToVector();
            ourVeh.Alpha = (int)data.Alpha;

            data.StreamedIn = true;
            data.LocalHandle = ourVeh.Handle;
        }

        private void StreamInPickup(RemotePickup pickup)
        {
            var newPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, pickup.ModelHash,
                pickup.Position.X, pickup.Position.Y, pickup.Position.Z,
                pickup.Rotation.X, pickup.Rotation.Y, pickup.Rotation.Z,
                512, pickup.Amount, 0, true, 0);

            var start = 0;
            while (Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup) == -1 && start < 20)
            {
                start++;
                Script.Yield();
            }

            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).FreezePosition = true;
            new Prop(Function.Call<int>(Hash.GET_PICKUP_OBJECT, newPickup)).IsPersistent = true;

            pickup.StreamedIn = true;
            pickup.LocalHandle = newPickup;
        }

        public void ClearAll()
        {
            LogManager.DebugLog("STARTING CLEARALL");

            lock (ClientMap)
            {
                LogManager.DebugLog("HANDLEMAP LOCKED");
                LogManager.DebugLog("HANDLEMAP SIZE: " + ClientMap.Count);

                foreach (var pair in ClientMap)
                {
                    if (!pair.StreamedIn) continue;

                    StreamOut(pair);                    
                }

                LogManager.DebugLog("CLEARING LISTS");
                ClientMap.Clear();
                _localHandleCounter = 0;
            }

        }
    }
}