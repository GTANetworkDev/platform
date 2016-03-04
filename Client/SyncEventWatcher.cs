using GTA;
using GTA.Native;
using GTANetworkShared;

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
        
        private bool _lights;
        private bool _highBeams;

        private Vehicle _lastTrailer;

        private Vehicle GetVehicleTrailerVehicle(Vehicle tanker)
        {
            if (!Function.Call<bool>(Hash.IS_VEHICLE_ATTACHED_TO_TRAILER, tanker))
                return null;
            var trailerArg = new OutputArgument();
            Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, tanker, trailerArg);
            return trailerArg.GetResult<Vehicle>();
        }

        private void SendSyncEvent(SyncEventType type, params object[] args)
        {
            var convertedArgs = Main.ParseNativeArguments(args);

            var obj = new SyncEvent();
            obj.EventType = (byte) type;
            obj.Arguments = convertedArgs;
            
            Main.SendToServer(obj, PacketType.SyncEvent, false, 30);
        }

        public void Tick()
        {
            var player = Game.Player.Character;
            var car = Game.Player.Character.CurrentVehicle;

            if (car != _lastCar)
            {
                _lastLandingGear = 0;
                for (int i = 0; i < _doors.Length; i++)
                {
                    _doors[i] = false;
                }
                
                _highBeams = false;
                _lights = true;
                _lastTrailer = null;
            }
            _lastCar = car;

            if (player.IsInVehicle() && SyncPed.GetResponsiblePed(player.CurrentVehicle).Handle == Game.Player.Character.Handle)
            {
                var lg = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, car);
                if (lg != _lastLandingGear)
                {
                    if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        SendSyncEvent(SyncEventType.LandingGearChange, Main.NetEntityHandler.EntityToNet(car.Handle), lg);
                }
                _lastLandingGear = lg;

                for (int i = 0; i < _doors.Length; i++)
                {
                    bool isOpen = false;
                    if ((isOpen = (Function.Call<float>(Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, car.Handle, i) > 0.5f)) != _doors[i])
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                            SendSyncEvent(SyncEventType.DoorStateChange, Main.NetEntityHandler.EntityToNet(car.Handle), i, isOpen);
                    }
                    _doors[i] = isOpen;
                }

                if (car.HighBeamsOn != _highBeams)
                {
                    if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        SendSyncEvent(SyncEventType.BooleanLights, Main.NetEntityHandler.EntityToNet(car.Handle), (int)Lights.Highbeams, car.HighBeamsOn);
                }
                _highBeams = car.HighBeamsOn;

                if (car.LightsOn != _lights)
                {
                    if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                        SendSyncEvent(SyncEventType.BooleanLights, Main.NetEntityHandler.EntityToNet(car.Handle), (int)Lights.NormalLights, car.LightsOn);
                }
                _lights = car.LightsOn;

                var trailer = GetVehicleTrailerVehicle(car);

                if (_lastTrailer != trailer)
                {
                    if (trailer == null)
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0)
                            SendSyncEvent(SyncEventType.TrailerDeTach, false, Main.NetEntityHandler.EntityToNet(car.Handle));
                    }
                    else
                    {
                        if (Main.NetEntityHandler.EntityToNet(car.Handle) != 0 && Main.NetEntityHandler.EntityToNet(trailer.Handle) != 0)
                        SendSyncEvent(SyncEventType.TrailerDeTach, true, Main.NetEntityHandler.EntityToNet(car.Handle),
                            Main.NetEntityHandler.EntityToNet(trailer.Handle));
                    }
                }

                _lastTrailer = trailer;

            }
        }
    }
}