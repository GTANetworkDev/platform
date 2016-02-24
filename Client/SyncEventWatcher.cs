using GTA;
using GTA.Native;
using MultiTheftAutoShared;

namespace MTAV
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
            }
            _lastCar = car;

            if (player.IsInVehicle())
            {
                var lg = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, car);
                if (lg != _lastLandingGear)
                {
                    SendSyncEvent(SyncEventType.LandingGearChange, Main.NetEntityHandler.EntityToNet(car.Handle), lg);
                }
                _lastLandingGear = lg;

                for (int i = 0; i < _doors.Length; i++)
                {
                    bool isOpen = false;
                    if ((isOpen = (Function.Call<float>(Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, car.Handle, i) > 0.5f)) != _doors[i])
                    {
                        SendSyncEvent(SyncEventType.DoorStateChange, Main.NetEntityHandler.EntityToNet(car.Handle), i, isOpen);
                    }
                    _doors[i] = isOpen;
                }

                if (car.HighBeamsOn != _highBeams)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, Main.NetEntityHandler.EntityToNet(car.Handle), (int)Lights.Highbeams, car.HighBeamsOn);
                }
                _highBeams = car.HighBeamsOn;

                if (car.LightsOn != _lights)
                {
                    SendSyncEvent(SyncEventType.BooleanLights, Main.NetEntityHandler.EntityToNet(car.Handle), (int)Lights.NormalLights, car.LightsOn);
                }
                _lights = car.LightsOn;


            }
        }
    }
}