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
    internal class Animation
    {
        internal string Dictionary { get; set; }
        internal string Name { get; set; }
        internal bool Loop { get; set; }
    }

    internal partial class SyncPed : RemotePlayer
    {
        internal void Render()
        {
            if (!StreamedIn) return; //|| (Flag & (int)EntityFlag.PlayerSpectating) != 0 || && IsSpectating
            if (string.IsNullOrEmpty(Name)) return;
            if (ModelHash == 0) return;

            // Does not return if:
            // ** Entity is Streamed in
            // ** Name is not Null or Empty
            // ** ModelHash is not 0

            if (Character != null && Character.Exists())
            {
               
                if (_isInVehicle)
                {
                    UpdateVehiclePosition();
                }
                else
                {
                    UpdateOnFootPosition();
                }

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
                try
                {
                    if (CreateCharacter()) return;
                    if (CreateVehicle()) return;
                }
                catch
                {
                    // ignored
                }

                if (Character != null && Character.Exists())
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
                //WorkaroundBlip();
            }
        }

        internal long Host;
        internal Ped Character;
        internal Vector3 _position;
        internal int VehicleNetHandle;
        internal Vector3 _rotation;
        internal bool _isInVehicle;

        internal Animation CurrentAnimation;
        internal int ModelHash;
        internal int CurrentWeapon;
        internal int Ammo;
        internal Vector3 AimCoords;

        internal Ped AimPlayer;
        internal bool AimedAtPlayer;

        internal float Latency;

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
        private bool _lastBurnout;
        private bool _lastSwimming;
        internal float VehicleRPM;
	    internal float SteeringScale;

        internal bool IsOnFire;
        private bool _lastFire;
        internal bool IsBeingControlledByScript;

        internal bool EnteringVehicle;
        private bool _lastEnteringVehicle;
        private bool _lastExitingVehicle;

        internal int VehicleSeat;
        internal int PedHealth;

        internal float VehicleHealth;

        internal bool Debug;

        private DateTime _stopTime;

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
        
        internal int PedArmor;
        

        internal bool DirtyWeapons;

        internal bool IsSpectating;


        //Packets
        //Flags
        internal bool IsVehDead;
        internal bool IsHornPressed;
        internal bool Siren;
        internal bool IsShooting;
        internal bool IsAiming;
        internal bool IsInBurnout;
        internal bool ExitingVehicle;
        internal bool IsPlayerDead;
        internal bool Braking;


        private float _lastSpeed;
        internal float Speed
        {
            get => _speed;
            set
            {
                _lastSpeed = _speed;
                _speed = value;
            }
        }

        internal byte OnFootSpeed;

        internal bool IsParachuteOpen;

        internal double AverageLatency => _latencyAverager.Count == 0 ? 0 : _latencyAverager.Average();

        internal long LastUpdateReceived
        {
            get => _lastUpdateReceived;
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

        internal long TicksSinceLastUpdate => Util.Util.TickCount - LastUpdateReceived;

        internal int DataLatency => (int)(((Latency * 1000) / 2) + (Main.Latency * 1000) / 2);

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
            get => _vehicleVelocity;
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
            get => _position;
            set
            {
                _lastPosition = _position;
                _position = value;
            }
        }

        private Vector3? _lastVehicleRotation;
        internal Vector3 VehicleRotation
        {
            get => _vehicleRotation;
            set
            {
                _lastVehicleRotation = _vehicleRotation;
                _vehicleRotation = value;
            }
        }

        private Vector3? _lastRotation;
        internal new Vector3 Rotation
        {
            get => _rotation;
            set
            {
                _lastRotation = _rotation;
                _rotation = value;
            }
        }


        private bool _lastVehicle;
        private bool _lastAiming;
        private bool _lastShooting;

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




        private int _mUiForceLocalCounter;
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
                _mUiForceLocalCounter = 0;
            else if (_mUiForceLocalCounter++ > 1)
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

        private long _seatEnterStart;

        private long _lastTickUpdate = Environment.TickCount;

        #endregion

    }

}