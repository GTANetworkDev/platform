//#define DISABLE_SLERP
//#define DISABLE_UNDER_FLOOR_FIX
#define DISABLE_ROTATION_SIM

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Networking
{
    internal enum SynchronizationMode
    {
        Dynamic,
        DeadReckoning,
        Teleport,
        TeleportRudimentary,
    }

    internal class Animation
    {
        internal string Dictionary { get; set; }
        internal string Name { get; set; }
        internal bool Loop { get; set; }
    }

    internal class SyncPed : RemotePlayer
    {
        internal SynchronizationMode SyncMode;
        internal long Host;
        internal Ped Character;
        internal Vector3 _position;
        internal int VehicleNetHandle;
        internal Vector3 _rotation;
        internal bool _isInVehicle;
        internal bool IsJumping;
        internal Animation CurrentAnimation;
        internal int ModelHash;
        internal int CurrentWeapon;
        internal bool IsAiming;
        internal Vector3 AimCoords;

        internal SyncPed AimPlayer;

        internal float Latency;
        internal bool IsHornPressed;
        internal bool _isRagdoll;
        internal Vehicle MainVehicle { get; set; }
        internal bool IsInActionMode;
        internal bool IsInCover;
        internal bool IsInLowCover;
        internal bool IsOnLadder;
        internal bool IsVaulting;
        internal bool IsCoveringToLeft;
        internal bool IsInMeleeCombat;
        internal bool IsFreefallingWithParachute;
        internal bool IsShooting;
        internal bool IsInBurnout;
        private bool _lastBurnout;
        private bool _lastSwimming;
        internal float VehicleRPM;
	    internal float SteeringScale;
        internal bool EnteringVehicle;
        private bool _lastEnteringVehicle;
        internal bool IsOnFire;
        private bool _lastFire;
        internal bool IsBeingControlledByScript;

        internal bool ExitingVehicle;
        private bool _lastExitingVehicle;

        internal int VehicleSeat;
        internal int PedHealth;

        internal float VehicleHealth;

        internal int VehicleHash
        {
            get
            {
                if (!Debug)
                {
                    if (VehicleNetHandle == 0) return 0;
                    var car = Main.NetEntityHandler.NetToStreamedItem(VehicleNetHandle) as RemoteVehicle;
                    return car.ModelHash;
                }

                return Character.CurrentVehicle?.Model.Hash ?? 0;
            }
        }

        internal Vector3 _vehicleRotation;
        internal int VehiclePrimaryColor;
        internal int VehicleSecondaryColor;
        internal bool Siren;
        internal int PedArmor;
        internal bool IsVehDead;
        internal bool IsPlayerDead;
        internal bool DirtyWeapons;

        private object _secondSnapshot;
        private object _firstSnapshot;

        private int _secondSnapshotTime;
        private int _firstSnapshotTime;

        internal object Snapshot
        {
            get { return _firstSnapshot; }
            set
            {
                _secondSnapshot = _firstSnapshot;
                _firstSnapshot = value;

                _secondSnapshotTime = _firstSnapshotTime;
                _firstSnapshotTime = Environment.TickCount;
            }
        }


        internal bool IsSpectating;

        internal bool Debug;
        
        private DateTime _stopTime;
        internal float Speed
        {
            get { return _speed; }
            set
            {
                _lastSpeed = _speed;
                _speed = value;
            }
        }

        internal byte OnFootSpeed;

        internal bool IsParachuteOpen;

        internal double AverageLatency
        {
            get { return _latencyAverager.Count == 0 ? 0 : _latencyAverager.Average(); }
        }

        internal long LastUpdateReceived
        {
            get { return _lastUpdateReceived; }
            set
            {
                if (_lastUpdateReceived != 0)
                {
                    _latencyAverager.Enqueue(value -_lastUpdateReceived);
                    if (_latencyAverager.Count >= 10)
                        _latencyAverager.Dequeue();
                }

                _lastUpdateReceived = value;
            }
        }

        internal long TicksSinceLastUpdate
        {
            get { return Util.Util.TickCount - LastUpdateReceived; }
        }

        internal int DataLatency
        {
            get
            {
                if (Debug) return Main._debugInterval;
                return (int)(((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2));
            }
        }

        internal Dictionary<int, int> VehicleMods
        {
            get { return _vehicleMods; }
            set
            {
                if (value == null) return;
                _vehicleMods = value;
            }
        }
        private Vector3 _carPosOnUpdate;
        /*
        private Vector3? _lastVehiclePos;
        internal Vector3 VehiclePosition
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
        */
        private Vector3 _lastVehVel;
        internal Vector3 VehicleVelocity
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
        internal Vector3 PedVelocity
        {
            get { return _pedVelocity; }
            set
            {
                _lastPedVel = _pedVelocity;
                _pedVelocity = value;
            }
        }

        private Vector3? _lastPosition;
        internal new Vector3 Position
        {
            get { return _position; }
            set
            {
                _lastPosition = _position;
                _position = value;
            }
        }

        private Vector3? _lastVehicleRotation;
        internal Vector3 VehicleRotation
        {
            get { return _vehicleRotation; }
            set
            {
                _lastVehicleRotation = _vehicleRotation;
                _vehicleRotation = value;
            }
        }

        private Vector3? _lastRotation;
        internal new Vector3 Rotation
        {
            get { return _rotation; }
            set
            {
                _lastRotation = _rotation;
                _rotation = value;
            }
        }

        internal bool IsRagdoll
        {
            get { return _isRagdoll; }
            set { _isRagdoll = value; }
        }

        internal int DEBUG_STEP
        {
            get { return DEBUG_STEP_backend; }
            set
            {
                DEBUG_STEP_backend = value;
                LogManager.DebugLog("NEXTSTEP FOR " + Name + ": " + value);

                if (Main.SlowDownClientForDebug)
                    GTA.UI.Screen.ShowSubtitle(Name + "-sp" + value.ToString());
            }
        }

        private bool _lastVehicle;
        private bool _lastAiming;
        private float _lastSpeed;
        private bool _lastShooting;
        private bool _lastJumping;
        private bool _blip;
        private bool _justEnteredVeh;
        private bool _playingGetupAnim;
        private DateTime _lastHornPress = DateTime.Now;
        private DateTime? _spazzout_prevention;
        
        private DateTime _enterVehicleStarted;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        //private Vector3 _vehiclePosition;
        private bool _lastVehicleShooting;

        private Queue<long> _latencyAverager;

        private Vector3 _lastStart;
        private Vector3 _lastEnd;

        private bool _lastReloading;
        internal bool IsReloading
        {
            get { return _isReloading; }
            set
            {
                _lastReloading = _isReloading;
                _isReloading = value;
            }
        }

        private int _playerSeat;
        private bool _lastDrivebyShooting;
        private bool _isStreamedIn;
        private Blip _mainBlip;
        private bool _lastHorn;
        private Prop _parachuteProp;
        private bool _leftSide;

        internal SyncPed(int hash, Vector3 pos, Vector3 rot, bool blip = true)
        {
            _position = pos;
            _rotation = rot;
            ModelHash = hash;
            _blip = blip;
            
            _latencyAverager = new Queue<long>();
        }

        internal SyncPed()
        {
            _blip = true;
            _latencyAverager = new Queue<long>();
        }

        public override int LocalHandle
        {
            get { return Character?.Handle ?? 0; }
            set { }
        }

        internal bool IsInVehicle
        {
            get { return _isInVehicle; }
            set
            {
                if (value ^ _isInVehicle)
                {
                    _spazzout_prevention = DateTime.Now;
                }


                _isInVehicle = value; 
            }
        }

        internal void SetBlipNameFromTextFile(Blip blip, string text)
        {
            Function.Call((Hash)0xF9113A30DE5C6670, "STRING");
            Function.Call((Hash)0x6C188BE134E074AA, text); //_ADD_TEXT_COMPONENT_STRING
            Function.Call((Hash)0xBC38B49BCB83BC9B, blip);
        }

        private int _modSwitch = 0;
        private int _clothSwitch = 0;
        private long _lastUpdateReceived;
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
        private int _scriptFire;

        internal bool IsCustomScenarioPlaying;
        internal bool HasCustomScenarioStarted;
        internal bool IsCustomAnimationPlaying;
        internal string CustomAnimationDictionary;
        internal string CustomAnimationName;
        internal int CustomAnimationFlag;
        internal long CustomAnimationStartTime;

        private bool IsOnScreenVisibleAndRange;

        #region NeoSyncPed

        internal bool CreateCharacter()
        {
            float hRange = _isInVehicle ? 150f : 200f;
            var gPos = Position;
            var inRange = Game.Player.Character.IsInRangeOfEx(gPos, hRange);

            return CreateCharacter(gPos, inRange);
        }

        bool CreateCharacter(Vector3 gPos, bool InRange)
        {

            if ((Character == null || !Character.Exists()) || (Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0)))
            {
                LogManager.DebugLog($"{Character == null}, {Character?.Exists()}, {Character?.Position} {gPos}, {Character?.Model.Hash}, {ModelHash}, {Character?.IsDead}, {PedHealth}");

                if (Character != null && Character.Exists())
                {
                    LogManager.DebugLog("DELETING CHARACTER");
                    Character.Delete();
                }

                 DEBUG_STEP = 3;

				LogManager.DebugLog("NEW PLAYER " + Name);

				var charModel = new Model(ModelHash);

				LogManager.DebugLog("REQUESTING MODEL FOR " + Name);

				Util.Util.LoadModel(charModel);

				LogManager.DebugLog("CREATING PED FOR " + Name);

			    Character = World.CreatePed(charModel, gPos, _rotation.Z, 26);
				charModel.MarkAsNoLongerNeeded();

				if (Character == null) return true;

                lock (Main.NetEntityHandler.ClientMap) Main.NetEntityHandler.HandleMap.Set(RemoteHandle, Character.Handle);

			    Character.CanBeTargetted = true;


				DEBUG_STEP = 4;

				Character.BlockPermanentEvents = true;
                Function.Call(Hash.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, Character, true);
				Character.IsInvincible = true;
				Character.CanRagdoll = false;

			    if (Team == -1 || Team != Main.LocalTeam)
			    {
			        Character.RelationshipGroup = Main.RelGroup;
                    Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH, Character, Main.RelGroup);
			    }
			    else
			    {
			        Character.RelationshipGroup = Main.FriendRelGroup;
                    Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH, Character, Main.FriendRelGroup);
                }

				LogManager.DebugLog("SETTINGS FIRING PATTERN " + Name);

				Character.FiringPattern = FiringPattern.FullAuto;

				Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Character); //BUG: <- Maybe causes crash?

                Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, Character, false);
                Function.Call(Hash.SET_PED_DROPS_WEAPONS_WHEN_DEAD, Character, false);
                
                Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, Character, true);
                Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, Character, Game.Player, true);
                Function.Call(Hash.SET_PED_GET_OUT_UPSIDE_DOWN_VEHICLE, Character, false);
                Function.Call(Hash.SET_PED_AS_ENEMY, Character, false);
                Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Character, true, true);
                
			    if (Alpha < 255) Character.Opacity = Alpha;

                LogManager.DebugLog("SETTING CLOTHES FOR " + Name);

				if (Props != null)
					foreach (var pair in Props)
					{
						Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value, Textures[pair.Key], 2);
					}

			    if (Accessories != null)
			    {
			        foreach (var pair in Accessories)
			        {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value.Item1, pair.Value.Item2, 2);
                    }
			    }

                Main.NetEntityHandler.ReattachAllEntities(this, false);

			    foreach (var source in Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemoteParticle && ((RemoteParticle) item).EntityAttached == RemoteHandle).Cast<RemoteParticle>())
			    {
			        Main.NetEntityHandler.StreamOut(source);
                    Main.NetEntityHandler.StreamIn(source);
			    }

                if (PacketOptimization.CheckBit(Flag, EntityFlag.Collisionless))
                {
                    Character.IsCollisionEnabled = false;
                }

                JavascriptHook.InvokeStreamInEvent(new LocalHandle(Character.Handle), (int)GTANetworkShared.EntityType.Player);

                LogManager.DebugLog("ATTACHING BLIP FOR " + Name);

                /*
				if (_blip)
				{
					Character.AttachBlip();

					if (Character.AttachedBlip == null || !Character.AttachedBlip.Exists()) return true;

					LogManager.DebugLog("SETTING BLIP COLOR FOR" + Name);

                    if (BlipSprite != -1)
                        Character.AttachedBlip.Sprite = (BlipSprite)BlipSprite;

                    if (BlipColor != -1)
						Character.AttachedBlip.Color = (BlipColor)BlipColor;
					else
						Character.AttachedBlip.Color = GTA.BlipColor.White;

					LogManager.DebugLog("SETTING BLIP SCALE FOR" + Name);

					Character.AttachedBlip.Scale = 0.8f;

					LogManager.DebugLog("SETTING BLIP NAME FOR" + Name);

					SetBlipNameFromTextFile(Character.AttachedBlip, Name);

					
					Character.AttachedBlip.Alpha = BlipAlpha;

					LogManager.DebugLog("BLIP DONE FOR" + Name);
				}
                */

				return true;
			}
		    return false;
	    }

        void DrawNametag()
        {
            if (!Main.UIVisible) return;

            if ((NametagSettings & 1) != 0) return;
           
            if (((Character.IsInRangeOfEx(Game.Player.Character.Position, 25f))) || Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, Character)) //Natives can slow down
            {
                if (Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, Game.Player.Character, Character, 17)) //Natives can slow down
                {

                    Vector3 targetPos;
                    targetPos = Character.GetBoneCoord(Bone.IK_Head) + new Vector3(0, 0, 0.5f);

                    targetPos += Character.Velocity / Game.FPS;

                    Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);
                    DEBUG_STEP = 6;
                    var nameText = Name == null ? "<nameless>" : Name;

                    if (!string.IsNullOrEmpty(NametagText))
                        nameText = NametagText;

                    if (TicksSinceLastUpdate > 10000)
                        nameText = "~r~AFK~w~~n~" + nameText;

                    var dist = (GameplayCamera.Position - Character.Position).Length();
                    var sizeOffset = Math.Max(1f - (dist / 30f), 0.3f);

                    Color defaultColor = Color.FromArgb(245, 245, 245);

                    if ((NametagSettings & 2) != 0)
                    {
                        byte r, g, b, a;

                        Util.Util.ToArgb(NametagSettings >> 8, out a, out r, out g, out b);

                        defaultColor = Color.FromArgb(r, g, b);
                    }

                    Util.Util.DrawText(nameText, 0, 0, 0.4f * sizeOffset, defaultColor.R, defaultColor.G, defaultColor.B, 255, 0, 1, false, true, 0);

                    DEBUG_STEP = 7;
                    if (Character != null)
                    {
                        var armorColor = Color.FromArgb(200, 220, 220, 220);
                        var bgColor = Color.FromArgb(100, 0, 0, 0);
                        var armorPercent = Math.Min(Math.Max(PedArmor / 100f, 0f), 1f);
                        var armorBar = Math.Round(150 * armorPercent);
                        armorBar = (armorBar * sizeOffset);

                    //Less latency with rectangles disabled
                        Util.Util.DrawRectangle(-75 * sizeOffset, 36 * sizeOffset, armorBar, 20 * sizeOffset, armorColor.R, armorColor.G,
                            armorColor.B, armorColor.A);
                        Util.Util.DrawRectangle(-75 * sizeOffset + armorBar, 36 * sizeOffset, (sizeOffset * 150) - armorBar, sizeOffset * 20,
                            bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                        Util.Util.DrawRectangle(-71 * sizeOffset, 40 * sizeOffset, (142 * Math.Min(Math.Max((PedHealth / 100f), 0f), 1f)) * sizeOffset, 12 * sizeOffset,
                            50, 250, 50, 150);
                    }
                    DEBUG_STEP = 8;
                    Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                }
            }
        }

        internal int _debugVehicleHash;
	    bool CreateVehicle()
	    {
	        if (_isInVehicle && MainVehicle != null && Character.IsInVehicle(MainVehicle) && Game.Player.Character.IsInVehicle(MainVehicle) && VehicleSeat == -1 && 
                Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER, Game.Player.Character) == -1 &&
	            Util.Util.GetPedSeat(Game.Player.Character) == 0)
	        {
	            Character.Task.WarpOutOfVehicle(MainVehicle);
                Game.Player.Character.Task.WarpIntoVehicle(MainVehicle, GTA.VehicleSeat.Driver);
	            Main.LastCarEnter = DateTime.Now;
                Script.Yield();
	            return true;
	        }

	        var createVehicle = (!_lastVehicle && _isInVehicle) ||
	                            (_lastVehicle && _isInVehicle &&
	                             (MainVehicle == null ||
	                              (!Character.IsInVehicle(MainVehicle) &&
	                               Game.Player.Character.VehicleTryingToEnter != MainVehicle) ||
	                              (VehicleSeat != Util.Util.GetPedSeat(Character) &&
	                               Game.Player.Character.VehicleTryingToEnter != MainVehicle)));

	        if (!Debug && MainVehicle != null)
	        {
	            createVehicle = createVehicle || Main.NetEntityHandler.EntityToNet(MainVehicle.Handle) != VehicleNetHandle;
	        }

            if (createVehicle)
			{
			    if (Debug)
			    {
			        if (MainVehicle != null) MainVehicle.Delete();
			        MainVehicle = World.CreateVehicle(new Model(_debugVehicleHash), Position, VehicleRotation.Z);
			        //MainVehicle.HasCollision = false;
			    }
			    else
			    {
			        MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);
			    }
				DEBUG_STEP = 10;

			    if (MainVehicle == null || MainVehicle.Handle == 0)
			    {
			        Character.Position = Position;
			        return true;
			    }
                

                if (Game.Player.Character.IsInVehicle(MainVehicle) &&
					VehicleSeat == Util.Util.GetPedSeat(Game.Player.Character))
				{
				    if (DateTime.Now.Subtract(Main.LastCarEnter).TotalMilliseconds < 1000)
				    {
				        return true;
				    }

					Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
					Util.Util.SafeNotify("~r~Car jacked!");
				}
				DEBUG_STEP = 11;

				if (MainVehicle != null && MainVehicle.Handle != 0)
				{
                    /*
				    if (VehicleSeat == -1)
				    {
				        //MainVehicle.Position = VehiclePosition;
				    }
				    else
				    {
				        Character.PositionNoOffset = MainVehicle.Position;
				    }*/
                    Character.PositionNoOffset = MainVehicle.Position;

                    MainVehicle.IsEngineRunning = true;
                    MainVehicle.IsInvincible = true;
                    Character.SetIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);
                    DEBUG_STEP = 12;
				}
				DEBUG_STEP = 13;
				_lastVehicle = true;
				_justEnteredVeh = true;
				_enterVehicleStarted = DateTime.Now;
				return true;
			}
		    return false;
	    }

	    bool UpdatePlayerPosOutOfRange(Vector3 gPos, bool inRange)
	    {
			if (!inRange)
			{
			    var delta = Util.Util.TickCount - LastUpdateReceived;
                if (delta < 10000)
				{
				    Vector3 lastPos = _lastPosition == null ? Position : _lastPosition.Value;

				    if (!_isInVehicle)
				    {
				        Character.PositionNoOffset = Vector3.Lerp(lastPos, gPos, Math.Min(1f, delta / 1000f));
				    }
					else if (MainVehicle != null && GetResponsiblePed(MainVehicle).Handle == Character.Handle)
					{
					    MainVehicle.PositionNoOffset = Vector3.Lerp(lastPos, gPos, Math.Min(1f, delta / 1000f));
                        #if !DISABLE_ROTATION_SIM
                        if (_lastVehiclePos != null)
                            MainVehicle.Quaternion = Main.DirectionToRotation(_lastVehiclePos.Value - gPos).ToQuaternion();
                        #endif
					}
                }
				return true;
			}
		    return false;
	    }

	    void WorkaroundBlip()
	    {
            if (_isInVehicle && MainVehicle != null && _blip  && (Character.GetOffsetInWorldCoords(new Vector3()) - MainVehicle.Position).Length() > 70f)
			{
				LogManager.DebugLog("Blip was too far away -- deleting");
				Character.Delete();
			}
		}

	    bool UpdatePosition()
	    {
            return _isInVehicle ? UpdateVehiclePosition() : UpdateOnFootPosition();
	    }

	    void UpdateVehicleInternalInfo()
	    {
	        if (MainVehicle.MemoryAddress == IntPtr.Zero) return;

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
            /*
			if (VehicleMods != null && _modSwitch % 50 == 0 &&
				Game.Player.Character.IsInRangeOfEx(Position, 30f))
			{
				var id = _modSwitch / 50;

				if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod(id))
				{
					Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
					MainVehicle.SetMod(id, VehicleMods[id], false);
					Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
				}
			}
			_modSwitch++;
            
			if (_modSwitch >= 2500)
				_modSwitch = 0;
                */
	        Function.Call(Hash.USE_SIREN_AS_HORN, MainVehicle, Siren); // No difference?

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

	        if (IsInBurnout && !_lastBurnout)
	        {
	            Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, true);
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Character, MainVehicle, 23, 120000); // 30 - burnout
            }

	        if (!IsInBurnout && _lastBurnout)
	        {
                Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, false);
                Character.Task.ClearAll();
            }

	        _lastBurnout = IsInBurnout;

            Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, MainVehicle, Speed > 0.2 && _lastSpeed > Speed);

            DEBUG_STEP = 18;

			if (MainVehicle.SirenActive && !Siren)
				MainVehicle.SirenActive = Siren;
			else if (!MainVehicle.SirenActive && Siren)
				MainVehicle.SirenActive = Siren;

			MainVehicle.CurrentRPM = VehicleRPM;
		    MainVehicle.SteeringAngle = Util.Util.ToRadians(SteeringScale);
	    }

        struct interpolation
        {
            internal Vector3 vecStart;
            internal Vector3 vecTarget;
            internal Vector3 vecError;
            internal long StartTime;
            internal long FinishTime;
            internal float LastAlpha;
        }

        private interpolation currentInterop = new interpolation();

        internal void StartInterpolation()
        {
            currentInterop = new interpolation();

            if (_isInVehicle)
            {
                if (_lastPosition == null) return;
                if (Main.VehicleLagCompensation)
                {

                    var dir = Position - _lastPosition.Value;
                    currentInterop.vecTarget = Position + dir;
                    currentInterop.vecError = dir;
                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
                else
                {
                    var dir = Position - _lastPosition.Value;
                    currentInterop.vecTarget = Position;
                    currentInterop.vecError = dir;
                    currentInterop.vecError *= Util.Util.Lerp(0.25f, Util.Util.Unlerp(100, 100, 400), 1f);
                }

                if (MainVehicle != null)
                    currentInterop.vecStart = MainVehicle.Position;
            }
            else
            {
                if (Main.OnFootLagCompensation)
                {
                    var dir = Position - _lastPosition;
                    currentInterop.vecTarget = Position; // + dir;
                    currentInterop.vecError = dir ?? new Vector3();
                    currentInterop.vecStart = Position;

                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
                else
                {
                    var dir = Position - _lastPosition;

                    currentInterop.vecTarget = Position;
                    currentInterop.vecError = dir ?? new Vector3();
                    currentInterop.vecError *= Util.Util.Lerp(0.25f, Util.Util.Unlerp(100, 100, 400), 1f);
                }

                if (Character != null)
                    currentInterop.vecStart = Character.Position;
            }

            currentInterop.StartTime = Util.Util.TickCount - DataLatency;
            currentInterop.FinishTime = currentInterop.StartTime + 100;
            currentInterop.LastAlpha = 0f;
        }

        private void VMultiVehiclePos()
        {

            bool isInRange = Game.Player.Character.IsInRangeOfEx(Position, Main.VehicleStreamingRange);

            if (isInRange)
            {
                Vector3 vecDif = Position - currentInterop.vecStart; // Différence entre les deux positions (nouvelle & voiture) fin de connaitre la direction
                float force = 1.20f + (float)Math.Sqrt(_latencyAverager.Average() / 2500) + (Speed / 250); // Calcul pour connaitre la force à appliquer à partir du ping & de la vitesse
                float forceVelo = 1.05f + (float)Math.Sqrt(_latencyAverager.Average() / 5000) + (Speed / 750); // calcul de la force à appliquer au vecteur

                if (MainVehicle.Velocity.Length() > VehicleVelocity.Length()) 
                {
                    MainVehicle.Velocity = VehicleVelocity * forceVelo + (vecDif * (force + 0.15f)); // Calcul
                }
                else
                {
                    MainVehicle.Velocity = VehicleVelocity * (forceVelo - 0.25f) + (vecDif * (force)); // Calcul
                }
                StuckVehicleCheck(Position);
            }
            else
            {
                MainVehicle.PositionNoOffset = currentInterop.vecTarget;
                //MainVehicle.Velocity = VehicleVelocity;
            }

            if (isInRange && _lastVehicleRotation != null && (_lastVehicleRotation.Value - _vehicleRotation).LengthSquared() > 1f /* && spazzout */)
            {
                MainVehicle.Quaternion = GTA.Math.Quaternion.Slerp(_lastVehicleRotation.Value.ToQuaternion(),
                    _vehicleRotation.ToQuaternion(),
                    Math.Min(1.5f, TicksSinceLastUpdate / (float)AverageLatency));
            }
            else
            {
                MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
            }
        }

        private void StuckVehicleCheck(Vector3 newPos)
        {
#if !DISABLE_UNDER_FLOOR_FIX

            const int VEHICLE_INTERPOLATION_WARP_THRESHOLD = 15;
            const int VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 10;

            float fThreshold = (VEHICLE_INTERPOLATION_WARP_THRESHOLD + VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * Speed);

            if (MainVehicle.Position.DistanceToSquared(currentInterop.vecTarget) > fThreshold * fThreshold)
            {
                // Abort all interpolation
                currentInterop.FinishTime = 0;
                MainVehicle.PositionNoOffset = currentInterop.vecTarget;
            }

            // Check if we're under floor
            bool bForceLocalZ = false;
            bool bValidVelocityZ = true;
            if (bValidVelocityZ /* && Check whether its not a plane or helicopter*/)
            {
                // If remote z higher by too much and remote not doing any z movement, warp local z coord
                float fDeltaZ = newPos.Z - MainVehicle.Position.Z;

                if (fDeltaZ > 0.4f && fDeltaZ < 10.0f)
                {
                    if (Math.Abs(VehicleVelocity.Z) < 0.01f)
                    {
                        bForceLocalZ = true;
                    }
                }
            }

            // Only force z coord if needed for at least two consecutive calls
            if (!bForceLocalZ)
                m_uiForceLocalZCounter = 0;
            else
            if (m_uiForceLocalZCounter++ > 1)
            {
                var t = new Vector3(MainVehicle.Position.X, MainVehicle.Position.Y, newPos.Z);
                MainVehicle.PositionNoOffset = t;
                currentInterop.FinishTime = 0;
            }
#endif
        }

        private int m_uiForceLocalZCounter;
        void DisplayVehiclePosition()
        {
            var spazzout = (_spazzout_prevention != null &&
                            DateTime.Now.Subtract(_spazzout_prevention.Value).TotalMilliseconds > 200);

            if ((Speed > 0.2f || IsInBurnout) && currentInterop.FinishTime > 0 && _lastPosition != null && spazzout)
            {
                /*
                Vector3 newPos;

                if (Main.VehicleLagCompensation)
                {
                    long currentTime = Util.Util.TickCount;
                    float alpha = Util.Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                    Vector3 comp = Util.Util.Lerp(new Vector3(), alpha, currentInterop.vecError);
                    newPos = VehiclePosition + comp;
                    int forceMultiplier = 3;

                    if (Game.Player.Character.IsInVehicle() &&
                        MainVehicle.IsTouching(Game.Player.Character.CurrentVehicle))
                    {
                        forceMultiplier = 1;
                    }

                    if (Game.Player.Character.IsInRangeOfEx(newPos, physicsRange))
                    {
                        MainVehicle.Velocity = VehicleVelocity + forceMultiplier*(newPos - MainVehicle.Position);
                    }
                    else
                    {
                        MainVehicle.PositionNoOffset = newPos;
                    }
                }
                else
                {
                    var dataLatency = DataLatency + TicksSinceLastUpdate;
                    newPos = VehiclePosition + VehicleVelocity * dataLatency / 1000;
                    MainVehicle.Velocity = VehicleVelocity + 2 * (newPos - MainVehicle.Position);
                }

                if (Debug)
                {
                    World.DrawMarker(MarkerType.DebugSphere, MainVehicle.Position, new Vector3(), new Vector3(),
                        new Vector3(1, 1, 1), Color.FromArgb(100, 255, 0, 0));
                    if (Game.Player.Character.IsInVehicle())
                        World.DrawMarker(MarkerType.DebugSphere, Game.Player.Character.CurrentVehicle.Position,
                            new Vector3(), new Vector3(),
                            new Vector3(1, 1, 1), Color.FromArgb(100, 0, 255, 0));
                    World.DrawMarker(MarkerType.DebugSphere, newPos, new Vector3(), new Vector3(),
                        new Vector3(1, 1, 1), Color.FromArgb(100, 0, 0, 255));
                }

                // Check if we're too far

                StuckVehicleCheck(newPos);

                //GTA.UI.Screen.ShowSubtitle("alpha: " + alpha);

                //MainVehicle.Alpha = 100;
                */

                VMultiVehiclePos();

                _stopTime = DateTime.Now;
                _carPosOnUpdate = MainVehicle.Position;
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && _lastPosition != null && spazzout && currentInterop.FinishTime > 0)
            {
                var dir = Position - _lastPosition.Value;
                var posTarget = Util.Util.LinearVectorLerp(_carPosOnUpdate, Position + dir,
                    (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0, 0);
            }
            else
            {
                MainVehicle.PositionNoOffset = Position;
            }

            DEBUG_STEP = 21;
#if !DISABLE_SLERP

            if (_lastVehicleRotation != null && (_lastVehicleRotation.Value - _vehicleRotation).LengthSquared() > 1f && spazzout)
            {
                MainVehicle.Quaternion = GTA.Math.Quaternion.Slerp(_lastVehicleRotation.Value.ToQuaternion(),
                    _vehicleRotation.ToQuaternion(),
                    Math.Min(1.5f, TicksSinceLastUpdate / (float)AverageLatency));
            }
            else
            {
                MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
            }
#else
            MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
#endif

        }

        internal bool IsFriend()
        {
            return (Team != -1 && Team == Main.LocalTeam);
        }

	    bool DisplayVehicleDriveBy()
	    {
            if (IsShooting && CurrentWeapon != 0 && VehicleSeat == -1 && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)VehicleHash))
			{
				var isRocket = WeaponDataProvider.IsVehicleWeaponRocket(CurrentWeapon);
				if (isRocket && DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds < 1500)
				{
					return true;
				}
				if (isRocket)
					_lastRocketshot = DateTime.Now;
				var isParallel =
					WeaponDataProvider.DoesVehicleHaveParallelWeapon(unchecked((VehicleHash)VehicleHash),
						isRocket);

				var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle(unchecked((VehicleHash)VehicleHash), isRocket);

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
					speed = 0xbf800000;
				else if ((VehicleHash) VehicleHash == GTA.VehicleHash.Savage ||
				         (VehicleHash) VehicleHash == GTA.VehicleHash.Hydra ||
				         (VehicleHash) VehicleHash == GTA.VehicleHash.Lazer)
				    hash = unchecked((int) WeaponHash.Railgun);
                else
					hash = unchecked((int)WeaponHash.CombatPDW);

			    int damage = IsFriend() ? 0 : 75;

				Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X,
						end.Y, end.Z, damage, true, hash, Character, true, false, speed);
			}

		    return false;
	    }

		bool UpdateVehicleMainData()
		{
            UpdateVehicleInternalInfo();	
			DEBUG_STEP = 19;

			DisplayVehiclePosition();

		    return false;
		}

	    void UpdateVehicleMountedWeapon()
	    {
            if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)VehicleHash, VehicleSeat))
            {
                var delay = 30;
                //if ((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Rhino) delay = 300;

				if (Game.GameTime - _lastVehicleAimUpdate > delay)
				{
					Function.Call(Hash.TASK_VEHICLE_AIM_AT_COORD, Character, AimCoords.X, AimCoords.Y,
						AimCoords.Z);
					_lastVehicleAimUpdate = Game.GameTime;
				}

                if (IsShooting)
                {
					if (((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino &&
						 DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 1000) ||
						((VehicleHash)VehicleHash != GTA.VehicleHash.Rhino))
					{
						_lastRocketshot = DateTime.Now;

						var baseTurretPos =
							MainVehicle.GetOffsetInWorldCoords(
								WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash, false));
						var doesBaseTurretDiffer =
							WeaponDataProvider.DoesVehiclesMuzzleDifferFromVehicleGunPos(
								(VehicleHash)VehicleHash);
						var barrellLength = WeaponDataProvider.GetVehicleTurretLength((VehicleHash)VehicleHash);

						var speed = 0xbf800000;
						var hash = WeaponHash.CombatPDW;
						if ((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino)
						{
						    hash = (WeaponHash) 1945616459;
						}

						Vector3 tPos = baseTurretPos;
						if (
							WeaponDataProvider.DoesVehicleHaveParallelWeapon((VehicleHash)VehicleHash, false) &&
							VehicleSeat == 1)
						{
							var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash,
								false);
							tPos =
								MainVehicle.GetOffsetInWorldCoords(new Vector3(muzzle.X * -1f, muzzle.Y, muzzle.Z));
						}

						if (doesBaseTurretDiffer)
						{
							var kekDir = (AimCoords - tPos);
							kekDir.Normalize();
							var rot = Main.DirectionToRotation(kekDir);
							var newDir = Main.RotationToDirection(new Vector3(0, 0, rot.Z));
							newDir.Normalize();
							tPos = tPos +
								   newDir *
								   WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash, true)
									   .Length();
						}


						var turretDir = (AimCoords - tPos);
						turretDir.Normalize();
						var start = tPos + turretDir * barrellLength;
						var end = start + turretDir * 100f;

						_lastStart = start;
						_lastEnd = end;

						var damage = WeaponDataProvider.GetWeaponDamage(WeaponHash.Minigun);
						if ((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino)
							damage = 210;

					    if (IsFriend())
					        damage = 0;

						Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X,
							end.Y, end.Z, damage, true, (int)hash, Character, true, false, speed);
					}
				}
			}
			else if (!WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)VehicleHash) || VehicleSeat != -1)
			{
				if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon)
				{
					//Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, 999, true, true);
					//Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);
					//Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
				    //Character.Weapons.Select((WeaponHash) CurrentWeapon);
                    Character.Weapons.RemoveAll();
                    Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
                }

				if (IsShooting || IsAiming)
				{
					if (!_lastDrivebyShooting)
				    {
                        Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, Character, false, false, false, false);

                        Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z,
				            0, 0, 0, unchecked((int) FiringPattern.SingleShot));
				    }
				    else
				    {
                        Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, Character, true, false, false, false);

                        Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z);
				    }

				    var rightSide = (VehicleSeat + 2)%2 == 0;

				    if (WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
				    {
                        const string rightDict = "veh@driveby@first_person@passenger_right_handed@throw";
				        const string leftDict = "veh@driveby@first_person@driver@throw";

				        string drivebyDict = rightSide ? rightDict : leftDict;

                        Function.Call(Hash.TASK_PLAY_ANIM_ADVANCED, Character, Util.Util.LoadDict(drivebyDict),
                            "sweep_low", Character.Position.X, Character.Position.Y, Character.Position.Z, Character.Rotation.X,
                            Character.Rotation.Y, Character.Rotation.Z, -8f, -8f, -1, 0, rightSide ? 0.6f : 0.3f, 0, 0);
                    }

                    if (IsShooting)
                    {
                        Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);
                        Function.Call(Hash.SET_PED_AMMO, Character, CurrentWeapon, 10);


                        if (AimPlayer != null && AimPlayer.Position != null)
                        {
                            AimCoords = AimPlayer.Position;
                            AimPlayer = null;
                        }

                        if (!WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
                        {
                            Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, Character, AimCoords.X, AimCoords.Y, AimCoords.Z,
                                true);
                        }
                        else if (DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 500)
                        {
                            _lastRocketshot = DateTime.Now;

                            var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash)CurrentWeapon);
                            var speed = 0xbf800000;
                            var weaponH = (WeaponHash)CurrentWeapon;

                            if (IsFriend())
                                damage = 0;

                            var start = Character.GetBoneCoord(rightSide ? Bone.SKEL_R_Hand : Bone.SKEL_L_Hand);
                            var end = AimCoords;

                            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
                                end.X,
                                end.Y, end.Z, damage, true, (int)weaponH, Character, false, true, speed);
                        }
                    }

                    _lastVehicleAimUpdate = Game.GameTime;
					_lastDrivebyShooting = IsShooting || IsAiming;

                    if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, Game.Player.Character, Character, true))
                    {

                        int boneHit = -1;
                        var boneHitArg = new OutputArgument();

                        if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, Game.Player.Character, boneHitArg))
                        {
                            boneHit = boneHitArg.GetResult<int>();
                        }

                        LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                        JavascriptHook.InvokeCustomEvent(api =>
                            api.invokeonLocalPlayerDamaged(them, CurrentWeapon, boneHit/*, playerHealth, playerArmor*/));
                    }

                    Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                    Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Game.Player.Character);
                }

				if (!IsShooting && !IsAiming && _lastDrivebyShooting && Game.GameTime - _lastVehicleAimUpdate > 200)
				{
					Character.Task.ClearAll();
					Character.Task.ClearSecondary();
					Function.Call(Hash.CLEAR_DRIVEBY_TASK_UNDERNEATH_DRIVING_TASK, Character);
					//Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, 0, 0, 0, 0, 0, 0, 0);
					//Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, 0, 0, 0);
					Character.Task.ClearLookAt();
					//GTA.UI.Screen.ShowNotification("Done shooting");
					//GTA.UI.Screen.ShowSubtitle("Done Shooting1", 300);
					_lastDrivebyShooting = false;
				}
			}
		}

	    bool UpdateVehiclePosition()
	    {
            DEBUG_STEP = 121;
            if (MainVehicle != null && Character.CurrentVehicle != null)
            {
                DEBUG_STEP = 122;
                UpdateVehicleMountedWeapon();
                DEBUG_STEP = 123;
                if (IsCustomAnimationPlaying)
                {
                    DisplayCustomAnimation();
                }
                DEBUG_STEP = 124;
                if (ExitingVehicle && !_lastExitingVehicle)
                {
                    Character.Task.ClearAll();
                    Character.Task.ClearSecondary();

                    if (Speed < 1f)
                    {
                        Character.Task.LeaveVehicle(MainVehicle, false);
                    }
                    else
                    {
                        Function.Call(Hash.TASK_LEAVE_VEHICLE, Character, MainVehicle, 4160);
                    }
                }
                if (!ExitingVehicle && _lastExitingVehicle)
                {
                    DirtyWeapons = true;
                }

                _lastExitingVehicle = ExitingVehicle;

                if (ExitingVehicle) return true;

                if (GetResponsiblePed(MainVehicle).Handle == Character.Handle &&
                    Environment.TickCount - LastUpdateReceived < 10000)
                {
                    UpdateVehicleMainData();
                    if (DisplayVehicleDriveBy()) return true;
                }
            }
            return false;
	    }

	    void UpdateProps()
	    {
            /*
            if (PedProps != null && _clothSwitch % 50 == 0 && Game.Player.Character.IsInRangeOfEx(IsInVehicle ? VehiclePosition : _position, 30f))
			{
				var id = _clothSwitch / 50;

				if (PedProps.ContainsKey(id) &&
					PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
				{
					Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
				}
			}
			_clothSwitch++;
			if (_clothSwitch >= 750)
				_clothSwitch = 0;
            */
		}

	    void UpdateCurrentWeapon()
	    {
            if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon || DirtyWeapons)
			{
                //Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, -1, true, true);
                //Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);

                //Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
                //Character.Weapons.Select((WeaponHash)CurrentWeapon);

                Character.Weapons.RemoveAll();
			    var p = IsInVehicle ? Position : Position;

			    Util.Util.LoadWeapon(CurrentWeapon);

                var wObj = Function.Call<int>(Hash.CREATE_WEAPON_OBJECT, CurrentWeapon, 999, p.X, p.Y, p.Z, true, 0, 0);
                
                if (WeaponTints != null && WeaponTints.ContainsKey(CurrentWeapon))
			    {
			        var bitmap = WeaponTints[CurrentWeapon];

			        Function.Call(Hash.SET_WEAPON_OBJECT_TINT_INDEX, wObj, bitmap);
			    }

			    if (WeaponComponents != null && WeaponComponents.ContainsKey(CurrentWeapon) && WeaponComponents[CurrentWeapon] != null)
			    {
			        foreach (var comp in WeaponComponents[CurrentWeapon])
			        {
                        Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_WEAPON_OBJECT, wObj, comp);
                    }
			    }

                Function.Call(Hash.GIVE_WEAPON_OBJECT_TO_PED, wObj, Character);

                DirtyWeapons = false;
			}

	        if (!_lastReloading && IsReloading && ((IsInCover && !IsInLowCover) || !IsInCover))
	        {
                Character.Task.ClearAll();
	            Character.Task.ReloadWeapon();
	        }
		}

	    void DisplayParachuteFreefall()
	    {
			if (!_lastFreefall)
			{
				Character.Task.ClearAllImmediately();
				Character.Task.ClearSecondary();
			}

            //UpdatePlayerPedPos(fixWarp: false);

            var target = Util.Util.LinearVectorLerp(_lastPosition ?? Position,
                _position,
                TicksSinceLastUpdate, (int)AverageLatency);

            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
                0);
            DEBUG_STEP = 25;
#if !DISABLE_SLERP
            Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                    Math.Min(1f, (DataLatency + TicksSinceLastUpdate) / (float)AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif

            if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
					"skydive@base", "free_idle",
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character,
					Util.Util.LoadDict("skydive@base"), "free_idle",
					8f, 10f, -1, 0, -8f, 1, 1, 1);
			}
		}

	    void DisplayOpenParachute()
	    {
            if (_parachuteProp == null)
			{
				_parachuteProp = World.CreateProp(new Model(1740193300), Character.Position,
					Character.Rotation, false, false);
				_parachuteProp.IsPositionFrozen = true;
				Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);

                _parachuteProp.AttachTo(Character, Character.GetBoneIndex(Bone.SKEL_Spine2), new Vector3(3.6f, 0, 0f), new Vector3(0, 90, 0));
                
				Character.Task.ClearAllImmediately();
				Character.Task.ClearSecondary();
			}
            

            var target = Util.Util.LinearVectorLerp(_lastPosition ?? Position,
                _position,
                TicksSinceLastUpdate, (int)AverageLatency);

            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
                0);

            DEBUG_STEP = 25;

	        Character.Quaternion = _rotation.ToQuaternion();

            if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
					"skydive@parachute@first_person", "chute_idle_right",
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character,
					Util.Util.LoadDict("skydive@parachute@first_person"), "chute_idle_right",
					8f, 10f, -1, 0, -8f, 1, 1, 1);
			}
			DEBUG_STEP = 26;
		}

        void DisplayCustomAnimation()
        {
            if (!IsCustomAnimationPlaying) return;

            if (!IsCustomScenarioPlaying)
            {
                if (
                    !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
                        CustomAnimationDictionary, CustomAnimationName,
                        3))
                {
                    Character.Task.ClearSecondary();

                    Function.Call(Hash.TASK_PLAY_ANIM, Character,
                        Util.Util.LoadDict(CustomAnimationDictionary), CustomAnimationName,
                        8f, 10f, -1, CustomAnimationFlag, -8f, 1, 1, 1);
                }

                if ((CustomAnimationFlag & 32) != 0) // this is a secondary animation
                {
                    DisplayWalkingAnimation(false);
                }

                var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character,
                    CustomAnimationDictionary, CustomAnimationName);

                if (currentTime >= .95f && (CustomAnimationFlag & 1) == 0)
                {
                    IsCustomAnimationPlaying = false;
                    Character.Task.ClearAnimation(CustomAnimationDictionary, CustomAnimationName);
                }

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
                    CustomAnimationDictionary, CustomAnimationName,
                    3) &&
                    Util.Util.TickCount - CustomAnimationStartTime >
                    Function.Call<float>(Hash.GET_ENTITY_ANIM_TOTAL_TIME, Character, CustomAnimationDictionary,
                        CustomAnimationName) + 100 &&
                        (CustomAnimationFlag & 1) == 0)
                {
                    IsCustomAnimationPlaying = false;
                    Character.Task.ClearAnimation(CustomAnimationDictionary, CustomAnimationName);
                }
            }
            else if (!HasCustomScenarioStarted)
            {
                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, Character, CustomAnimationName, 0, 0);
                HasCustomScenarioStarted = true;
            }
        }

        void DisplayMeleeAnimation()
	    {
            var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character,
						lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);

			UpdatePlayerPedPos();

			if (!meleeSwingDone && CurrentWeapon != unchecked((int)WeaponHash.Unarmed))
			{
				var gunEntity = Function.Call<Prop>((Hash)0x3B390A939AF0B5FC, Character);
				if (gunEntity != null)
				{
					Vector3 min;
					Vector3 max;
					gunEntity.Model.GetDimensions(out min, out max);
					var start = gunEntity.GetOffsetInWorldCoords(min);
					var end = gunEntity.GetOffsetInWorldCoords(max);
					var ray = World.RaycastCapsule(start, end, (int)Math.Abs(end.X - start.X),
						IntersectOptions.Peds1, Character);
					//Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
					if (ray.DitHit && ray.DitHitEntity &&
						ray.HitEntity.Handle == Game.Player.Character.Handle)
					{
                        LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                        JavascriptHook.InvokeCustomEvent(api =>
                            api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));

                        if (!Main.NetEntityHandler.LocalCharacter.IsInvincible)
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
				if (ray.DitHit && ray.DitHitEntity && ray.HitEntity.Handle == Game.Player.Character.Handle)
				{
                    LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                    JavascriptHook.InvokeCustomEvent(api =>
                        api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));
                    if (!Main.NetEntityHandler.LocalCharacter.IsInvincible)
                        Game.Player.Character.ApplyDamage(25);
					meleeSwingDone = true;
				}
			}

			DEBUG_STEP = 28;
			if (currentTime >= 0.95f)
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

	    void DisplayMeleeCombat()
	    {
            string secondaryAnimDict = null;
			var ourAnim = GetMovementAnim(OnFootSpeed, false ,false);
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
            
			if (animDict != null &&
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim,
					8f, 10f, -1, 0, -8f, 1, 1, 1);
			}

			if (secondaryAnimDict != null &&
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, secAnim,
					3))
			{
                Character.Task.ClearSecondary();
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(secondaryAnimDict), secAnim,
					8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
			}

			UpdatePlayerPedPos();
		}

	    void DisplayAimingAnimation()
	    {
            var hands = GetWeaponHandsHeld(CurrentWeapon);
	        /*if (IsReloading)
	        {
                UpdatePlayerPedPos();
                return;
	        }*/
#if !CRASHTEST
            if (WeaponDataProvider.NeedsManualRotation(CurrentWeapon))
            {
#if !DISABLE_SLERP
                var latency = DataLatency + TicksSinceLastUpdate;
                Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, latency / (float)AverageLatency));
#else
                Character.Quaternion = Rotation.ToQuaternion();
#endif
                if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, "weapons@projectile@", "aimlive_m", 3))
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict("weapons@projectile@"), "aimlive_m",
                        8f, 10f, -1, 0, -8f, 1, 1, 1);
                }
            }
            else
#endif
            if (hands == 1 || hands == 2 || hands == 5 || hands == 6)
            {
                //UpdatePlayerPedPos(false);
                VMultiOnfootPosition();
            }

        }

	    void DisplayMeleeAnimation(int hands)
	    {
            Character.Task.ClearSecondary();

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
			if (CurrentWeapon == unchecked((int)WeaponHash.Knife) || CurrentWeapon == -538741184 ||
				CurrentWeapon == unchecked((int)WeaponHash.Dagger))
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
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(ourAnim.Split()[0]),
					ourAnim.Split()[1],
					8f, 10f, -1, 0, -8f, 1, 1, 1);
			}
#if !DISABLE_SLERP
            var latency = DataLatency + TicksSinceLastUpdate;
            Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, latency / (float)AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif
        }

	    void DisplayWeaponShootingAnimation()
	    {
            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
			var animDict = GetAnimDictionary(ourAnim);

            //var playerHealth = BitConverter.GetBytes(Game.Player.Character.Health);
            //var playerArmor = BitConverter.GetBytes(Game.Player.Character.Armor);

            if (!IsInCover)
	        {
                Character.Task.ClearSecondary();

	            if (animDict != null && Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
	            {
	                Character.Task.ClearAnimation(animDict, ourAnim);
	            }
	        }
	        else if (animDict != null)
	        {
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim,
                    8f, 10f, -1, 2, -8f, 1, 1, 1);
            }

	        Function.Call(Hash.SET_AI_WEAPON_DAMAGE_MODIFIER, 1f);
	        Function.Call(Hash.SET_PED_SHOOT_RATE, Character, 100);
            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);

	        if (!IsInCover)
	        {
	            DisplayAimingAnimation();
	        }

	        var gunEnt = Function.Call<Prop>((Hash)0x3B390A939AF0B5FC, Character);
	        if (gunEnt != null)
	        {
                //var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, 0));
                var start = gunEnt.Position;
                var dir = (AimCoords - start);
	            dir.Normalize();
	            var end = start + dir*100f;

	            if (IsInCover) // Weapon spread
	            {
	                end += Vector3.RandomXYZ()*2f;
	            }

	            if (!WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
	            {
	                Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, Character, end.X, end.Y, end.Z, true);
	            }
	            else
	            {
	                var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash) CurrentWeapon);
	                var speed = 0xbf800000;
	                var weaponH = (WeaponHash) CurrentWeapon;


	                if (weaponH == WeaponHash.Minigun)
	                    weaponH = WeaponHash.CombatPDW;

	                if (IsFriend())
	                    damage = 0;

	                Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
	                    end.X,
	                    end.Y, end.Z, damage, true, (int) weaponH, Character, false, true, speed);

	                _lastStart = start;
	                _lastEnd = end;
	            }

	            if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, Game.Player.Character, Character, true))
	            {

	                int boneHit = -1;
                    var boneHitArg = new OutputArgument();

	                if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, Game.Player.Character, boneHitArg))
	                {
	                    boneHit = boneHitArg.GetResult<int>();
	                }

                    LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                    JavascriptHook.InvokeCustomEvent(api =>
                        api.invokeonLocalPlayerDamaged(them, CurrentWeapon, boneHit/*, playerHealth, playerArmor*/));
	            }

                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Game.Player.Character);
            }
        }

	    void DisplayShootingAnimation()
	    {
            var hands = GetWeaponHandsHeld(CurrentWeapon);
            if (IsReloading) return;
			if (hands == 3 || hands == 4 || hands == 0)
			{
				DisplayMeleeAnimation(hands);
			}
			else
			{
                DisplayWeaponShootingAnimation();
            }
            
            //UpdatePlayerPedPos();

	        if (WeaponDataProvider.NeedsManualRotation(CurrentWeapon))
	        {
#if !DISABLE_SLERP
                var latency = DataLatency + TicksSinceLastUpdate;
                Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, latency / (float)AverageLatency));
#else
                Character.Quaternion = Rotation.ToQuaternion();
#endif
            }
		}

	    void DisplayWalkingAnimation(bool displaySecondary = true)
	    {
	        if (IsReloading || (IsInCover && IsShooting && !IsAiming)) return;

            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
			var animDict = GetAnimDictionary(ourAnim);
			var secondaryAnimDict = GetSecondaryAnimDict();
	        var flag = GetAnimFlag();

			DEBUG_STEP = 34;

			if (animDict != null && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
					3))
			{
			    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim,
			        8f, 10f, -1, flag, -8f, 1, 1, 1);
			}

	        if (displaySecondary)
	        {
	            if (secondaryAnimDict != null &&
	                !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, ourAnim,
	                    3))
	            {
	                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(secondaryAnimDict), ourAnim,
	                    8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
	            }
	            else if (secondaryAnimDict == null)
	            {
	                Character.Task.ClearSecondary();
	            }
	        }
	    }

		bool UpdateOnFootPosition()
		{
            UpdateProps();

            DEBUG_STEP = 23;

            UpdateCurrentWeapon();
            
            if (!_lastJumping && IsJumping)
            {
                Character.Task.Jump();
            }

            if (IsOnFire && !_lastFire)
            {
                Character.IsInvincible = false;
                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);
                _scriptFire = Function.Call<int>(Hash.START_ENTITY_FIRE, Character);
            }
            else if (!IsOnFire && _lastFire)
            {
                Function.Call(Hash.STOP_ENTITY_FIRE, Character);
                Character.IsInvincible = true;
                if (Character.IsDead) Function.Call(Hash.RESURRECT_PED, Character);

                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);

                _scriptFire = 0;
            }

            _lastFire = IsOnFire;

            if (EnteringVehicle && !_lastEnteringVehicle)
            {
                Entity targetVeh = null;
                if (Debug)
                {
                    targetVeh = MainVehicle;
                }
                else
                {
                    targetVeh = Main.NetEntityHandler.NetToEntity(VehicleNetHandle);
                }

                if (targetVeh != null)
                {
                    Character.Task.ClearAll();
                    Character.Task.ClearSecondary();
                    Character.Task.ClearAllImmediately();
                    Character.IsPositionFrozen = false;
                    Character.Task.EnterVehicle(new Vehicle(targetVeh.Handle), (GTA.VehicleSeat)VehicleSeat, -1, 2f);
                    _seatEnterStart = Util.Util.TickCount;
                }
            }

            _lastEnteringVehicle = EnteringVehicle;

            if (EnteringVehicle) return true;

            Character.CanBeTargetted = true;

            DEBUG_STEP = 24;
            if (IsFreefallingWithParachute)
            {
                DisplayParachuteFreefall();
                return false;
            }

            if (IsParachuteOpen)
            {
                DisplayOpenParachute();
                return false;
            }

            if (_parachuteProp != null)
            {
                _parachuteProp.Delete();
                _parachuteProp = null;
            }
            DEBUG_STEP = 27;

            bool ragdoll = IsRagdoll;

            if (IsPlayerDead) ragdoll = true;

            if (ragdoll)
            {
                if (!Character.IsRagdoll)
                {
                    Character.CanRagdoll = true;
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, Character, 50000, 60000, 0, 1, 1, 1);
                }

                var dir = Position - (_lastPosition ?? Position);
                var vdir = PedVelocity - _lastPedVel;
                var target = Util.Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                    TicksSinceLastUpdate,
                    (int)AverageLatency);

                var posTarget = Util.Util.LinearVectorLerp(Position, Position + dir,
                    TicksSinceLastUpdate,
                    (int)AverageLatency);

                const int PED_INTERPOLATION_WARP_THRESHOLD = 15;
                const int PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 5;

                float fThreshold = (PED_INTERPOLATION_WARP_THRESHOLD +
                                    PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * PedVelocity.Length());

                if (Character.Position.DistanceToSquared(currentInterop.vecTarget) > fThreshold * fThreshold)
                {
                    Character.PositionNoOffset = currentInterop.vecTarget;
                }
                else
                {
                    Character.Velocity = target + 2 * (posTarget - Character.Position);
                }

                _stopTime = DateTime.Now;
                _carPosOnUpdate = Character.Position;

                return true;
            }
            else if (!ragdoll && Character.IsRagdoll)
            {
                Character.CanRagdoll = false;
                Character.Task.ClearAllImmediately();
                //Character.PositionNoOffset = Position;

                if (!IsPlayerDead)
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, Character,
                        Util.Util.LoadDict("anim@sports@ballgame@handball@"), "ball_get_up",
                        12f, 12f, -1, 0, -10f, 1, 1, 1);

                    _playingGetupAnim = true;
                }

                return true;
            }

            if (_playingGetupAnim)
            {
                var getupAnim = GetAnimalGetUpAnimation().Split();

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, getupAnim[0], getupAnim[1], 3))
                {
                    UpdatePlayerPedPos();
                    var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character, getupAnim[0],
                        getupAnim[1]);

                    if (currentTime >= 0.7f)
                    {
                        Character.Task.ClearAnimation(getupAnim[0], getupAnim[1]);
                        Character.Task.ClearAll();
                        _playingGetupAnim = false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            if (lastMeleeAnim != null)
            {
                DisplayMeleeAnimation();
            }
            else if (IsInMeleeCombat)
            {
                DisplayMeleeCombat();
            }

            DEBUG_STEP = 29;

            if (IsAiming && !IsCustomAnimationPlaying)
            {
                DisplayAimingAnimation();
            }
            if (IsShooting && !IsCustomAnimationPlaying)
            {
                DisplayShootingAnimation();
            }

            else if (IsCustomAnimationPlaying)
            {
                if ((CustomAnimationFlag & 48) == 48)
                {
                    VMultiOnfootPosition();
                }
                else
                {
                    UpdatePlayerPedPos();
                }
                DisplayCustomAnimation();
            }


            DEBUG_STEP = 32;
            if (!IsAiming && !IsShooting && !IsJumping && !IsInMeleeCombat && !IsCustomAnimationPlaying)
            {
                //UpdatePlayerPedPos();

                //DisplayWalkingAnimation();

                VMultiOnfootPosition();
            }

			return false;
	    }

        private Vector3 _lastAimCoords;
        private Prop _aimingProp;
        private Prop _followProp;
        private long lastAimSet;

        private bool lastMoving;

        internal void VMultiOnfootPosition()
        {
            /*
             * if (IsReloading || (IsInCover && IsShooting && !IsAiming))
            {
                UpdatePlayerPedPos();
                return;
            }
            */

            long tServer = DataLatency;

            float lerpValue = 0f;
            var length = Position.DistanceToSquared(Character.Position);
            
            if (length > 0.05f * 0.05f && length < Main.PlayerStreamingRange * Main.PlayerStreamingRange)
            {
                lerpValue = lerpValue + ((tServer * 2) / 50000f);
                if (Character.IsSwimming)
                {
                    Character.PositionNoOffset = GTA.Math.Vector3.Lerp(
                        new GTA.Math.Vector3(Character.Position.X, Character.Position.Y, Character.Position.Z),
                        new GTA.Math.Vector3(Position.X, Position.Y, Position.Z), lerpValue);
                }
                else
                {
                    var tmpPosition = Vector3.Lerp(
                        new Vector3(Character.Position.X, Character.Position.Y, Character.Position.Z),
                        new GTA.Math.Vector3(
                            Position.X + ((PedVelocity.X / 5)),
                            Position.Y + ((PedVelocity.Y / 5)),
                            Position.Z + ((PedVelocity.Z / 5))),
                        lerpValue);
                    Character.PositionNoOffset = tmpPosition;
                }
                if (!Character.IsSwimming && _lastSwimming)
                {
                    Character.Task.ClearAllImmediately();
                }
                _lastSwimming = Character.IsSwimming;
                _carPosOnUpdate = Character.Position;
                _stopTime = DateTime.Now;
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && currentInterop.FinishTime != 0)
            {
                var posTarget = Util.Util.LinearVectorLerp(_carPosOnUpdate, Position + (Position - (_lastPosition ?? Position)),
                    (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0);
            }
            else
            {
                //Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,Position.Z, 0, 0, 0);
            }

            Character.Quaternion = GTA.Math.Quaternion.Lerp(Character.Quaternion, Rotation.ToQuaternion(), 0.10f); // mise à jours de la rotation

            if (length < Main.PlayerStreamingRange * Main.PlayerStreamingRange)
            { 
                Character.Velocity = PedVelocity; // Mise à jours de la vitesse

                var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
                var animDict = GetAnimDictionary(ourAnim);
                if (ourAnim != null && animDict != null)
                {
                    var flag = GetAnimFlag();
                    DEBUG_STEP = 34;
                    if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
                        3))
                    {
                        Character.Task.ClearAll();
                        Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim,
                            8f, 10f, -1, flag, -8f, 1, 1, 1);
                    }
                }
                else
                {
                    if (IsAiming && !IsReloading)
                    {
                        if (_aimingProp != null && _aimingProp.Exists())
                        {
                            this._aimingProp.Position = AimCoords;
                        }
                        else
                        {
                            this._aimingProp = World.CreateProp(new Model(-512779781), this.AimCoords, false, false);
                            this._aimingProp.IsCollisionEnabled = false;
                            this._aimingProp.Opacity = 0;
                        }

                        if (_followProp != null && _followProp.Exists())
                        {
                            this._followProp.Position = Position + PedVelocity * 1.25f;
                        }
                        else
                        {
                            this._followProp = World.CreateProp(new Model(-512779781), Position, false, false);
                            this._followProp.IsCollisionEnabled = false;
                            this._followProp.Opacity = 0;
                        }

                        bool isAiming = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, Character, (int)290);
                        if (length < (Main.PlayerStreamingRange * Main.PlayerStreamingRange) / 2)
                        {
                            if ((!isAiming || Main.TickCount % 25 == 0) && OnFootSpeed == 0)
                            {
                                Function.Call(Hash.TASK_AIM_GUN_AT_ENTITY, Character, _aimingProp, -1, false);
                                _lastAimCoords = AimCoords;
                            }
                            else if ((!isAiming || Main.TickCount % 25 == 0) && OnFootSpeed > 0)
                            {
                                Function.Call(Hash.TASK_GO_TO_ENTITY_WHILE_AIMING_AT_ENTITY, Character, _followProp, _aimingProp, (float)OnFootSpeed, false, 10000, 10000, true, true, (uint)FiringPattern.FullAuto);
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, (float)OnFootSpeed);
                                _lastAimCoords = AimCoords;
                            }
                        }
                    }
                    else
                    {
                        Vector3 predictPosition = this.Position + (this.Position - Character.Position) + PedVelocity * 1.25f;
                        var range = predictPosition.DistanceToSquared(Character.Position);
                        if (length < (Main.PlayerStreamingRange * Main.PlayerStreamingRange) / 2)
                        {
                            if (OnFootSpeed == 1 && (range > 0.1f))
                            {
                                if (!Character.IsWalking || (Main.TickCount % 50 == 0 || range > 0.25f))
                                {
                                    Character.Task.GoTo(predictPosition, true);

                                    Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 1.0f);
                                }
                                lastMoving = true;
                            }

                            else if (OnFootSpeed == 2)
                            {
                                if (!Character.IsRunning || (Main.TickCount % 50 == 0 || range > 0.50f))
                                {
                                    Character.Task.RunTo(predictPosition, true);
                                    Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 2.0f);
                                }
                                lastMoving = true;
                            }
                            else if (OnFootSpeed == 3)
                            {
                                if (!Character.IsSprinting || (Main.TickCount % 50 == 0 || range > 0.75f))
                                {
                                    Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, predictPosition.X, predictPosition.Y, predictPosition.Z, 3.0f, -1, 0.0f, 0.0f);
                                    Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Character, 1.49f);
                                    Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 3.0f);

                                }
                                lastMoving = true;
                            }
                            else
                            {
                                //Character.Task.AchieveHeading(Rotation.Z);
                                if (lastMoving == true)
                                {
                                    Character.Task.StandStill(2000);
                                    lastMoving = false;
                                }
                            }
                        }
                    }
                }
                StuckDetection();
            }
        }

        internal void StuckDetection()
        {
#if !DISABLE_UNDER_FLOOR_FIX
            const int PED_INTERPOLATION_WARP_THRESHOLD = 5;
            const int PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 5;

            // Check if the distance to interpolate is too far.
            float fThreshold = (PED_INTERPOLATION_WARP_THRESHOLD + PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * PedVelocity.Length());

            if (Character.Position.DistanceToSquared(currentInterop.vecTarget) > fThreshold * fThreshold
                /* || Character.Position.DistanceToSquared(currentInterop.vecTarget) > 25*/)
            {
                // Abort all interpolation
                currentInterop.FinishTime = 0;
                Character.PositionNoOffset = currentInterop.vecTarget;
            }

            // Calc remote movement
            var vecRemoteMovement = Position - (_lastPosition ?? Position);

            // Calc local error
            var vecLocalError = currentInterop.vecTarget - Character.Position;

            // Small remote movement + local position error = force a warp
            bool bForceLocalZ = false;
            bool bForceLocalXY = false;
            if (Math.Abs(vecRemoteMovement.Z) < 0.01f)
            {
                float fLocalErrorZ = Math.Abs(vecLocalError.Z);
                if (fLocalErrorZ > 0.1f && fLocalErrorZ < 10)
                {
                    bForceLocalZ = true;
                }
            }

            if (Math.Abs(vecRemoteMovement.X) < 0.01f)
            {
                float fLocalErrorX = Math.Abs(vecLocalError.X);
                if (fLocalErrorX > 0.1f && fLocalErrorX < 10)
                {
                    bForceLocalXY = true;
                }
            }

            if (Math.Abs(vecRemoteMovement.Y) < 0.01f)
            {
                float fLocalErrorY = Math.Abs(vecLocalError.Y);
                if (fLocalErrorY > 0.1f && fLocalErrorY < 10)
                {
                    bForceLocalXY = true;
                }
            }

            // Only force position if needed for at least two consecutive calls
            if (!bForceLocalZ && !bForceLocalXY)
                m_uiForceLocalCounter = 0;
            else if (m_uiForceLocalCounter++ > 1)
            {
                Vector3 targetPos = Character.Position;

                if (bForceLocalZ)
                {
                    targetPos = new Vector3(targetPos.X, targetPos.Y, currentInterop.vecTarget.Z);
                    Character.Velocity = new Vector3(Character.Velocity.X, Character.Velocity.Y, 0);
                }
                if (bForceLocalXY)
                {
                    targetPos = new Vector3(currentInterop.vecTarget.X, currentInterop.vecTarget.Y, targetPos.Z);
                }

                Character.PositionNoOffset = targetPos;
                currentInterop.FinishTime = 0;
            }
#endif
        }

        private int DEBUG_STEP_backend;
        private long _seatEnterStart;
        private bool _isReloading;

        public float hRange = Main.GlobalStreamingRange; // 1km
        private long _lastTickUpdate = Environment.TickCount;

        internal void DisplayLocally()
        {
            if (!StreamedIn || IsSpectating || (Flag & (int)EntityFlag.PlayerSpectating) != 0 || ModelHash == 0 || string.IsNullOrEmpty(Name)) return;
            bool inRange = Game.Player.Character.IsInRangeOfEx(Position, hRange);

            if (inRange)
            {
#if DEBUG
                PedThread.InRangePlayers++;
#endif
                if (Environment.TickCount - _lastTickUpdate > 500)
                {
                    _lastTickUpdate = Environment.TickCount;
                    if (CreateCharacter(Position, inRange)) return;
                    if (CreateVehicle()) return;

                    if (Character != null)
                    {
                        Character.Health = PedHealth;
                        if (IsPlayerDead && !Character.IsDead && IsInVehicle)
                        {
                            Function.Call(Hash.SET_PED_PLAYS_HEAD_ON_HORN_ANIM_WHEN_DIES_IN_VEHICLE, Character, true);
                            Character.IsInvincible = false;
                            Character.Kill();
                        }

                        Function.Call(Hash.SET_PED_CONFIG_FLAG, Character, 400, true); // Can attack friendlies
                    }
                    WorkaroundBlip();
                }
                if (Character != null && Character.Exists())
                {
                    bool enteringSeat = _seatEnterStart != 0 && Util.Util.TickCount - _seatEnterStart < 500;
                    if (UpdatePlayerPosOutOfRange(Position, Game.Player.Character.IsInRangeOfEx(Position, Main.PlayerStreamingRange))) return;

                    if ((enteringSeat || Character.IsSubtaskActive(67) || IsBeingControlledByScript || Character.IsExitingLeavingCar())) {
                        DrawNametag();
                        return;
                    }
#if DEBUG
                    if (PedThread.ToggleUpdate) {
#endif
                        UpdatePosition();
#if DEBUG
                    }
                    if (PedThread.ToggleNametag) {
#endif
                        DrawNametag();
#if DEBUG
                    }
#endif
                    _lastJumping = IsJumping;
                    _lastFreefall = IsFreefallingWithParachute;
                    _lastShooting = IsShooting;
                    _lastAiming = IsAiming;
                    _lastVehicle = _isInVehicle;
                    _lastEnteringVehicle = EnteringVehicle;
                }
            }
            else
            {
                if (Character != null && Character.Exists() && Environment.TickCount - _lastTickUpdate > 500)
                {
                    Character.Delete();
                }
            }
        }

        internal static Ped GetResponsiblePed(Vehicle veh)
        {
            if (veh == null || veh.Handle == 0 || !veh.Exists()) return new Ped(0);

            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
            }

            return new Ped(0);
        }

        internal string GetAnimDictionary(string ourAnim = "")
        {
            if (IsInCover) return GetCoverIdleAnimDict();
            if (IsOnLadder) return "laddersbase";
            if (IsVaulting) return "move_climb";

            if (GetAnimalAnimationDictionary(ModelHash) != null)
                return GetAnimalAnimationDictionary(ModelHash);
            /*
            string dict = "move_m@generic";

            if (Character.Gender == Gender.Female)
                dict = "move_f@generic";

            dict = Character.SubmersionLevel >= 0.8f ? ourAnim == "idle" ? "swimming@base" : "swimming@swim" : dict;
            */

            return null;
        }

        internal uint GetAnimFlag()
        {
            if (IsVaulting && !IsOnLadder)
                return 2 | 2147483648;
            return 1 | 2147483648; // Loop + dont move
        }

        private int m_uiForceLocalCounter;
        private void UpdatePlayerPedPos(bool updateRotation = true, bool fixWarp = true)
        {
            Vector3 newPos;

            if (!Main.OnFootLagCompensation)
            {
                long currentTime = Util.Util.TickCount;

                float alpha = Util.Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                Vector3 comp = Util.Util.Lerp(new Vector3(), alpha, currentInterop.vecError);

                newPos = Position + comp;
            }
            else
            {
                var latency = DataLatency + TicksSinceLastUpdate;
                newPos = Position + (PedVelocity*latency/1000);
            }

            if ((OnFootSpeed > 0 || IsAnimal(ModelHash)) && currentInterop.FinishTime != 0)
            {
                if (Game.Player.Character.IsInRangeOfEx(newPos, Main.PlayerStreamingRange))
                {
                    Character.Velocity = PedVelocity + 10*(newPos - Character.Position);
                }
                else
                {
                    Character.PositionNoOffset = newPos;
                }

                StuckDetection();
                _stopTime = DateTime.Now;
                _carPosOnUpdate = Character.Position;
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && currentInterop.FinishTime != 0)
            {
                var posTarget = Util.Util.LinearVectorLerp(_carPosOnUpdate, Position + (Position - (_lastPosition ?? Position)),
                    (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0);
            }
            else
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,
                    Position.Z, 0, 0, 0);
            }

            if (Debug && false)
            {
                World.DrawMarker(MarkerType.DebugSphere, Character.Position, new Vector3(), new Vector3(),
                    new Vector3(1, 1, 1), Color.FromArgb(100, 255, 0, 0));
                World.DrawMarker(MarkerType.DebugSphere, Game.Player.Character.Position,
                        new Vector3(), new Vector3(),
                        new Vector3(1, 1, 1), Color.FromArgb(100, 0, 255, 0));
                World.DrawMarker(MarkerType.DebugSphere, newPos, new Vector3(), new Vector3(),
                    new Vector3(1, 1, 1), Color.FromArgb(100, 0, 0, 255));
            }

            DEBUG_STEP = 33;

            if (updateRotation)
            {
#if !DISABLE_SLERP
                if (!Character.IsSwimmingUnderWater)
                {
                    Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                        Math.Min(1f, (DataLatency + TicksSinceLastUpdate)/(float) AverageLatency));
                }
                else
                {
                    Character.Quaternion = Rotation.ToQuaternion();
                }
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif
            }
        }

        internal string GetCoverIdleAnimDict()
        {
            if (!IsInCover) return "";
            var altitude = IsInLowCover ? "low" : "high";

            var hands = GetWeaponHandsHeld(CurrentWeapon);

            if (IsShooting && !IsAiming)
            {
                if (hands == 1) return "cover@weapon@1h";
                if (hands == 2 || hands == 5) return "cover@weapon@2h";
            }

            if (hands == 1) return "cover@idles@1h@" + altitude +"@_a";
            if (hands == 2 || hands == 5) return "cover@idles@2h@" + altitude +"@_a";
            if (hands == 3 || hands == 4 || hands == 0) return "cover@idles@unarmed@" + altitude + "@_a";
            return "";
        }

        internal string GetSecondaryAnimDict()
        {
	        if (CurrentWeapon == unchecked((int) WeaponHash.Unarmed)) return null;
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
                CurrentWeapon == unchecked((int) WeaponHash.SawnOffShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.GrenadeLauncher) ||
                CurrentWeapon == unchecked((int)WeaponHash.Railgun))
                return "move_weapon@rifle@generic";
            return null;
        }

        internal int GetWeaponHandsHeld(int weapon)
        {
            if (weapon == unchecked((int) WeaponHash.Unarmed)) return 0;
            if (weapon == unchecked((int)WeaponHash.RPG) ||
                weapon == unchecked((int)WeaponHash.HomingLauncher) ||
                weapon == unchecked((int)WeaponHash.Firework))
                return 5;
            if (weapon == unchecked((int)WeaponHash.Minigun))
                return 5;
            if (weapon == unchecked((int)WeaponHash.GolfClub) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Poolcue) ||
                weapon == unchecked((int)WeaponHash.Bat))
                return 4;
            if (weapon == unchecked((int) WeaponHash.Knife) || weapon == unchecked((int) WeaponHash.Nightstick) ||
                weapon == unchecked((int) WeaponHash.Hammer) || weapon == unchecked((int) WeaponHash.Crowbar) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Wrench) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Battleaxe) ||
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
                weapon == unchecked((int)WeaponHash.SawnOffShotgun) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Autoshotgun) ||
                weapon == unchecked((int)WeaponHash.CompactRifle))
                return 2;
            return 1;
        }

        internal static int GetPedSpeed(float speed)
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

        internal string GetMovementAnim(int speed, bool inCover, bool coverFacingLeft)
        {
            if (inCover)
            {
                if (IsShooting && !IsAiming)
                {
                    if (IsInLowCover)
                        return coverFacingLeft ? "blindfire_low_l_aim_med" : "blindfire_low_r_aim_med";
                    return coverFacingLeft ? "blindfire_hi_l_aim_med" : "blindfire_hi_r_aim_med";
                }
                
                return coverFacingLeft ? "idle_l_corner" : "idle_r_corner";
            }

            if (IsOnLadder)
            {
                if (Math.Abs(PedVelocity.Z) < 0.5) return "base_left_hand_up";
                else if (PedVelocity.Z > 0) return "climb_up";
                else if (PedVelocity.Z < 0)
                {
                    if (PedVelocity.Z < -2f)
                        return "slide_climb_down";
                    return "climb_down";
                }
            }

            if (IsVaulting) return "standclimbup_180_low";

            if (GetAnimalAnimationName(ModelHash,speed) != null)
                return GetAnimalAnimationName(ModelHash,speed);
            /*
            if (speed == 0) return "idle";
            if (speed == 1) return "walk";
            if (speed == 2) return "run";
            if (speed == 3) return "sprint";*/
            return null;
        }

        internal static bool IsAnimal(int model)
        {
            return GetAnimalAnimationDictionary(model) != null;
        }

        internal static string GetAnimalAnimationName(int modelhash, int speed)
        {
            var hash = (PedHash)modelhash;

            switch (hash)
            {
                case PedHash.Cat:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Boar:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "trot";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.ChickenHawk:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "glide";
                    if (speed == 3) return "flapping";
                }
                    break;
                case PedHash.Chop:
                case PedHash.Shepherd:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Cormorant:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "glide";
                    if (speed == 3) return "flapping";
                }
                    break;
                case PedHash.Cow:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "trot";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Coyote:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "trot";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Crow:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "glide";
                    if (speed == 3) return "flapping";
                }
                    break;
                case PedHash.Deer:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "trot";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Dolphin:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "swim";
                    if (speed == 2) return "accelerate";
                    if (speed == 3) return "accelerate";
                }
                    break;
                case PedHash.Fish:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "swim";
                    if (speed == 2) return "accelerate";
                    if (speed == 3) return "accelerate";
                }
                    break;
                case PedHash.Hen:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "run";
                    if (speed == 3) return "run";
                }
                    break;
                case PedHash.Humpback:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "swim";
                    if (speed == 2) return "accelerate";
                    if (speed == 3) return "accelerate";
                }
                    break;
                case PedHash.Husky:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.TigerShark:
                case PedHash.HammerShark:
                case PedHash.KillerWhale:
                case PedHash.Stingray:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "swim";
                    if (speed == 2) return "accelerate";
                    if (speed == 3) return "accelerate";
                }
                    break;
                case PedHash.Pig:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "trot";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Seagull:
                case PedHash.Pigeon:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "glide";
                    if (speed == 3) return "flapping";
                }
                    break;
                case PedHash.Pug:
                case PedHash.Poodle:
                case PedHash.Westy:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Rabbit:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
                case PedHash.Rat:
                    {
                        if (speed == 0) return "idle";
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Rottweiler:
                case PedHash.Retriever:
                {
                    if (speed == 0) return "idle";
                    if (speed == 1) return "walk";
                    if (speed == 2) return "canter";
                    if (speed == 3) return "gallop";
                }
                    break;
            }

            return null;
        }

        internal static string GetAnimalAnimationDictionary(int modelhash)
        {
            var hash = (PedHash)modelhash;

            if (hash == PedHash.Boar)
                return "creatures@boar@move";
            if (hash == PedHash.Cat)
                return "creatures@cat@move";
            if (hash == PedHash.ChickenHawk)
                return "creatures@chickenhawk@move";
            if (hash == PedHash.Chop || hash == PedHash.Shepherd)
                return "creatures@dog@move";
            if (hash == PedHash.Cormorant)
                return "creatures@cormorant@move";
            if (hash == PedHash.Cow)
                return "creatures@cow@move";
            if (hash == PedHash.Coyote)
                return "creatures@coyote@move";
            if (hash == PedHash.Crow)
                return "creatures@crow@move";
            if (hash == PedHash.Deer)
                return "creatures@deer@move";
            if (hash == PedHash.Dolphin)
                return "creatures@dolphin@move";
            if (hash == PedHash.Fish)
                return "creatures@fish@move";
            if (hash == PedHash.Hen)
                return "creatures@hen@move";
            if (hash == PedHash.Humpback)
                return "creatures@humpback@move";
            if (hash == PedHash.Husky)
                return "creatures@husky@move";
            if (hash == PedHash.KillerWhale)
                return "creatures@killerwhale@move";
            if (hash == PedHash.Pig)
                return "creatures@pig@move";
            if (hash == PedHash.Pigeon)
                return "creatures@pigeon@move";
            if (hash == PedHash.Poodle || hash == PedHash.Pug || hash == PedHash.Westy)
                return "creatures@pug@move";
            if (hash == PedHash.Rabbit)
                return "creatures@rabbit@move";
            if (hash == PedHash.Rat)
                return "creatures@rat@move";
            if (hash == PedHash.Retriever)
                return "creatures@retriever@move";
            if (hash == PedHash.Rottweiler)
                return "creatures@rottweiler@move";
            if (hash == PedHash.Seagull)
                return "creatures@pigeon@move";
            if (hash == PedHash.HammerShark || hash == PedHash.TigerShark)
                return "creatures@shark@move";
            if (hash == PedHash.Stingray)
                return "creatures@stingray@move";

            return null;
        }

        internal string GetAnimalGetUpAnimation()
        {
            var hash = (PedHash) ModelHash;

            if (hash == PedHash.Boar)
                return "creatures@boar@getup getup_l";
            

            return "anim@sports@ballgame@handball@ ball_get_up";
        }

        internal void Clear()
        {
            if (_aimingProp != null)
            {
                _aimingProp.Delete();
                _aimingProp = null;
            }

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

            lock (Main.NetEntityHandler.ClientMap) Main.NetEntityHandler.HandleMap.Remove(RemoteHandle);
        }

#endregion

    }

}