using GTA;
using GTA.Native;
using GTANetworkShared;
using System.Linq;

namespace GTANetwork
{
    public class SyncEventWatcher
    {
        private Main _instance;

        public SyncEventWatcher(Main parent)
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

        public static Vehicle GetVehicleTrailerVehicle(Vehicle tanker)
        {
            if (!Function.Call<bool>(Hash.IS_VEHICLE_ATTACHED_TO_TRAILER, tanker))
                return null;
            var trailerArg = new OutputArgument();
            Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, tanker, trailerArg);
            return trailerArg.GetResult<Vehicle>();
        }

        public static Vehicle GetVehicleTowtruckVehicle(Vehicle tanker)
        {
            return Function.Call<Vehicle>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, tanker);
        }

        public static Vehicle GetVehicleCargobobVehicle(Vehicle tanker)
        {
            return Function.Call<Vehicle>(Hash.GET_VEHICLE_ATTACHED_TO_CARGOBOB, tanker);
        }

        private void SendSyncEvent(SyncEventType type, params object[] args)
        {
            var convertedArgs = Main.ParseNativeArguments(args);

            var obj = new SyncEvent();
            obj.EventType = (byte) type;
            obj.Arguments = convertedArgs;
            
            Main.SendToServer(obj, PacketType.SyncEvent, false, ConnectionChannel.SyncEvent);
        }

        public void Tick()
        {
            var player = Game.Player.Character;
            var car = Game.Player.Character.CurrentVehicle;

            foreach (var pickup in Main.NetEntityHandler.ClientMap.Where(item => item is RemotePickup).Cast<RemotePickup>())
            {
                if (!pickup.StreamedIn || !Function.Call<bool>(Hash.DOES_PICKUP_EXIST, pickup.LocalHandle)) continue;
                if (!player.IsInRangeOf(Function.Call<GTA.Math.Vector3>(Hash.GET_PICKUP_COORDS, pickup.LocalHandle), 20f)) continue;
                if (Function.Call<int>(Hash.GET_PICKUP_OBJECT, pickup.LocalHandle) == -1)
                {
                    Function.Call(Hash.REMOVE_PICKUP, pickup.LocalHandle);
                    SendSyncEvent(SyncEventType.PickupPickedUp, pickup.RemoteHandle);
                }
            }

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

            if (player.IsInVehicle() && Util.GetResponsiblePed(player.CurrentVehicle).Handle == Game.Player.Character.Handle)
            {
                int carNetHandle = Main.NetEntityHandler.EntityToNet(car.Handle);

                var lg = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, car);
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

                if (car.HighBeamsOn != _highBeams)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, carNetHandle, (int)Lights.Highbeams, car.HighBeamsOn);
                }
                _highBeams = car.HighBeamsOn;

                if (car.LightsOn != _lights)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, carNetHandle, (int)Lights.NormalLights, car.LightsOn);
                }
                _lights = car.LightsOn;

                Vehicle trailer;

                if ((VehicleHash) car.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash) car.Model.Hash == VehicleHash.TowTruck2)
                    trailer = GetVehicleTowtruckVehicle(car);
                else if ((VehicleHash) car.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash) car.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                    trailer = GetVehicleCargobobVehicle(car);
                else trailer = GetVehicleTrailerVehicle(car);

                if (_lastTrailer != trailer)
                {
                    if (trailer == null)
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        {
                            SendSyncEvent(SyncEventType.TrailerDeTach, false, carNetHandle);

                            ((RemoteVehicle) Main.NetEntityHandler.NetToStreamedItem(carNetHandle)).Trailer = 0;
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
                    }
                    _tires[i] = isBusted;
                }


                var newStation = (int) Game.RadioStation;

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