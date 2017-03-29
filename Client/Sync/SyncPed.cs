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
using GTANetwork.Streamer;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Sync
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

    internal partial class SyncPed : RemotePlayer
    {
        internal void DisplayLocally()
        {
            if (!StreamedIn && IsSpectating || (Flag & (int)EntityFlag.PlayerSpectating) != 0 || ModelHash == 0 || string.IsNullOrEmpty(Name)) return;
            if (Character != null && Character.Exists())
            {
                if (_isInVehicle) UpdateVehiclePosition(); else UpdateOnFootPosition();


                // USE ON SCREEN RENDERING
                //OUT OF RANGE ENTITIES ARE NOT STREAMED IN, THUS MAKING THIS OBSOLETE
                //if (UpdatePlayerPosOutOfRange(Position, Main.PlayerChar.IsInRangeOfEx(Position, StreamerThread.CloseRange))) return;

                _lastJumping = IsJumping;
                _lastFreefall = IsFreefallingWithParachute;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
                _lastVehicle = _isInVehicle;
                _lastEnteringVehicle = EnteringVehicle;
            }

            if (Environment.TickCount - _lastTickUpdate > 500)
            {
                _lastTickUpdate = Environment.TickCount;
                if (CreateCharacter()) return;
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
        }


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
        internal int Ammo;
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
                //if (Debug) return Main._debugInterval;
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

        #region NeoSyncPed




        //TODO: Use this
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
	        if (!_isInVehicle || MainVehicle == null || !_blip || !((Character.GetOffsetInWorldCoords(new Vector3()) - MainVehicle.Position).Length() > 70f)) return;
	        Character.Delete();
	    }




       


	   

	    void UpdateProps()
	    {
            /*
            if (PedProps != null && _clothSwitch % 50 == 0 && Main.PlayerChar.IsInRangeOfEx(IsInVehicle ? VehiclePosition : _position, 30f))
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

            //UpdatePlayerPedPos();
            Ped PlayerChar = Game.Player.Character;

            if (!meleeSwingDone && CurrentWeapon != unchecked((int)WeaponHash.Unarmed))
			{

                var gunEntity = Function.Call<Prop>((Hash)0x3B390A939AF0B5FC, Character);
				if (gunEntity != null)
				{
					gunEntity.Model.GetDimensions(out Vector3 min, out Vector3 max);
					var start = gunEntity.GetOffsetInWorldCoords(min);
					var end = gunEntity.GetOffsetInWorldCoords(max);
					var ray = World.RaycastCapsule(start, end, (int)Math.Abs(end.X - start.X),
						IntersectOptions.Peds1, Character);
					//Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
					if (ray.DitHit && ray.DitHitEntity &&
						ray.HitEntity.Handle == PlayerChar.Handle)
					{
                        LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                        JavascriptHook.InvokeCustomEvent(api =>
                            api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));

                        if (!Main.NetEntityHandler.LocalCharacter.IsInvincible)
                            PlayerChar.ApplyDamage(25);
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
				if (ray.DitHit && ray.DitHitEntity && ray.HitEntity.Handle == PlayerChar.Handle)
				{
                    LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                    JavascriptHook.InvokeCustomEvent(api =>
                        api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));
                    if (!Main.NetEntityHandler.LocalCharacter.IsInvincible)
                        PlayerChar.ApplyDamage(25);
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

			var animDict = GetAnimDictionary();
            
			if (animDict != null && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim, 8f, 10f, -1, 0, -8f, 1, 1, 1);
			}

			if (secondaryAnimDict != null && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, secAnim, 3))
			{
                Character.Task.ClearSecondary();
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(secondaryAnimDict), secAnim, 8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
			}

			//UpdatePlayerPedPos();
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

            //var playerHealth = BitConverter.GetBytes(Main.PlayerChar.Health);
            //var playerArmor = BitConverter.GetBytes(Main.PlayerChar.Armor);

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
                Ped PlayerChar = Game.Player.Character;

                if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, PlayerChar, Character, true))
	            {

	                int boneHit = -1;
                    var boneHitArg = new OutputArgument();

	                if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, PlayerChar, boneHitArg))
	                {
	                    boneHit = boneHitArg.GetResult<int>();
	                }

                    LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                    JavascriptHook.InvokeCustomEvent(api =>
                        api.invokeonLocalPlayerDamaged(them, CurrentWeapon, boneHit/*, playerHealth, playerArmor*/));
	            }

                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, PlayerChar);
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


        private Vector3 _lastAimCoords;
        private Prop _aimingProp;
        private Prop _followProp;
        private long lastAimSet;

        private bool lastMoving;


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
            /*
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
             */

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

        public float hRange = StreamerThread.GlobalRange; // 1km
        private long _lastTickUpdate = Environment.TickCount;




        private int m_uiForceLocalCounter;
      


#endregion

    }

}