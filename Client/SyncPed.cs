using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace GTANetwork
{
    public enum SynchronizationMode
    {
        Dynamic,
        EntityLerping,
        DeadReckoning,
        Experimental,
        Teleport
    }

    public class Animation
    {
        public string Dictionary { get; set; }
        public string Name { get; set; }
        public bool Loop { get; set; }
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
        public Animation CurrentAnimation;
        public int ModelHash;
        public int CurrentWeapon;
        public bool IsAiming;
        public Vector3 AimCoords;
        public float Latency;
        public bool IsHornPressed;
        public bool _isRagdoll;
        public Vehicle MainVehicle { get; set; }
        public bool IsInActionMode;
        public bool IsInCover;
        public bool IsInMeleeCombat;
        public bool IsFreefallingWithParachute;
        public bool IsShooting;
        public float VehicleRPM;

        public int Team = -1;
        public int BlipSprite = -1;
        public int BlipColor = -1;
        public int BlipAlpha = -1;

        public int VehicleSeat;
        public int PedHealth;

        public float VehicleHealth;
        public int VehicleHash;
        public Vector3 _vehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public string Name;
        public bool Siren;
        public int PedArmor;
        public bool IsVehDead;

        public bool IsSpectating;

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

        public int DEBUG_STEP
        {
            get { return DEBUG_STEP_backend; }
            set
            {
                DEBUG_STEP_backend = value;
                LogManager.DebugLog("NEXTSTEP FOR " + Name + ": " + value);

                if (Main.SlowDownClientForDebug)
                    Script.Yield();
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
        public int RelGroup;
        public int FriendRelGroup;
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private bool _lastVehicleShooting;

        private Queue<double> _latencyAverager;

        private Vector3 _lastStart;
        private Vector3 _lastEnd;

        private int _playerSeat;
        private bool _lastDrivebyShooting;
        private bool _isStreamedIn;
        private Blip _mainBlip;
        private bool _lastHorn;
        private Prop _parachuteProp;
        private bool _leftSide;

        public SyncPed(int hash, Vector3 pos, Vector3 rot, bool blip = true)
        {
            _position = pos;
            _rotation = rot;
            ModelHash = hash;
            _blip = blip;
            
            _latencyAverager = new Queue<double>();

            RelGroup = World.AddRelationshipGroup("SYNCPED");
            FriendRelGroup = World.AddRelationshipGroup("SYNCPED_TEAMMATES");
            World.SetRelationshipBetweenGroups(Relationship.Neutral, RelGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Neutral, Game.Player.Character.RelationshipGroup, RelGroup);

            World.SetRelationshipBetweenGroups(Relationship.Companion, FriendRelGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Companion, Game.Player.Character.RelationshipGroup, FriendRelGroup);
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
        private string lastMeleeAnim;
        private float meleeanimationend;
        private float meleeDamageStart;
        private float meleeDamageEnd;
        private bool meleeSwingDone;
        private bool _lastFreefall;
        private DateTime _lastRocketshot;
        private int _lastVehicleAimUpdate;

        private int DEBUG_STEP_backend;

        public void DisplayLocally()
        {
            try
            {

                if (IsSpectating) return;


                DEBUG_STEP = 0;
                
                const float hRange = 300f;
                var gPos = IsInVehicle ? VehiclePosition : _position;
                var inRange = Game.Player.Character.IsInRangeOf(gPos, hRange);

                DEBUG_STEP = 1;

                if (_lastStart != null && _lastEnd != null)
                {
                    //Function.Call(Hash.DRAW_LINE, _lastStart.X, _lastStart.Y, _lastStart.Z, _lastEnd.X, _lastEnd.Y,
                    //_lastEnd.Z, 255, 255, 255, 255);
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
                DEBUG_STEP = 2;

                if (Character == null || !Character.Exists() || !Character.IsInRangeOf(gPos, hRange) ||
                    Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
                {
                    LogManager.DebugLog($"{Character == null}, {Character?.Exists()}, {Character?.IsInRangeOf(gPos, hRange)}, {Character?.Model.Hash}, {ModelHash}, {Character?.IsDead}, {PedHealth}");
                    if (Character != null) Character.Delete();
                    DEBUG_STEP = 3;
                    LogManager.DebugLog("NEW PLAYER " + Name);
                    var charModel = new Model(ModelHash);
                    LogManager.DebugLog("REQUESTING MODEL FOR " + Name);
                    charModel.Request(10000);
                    LogManager.DebugLog("CREATING PED FOR " + Name);
                    Character = World.CreatePed(charModel, gPos, _rotation.Z);
                    charModel.MarkAsNoLongerNeeded();
                    if (Character == null) return;
                    DEBUG_STEP = 4;
                    Character.BlockPermanentEvents = true;
                    Character.IsInvincible = true;
                    Character.CanRagdoll = false;
                    if (Team == -1 || Team != Main.LocalTeam)
                        Character.RelationshipGroup = RelGroup;
                    else
                        Character.RelationshipGroup = FriendRelGroup;
                    LogManager.DebugLog("SETTINGS FIRING PATTERN " + Name);
                    Character.FiringPattern = FiringPattern.FullAuto;

                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Character);

                    //Character.FreezePosition = true;
                    LogManager.DebugLog("SETTING CLOTHES FOR " + Name);
                    if (PedProps != null)
                    foreach (var pair in PedProps)
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value, 0, 2);
                    }
                    LogManager.DebugLog("ATTACHING BLIP FOR " + Name);
                    if (_blip)
                    {
                        Character.AddBlip();
                        if (Character.CurrentBlip == null || !Character.CurrentBlip.Exists()) return;
                        LogManager.DebugLog("SETTING BLIP COLOR FOR" + Name);
                        if (BlipColor != -1)
                            Character.CurrentBlip.Color = (BlipColor) BlipColor;
                        else
                            Character.CurrentBlip.Color = GTA.BlipColor.White;
                        LogManager.DebugLog("SETTING BLIP SCALE FOR" + Name);
                        Character.CurrentBlip.Scale = 0.8f;
                        LogManager.DebugLog("SETTING BLIP NAME FOR" + Name);
                        SetBlipNameFromTextFile(Character.CurrentBlip, Name);
                        if (BlipSprite != -1)
                            Character.CurrentBlip.Sprite = (BlipSprite) BlipSprite;
                        if (BlipAlpha != -1)
                            Character.CurrentBlip.Alpha = BlipAlpha;
                        LogManager.DebugLog("BLIP DONE FOR" + Name);
                    }
                    
                    return;
                }
                DEBUG_STEP = 5;
                if (!IsInVehicle)
                {
                    bool isAiming = false;
                    if ((!Character.IsOccluded && (Character.IsInRangeOf(Game.Player.Character.Position, 30f))) ||
                        (isAiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, Character)))
                    {
                        var ray = World.Raycast(GameplayCamera.Position, Character.GetBoneCoord(Bone.IK_Head),
                            IntersectOptions.Everything,
                            Game.Player.Character);
                        if (ray.HitEntity == Character || isAiming)
                        {
                            var oldPos = UI.WorldToScreen(Character.Position + new Vector3(0, 0, 1.2f));
                            var targetPos = Character.Position + new Vector3(0, 0, 1.2f);
                            if (oldPos.X != 0 && oldPos.Y != 0)
                            {
                                Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);
                                DEBUG_STEP = 6;
                                var nameText = Name == null ? "<nameless>" : Name;

                                if (DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds > 10000)
                                    nameText = "~r~AFK~w~~n~" + nameText;

                                var dist = (GameplayCamera.Position - Character.Position).Length();
                                var sizeOffset = Math.Max(1f - (dist/30f), 0.3f);

                                new UIResText(nameText, new Point(0, 0), 0.4f*sizeOffset, Color.WhiteSmoke,
                                    Font.ChaletLondon, UIResText.Alignment.Centered)
                                {
                                    Outline = true,
                                }.Draw();
                                DEBUG_STEP = 7;
                                if (Character != null)
                                {
                                    var armorColor = Color.FromArgb(100, 220, 220, 220);
                                    var bgColor = Color.FromArgb(100, 0, 0, 0);
                                    var armorPercent = Math.Min(Math.Max(PedArmor/100f, 0f), 1f);
                                    var armorBar = (int) Math.Round(150*armorPercent);
                                    armorBar = (int) (armorBar*sizeOffset);

                                    new UIResRectangle(
                                        new Point(0, 0) - new Size((int) (75*sizeOffset), (int) (-36*sizeOffset)),
                                        new Size(armorBar, (int) (20*sizeOffset)),
                                        armorColor).Draw();

                                    new UIResRectangle(
                                        new Point(0, 0) - new Size((int) (75*sizeOffset), (int) (-36*sizeOffset)) +
                                        new Size(armorBar, 0),
                                        new Size((int) (sizeOffset*150) - armorBar, (int) (sizeOffset*20)),
                                        bgColor).Draw();

                                    new UIResRectangle(
                                        new Point(0, 0) - new Size((int) (71*sizeOffset), (int) (-40*sizeOffset)),
                                        new Size(
                                            (int) ((142*Math.Min(Math.Max(2*(PedHealth/100f), 0f), 1f))*sizeOffset),
                                            (int) (12*sizeOffset)),
                                        Color.FromArgb(150, 50, 250, 50)).Draw();
                                }
                                DEBUG_STEP = 8;
                                Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                            }
                        }
                    }
                }
                else if (IsInVehicle && MainVehicle != null && Character.IsInRangeOf(GameplayCamera.Position, 100f) && !Character.IsOccluded && MainVehicle.IsOnScreen)
                {
                    
                    var oldPos = UI.WorldToScreen(Character.Position + new Vector3(0, 0, 2f));
                    var targetPos = Character.Position + new Vector3(0, 0, 2f);
                    if (oldPos.X != 0 && oldPos.Y != 0)
                    {
                        Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);
                        DEBUG_STEP = 6;
                        var nameText = Name == null ? "<nameless>" : Name;

                        if (DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds > 10000)
                            nameText = "~r~AFK~w~~n~" + nameText;

                        var dist = (GameplayCamera.Position - Character.Position).Length();
                        var sizeOffset = Math.Max(1f - (dist / 100f), 0.3f);

                        new UIResText(nameText, new Point(0, 0), 0.4f * sizeOffset, Color.WhiteSmoke,
                            Font.ChaletLondon, UIResText.Alignment.Centered)
                        {
                            Outline = true,
                        }.Draw();
                        DEBUG_STEP = 7;
                        if (Character != null)
                        {
                            var bgColor = Color.FromArgb(100, 0, 0, 0);

                            new UIResRectangle(new Point(0, 0) - new Size((int)(75 * sizeOffset), (int)(-36 * sizeOffset)),
                                new Size((int)(sizeOffset * 150), (int)(sizeOffset * 20)),
                                bgColor).Draw();

                            new UIResRectangle(
                                new Point(0, 0) - new Size((int)(71 * sizeOffset), (int)(-40 * sizeOffset)),
                                new Size(
                                    (int)((142 * Math.Min(Math.Max(((VehicleHealth) / 1000f), 0f), 1f)) * sizeOffset),
                                    (int)(12 * sizeOffset)),
                                Color.FromArgb(150, 50, 250, 50)).Draw();
                        }
                        DEBUG_STEP = 8;
                        Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                    }
                    
                }

                DEBUG_STEP = 9;
                if ((!_lastVehicle && IsInVehicle && VehicleHash != 0) ||
                    (_lastVehicle && IsInVehicle &&
                     (MainVehicle == null || (!Character.IsInVehicle(MainVehicle) && Game.Player.Character.GetVehicleIsTryingToEnter() != MainVehicle) ||
                      Main.NetEntityHandler.EntityToNet(MainVehicle.Handle) != VehicleNetHandle ||
                      (VehicleSeat != Util.GetPedSeat(Character) && Game.Player.Character.GetVehicleIsTryingToEnter() != MainVehicle))))
                {
                    if (Debug)
                    {
                        if (MainVehicle != null) MainVehicle.Delete();
                        MainVehicle = World.CreateVehicle(new Model(VehicleHash), VehiclePosition, VehicleRotation.Z);
                    }
                    else
                        MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);
                    DEBUG_STEP = 10;


                    UI.Notify(Util.GetPedSeat(Game.Player.Character).ToString() + " in " + VehicleSeat);
                    if (Game.Player.Character.IsInVehicle(MainVehicle) &&
                        VehicleSeat == Util.GetPedSeat(Game.Player.Character))
                    {
                        Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
                        Util.SafeNotify("~r~Car jacked!");
                    }
                    DEBUG_STEP = 11;

                    if (MainVehicle != null)
                    {
                        if (VehicleSeat == -1)
                            MainVehicle.Position = VehiclePosition;
                        MainVehicle.EngineRunning = true;
                        //MainVehicle.PrimaryColor = (VehicleColor) VehiclePrimaryColor;
                        //MainVehicle.SecondaryColor = (VehicleColor) VehicleSecondaryColor;
                        MainVehicle.Rotation = _vehicleRotation;
                        MainVehicle.IsInvincible = true;
                        //Character.Task.WarpIntoVehicle(MainVehicle, (VehicleSeat) VehicleSeat);
                        Character.SetIntoVehicle(MainVehicle, (VehicleSeat) VehicleSeat);
                        DEBUG_STEP = 12;
                    }
                    DEBUG_STEP = 13;
                    _lastVehicle = true;
                    _justEnteredVeh = true;
                    _enterVehicleStarted = DateTime.Now;
                    return;
                }

                if (_lastVehicle && _justEnteredVeh && IsInVehicle && !Character.IsInVehicle(MainVehicle) &&
                    DateTime.Now.Subtract(_enterVehicleStarted).TotalSeconds <= 4)
                {
                    return;
                }
                _justEnteredVeh = false;
                DEBUG_STEP = 14;
                if (_lastVehicle && !IsInVehicle && MainVehicle != null)
                {
                    if (Character != null) Character.Task.LeaveVehicle(MainVehicle, true);
                }

                if (Character != null)
                {
                    Character.Health = (int) ((PedHealth/(float) 100)*Character.MaxHealth);
                }

                _switch++;
                DEBUG_STEP = 15;
                if (!inRange)
                {
                    if (Character != null)
                    {
                        if (!IsInVehicle) Character.Position = gPos;
                        else if (MainVehicle != null && GetResponsiblePed(MainVehicle).Handle == Character.Handle)
                        {
                            MainVehicle.Position = VehiclePosition;
                            MainVehicle.Rotation = VehicleRotation;
                            Character.Position = gPos;
                        }
                    }
                    return;
                }

                DEBUG_STEP = 16;

                if ((Character.CurrentBlip == null || (Character.CurrentBlip.Position - Character.Position).Length() > 5f) && _blip)
                {
                    LogManager.DebugLog("Blip was too far away -- deleting");
                    Character.Delete();
                }

                if (IsInVehicle)
                {
                    if (GetResponsiblePed(MainVehicle).Handle == Character.Handle)
                    {
                        MainVehicle.EngineHealth = VehicleHealth;
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
                        DEBUG_STEP = 17;
                        //MainVehicle.PrimaryColor = (VehicleColor) VehiclePrimaryColor;
                        //MainVehicle.SecondaryColor = (VehicleColor) VehicleSecondaryColor;

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
                        DEBUG_STEP = 18;

                        if (MainVehicle.SirenActive && !Siren)
                            MainVehicle.SirenActive = Siren;
                        else if (!MainVehicle.SirenActive && Siren)
                            MainVehicle.SirenActive = Siren;

                        MainVehicle.CurrentRPM = VehicleRPM;
                        
                        DEBUG_STEP = 19;

                        
                        var dir = VehiclePosition - _lastVehiclePos;

                        var syncMode = Main.GlobalSyncMode;
                        if (syncMode == SynchronizationMode.Dynamic)
                        {
                            if (AverageLatency > 70)
                                syncMode = SynchronizationMode.EntityLerping;
                            else
                                syncMode = SynchronizationMode.DeadReckoning;
                        }
                        DEBUG_STEP = 20;
                        if (syncMode == SynchronizationMode.DeadReckoning)
                        {
                            var vdir = VehicleVelocity - _lastVehVel;
                            var target = Util.LinearVectorLerp(VehicleVelocity, VehicleVelocity + vdir,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            var posTarget = Util.LinearVectorLerp(VehiclePosition, VehiclePosition + dir,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            if (Speed > 0.5f)
                            {
                                MainVehicle.Velocity = target + 2*(posTarget - MainVehicle.Position);
                                _stopTime = DateTime.Now;
                                _carPosOnUpdate = MainVehicle.Position;
                            }
                            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                            {
                                posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                                    (int) DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                                    posTarget.Z, 0, 0, 0, 0);
                            }
                            else
                            {
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X,
                                    VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                            }
                        }
                        else if (syncMode == SynchronizationMode.EntityLerping)
                        {
                            var target = Util.LinearVectorLerp(_lastVehVel, VehicleVelocity,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            var posTarget = Util.LinearVectorLerp(_lastVehiclePos, VehiclePosition,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            if (Speed > 0)
                            {
                                MainVehicle.Velocity = target + 2*(posTarget - MainVehicle.Position);
                                _stopTime = DateTime.Now;
                                _carPosOnUpdate = MainVehicle.Position;
                            }
                            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
                            {
                                posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                                    (int) DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                                    posTarget.Z, 0, 0, 0, 0);
                            }
                            else
                            {
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X,
                                    VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                            }
                        }
                        else if (syncMode == SynchronizationMode.Experimental)
                        {
                            var vdir = VehicleVelocity - _lastVehVel;
                            var target = Util.LinearVectorLerp(VehicleVelocity, VehicleVelocity + vdir,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            var posTarget = Util.LinearVectorLerp(VehiclePosition, VehiclePosition + dir,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            if (Speed > 0)
                                MainVehicle.Velocity = target + 2*(posTarget - MainVehicle.Position);
                            else
                            {
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                                    posTarget.Z, 0, 0, 0, 0);
                            }
                        }
                        else if (syncMode == SynchronizationMode.Teleport)
                        {
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, VehiclePosition.X,
                                VehiclePosition.Y, VehiclePosition.Z, 0, 0, 0, 0);
                        }

                        DEBUG_STEP = 21;
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

                        
                        if (IsShooting && CurrentWeapon != 0 && VehicleSeat == -1 && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash) VehicleHash))
                        {
                            var isRocket = WeaponDataProvider.IsVehicleWeaponRocket(CurrentWeapon);
                            if (isRocket && DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds < 1500)
                            {
                                return;
                            }
                            if (isRocket)
                                _lastRocketshot = DateTime.Now;
                            var isParallel =
                                WeaponDataProvider.DoesVehicleHaveParallelWeapon(unchecked((VehicleHash) VehicleHash),
                                    isRocket);

                            var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle(unchecked((VehicleHash) VehicleHash), isRocket);

                            if (isParallel && _leftSide)
                            {
                                muzzle = new Vector3(muzzle.X * -1f, muzzle.Y, muzzle.Z);
                            }
                            _leftSide = !_leftSide;

                            var start =
                                MainVehicle.GetOffsetInWorldCoords(muzzle);
                            var end = start + Main.RotationToDirection(VehicleRotation) * 100f;
                            var hash = CurrentWeapon;
                            var speed = 0xbf800000;

                            if (isRocket)
                                speed = 500;
                            else
                                hash = unchecked((int)WeaponHash.CombatPDW);

                            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X,
                                    end.Y, end.Z, 75, true, hash, Character, true, false, speed);
                        }
                    }

                    if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash) VehicleHash, VehicleSeat))
                    {
                        if (Game.GameTime - _lastVehicleAimUpdate > 30)
                        {
                            Function.Call(Hash.TASK_VEHICLE_AIM_AT_COORD, Character, AimCoords.X, AimCoords.Y,
                                AimCoords.Z);
                            _lastVehicleAimUpdate = Game.GameTime;
                        }

                        if (IsShooting)
                        {
                            if (((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Rhino &&
                                 DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 1000) ||
                                ((VehicleHash) VehicleHash != GTA.Native.VehicleHash.Rhino))
                            {
                                _lastRocketshot = DateTime.Now;

                                var baseTurretPos =
                                    MainVehicle.GetOffsetInWorldCoords(
                                        WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash) VehicleHash, false));
                                var doesBaseTurretDiffer =
                                    WeaponDataProvider.DoesVehiclesMuzzleDifferFromVehicleGunPos(
                                        (VehicleHash) VehicleHash);
                                var barrellLength = WeaponDataProvider.GetVehicleTurretLength((VehicleHash) VehicleHash);

                                var speed = 0xbf800000;
                                var hash = WeaponHash.CombatPDW;
                                if ((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Rhino)
                                {
                                    hash = WeaponHash.RPG;
                                }

                                Vector3 tPos = baseTurretPos;
                                if (
                                    WeaponDataProvider.DoesVehicleHaveParallelWeapon((VehicleHash) VehicleHash, false) &&
                                    VehicleSeat == 1)
                                {
                                    var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash) VehicleHash,
                                        false);
                                    tPos =
                                        MainVehicle.GetOffsetInWorldCoords(new Vector3(muzzle.X*-1f, muzzle.Y, muzzle.Z));
                                }

                                if (doesBaseTurretDiffer)
                                {
                                    var kekDir = (AimCoords - tPos);
                                    kekDir.Normalize();
                                    var rot = Main.DirectionToRotation(kekDir);
                                    var newDir = Main.RotationToDirection(new Vector3(0, 0, rot.Z));
                                    newDir.Normalize();
                                    tPos = tPos +
                                           newDir*
                                           WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash) VehicleHash, true)
                                               .Length();
                                }


                                var turretDir = (AimCoords - tPos);
                                turretDir.Normalize();
                                var start = tPos + turretDir*barrellLength;
                                var end = start + turretDir*100f;

                                _lastStart = start;
                                _lastEnd = end;

                                var damage = WeaponDataProvider.GetWeaponDamage(WeaponHash.Minigun);
                                if ((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Rhino)
                                    damage = 210;

                                Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X,
                                    end.Y, end.Z, damage, true, (int) hash, Character, true, false, speed);
                            }
                        }
                    }
                    else if (!WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash) VehicleHash) || VehicleSeat != -1)
                    {
                        if (Character.Weapons.Current.Hash != (WeaponHash) CurrentWeapon)
                        {
                            //Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, 999, true, true);
                            //Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);
                            Character.Weapons.Give((WeaponHash) CurrentWeapon, -1, true, true);
                        }

                        if (IsShooting)
                        {
                            if (IsShooting && _lastShooting && Game.GameTime - _lastVehicleAimUpdate > 30)
                            {
                                Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);
                                Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z);
                            }

                            if (IsShooting && !_lastShooting)
                            {
                                Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z,
                                    0, 0, 0, unchecked((int) FiringPattern.FullAuto));
                            }

                            _lastVehicleAimUpdate = Game.GameTime;
                            _lastDrivebyShooting = IsShooting;
                        }

                        if (!IsShooting && _lastDrivebyShooting && Game.GameTime - _lastVehicleAimUpdate > 200)
                        {
                            Character.Task.ClearAll();
                            Character.Task.ClearSecondary();
                            Function.Call(Hash.CLEAR_DRIVEBY_TASK_UNDERNEATH_DRIVING_TASK, Character);
                            //Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                            //Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, 0, 0, 0);
                            Character.Task.ClearLookAt();
                            //UI.Notify("Done shooting");
                            //UI.ShowSubtitle("Done Shooting1", 300);
                            _lastDrivebyShooting = false;
                        }

                        
                    }

                    //UI.ShowSubtitle("SS: " + IsShooting + " _LS: " + _lastShooting);
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
                    DEBUG_STEP = 22;
                    _clothSwitch++;
                    if (_clothSwitch >= 750)
                        _clothSwitch = 0;
                    DEBUG_STEP = 23;

                    if (Character.Weapons.Current.Hash != (WeaponHash) CurrentWeapon)
                    {
                        //Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, -1, true, true);
                        //Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);

                        Character.Weapons.Give((WeaponHash) CurrentWeapon, -1, true, true);
                    }

                    if (!_lastJumping && IsJumping)
                    {
                        //Character.FreezePosition = false;
                        Character.Task.Jump();
                    }

                    if (!IsJumping && _lastJumping)
                    {
                        //Character.FreezePosition = true;
                    }

                    DEBUG_STEP = 24;
                    if (IsFreefallingWithParachute)
                    {
                        Character.FreezePosition = true;
                        Character.CanRagdoll = false;

                        if (!_lastFreefall)
                        {
                            Character.Task.ClearAllImmediately();
                            Character.Task.ClearSecondary();
                        }

                        var target = Util.LinearVectorLerp(_lastPosition,
                            _position,
                            (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);

                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
                            0);
                        DEBUG_STEP = 25;
                        if ((Util.Denormalize(_lastRotation.Z) < 180f &&
                             Util.Denormalize(_rotation.Z) > 180f) ||
                            (Util.Denormalize(_lastRotation.Z) > 180f &&
                             Util.Denormalize(_rotation.Z) < 180f))
                            Character.Quaternion = _rotation.ToQuaternion();
                        else
                            Character.Quaternion =
                                Util.LinearVectorLerp(_lastRotation, _rotation,
                                    (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                                    (int)AverageLatency)
                                    .ToQuaternion();
                        if (
                            !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
                                "skydive@base", "free_idle",
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character,
                                Util.LoadDict("skydive@base"), "free_idle",
                                8f, 1f, -1, 0, -8f, 1, 1, 1);
                        }
                    }
                    else if (IsParachuteOpen)
                    {
                        if (_parachuteProp == null)
                        {
                            _parachuteProp = World.CreateProp(new Model(1740193300), Character.Position,
                                Character.Rotation, false, false);
                            _parachuteProp.FreezePosition = true;
                            Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);
                            Character.Task.ClearAllImmediately();
                            Character.Task.ClearSecondary();
                        }

                        Character.FreezePosition = true;
                        Character.CanRagdoll = false;
                        
                        var target = Util.LinearVectorLerp(_lastPosition,
                            _position,
                            (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
                            0);
                        DEBUG_STEP = 25;
                        if ((Util.Denormalize(_lastRotation.Z) < 180f &&
                             Util.Denormalize(_rotation.Z) > 180f) ||
                            (Util.Denormalize(_lastRotation.Z) > 180f &&
                             Util.Denormalize(_rotation.Z) < 180f))
                            Character.Quaternion = _rotation.ToQuaternion();
                        else
                            Character.Quaternion =
                                Util.LinearVectorLerp(_lastRotation, _rotation,
                                    (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                                    (int) AverageLatency)
                                    .ToQuaternion();
                        
                        _parachuteProp.Position = Character.Position + new Vector3(0, 0, 3.7f) +
                                                  Character.ForwardVector*0.5f;
                        _parachuteProp.Quaternion = Character.Quaternion;
                        if (
                            !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
                                "skydive@parachute@first_person", "chute_idle_right",
                                3))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, Character,
                                Util.LoadDict("skydive@parachute@first_person"), "chute_idle_right",
                                8f, 1f, -1, 0, -8f, 1, 1, 1);
                        }
                        DEBUG_STEP = 26;
                    }
                    else
                    {
                        Character.FreezePosition = false;

                        if (_parachuteProp != null)
                        {
                            _parachuteProp.Delete();
                            _parachuteProp = null;
                        }
                        DEBUG_STEP = 27;
                        if (lastMeleeAnim != null)
                        {
                            var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character,
                                lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);

                            UpdatePlayerPedPos();

                            if (!meleeSwingDone && CurrentWeapon != unchecked ((int)WeaponHash.Unarmed))
                            {
                                var gunEntity = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
                                if (gunEntity != null)
                                {
                                    Vector3 min;
                                    Vector3 max;
                                    gunEntity.Model.GetDimensions(out min, out max);
                                    var start = gunEntity.GetOffsetInWorldCoords(min);
                                    var end = gunEntity.GetOffsetInWorldCoords(max);
                                    var ray = World.RaycastCapsule(start, end, (int) Math.Abs(end.X - start.X),
                                        IntersectOptions.Peds1, Character);
                                    //Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
                                    if (ray.DitHitAnything && ray.DitHitEntity &&
                                        ray.HitEntity.Handle == Game.Player.Character.Handle)
                                    {
                                        Game.Player.Character.ApplyDamage(25);
                                        meleeSwingDone = true;
                                    }
                                }
                            }
                            else if (!meleeSwingDone && CurrentWeapon == unchecked((int)WeaponHash.Unarmed))
                            {
                                var rightfist = Character.GetBoneCoord(Bone.IK_R_Hand);
                                var start = rightfist - new Vector3(0, 0, 0.5f);
                                var end = rightfist + new Vector3(0, 0, 0.5f);
                                var ray = World.RaycastCapsule(start, end, (int)Math.Abs(end.X - start.X), IntersectOptions.Peds1, Character);
                                if (ray.DitHitAnything && ray.DitHitEntity && ray.HitEntity.Handle == Game.Player.Character.Handle)
                                {
                                    Game.Player.Character.ApplyDamage(25);
                                    meleeSwingDone = true;
                                }
                            }

                            DEBUG_STEP = 28;
                            if (currentTime >= 1f)
                            {
                                lastMeleeAnim = null;
                                meleeSwingDone = false;
                            }
                            if (currentTime >= meleeanimationend)
                            {
                                Character.Task.ClearAnimation(lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);
                                lastMeleeAnim = null;
                                meleeSwingDone = false;
                            }
                        }
                        else if (IsInMeleeCombat)
                        {
                            string secondaryAnimDict = null;
                            var ourAnim = GetMovementAnim(GetPedSpeed(Speed));
                            var hands = GetWeaponHandsHeld(CurrentWeapon);
                            var secAnim = ourAnim;
                            if (hands == 3) secondaryAnimDict = "move_strafe@melee_small_weapon";
                            if (hands == 4) secondaryAnimDict = "move_strafe@melee_large_weapon";
                            if (hands == 0)
                            {
                                secondaryAnimDict = "melee@unarmed@streamed_core_fps";
                                secAnim = "idle";
                            }
                            //
                            var animDict = GetAnimDictionary();


                            if (
                                !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
                                    3))
                            {
                                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
                                    8f, 1f, -1, 0, -8f, 1, 1, 1);
                            }

                            if (secondaryAnimDict != null &&
                                !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, secAnim,
                                    3))
                            {
                                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(secondaryAnimDict), secAnim,
                                    8f, 1f, -1, 32 | 16, -8f, 1, 1, 1);
                            }

                            UpdatePlayerPedPos();
                        }
                        DEBUG_STEP = 29;
                        if (IsAiming && !IsShooting)
                        {
                            var hands = GetWeaponHandsHeld(CurrentWeapon);

                            if (hands == 1 || hands == 2 || hands == 5 || hands == 6)
                            {
                                //Character.FreezePosition = false;
                                Character.Task.AimAt(AimCoords, -1);
                            }

                            var dirVector = Position - _lastPosition;

                            var target = Util.LinearVectorLerp(Position,
                                (Position) + dirVector,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0,
                                0, 0, 0);
                        }

                        DEBUG_STEP = 30;

                        if (IsShooting)
                        {
                            var hands = GetWeaponHandsHeld(CurrentWeapon);

                            if (hands == 3 || hands == 4 || hands == 0)
                            {
                                if (Character != null) Character.Task.ClearSecondary();

                                var ourAnim = "";
                                var anim = 0;
                                if (hands == 3)
                                {
                                    ourAnim = "melee@small_wpn@streamed_core_fps small_melee_wpn_short_range_0";
                                    anim = 0;
                                    meleeanimationend = 0.3f;
                                }
                                if (hands == 4)
                                {
                                    ourAnim = "melee@large_wpn@streamed_core short_0_attack";
                                    meleeanimationend = 0.55f;
                                    anim = 1;
                                }
                                if (hands == 0)
                                {
                                    ourAnim = "melee@unarmed@streamed_core_fps heavy_punch_a";
                                    meleeanimationend = 0.9f;
                                    anim = 2;
                                }
                                if (CurrentWeapon == unchecked((int) WeaponHash.Knife) || CurrentWeapon == -538741184 ||
                                    CurrentWeapon == unchecked((int) WeaponHash.Dagger))
                                {
                                    ourAnim = "melee@knife@streamed_core knife_short_range_0";
                                    meleeanimationend = 0.9f;
                                    anim = 2;
                                }

                                DEBUG_STEP = 31;
                                lastMeleeAnim = ourAnim;

                                if (
                                    !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, ourAnim.Split()[0],
                                        ourAnim.Split()[1],
                                        3))
                                {
                                    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(ourAnim.Split()[0]),
                                        ourAnim.Split()[1],
                                        8f, 1f, -1, 0, -8f, 1, 1, 1);
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
                            }
                            else
                            {
                                //Character.FreezePosition = false;
                                Character.Task.AimAt(AimCoords, -1);

                                var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
                                if (gunEnt != null)
                                {
                                    var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, -0.01f));
                                    var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash) CurrentWeapon);
                                    var speed = 0xbf800000;
                                    var weaponH = (WeaponHash) CurrentWeapon;
                                    if (weaponH == WeaponHash.RPG || weaponH == WeaponHash.HomingLauncher ||
                                        weaponH == WeaponHash.GrenadeLauncher || weaponH == WeaponHash.Firework)
                                        speed = 500;

                                    if (weaponH == WeaponHash.Minigun)
                                        weaponH = WeaponHash.CombatPDW;

                                    var dir = (AimCoords - start);
                                    dir.Normalize();
                                    var end = start + dir*100f;

                                    Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
                                        end.X,
                                        end.Y, end.Z, damage, true, (int) weaponH, Character, true, false, speed);

                                    _lastStart = start;
                                    _lastEnd = end;
                                }
                            }

                            var dirVector = Position - _lastPosition;

                            var target = Util.LinearVectorLerp(Position,
                                (Position) + dirVector,
                                (int) DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int) AverageLatency);

                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0,
                                0, 0, 0);
                        }

                        DEBUG_STEP = 32;
                        if (!IsAiming && !IsShooting && !IsJumping && !IsInMeleeCombat)
                        {
                            //Character.FreezePosition = true;
                            UpdatePlayerPedPos();

                            var ourAnim = GetMovementAnim(GetPedSpeed(Speed));
                            var animDict = GetAnimDictionary(ourAnim);
                            var secondaryAnimDict = GetSecondaryAnimDict();
                            DEBUG_STEP = 34;
                            if (
                                !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
                                    3))
                            {
                                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
                                    8f, 1f, -1, 0, -8f, 1, 1, 1);
                            }

                            if (secondaryAnimDict != null &&
                                !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, ourAnim,
                                    3))
                            {
                                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(secondaryAnimDict), ourAnim,
                                    8f, 1f, -1, 32 | 16, -8f, 1, 1, 1);
                            }
                        }
                    }
                }

                _lastJumping = IsJumping;
                _lastFreefall = IsFreefallingWithParachute;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
                _lastVehicle = IsInVehicle;
                DEBUG_STEP = 35;
            }
            catch (Exception ex)
            {
                Util.SafeNotify("Caught unhandled exception in PedThread for player " + Name);
                Util.SafeNotify(ex.Message);
                Util.SafeNotify("LAST STEP: " + DEBUG_STEP);

                LogManager.LogException(ex, "PEDTHREAD FOR " + Name + " LASTSTEP: " + DEBUG_STEP);
                //throw;
            }
        }

        public static Ped GetResponsiblePed(Vehicle veh)
        {
            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
            }

            return new Ped(0);
        }

        public string GetAnimDictionary(string ourAnim = "")
        {
            string dict = "move_m@generic";

            if (Character.Gender == Gender.Female)
                dict = "move_f@generic";

            dict = Character.IsInWater ? ourAnim == "idle" ? "swimming@base" : "swimming@swim" : dict;

            return dict;
        }

        private void UpdatePlayerPedPos()
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

                var posTarget = Util.LinearVectorLerp(Position, Position + dir,
                        (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds, (int)AverageLatency);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0, 0);

                /*

                var vdir = PedVelocity - _lastPedVel;
                var target = Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                    (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                    (int)AverageLatency);

                var posTarget = Util.LinearVectorLerp(Position, Position + dir,
                    (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                    (int)AverageLatency);

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
                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y,
                        posTarget.Z, 0, 0, 0, 0);
                }
                else
                {
                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,
                        Position.Z, 0, 0, 0, 0);
                }

                */
            }
            else if (syncMode == SynchronizationMode.EntityLerping)
            {
                var target = Util.LinearVectorLerp(_lastPosition, Position,
                    (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                    (int)AverageLatency);

                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z,
                    0, 0, 0, 0);
            }
            else if (syncMode == SynchronizationMode.Teleport)
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,
                    Position.Z, 0, 0, 0, 0);
            }

            DEBUG_STEP = 33;
            if (Main.LerpRotaion)
            {
                if ((Util.Denormalize(_lastRotation.Z) < 180f &&
                     Util.Denormalize(Rotation.Z) > 180f) ||
                    (Util.Denormalize(_lastRotation.Z) > 180f &&
                     Util.Denormalize(Rotation.Z) < 180f))
                    Character.Rotation = Rotation;
                else
                    Character.Rotation = Util.LinearVectorLerp(_lastRotation, Rotation,
                        (int)DateTime.Now.Subtract(LastUpdateReceived).TotalMilliseconds,
                        (int)AverageLatency);
            }
            else
            {
                Character.Rotation = Rotation;
            }
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
                return "move_weapon@rifle@generic";
            return GetAnimDictionary();
        }

        public int GetWeaponHandsHeld(int weapon)
        {
            if (weapon == unchecked((int) WeaponHash.Unarmed)) return 0;
            if (weapon == unchecked((int)WeaponHash.RPG) ||
                weapon == unchecked((int)WeaponHash.HomingLauncher) ||
                weapon == unchecked((int)WeaponHash.Firework))
                return 5;
            if (weapon == unchecked((int)WeaponHash.Minigun))
                return 5;
            if (weapon == unchecked((int)WeaponHash.GolfClub) ||
                weapon == unchecked((int)WeaponHash.Bat))
                return 4;
            if (weapon == unchecked((int) WeaponHash.Knife) || weapon == unchecked((int) WeaponHash.Nightstick) ||
                weapon == unchecked((int) WeaponHash.Hammer) || weapon == unchecked((int) WeaponHash.Crowbar) ||
                weapon == unchecked((int) WeaponHash.Dagger) || weapon == unchecked((int) WeaponHash.Hatchet) ||
                weapon == unchecked((int) WeaponHash.KnuckleDuster) || weapon == -581044007 || weapon == -102323637 || weapon == -538741184)
                return 3;
            if (weapon == -1357824103 || weapon == -1074790547 ||
                (weapon == 2132975508 || weapon == -2084633992) ||
                (weapon == -952879014 || weapon == 100416529) ||
                weapon == unchecked((int)WeaponHash.Gusenberg) ||
                weapon == unchecked((int)WeaponHash.MG) || weapon == unchecked((int)WeaponHash.CombatMG) ||
                weapon == unchecked((int)WeaponHash.CombatPDW) ||
                weapon == unchecked((int)WeaponHash.AssaultSMG) ||
                weapon == unchecked((int)WeaponHash.SMG) ||
                weapon == unchecked((int)WeaponHash.HeavySniper) ||
                weapon == unchecked((int)WeaponHash.PumpShotgun) ||
                weapon == unchecked((int)WeaponHash.HeavyShotgun) ||
                weapon == unchecked((int)WeaponHash.Musket) ||
                weapon == unchecked((int)WeaponHash.AssaultShotgun) ||
                weapon == unchecked((int)WeaponHash.BullpupShotgun) ||
                weapon == unchecked((int)WeaponHash.SawnOffShotgun))
                return 2;
            return 1;
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
            LogManager.DebugLog("CLEAR FOR " + Name);
            if (Character != null)
            {
                Character.Model.MarkAsNoLongerNeeded();
                Character.Delete();
            }
            if (_mainBlip != null && _mainBlip.Exists())
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
        public static bool DoesVehicleHaveParallelWeapon(VehicleHash model, bool rockets)
        {
            if (model == VehicleHash.Savage)
            {
                if (!rockets)
                    return false;
                else
                    return true;
            }
            else if (model == VehicleHash.Buzzard)
            {
                if (!rockets)
                    return true;
                else
                    return true;
            }
            else if (model == VehicleHash.Hydra)
            {
                if (!rockets)
                    return true;
                else return true;
            }
            else if (model == VehicleHash.Lazer)
            {
                if (!rockets)
                    return true;
                else return true;
            }

            if (model == VehicleHash.Valkyrie) return true;


            return false;
        }

        public static bool DoesVehiclesMuzzleDifferFromVehicleGunPos(VehicleHash model)
        {
            if (model == VehicleHash.Rhino || model == VehicleHash.Insurgent || model == VehicleHash.Limo2) return true;
            return false;
        }

        public static bool DoesVehicleSeatHaveMountedGuns(VehicleHash vehicle)
        {
            if (vehicle == VehicleHash.Savage || vehicle == VehicleHash.Buzzard || vehicle == VehicleHash.Annihilator ||
                vehicle == VehicleHash.Rhino || vehicle == VehicleHash.Hydra || vehicle == VehicleHash.Lazer ||
                vehicle == VehicleHash.Valkyrie)
            {
                return true;
            }
            return false;
        }

        public static Vector3 GetVehicleWeaponMuzzle(VehicleHash model, bool rockets)
        {
            if (model == VehicleHash.Savage)
            {
                if (!rockets)
                    return new Vector3(0f, 6.45f, -0.5f);
                else
                    return new Vector3(-2.799f, -0.599f, -0.15f);
            }
            else if (model == VehicleHash.Buzzard || model == VehicleHash.Annihilator)
            {
                if (!rockets)
                    return new Vector3(1.1f, 0.2f, -0.25f);
                else
                    return new Vector3(1.55f, 0.2f, -0.35f);
            }
            else if (model == VehicleHash.Hydra)
            {
                if (!rockets)
                    return new Vector3(0.4f, 1.6f, -1f);
                else return new Vector3(5.05f, -0.14f, -0.9f);
            }
            else if (model == VehicleHash.Lazer)
            {
                if (!rockets)
                    return new Vector3(0.75f, 3.19f, 0.4f);
                else return new Vector3(4.95f, 0.55f, 0.15f);
            }

            if (model == VehicleHash.Technical)
                return  new Vector3(0f, -1.359f, 1.799f);

            if (model == VehicleHash.Rhino)
            {
                if (!rockets) return new Vector3(0f, 0f, 1.369f);
                else if (rockets) return new Vector3(0, 1.699f, 0f);
            }

            if (model == VehicleHash.Insurgent)
            {
                if (!rockets) return new Vector3(0f, -0.6599f, 2.029f);
                else return new Vector3(0f, 0.5299f, 0f);
            }

            if (model == VehicleHash.Limo2)
            {
                if (!rockets) return new Vector3(0, -0.9199f, 1.2999f);
                else return new Vector3(0f, 0.5699f, 0f);
            }

            if (model == VehicleHash.Valkyrie || model == VehicleHash.Valkyrie2)
            {
                return new Vector3(1.5799f, -0.03f, 0.02f);
            }

            return new Vector3();
        }

        public static float GetVehicleTurretLength(VehicleHash veh)
        {
            if (veh == VehicleHash.Technical) return 1.8098f;
            if (veh == VehicleHash.Rhino) return 4.55014f;
            if (veh == VehicleHash.Insurgent) return 0.68f;
            if (veh == VehicleHash.Limo2) return 1.3198f;
            if (veh == VehicleHash.Valkyrie || veh == VehicleHash.Valkyrie2) return 0.86f;
            return 0f;
        }

        public static bool IsVehicleWeaponRocket(int hash)
        {
            switch (hash)
            {
                default:
                    return false;
                case 1186503822:
                case -494786007:
                case 1638077257:
                    return false;
                case -821520672:
                case -123497569:
                    return true;
            }
        }

        public static bool DoesVehicleSeatHaveGunPosition(VehicleHash vehicle, int vehiclepos, bool anySeat = false)
        {
            if (vehicle == VehicleHash.Rhino && (vehiclepos == -1 || anySeat)) return true;
            if (vehicle == VehicleHash.Insurgent && (vehiclepos == 7 || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie && (vehiclepos == (int) VehicleSeat.Passenger || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie && (vehiclepos == 1 || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie && (vehiclepos == 2 || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie2 && (vehiclepos == (int)VehicleSeat.Passenger || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie2 && (vehiclepos == 1 || anySeat)) return true;
            if (vehicle == VehicleHash.Valkyrie2 && (vehiclepos == 2 || anySeat)) return true;
            if (vehicle == VehicleHash.Technical && (vehiclepos == 1 || anySeat)) return true;
            if (vehicle == VehicleHash.Limo2 && (vehiclepos == 3 || anySeat)) return true;
            return false;
        }

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
                case (WeaponHash) 1649403952:
                    return 36;
                case (WeaponHash)4019527611:
                    return 165;
            }
        }
    }
}