using GTANetworkServer.Constant;
using GTANetworkShared;

namespace GTANetworkServer
{
    public class Vehicle : Entity
    {
        internal Vehicle(ServerAPI father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public int primaryColor
        {
            get { return Base.getVehiclePrimaryColor(this); }
            set { Base.setVehiclePrimaryColor(this, value); }
        }

        public int secondaryColor
        {
            get { return Base.getVehicleSecondaryColor(this); }
            set { Base.setVehicleSecondaryColor(this, value); }
        }

        public Color customPrimaryColor
        {
            get { return Base.getVehicleCustomPrimaryColor(this); }
            set { Base.setVehicleCustomPrimaryColor(this, value.red, value.green, value.blue); }
        }

        public Color customSecondaryColor
        {
            get { return Base.getVehicleCustomSecondaryColor(this); }
            set { Base.setVehicleCustomSecondaryColor(this, value.red, value.green, value.blue); }
        }

        public float health
        {
            get { return Base.getVehicleHealth(this); }
            set { Base.setVehicleHealth(this, value); }
        }

        public int livery
        {
            get { return Base.getVehicleLivery(this); }
            set { Base.setVehicleLivery(this, value); }
        }

        public Vehicle trailer
        {
            get { return new Vehicle(Base, Base.getVehicleTrailer(this)); }
        }

        public Vehicle traileredBy
        {
            get { return new Vehicle(Base, Base.getVehicleTraileredBy(this)); }
        }

        public bool siren
        {
            get { return Base.getVehicleSirenState(this); }
        }

        public string numberPlate
        {
            get { return Base.getVehicleNumberPlate(this); }
            set { Base.setVehicleNumberPlate(this, value); }
        }

        public bool specialLight
        {
            get { return Base.getVehicleSpecialLightStatus(this); }
            set { Base.setVehicleSpecialLightStatus(this, value); }
        }

        public bool bulletproofTyres
        {
            get { return Base.getVehicleBulletproofTyres(this); }
            set { Base.setVehicleBulletproofTyres(this, value); }
        }

        public int numberPlateStyle
        {
            get { return Base.getVehicleNumberPlateStyle(this); }
            set { Base.setVehicleNumberPlateStyle(this, value); }
        }

        public int pearlescentColor
        {
            get { return Base.getVehiclePearlescentColor(this); }
            set { Base.setVehiclePearlescentColor(this, value); }
        }

        public int wheelColor
        {
            get { return Base.getVehicleWheelColor(this); }
            set { Base.setVehicleWheelColor(this, value); }
        }

        public int wheelType
        {
            get { return Base.getVehicleWheelType(this); }
            set { Base.setVehicleWheelType(this, value); }
        }

        public bool engineStatus
        {
            get { return Base.getVehicleEngineStatus(this); }
            set { Base.setVehicleEngineStatus(this, value); }
        }

        public Color tyreSmokeColor
        {
            get { return Base.getVehicleTyreSmokeColor(this); }
            set { Base.setVehicleTyreSmokeColor(this, value.red, value.green, value.blue); }
        }

        public Color modColor1
        {
            get { return Base.getVehicleModColor1(this); }
            set { Base.setVehicleModColor1(this, value.red, value.green, value.blue); }
        }

        public Color modColor2
        {
            get { return Base.getVehicleModColor2(this); }
            set { Base.setVehicleModColor2(this, value.red, value.green, value.blue); }
        }

        public int windowTint
        {
            get { return Base.getVehicleWindowTint(this); }
            set { Base.setVehicleWindowTint(this, value); }
        }

        public float enginePowerMultiplier
        {
            get { return Base.getVehicleEnginePowerMultiplier(this); }
            set { Base.setVehicleEnginePowerMultiplier(this, value); }
        }

        public float engineTorqueMultiplier
        {
            get { return Base.getVehicleEngineTorqueMultiplier(this); }
            set { Base.setVehicleEngineTorqueMultiplier(this, value); }
        }

        public Color neonColor
        {
            get { return Base.getVehicleNeonColor(this); }
            set { Base.setVehicleNeonColor(this, value.red, value.green, value.blue); }
        }

        public int dashboardColor
        {
            get { return Base.getVehicleDashboardColor(this); }
            set { Base.setVehicleDashboardColor(this, value); }
        }

        public int trimColor
        {
            get { return Base.getVehicleTrimColor(this); }
            set { Base.setVehicleTrimColor(this, value); }
        }

        public string displayName
        {
            get { return Base.getVehicleDisplayName((VehicleHash)model); }
        }

        public Client[] occupants
        {
            get { return Base.getVehicleOccupants(this); }
        }
        public float maxOccupants
        {
            get { return Base.getVehicleMaxOccupants((VehicleHash)model); }
        }

        public float maxSpeed
        {
            get { return Base.getVehicleMaxSpeed((VehicleHash)model); }
        }

        public float maxAcceleration
        {
            get { return Base.getVehicleMaxAcceleration((VehicleHash)model); }
        }

        public float maxTraction
        {
            get { return Base.getVehicleMaxTraction((VehicleHash)model); }
        }

        public float maxBraking
        {
            get { return Base.getVehicleMaxBraking((VehicleHash)model); }
        }

        public bool locked
        {
            get { return Base.getVehicleLocked(this); }
            set { Base.setVehicleLocked(this, value); }
        }

        public int Class
        {
            get { return Base.getVehicleClass((VehicleHash)model); }
        }

        public int ClassName
        {
            get { return Base.getVehicleClass((VehicleHash)model); }
        }

        #endregion

        #region Methods

        public void repair()
        {
            Base.repairVehicle(this);
        }

        public void popTyre(int tyre)
        {
            Base.popVehicleTyre(this, tyre, true);
        }

        public void fixTyre(int tyre)
        {
            Base.popVehicleTyre(this, tyre, false);
        }

        public bool isTyrePopped(int tyre)
        {
            return Base.isVehicleTyrePopped(this, tyre);
        }

        public void breakDoor(int door)
        {
            Base.breakVehicleDoor(this, door, true);
        }

        public void fixDoor(int door)
        {
            Base.breakVehicleDoor(this, door, false);
        }

        public bool isDoorBroken(int door)
        {
            return Base.isVehicleDoorBroken(this, door);
        }

        public void openDoor(int door)
        {
            Base.setVehicleDoorState(this, door, true);
        }

        public void closeDoor(int door)
        {
            Base.setVehicleDoorState(this, door, false);
        }

        public bool isDoorOpen(int door)
        {
            return Base.getVehicleDoorState(this, door);
        }

        public void breakWindow(int window)
        {
            Base.breakVehicleWindow(this, window, true);
        }

        public void fixWindow(int window)
        {
            Base.breakVehicleWindow(this, window, false);
        }

        public bool isWindowBroken(int window)
        {
            return Base.isVehicleWindowBroken(this, window);
        }

        public void setExtra(int extra, bool enabled)
        {
            Base.setVehicleExtra(this, extra, enabled);
        }

        public bool getExtra(int extra)
        {
            return Base.getVehicleExtra(this, extra);
        }

        public void setMod(int slot, int mod)
        {
            Base.setVehicleMod(this, slot, mod);
        }

        public int getMod(int slot)
        {
            return Base.getVehicleMod(this, slot);
        }

        public void removeMod(int slot)
        {
            Base.removeVehicleMod(this, slot);
        }

        public void setNeons(int slot, bool turnedon)
        {
            Base.setVehicleNeonState(this, slot, turnedon);
        }

        public bool getNeons(int slot)
        {
            return Base.getVehicleNeonState(this, slot);
        }
        #endregion
    }
}