using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace MTAV
{
    public enum SynchronizationMode
    {
        Dynamic,
        EntityLerping,
        DeadReckoning,
        Experimental,
        Teleport
    }

    public class SyncPed
    {
        public SynchronizationMode SyncMode;
        public long Host;
        public Ped Character;
        public Vector3 _position;
        public int VehicleNetHandle;
        public Vector3 _rotation;
        public bool IsInVehicle;
        public bool IsJumping;
        public int ModelHash;
        public int CurrentWeapon;
        public bool IsShooting;
        public bool IsAiming;
        public Vector3 AimCoords;
        public float Latency;
        public bool IsHornPressed;
        public Vehicle MainVehicle { get; set; }

        public int VehicleSeat;
        public int PedHealth;

        public int VehicleHealth;
        public int VehicleHash;
        public Vector3 _vehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public string Name;
        public bool Siren;

        public bool Debug;

        private DateTime _stopTime;
        public float Speed
        {
            get { return _speed; }
            set
            {
                _lastSpeed = _speed;
                _speed = value;
            }
        }

        public bool IsParachuteOpen;

        public double AverageLatency
        {
            get { return _latencyAverager.Count == 0 ? 0 : _latencyAverager.Average(); }
        }

        public DateTime LastUpdateReceived
        {
            get { return _lastUpdateReceived; }
            set
            {
                if (_lastUpdateReceived != new DateTime())
                {
                    _latencyAverager.Enqueue(value.Subtract(_lastUpdateReceived).TotalMilliseconds);
                    if (_latencyAverager.Count >= 10)
                        _latencyAverager.Dequeue();
                }

                _lastUpdateReceived = value;
            }
        }

        public Dictionary<int, int> VehicleMods
        {
            get { return _vehicleMods; }
            set
            {
                if (value == null) return;
                _vehicleMods = value;
            }
        }

        public Dictionary<int, int> PedProps
        {
            get { return _pedProps; }
            set
            {
                if (value == null) return;
                _pedProps = value;
            }
        }

        private Vector3 _lastVehiclePos;
        private Vector3 _carPosOnUpdate;
        public Vector3 VehiclePosition
        {
            get { return _vehiclePosition; }
            set
            {
                _lastVehiclePos = _vehiclePosition;
                _vehiclePosition = value;

                if (MainVehicle != null)
                    _carPosOnUpdate = MainVehicle.Position;
            }
        }

        private Vector3 _lastVehVel;
        public Vector3 VehicleVelocity
        {
            get { return _vehicleVelocity; }
            set
            {
                _lastVehVel = _vehicleVelocity;
                _vehicleVelocity = value; 
            }
        }

        private Vector3 _lastPosition;
        public Vector3 Position
        {
            get { return _position; }
            set
            {
                _lastPosition = _position;
                _position = value;
            }
        }

        private Vector3 _lastVehicleRotation;
        public Vector3 VehicleRotation
        {
            get { return _vehicleRotation; }
            set
            {
                _lastVehicleRotation = _vehicleRotation;
                _vehicleRotation = value;
            }
        }

        private Vector3 _lastRotation;
        public Vector3 Rotation
        {
            get { return _rotation; }
            set
            {
                _lastRotation = _rotation;
                _rotation = value; 
            }
        }

        private bool _lastVehicle;
        private uint _switch;
        private bool _lastAiming;
        private float _lastSpeed;
        private bool _lastShooting;
        private bool _lastJumping;
        private bool _blip;
        private bool _justEnteredVeh;
        private DateTime _lastHornPress = DateTime.Now;
        private int _relGroup;
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private Queue<double> _latencyAverager;

        private int _playerSeat;
        private bool _isStreamedIn;
        private Blip _mainBlip;
        private bool _lastHorn;
        private Prop _parachuteProp;

        //public SyncPed(int hash, Vector3 pos, Quaternion rot, bool blip = true)
        public SyncPed(int hash, Vector3 pos, Vector3 rot, bool blip = true)
        {
            _position = pos;
            _rotation = rot;
            ModelHash = hash;
            _blip = blip;

            _latencyAverager = new Queue<double>();

            _relGroup = World.AddRelationshipGroup("SYNCPED");
            World.SetRelationshipBetweenGroups(Relationship.Neutral, _relGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Neutral, Game.Player.Character.RelationshipGroup, _relGroup);
        }

        public void SetBlipNameFromTextFile(Blip blip, string text)
        {
            Function.Call(Hash._0xF9113A30DE5C6670, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0xBC38B49BCB83BC9B, blip);
        }

        private int _modSwitch = 0;
        private int _clothSwitch = 0;
        private DateTime _lastUpdateReceived;
        private float _speed;
        private Vector3 _vehicleVelocity;

        public void DisplayLocally()
        {
            const float hRange = 200f;
            var gPos = IsInVehicle ? VehiclePosition : _position;
            var inRange = Game.Player.Character.IsInRangeOf(gPos, hRange);
            
            /*
            if (inRange && !_isStreamedIn)
            {
                _isStreamedIn = true;
                if (_mainBlip != null)
                {
                    _mainBlip.Remove();
                    _mainBlip = null;
                }
            }
            else if(!inRange && _isStreamedIn)
            {
                Clear();
                _isStreamedIn = false;
            }


            if (!inRange)
            {
                if (_mainBlip == null && _blip)
                {
                    _mainBlip = World.CreateBlip(gPos);
                    _mainBlip.Color = BlipColor.White;
                    _mainBlip.Scale = 0.8f;
                    SetBlipNameFromTextFile(_mainBlip, Name == null ? "<nameless>" : Name);
                }
                if(_blip && _mainBlip != null)
                    _mainBlip.Position = gPos;
                return;
            }
            */
            
            if (Character == null || !Character.Exists() || !Character.IsInRangeOf(gPos, hRange) || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
            {
                if (Character != null) Character.Delete();

                Character = World.CreatePed(new Model(ModelHash), gPos, _rotation.Z);
                                if (Character == null) return;

                Character.BlockPermanentEvents = true;
                Character.IsInvincible = true;
                Character.CanRagdoll = false;
                Character.RelationshipGroup = _relGroup;
                if (_blip)
                {
                    Character.AddBlip();
                    if (Character.CurrentBlip == null) return;
                    Character.CurrentBlip.Color = BlipColor.White;
                    Character.CurrentBlip.Scale = 0.8f;
                    SetBlipNameFromTextFile(Character.CurrentBlip, Name);
                }
                return;
            }

            if (!Character.IsOccluded && Character.IsInRangeOf(Game.Player.Character.Position, 20f))
            {
                var oldPos = UI.WorldToScreen(Character.Position + new Vector3(0, 0, 1.2f));
                var targetPos = Character.Position + new Vector3(0, 0, 1.2f);
                if (oldPos.X != 0 && oldPos.Y != 0)
                {
                    var res = UIMenu.GetScreenResolutionMantainRatio();
                    var pos = new Point((int)((oldPos.X / (float)UI.WIDTH) * res.Width),
                        (int)((oldPos.Y / (float)UI.HEIGHT) * res.Height));

                    Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);

                    new UIResText(Name == null ? "<nameless>" : Name, new Point(0, 0), 0.3f, Color.WhiteSmoke, Font.ChaletLondon, UIResText.Alignment.Centered)
                    {
                        Outline = true,
                    }.Draw();

                    if (Character != null)
                    {
                        new UIResRectangle(new Point(0, 0) - new Size(75, -36), new Size(150, 20), Color.FromArgb(100, 0, 0, 0)).Draw();
                        new UIResRectangle(new Point(0, 0) - new Size(71, -40),
                            new Size((int) (142*Math.Min(Math.Max(2*(PedHealth/100f), 0f), 1f)), 12),
                            Color.FromArgb(150, 50, 250, 50)).Draw();
                    }

                    Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                }
            }

            if ((!_lastVehicle && IsInVehicle && VehicleHash != 0) || (_lastVehicle && IsInVehicle && (MainVehicle == null || !Character.IsInVehicle(MainVehicle) || MainVehicle.Model.Hash != VehicleHash || Main.NetEntityHandler.EntityToNet(MainVehicle.Handle) != VehicleNetHandle || VehicleSeat != Util.GetPedSeat(Character))))
            {
                if (Debug)
                {
                    if (MainVehicle != null) MainVehicle.Delete();
                    MainVehicle = World.CreateVehicle(new Model(VehicleHash), VehiclePosition, VehicleRotation.Z);
                }
                else
                    MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);

                if (Game.Player.Character.IsInVehicle(MainVehicle) &&
                        VehicleSeat == Util.GetPedSeat(Game.Player.Character))
                {
                    Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
                    UI.Notify("~r~Car jacked!");
                }
                

                if (MainVehicle != null)
                {
                    MainVehicle.PrimaryColor = (VehicleColor)VehiclePrimaryColor;
                    MainVehicle.SecondaryColor = (VehicleColor)VehicleSecondaryColor;
                    MainVehicle.Rotation = _vehicleRotation;
                    MainVehicle.IsInvincible = true;
                    Character.Task.WarpIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);
                }

                _lastVehicle = true;
                _justEnteredVeh = true;
                _enterVehicleStarted = DateTime.Now;
                return;
            }
           
            if (_lastVehicle && _justEnteredVeh && IsInVehicle && !Character.IsInVehicle(MainVehicle) && DateTime.Now.Subtract(_enterVehicleStarted).TotalSeconds <= 4)
            {
                return;
            }
            _justEnteredVeh = false;

            if (_lastVehicle && !IsInVehicle && MainVehicle != null)
            {
                if (Character != null) Character.Task.LeaveVehicle(MainVehicle, true);
            }

            if (Character != null)
            {
                Character.Health = (int)((PedHealth/(float)100) * Character.MaxHealth);
            }

            _switch++;

            if (!inRange)
            {
                if (Character != null)
                    Character.Position = _position;
                if (MainVehicle != null)
                    MainVehicle.Position = VehiclePosition;
                return;
            }

            if (IsInVehicle)
            {
                if (VehicleSeat == (int) GTA.VehicleSeat.Driver ||
                    MainVehicle.GetPedOnSeat(GTA.VehicleSeat.Driver) == null)
                {
                    MainVehicle.Health = VehicleHealth;
                    if (MainVehicle.Health <= 0)
                    {
                        MainVehicle.IsInvincible = false;
                        //_mainVehicle.Explode();
                    }
                    else
                    {
                        MainVehicle.IsInvincible = true;
                        if (MainVehicle.IsDead)
                            MainVehicle.Repair();
                    }

                    MainVehicle.PrimaryColor = (VehicleColor) VehiclePrimaryColor;
                    MainVehicle.SecondaryColor = (VehicleColor) VehicleSecondaryColor;

                    if (VehicleMods != null && _modSwitch%50 == 0 &&
                        Game.Player.Character.IsInRangeOf(VehiclePosition, 30f))
                    {
                        var id = _modSwitch/50;

                        if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod((VehicleMod) id))
                        {
                            Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
                            MainVehicle.SetMod((VehicleMod) id, VehicleMods[id], false);
                            Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
                        }
                    }
                    _modSwitch++;

                    if (_modSwitch >= 2500)
                        _modSwitch = 0;

                    if (IsHornPressed && !_lastHorn)
                    {
                        _lastHorn = true;
                        MainVehicle.SoundHorn(99999);
                    }

                    if (!IsHornPressed && _lastHorn)
                    {
                        _lastHorn = false;
                        MainVehicle.SoundHorn(1);
                    }

                    if (MainVehicle.SirenActive && !Siren)
                        MainVehicle.SirenActive = Siren;
                    else if (!MainVehicle.SirenActive && Siren)
                        MainVehicle.SirenActive = Siren;

                    var dir = VehiclePosition - _lastVehiclePos;
                    
                    var syncMode = Main.GlobalSyncMode;
                    if (syncMode == SynchronizationMode.Dynamic)
                    {
                        if (AverageLatency > 70)
                            syncMode = SynchronizationMode.EntityLerping;
                        else
                            syncMode = SynchronizationMode.DeadReckoning;
                    }

                    if (syncMode == SynchronizationMode.DeadReckoning)
                    {
                        var vdir = VehicleVelocity - _lastVehVel;
                        var target = Util.LinearVectorLerp(VehicleVelocity, VehicleVelocity + vdir,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        var posTarget = Util.LinearVectorLerp(VehiclePosition, VehiclePosition + dir,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        if (Speed > 0.5f)
                        {
                            MainVehicle.Velocity = target + 2 * (posTarget - MainVehicle.Position);
                            _stopTime = DateTime.Now;
                            _carPosOnUpdate = MainVehicle.Position;
                        }
                        else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                        {
                            posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                            (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0, 0);
                        }
                        else
                        {
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X, VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                        }
                    }
                    else if (syncMode == SynchronizationMode.EntityLerping)
                    {
                        var target = Util.LinearVectorLerp(_lastVehVel, VehicleVelocity,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        var posTarget = Util.LinearVectorLerp(_lastVehiclePos, VehiclePosition,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        if (Speed > 0)
                        { 
                            MainVehicle.Velocity = target + 2 * (posTarget - MainVehicle.Position);
                            _stopTime = DateTime.Now;
                            _carPosOnUpdate = MainVehicle.Position;
                        }
                        else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                        {
                            posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                            (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0, 0);
                        }
                        else
                        {
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X, VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                        }
                    }
                    else if (syncMode == SynchronizationMode.Experimental)
                    {
                        var vdir = VehicleVelocity - _lastVehVel;
                        var target = Util.LinearVectorLerp(VehicleVelocity, VehicleVelocity + vdir,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        var posTarget = Util.LinearVectorLerp(VehiclePosition, VehiclePosition + dir,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        if (Speed > 0)
                            MainVehicle.Velocity = target + 2*(posTarget - MainVehicle.Position);
                        else
                        {
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0, 0);
                        }
                    }
                    else if (syncMode == SynchronizationMode.Teleport)
                    {
                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X, VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                    }

                    if (Main.LerpRotaion)
                    {
                        if ((Util.Denormalize(_lastVehicleRotation.Z) < 180f &&
                             Util.Denormalize(_vehicleRotation.Z) > 180f) ||
                            (Util.Denormalize(_lastVehicleRotation.Z) > 180f &&
                             Util.Denormalize(_vehicleRotation.Z) < 180f))
                            MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
                        else
                        {
                            var lerpedRot =
                                Util.LinearVectorLerp(_lastVehicleRotation, _vehicleRotation,
                                    (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                                    (int) AverageLatency);
                            MainVehicle.Quaternion = lerpedRot.ToQuaternion();
                        }
                    }
                    else
                    {
                        MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
                    }
                }
            }
            else
            {
                if (PedProps != null && _clothSwitch%50 == 0 && Game.Player.Character.IsInRangeOf(_position, 30f))
                {
                    var id = _clothSwitch/50;

                    if (PedProps.ContainsKey(id) &&
                        PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
                    }
                }

                _clothSwitch++;
                if (_clothSwitch >= 750)
                    _clothSwitch = 0;

                if (Character.Weapons.Current.Hash != (WeaponHash) CurrentWeapon)
                {
                    Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, 999, true, true);
                    Character.Weapons.Select(Character.Weapons[(WeaponHash)CurrentWeapon]);
                }

                if (!_lastJumping && IsJumping)
                {
                    Character.Task.Jump();
                }

                if (IsParachuteOpen)
                {

                    if (_parachuteProp == null)
                    {
                        _parachuteProp = World.CreateProp(new Model(1740193300), Character.Position,
                            Character.Rotation, false, false);
                        _parachuteProp.FreezePosition = true;
                        Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);
                    }

                    Character.FreezePosition = true;
                    
                    var target = Util.LinearVectorLerp(_lastPosition - new Vector3(0, 0, 1),
                        _position - new Vector3(0, 0, 1),
                        (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);

                    if ((Util.Denormalize(_lastRotation.Z) < 180f &&
                         Util.Denormalize(_rotation.Z) > 180f) ||
                        (Util.Denormalize(_lastRotation.Z) > 180f &&
                         Util.Denormalize(_rotation.Z) < 180f))
                        Character.Quaternion = _rotation.ToQuaternion();
                    else
                        Character.Quaternion =
                        Util.LinearVectorLerp(_lastRotation, _rotation,
                            (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency)
                            .ToQuaternion();

                    _parachuteProp.Position = Character.Position + new Vector3(0, 0, 3.7f) + Character.ForwardVector * 0.5f;
                    _parachuteProp.Quaternion = Character.Quaternion;
                    if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, "skydive@parachute@first_person", "chute_idle_right",
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict("skydive@parachute@first_person"), "chute_idle_right",
                                8f, 1f, -1, 0, -8f, 0, 0, 0);
                        }
                }
                else
                {
                    var dest = _position;
                    Character.FreezePosition = false;

                    if (_parachuteProp != null)
                    {
                        _parachuteProp.Delete();
                        _parachuteProp = null;
                    }

                    const int threshold = 50;
                    if (IsAiming && !IsShooting && !Character.IsInRangeOf(_position, 0.5f) && _switch%threshold == 0)
                    {
                        Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                            dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                            (uint) FiringPattern.FullAuto);
                    }
                    else if (IsAiming && !IsShooting && Character.IsInRangeOf(_position, 0.5f))
                    {
                        Character.Task.AimAt(AimCoords, 100);
                    }
                    /*
                    if (!Character.IsInRangeOf(_position, 0.5f) &&
                        ((IsShooting && !_lastShooting) ||
                            (IsShooting && _lastShooting && _switch%(threshold*2) == 0)))
                    {
                        Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                            dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 1, 0x3F000000, 0x40800000, 1, 0, 0,
                            (uint) FiringPattern.FullAuto);
                    }
                    else if ((IsShooting && !_lastShooting) ||
                                (IsShooting && _lastShooting && _switch%(threshold/2) == 0))
                    {

                        Function.Call(Hash.TASK_SHOOT_AT_COORD, Character.Handle, AimCoords.X, AimCoords.Y,
                            AimCoords.Z, 1500, (uint) FiringPattern.FullAuto);
                    }*/

                    if (IsShooting)
                    {
                        if (!Character.IsInRangeOf(_position, 0.5f) && _switch % threshold == 0)
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                                dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                                (uint)FiringPattern.FullAuto);
                        }
                        else if (Character.IsInRangeOf(_position, 0.5f))
                        {
                            Character.Task.AimAt(AimCoords, 100);
                        }

                        var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
                        var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, -0.01f));
                        var damage = 25;
                        Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, AimCoords.X,
                            AimCoords.Y, AimCoords.Z, damage, true, CurrentWeapon, Character, true, true, 0xbf800000);
                    }

                    if (!IsAiming && !IsShooting && !IsJumping)
                    {
                        var syncMode = Main.GlobalSyncMode;

                        if (syncMode == SynchronizationMode.Dynamic)
                        {
                            if (AverageLatency > 70)
                                syncMode = SynchronizationMode.EntityLerping;
                            else
                                syncMode = SynchronizationMode.DeadReckoning;
                        }

                        if (syncMode == SynchronizationMode.DeadReckoning)
                        {
                            var dirVector = Position - _lastPosition;

                            var target = Util.LinearVectorLerp(Position,
                                (Position) + dirVector,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);
                        }
                        else if (syncMode == SynchronizationMode.EntityLerping)
                        {
                            var target = Util.LinearVectorLerp(_lastPosition, Position,
                                (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);
                        }

                        if (Main.LerpRotaion)
                        {
                            if ((Util.Denormalize(_lastRotation.Z) < 180f &&
                                 Util.Denormalize(Rotation.Z) > 180f) ||
                                (Util.Denormalize(_lastRotation.Z) > 180f &&
                                 Util.Denormalize(Rotation.Z) < 180f))
                                Character.Rotation = Rotation;
                            else
                                Character.Rotation = Util.LinearVectorLerp(_lastRotation, Rotation,
                                    (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                                    (int) AverageLatency);
                        }
                        else
                        {
                            Character.Rotation = Rotation;
                        }

                        var ourAnim = GetMovementAnim(GetPedSpeed(Speed));

                        if (
                            !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, "move_m@generic", ourAnim,
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict("move_m@generic"), ourAnim,
                                8f, 1f, -1, 0, -8f, 0, 0, 0);
                        }
                    }
                }
                _lastJumping = IsJumping;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
            }
            _lastVehicle = IsInVehicle;
        }

        public static int GetPedSpeed(float speed)
        {
            if (speed < 0.5f)
            {
                return 0;
            }
            else if (speed >= 0.5f && speed < 4f)
            {
                return 1;
            }
            else if (speed >= 4f && speed < 6.4f)
            {
                return 2;
            }
            else if (speed >= 6.4f)
                return 3;
            return 0;
        }

        public static string GetMovementAnim(int speed)
        {
            if (speed == 0) return "idle";
            if (speed == 1) return "walk";
            if (speed == 2) return "run";
            if (speed == 3) return "sprint";
            return "";
        }

        public void Clear()
        {
            /*if (_mainVehicle != null && Character.IsInVehicle(_mainVehicle) && Game.Player.Character.IsInVehicle(_mainVehicle))
            {
                _playerSeat = Util.GetPedSeat(Game.Player.Character);
            }
            else
            {
                _playerSeat = -2;
            }*/

            if (Character != null)
            {
                Character.Model.MarkAsNoLongerNeeded();
                Character.Delete();
            }
            if (_mainBlip != null)
            {
                _mainBlip.Remove();
                _mainBlip = null;
            }

            if (_parachuteProp != null)
            {
                _parachuteProp.Delete();
                _parachuteProp = null;
            }
        }
    }
}