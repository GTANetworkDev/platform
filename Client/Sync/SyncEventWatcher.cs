using System;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Util;
using GTANetworkShared;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Streamer
{
    internal class SyncEventWatcher
    {
        private Main _instance;

        internal SyncEventWatcher(Main parent)
        {
            _instance = parent;
        }

        private int _lastLandingGear;
        private Vehicle _lastCar;

        private bool[] _doors = new bool[7];
        private bool[] _tires = new bool[8];

        private bool _lights;
        private bool _highBeams;
        private int _radioStation;

        private Vehicle _lastTrailer;

        internal static Vehicle GetVehicleTrailerVehicle(Vehicle tanker)
        {
            if (!Function.Call<bool>(Hash.IS_VEHICLE_ATTACHED_TO_TRAILER, tanker))
                return null;
            var trailerArg = new OutputArgument();
            Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, tanker, trailerArg);
            return trailerArg.GetResult<Vehicle>();
        }

        internal static Vehicle GetVehicleTowtruckVehicle(Vehicle tanker)
        {
            return Function.Call<Vehicle>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, tanker);
        }

        internal static Vehicle GetVehicleCargobobVehicle(Vehicle tanker)
        {
            return Function.Call<Vehicle>(Hash.GET_VEHICLE_ATTACHED_TO_CARGOBOB, tanker);
        }

        internal static void SendSyncEvent(SyncEventType type, params object[] args)
        {
            var convertedArgs = Main.ParseNativeArguments(args);

            var obj = new SyncEvent();
            obj.EventType = (byte)type;
            obj.Arguments = convertedArgs;

            Main.SendToServer(obj, PacketType.SyncEvent, false, ConnectionChannel.SyncEvent);
        }

        private int _lastCheck;
        internal void Tick()
        {
            if (!Main.IsOnServer()) return;

            var player = Game.Player.Character;
            var car = player.CurrentVehicle;
            foreach (var pickup in Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemotePickup).Cast<RemotePickup>())
            {
                if (!pickup.StreamedIn || !Function.Call<bool>(Hash.DOES_PICKUP_EXIST, pickup.LocalHandle)) continue;
                if (!player.IsInRangeOfEx(Function.Call<GTA.Math.Vector3>(Hash.GET_PICKUP_COORDS, pickup.LocalHandle), 20f)) continue;

                var obj = Function.Call<int>(Hash.GET_PICKUP_OBJECT, pickup.LocalHandle);

                if (obj == -1)
                {
                    if (PickupToWeapon.Translate(pickup.ModelHash) != 0)
                    {
                        CrossReference.EntryPoint.WeaponInventoryManager.Allow((GTANetworkShared.WeaponHash)PickupToWeapon.Translate(pickup.ModelHash));
                    }

                    JavascriptHook.InvokeCustomEvent(
                        api => api?.invokeonPlayerPickup(new LocalHandle(pickup.RemoteHandle, HandleType.NetHandle)));
                    Function.Call(Hash.REMOVE_PICKUP, pickup.LocalHandle);
                    SendSyncEvent(SyncEventType.PickupPickedUp, pickup.RemoteHandle);
                }
                else if ((pickup.Flag & (byte)EntityFlag.Collisionless) != 0)
                {
                    new Prop(obj).IsCollisionEnabled = false;
                }
                else if ((pickup.Flag & (byte)EntityFlag.Collisionless) == 0 && !new Prop(obj).IsCollisionEnabled)
                {
                    new Prop(obj).IsCollisionEnabled = true;
                }
            }

            if (Environment.TickCount - _lastCheck < 1000) return;
            _lastCheck = Environment.TickCount;

            if (car != _lastCar)
            {
                _lastLandingGear = 0;
                for (int i = 0; i < _doors.Length; i++)
                {
                    _doors[i] = false;
                }
                for (int i = 0; i < _tires.Length; i++)
                {
                    _tires[i] = false;
                }
                _highBeams = false;
                _lights = true;
                _lastTrailer = null;
                _radioStation = 0;
            }
            _lastCar = car;

            if (player.IsInVehicle() && Util.Util.GetResponsiblePed(player.CurrentVehicle, player).Handle == 0)
            {
                int carNetHandle = Main.NetEntityHandler.EntityToNet(car.Handle);
                var lg = Function.Call<int>(Hash.GET_LANDING_GEAR_STATE, car);
                if (lg != _lastLandingGear)
                {
                    SendSyncEvent(SyncEventType.LandingGearChange, carNetHandle, lg);
                }
                _lastLandingGear = lg;
                for (int i = 0; i < _doors.Length; i++)
                {
                    bool isOpen = false;
                    if ((isOpen = (Function.Call<float>(Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, car.Handle, i) > 0.5f)) != _doors[i])
                    {
                        SendSyncEvent(SyncEventType.DoorStateChange, carNetHandle, i, isOpen);
                    }
                    _doors[i] = isOpen;
                }

                //Fixed the synchronization of optics in the transport
                if (car.AreHighBeamsOn != _highBeams)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, carNetHandle, (int)Lights.Highbeams, car.AreHighBeamsOn);
                }
                _highBeams = car.AreHighBeamsOn;

                if (car.AreLightsOn != _lights)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, carNetHandle, (int)Lights.NormalLights, car.AreLightsOn);
                }
                _lights = car.AreLightsOn;
                /////////////////////////////////

                Vehicle trailer;
                switch ((VehicleHash)car.Model.Hash)
                {
                    case VehicleHash.TowTruck:
                    case VehicleHash.TowTruck2:
                        trailer = GetVehicleTowtruckVehicle(car);
                        break;
                    case VehicleHash.Cargobob:
                    case VehicleHash.Cargobob2:
                    case VehicleHash.Cargobob3:
                    case VehicleHash.Cargobob4:
                        trailer = GetVehicleCargobobVehicle(car);
                        break;
                    default:
                        trailer = GetVehicleTrailerVehicle(car);
                        break;
                }
                if (_lastTrailer != trailer)
                {
                    if (trailer == null)
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        {
                            SendSyncEvent(SyncEventType.TrailerDeTach, false, carNetHandle);

                            ((RemoteVehicle)Main.NetEntityHandler.NetToStreamedItem(carNetHandle)).Trailer = 0;
                            if (_lastTrailer != null)
                            {
                                var trailerH = (RemoteVehicle)Main.NetEntityHandler.EntityToStreamedItem(_lastTrailer.Handle);
                                trailerH.TraileredBy = 0;
                            }
                        }
                    }
                    else
                    {
                        if (Main.NetEntityHandler.EntityToNet(trailer.Handle) != 0)
                        {
                            SendSyncEvent(SyncEventType.TrailerDeTach, true, carNetHandle,
                            Main.NetEntityHandler.EntityToNet(trailer.Handle));

                            var trailerH = (RemoteVehicle)Main.NetEntityHandler.EntityToStreamedItem(trailer.Handle);
                            trailerH.TraileredBy = carNetHandle;
                        }
                    }
                }
                _lastTrailer = trailer;

                for (int i = 0; i < _tires.Length; i++)
                {
                    bool isBusted = false;
                    if ((isBusted = car.IsTireBurst(i)) != _tires[i])
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                            SendSyncEvent(SyncEventType.TireBurst, Main.NetEntityHandler.EntityToNet(car.Handle), i, isBusted);

                        var lI = i;
                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleTyreBurst(lI));
                    }
                    _tires[i] = isBusted;
                }

                var newStation = (int)Game.RadioStation;

                if (newStation != _radioStation)
                {
                    if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        SendSyncEvent(SyncEventType.RadioChange, Main.NetEntityHandler.EntityToNet(car.Handle), newStation);
                }

                _radioStation = newStation;
            }
        }
    }
}