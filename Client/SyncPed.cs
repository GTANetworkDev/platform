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
        public bool _isRagdoll;
        public Vehicle MainVehicle { get; set; }

        public bool IsInCover;

        public int VehicleSeat;
        public int PedHealth;

        public int VehicleHealth;
        public int VehicleHash;
        public Vector3 _vehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public string Name;
        public bool Siren;
        public int PedArmor;
        public bool IsVehDead;

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

        private Vector3 _lastPedVel;
        private Vector3 _pedVelocity;
        public Vector3 PedVelocity
        {
            get { return _pedVelocity; }
            set
            {
                _lastPedVel = _pedVelocity;
                _pedVelocity = value;
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

        public bool IsRagdoll
        {
            get { return _isRagdoll; }
            set
            {
                if (!_isRagdoll && value)
                {
                    if (Character != null)
                    {
                        Character.CanRagdoll = true;
                        Function.Call(Hash.SET_PED_TO_RAGDOLL, -1, -1, 0, true, true, true);
                    }
                }
                else if (_isRagdoll && !value)
                {
                    if (Character != null)
                    {
                        Character.CanRagdoll = false;
                        Character.Task.ClearAllImmediately();
                    }
                }
                
                _isRagdoll = value;
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

        private Vector3 _lastStart;
        private Vector3 _lastEnd;

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
            const float hRange = 300f;
            var gPos = IsInVehicle ? VehiclePosition : _position;
            var inRange = Game.Player.Character.IsInRangeOf(gPos, hRange);

            if (_lastStart != null && _lastEnd != null)
            {
                //Function.Call(Hash.DRAW_LINE, _lastStart.X, _lastStart.Y, _lastStart.Z, _lastEnd.X, _lastEnd.Y,
                    //_lastEnd.Z, 255, 255, 255, 255);
            }

            if (inRange && Character != null)
            {
                Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, Character, true);
            }

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

            bool isAiming = false;
            if (!Character.IsOccluded && (Character.IsInRangeOf(Game.Player.Character.Position, 30f)) || (isAiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, Character)))
            {
                var ray = World.Raycast(GameplayCamera.Position, Character.Position, IntersectOptions.Everything, Game.Player.Character);
                if (ray.HitEntity == Character || isAiming || Character.IsInVehicle())
                { 
                    var oldPos = UI.WorldToScreen(Character.Position + new Vector3(0, 0, 1.2f));
                    var targetPos = Character.Position + new Vector3(0, 0, 1.2f);
                    if (oldPos.X != 0 && oldPos.Y != 0)
                    {
                        Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);

                        var nameText = Name == null ? "<nameless>" : Name;

                        if (DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds > 10000)
                            nameText = "~r~AFK~w~~n~" + nameText;

                        var dist = (GameplayCamera.Position - Character.Position).Length();
                        var sizeOffset = Math.Max(1f - (dist / 30f), 0.3f);

                        new UIResText(nameText, new Point(0, 0), 0.4f * sizeOffset, Color.WhiteSmoke,
                            Font.ChaletLondon, UIResText.Alignment.Centered)
                        {
                            Outline = true,
                        }.Draw();

                        if (Character != null)
                        {
                            var armorColor = Color.FromArgb(100, 220, 220, 220);
                            var bgColor = Color.FromArgb(100, 0, 0, 0);
                            var armorPercent = Math.Min(Math.Max(PedArmor/100f, 0f), 1f);
                            var armorBar = (int) Math.Round(150*armorPercent);
                            armorBar = (int)(armorBar * sizeOffset);

                            new UIResRectangle(new Point(0, 0) - new Size((int)(75*sizeOffset), (int)(-36*sizeOffset)), new Size(armorBar, (int)(20*sizeOffset)),
                                armorColor).Draw();

                            new UIResRectangle(new Point(0, 0) - new Size((int)(75 * sizeOffset), (int)(-36 * sizeOffset)) + new Size(armorBar, 0), new Size((int)(sizeOffset*150) - armorBar, (int)(sizeOffset*20)),
                                bgColor).Draw();

                            new UIResRectangle(new Point(0, 0) - new Size((int)(71 * sizeOffset), (int)(-40 * sizeOffset)),
                                new Size((int) ((142*Math.Min(Math.Max(2*(PedHealth/100f), 0f), 1f)) * sizeOffset), (int)(12*sizeOffset)),
                                Color.FromArgb(150, 50, 250, 50)).Draw();
                        }

                        Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                    }
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
                    if (VehicleSeat == -1)
                        MainVehicle.Position = VehiclePosition;
                    MainVehicle.EngineRunning = true;
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
                    Character.Position = gPos;
                if (MainVehicle != null)
                {
                    MainVehicle.Position = VehiclePosition;
                    MainVehicle.Rotation = VehicleRotation;
                }
                return;
            }

            if (IsInVehicle)
            {
                if (VehicleSeat == (int) GTA.VehicleSeat.Driver ||
                    MainVehicle.GetPedOnSeat(GTA.VehicleSeat.Driver) == null)
                {
                    MainVehicle.Health = VehicleHealth;
                    if (IsVehDead && !MainVehicle.IsDead)
                    {
                        MainVehicle.IsInvincible = false;
                        MainVehicle.Explode();
                    }
                    else if (!IsVehDead && MainVehicle.IsDead)
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

                    if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon)
                    {
                        Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, 999, true, true);
                        Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);
                    }

                    if (IsAiming && !IsShooting)
                    {
                        Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z, 100f, 80, 0, Function.Call<int>(Hash.GET_HASH_KEY, "firing_pattern_burst_fire_driveby"));
                    }
                    else if (IsShooting)
                    {
                        Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z, 100f, 80, 1, Function.Call<int>(Hash.GET_HASH_KEY, "firing_pattern_burst_fire_driveby"));
                    }


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
                    Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);
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

                    if (IsAiming && !IsShooting && Character.IsInRangeOf(_position, 0.5f))
                    {
                        Character.Task.AimAt(AimCoords, 1);

                        var dirVector = Position - _lastPosition;

                        var target = Util.LinearVectorLerp(Position,
                            (Position) + dirVector,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);
                    }
                    

                    if (IsShooting)
                    {
                        Character.Task.AimAt(AimCoords, 100);

                        var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
                        var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, -0.01f));
                        var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash) CurrentWeapon);
                        var speed = 0xbf800000;
                        var weaponH = (WeaponHash) CurrentWeapon;
                        if (weaponH == WeaponHash.RPG || weaponH == WeaponHash.HomingLauncher ||
                            weaponH == WeaponHash.GrenadeLauncher || weaponH == WeaponHash.Firework)
                            speed = 500;

                        if (weaponH == WeaponHash.Minigun)
                            weaponH = WeaponHash.CombatPDW;
                        /*
                        var end = AimCoords;

                        if ((end - start).Length() > 20f)
                        {
                            var dir = (start - end);
                            dir.Normalize();
                            start = end + dir*20f;
                        }*/
                        
                        Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, AimCoords.X,
                            AimCoords.Y, AimCoords.Z, damage, true, (int)weaponH, Character, true, true, speed);

                        _lastStart = start;
                        _lastEnd = AimCoords;

                        var dirVector = Position - _lastPosition;

                        var target = Util.LinearVectorLerp(Position,
                            (Position) + dirVector,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);
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
                            
                            var dir = Position - _lastPosition;

                            var vdir = PedVelocity - _lastPedVel;
                            var target = Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                                (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                            var posTarget = Util.LinearVectorLerp(Position, Position + dir,
                                (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                            if (GetPedSpeed(PedVelocity.Length()) > 0)
                            {
                                Character.Velocity = target + 2 * (posTarget - Character.Position);
                                _stopTime = DateTime.Now;
                                _carPosOnUpdate = Character.Position;
                            }
                            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                            {
                                posTarget = Util.LinearVectorLerp(_carPosOnUpdate, Position + dir,
                                (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0, 0);
                            }
                            else
                            {
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y, Position.Z, 0, 0, 0, 0);
                            }
                        }
                        else if (syncMode == SynchronizationMode.EntityLerping)
                        {
                            var target = Util.LinearVectorLerp(_lastPosition, Position,
                                (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);
                        }

                        else if (syncMode == SynchronizationMode.Teleport)
                        {
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y, Position.Z, 0, 0, 0, 0);
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
                        var animDict = GetAnimDictionary();
                        var secondaryAnimDict = GetSecondaryAnimDict();

                        if (
                            !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
                                8f, 1f, -1, 0, -8f, 0, 0, 0);
                        }

                        if (secondaryAnimDict != null &&
                            !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, ourAnim,
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(secondaryAnimDict), ourAnim,
                                8f, 1f, -1, 32 | 16, -8f, 0, 0, 0);
                        }
                    }
                }
                _lastJumping = IsJumping;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
            }
            _lastVehicle = IsInVehicle;
        }

        public string GetAnimDictionary()
        {
            string dict = "move_m@generic";

            if (Character.Gender == Gender.Female)
                dict = "move_f@generic";

            return dict;
        }

        public string GetSecondaryAnimDict()
        {
            if (CurrentWeapon == unchecked((int) WeaponHash.Unarmed)) return GetAnimDictionary();
            if (CurrentWeapon == unchecked((int) WeaponHash.RPG) ||
                CurrentWeapon == unchecked((int) WeaponHash.HomingLauncher) ||
                CurrentWeapon == unchecked((int)WeaponHash.Firework))
                return "weapons@heavy@rpg";
            if (CurrentWeapon == unchecked((int) WeaponHash.Minigun))
                return "weapons@heavy@minigun";
            if (CurrentWeapon == unchecked((int) WeaponHash.GolfClub) ||
                CurrentWeapon == unchecked((int) WeaponHash.Bat))
                return "weapons@melee_2h";
            if (Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, CurrentWeapon) ==
                     Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, unchecked((int) WeaponHash.Bat)))
                return "weapons@melee_1h";
            if (CurrentWeapon == -1357824103 || CurrentWeapon == -1074790547 ||
                (CurrentWeapon == 2132975508 || CurrentWeapon == -2084633992) ||
                (CurrentWeapon == -952879014 || CurrentWeapon == 100416529) ||
                CurrentWeapon == unchecked((int) WeaponHash.Gusenberg) ||
                CurrentWeapon == unchecked((int) WeaponHash.MG) || CurrentWeapon == unchecked((int) WeaponHash.CombatMG) ||
                CurrentWeapon == unchecked((int) WeaponHash.CombatPDW) ||
                CurrentWeapon == unchecked((int) WeaponHash.AssaultSMG) ||
                CurrentWeapon == unchecked((int) WeaponHash.SMG) ||
                CurrentWeapon == unchecked((int) WeaponHash.HeavySniper) ||
                CurrentWeapon == unchecked((int) WeaponHash.PumpShotgun) ||
                CurrentWeapon == unchecked((int) WeaponHash.HeavyShotgun) ||
                CurrentWeapon == unchecked((int) WeaponHash.Musket) ||
                CurrentWeapon == unchecked((int) WeaponHash.AssaultShotgun) ||
                CurrentWeapon == unchecked((int) WeaponHash.BullpupShotgun) ||
                CurrentWeapon == unchecked((int) WeaponHash.SawnOffShotgun))
                return "weapons@machinegun@";
            return GetAnimDictionary();
        }

        public static int GetPedSpeed(float speed)
        {
            if (speed < 0.5f)
            {
                return 0;
            }
            else if (speed >= 0.5f && speed < 3.7f)
            {
                return 1;
            }
            else if (speed >= 3.7f && speed < 6.2f)
            {
                return 2;
            }
            else if (speed >= 6.2f)
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

    public static class WeaponDataProvider
    {
        public static int GetWeaponDamage(WeaponHash weapon)
        {
            switch (weapon)
            {
                default:
                    return 0;
                case WeaponHash.SMG:
                    return 22;
                case WeaponHash.AssaultSMG:
                    return 23;
                case WeaponHash.AssaultRifle:
                    return 30;
                case WeaponHash.CarbineRifle:
                    return 32;
                case WeaponHash.AdvancedRifle:
                    return 34;
                case WeaponHash.MG:
                    return 40;
                case WeaponHash.CombatMG:
                    return 45;
                case WeaponHash.PumpShotgun:
                    return 29;
                case WeaponHash.SawnOffShotgun:
                    return 40;
                case WeaponHash.AssaultShotgun:
                    return 32;
                case WeaponHash.BullpupShotgun:
                    return 14;
                case WeaponHash.StunGun:
                    return 1;
                case WeaponHash.SniperRifle:
                    return 101;
                case WeaponHash.HeavySniper:
                    return 216;
                case WeaponHash.Minigun:
                    return 30;
                case WeaponHash.Pistol:
                    return 26;
                case WeaponHash.CombatPistol:
                    return 27;
                case WeaponHash.APPistol:
                    return 28;
                case WeaponHash.Pistol50:
                    return 51;
                case WeaponHash.MicroSMG:
                    return 21;
                case WeaponHash.Snowball:
                    return 25;
                case WeaponHash.CombatPDW:
                    return 28;
                case WeaponHash.MarksmanPistol:
                    return 220;
                case (WeaponHash)0x47757124:
                    return 10;
                case WeaponHash.SNSPistol:
                    return 28;
                case WeaponHash.HeavyPistol:
                    return 40;
                case WeaponHash.VintagePistol:
                    return 34;
                case (WeaponHash)0xC1B3C3D1:
                    return 160;
                case WeaponHash.Musket:
                    return 165;
                case WeaponHash.HeavyShotgun:
                    return 117;
                case WeaponHash.SpecialCarbine:
                    return 34;
                case WeaponHash.BullpupRifle:
                    return 32;
                case WeaponHash.Gusenberg:
                    return 34;
                case WeaponHash.MarksmanRifle:
                    return 65;
                case WeaponHash.RPG:
                    return 50;
                case WeaponHash.GrenadeLauncher:
                    return 75;
                case WeaponHash.Firework:
                    return 20;
                case WeaponHash.Railgun:
                    return 30;
            }
        }
    }
}