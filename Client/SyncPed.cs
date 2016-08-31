//#define DISABLE_SLERP
//#define DISABLE_UNDER_FLOOR_FIX
#define DISABLE_ROTATION_SIM

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetworkShared;
using NativeUI;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public enum SynchronizationMode
    {
        Dynamic,
        DeadReckoning,
        Teleport,
        TeleportRudimentary,
    }

    public class Animation
    {
        public string Dictionary { get; set; }
        public string Name { get; set; }
        public bool Loop { get; set; }
    }

    public class SyncPed : RemotePlayer
    {
        public SynchronizationMode SyncMode;
        public long Host;
        public Ped Character;
        public Vector3 _position;
        public int VehicleNetHandle;
        public Vector3 _rotation;
        public bool _isInVehicle;
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
        public bool IsInLowCover;
        public bool IsOnLadder;
        public bool IsVaulting;
        public bool IsCoveringToLeft;
        public bool IsInMeleeCombat;
        public bool IsFreefallingWithParachute;
        public bool IsShooting;
        public bool IsInBurnout;
        private bool _lastBurnout;
        public float VehicleRPM;
	    public float SteeringScale;
        public bool EnteringVehicle;
        private bool _lastEnteringVehicle;
        public bool IsOnFire;
        public bool IsBeingControlledByScript;

        public bool ExitingVehicle;
        private bool _lastExitingVehicle;

        public int VehicleSeat;
        public int PedHealth;

        public float VehicleHealth;

        public int VehicleHash
        {
            get
            {
                if (VehicleNetHandle == 0) return 0;
                var car = Main.NetEntityHandler.NetToStreamedItem(VehicleNetHandle) as RemoteVehicle;
                return car.ModelHash;
            }
        }

        public Vector3 _vehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public bool Siren;
        public int PedArmor;
        public bool IsVehDead;
        public bool IsPlayerDead;
        public bool DirtyWeapons;

        private object _secondSnapshot;
        private object _firstSnapshot;

        private int _secondSnapshotTime;
        private int _firstSnapshotTime;

        public object Snapshot
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

        public byte OnFootSpeed;

        public bool IsParachuteOpen;

        public double AverageLatency
        {
            get { return _latencyAverager.Count == 0 ? 0 : _latencyAverager.Average(); }
        }

        public long LastUpdateReceived
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

        public long TicksSinceLastUpdate
        {
            get { return Util.TickCount - LastUpdateReceived; }
        }

        public int DataLatency
        {
            get
            {
                if (Debug) return Main._debugInterval;
                return (int)(((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2));
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
        
        private Vector3? _lastVehiclePos;
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
        public new Vector3 Position
        {
            get { return _isInVehicle ? _vehiclePosition : _position; }
            set
            {
                _lastPosition = _position;
                _position = value;
                if (!_isInVehicle)
                    _lastVehiclePos = null;
            }
        }

        private Vector3? _lastVehicleRotation;
        public Vector3 VehicleRotation
        {
            get { return _vehicleRotation; }
            set
            {
                _lastVehicleRotation = _vehicleRotation;
                _vehicleRotation = value;
            }
        }

        private Vector3? _lastRotation;
        public new Vector3 Rotation
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
            set { _isRagdoll = value; }
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
        private DateTime? _spazzout_prevention;
        
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private bool _lastVehicleShooting;

        private Queue<long> _latencyAverager;

        private Vector3 _lastStart;
        private Vector3 _lastEnd;

        private bool _lastReloading;
        public bool IsReloading
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

        public SyncPed(int hash, Vector3 pos, Vector3 rot, bool blip = true)
        {
            _position = pos;
            _rotation = rot;
            ModelHash = hash;
            _blip = blip;
            
            _latencyAverager = new Queue<long>();
        }

        public SyncPed()
        {
            _blip = true;
            _latencyAverager = new Queue<long>();
        }

        public override int LocalHandle
        {
            get { return Character?.Handle ?? 0; }
            set { }
        }

        public bool IsInVehicle
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

        public void SetBlipNameFromTextFile(Blip blip, string text)
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

        public bool IsCustomScenarioPlaying;
        public bool HasCustomScenarioStarted;
        public bool IsCustomAnimationPlaying;
        public string CustomAnimationDictionary;
        public string CustomAnimationName;
        public int CustomAnimationFlag;
        public long CustomAnimationStartTime;

        #region NeoSyncPed

        public bool CreateCharacter()
        {
            float hRange = _isInVehicle ? 150f : 200f;
            var gPos = _isInVehicle ? VehiclePosition : _position;
            var inRange = Game.Player.Character.IsInRangeOfEx(gPos, hRange);

            return CreateCharacter(gPos, hRange);
        }

        bool CreateCharacter(Vector3 gPos, float hRange)
        {
			if (Character == null || !Character.Exists() || (!Character.IsInRangeOfEx(gPos, hRange) && Environment.TickCount - LastUpdateReceived < 5000) || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
			{
				LogManager.DebugLog($"{Character == null}, {Character?.Exists()}, {Character?.Position} {gPos}, {hRange}, {Character?.IsInRangeOfEx(gPos, hRange)}, {Character?.Model.Hash}, {ModelHash}, {Character?.IsDead}, {PedHealth}");
                
				if (Character != null && Character.Exists()) Character.Delete();
                
				DEBUG_STEP = 3;

				LogManager.DebugLog("NEW PLAYER " + Name);

				var charModel = new Model(ModelHash);

				LogManager.DebugLog("REQUESTING MODEL FOR " + Name);

				Util.LoadModel(charModel);

				LogManager.DebugLog("CREATING PED FOR " + Name);

				Character = World.CreatePed(charModel, gPos, _rotation.Z);
				charModel.MarkAsNoLongerNeeded();

				if (Character == null) return true;


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

                if (PacketOptimization.CheckBit(Flag, EntityFlag.Collisionless))
                {
                    Character.IsCollisionEnabled = false;
                }

                JavascriptHook.InvokeStreamInEvent(new LocalHandle(Character.Handle), (int)GTANetworkShared.EntityType.Player);

                LogManager.DebugLog("ATTACHING BLIP FOR " + Name);

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

				return true;
			}
		    return false;
	    }

	    void DrawNametag()
	    {
	        if (!Main.UIVisible) return;
            
			bool isAiming = false;
			if ((!Character.IsOccluded && (Character.IsInRangeOfEx(Game.Player.Character.Position, 30f))) ||
				(isAiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, Character)))
			{
				if (Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, Game.Player.Character, Character, 17) || isAiming)
				{
					var oldPos = GTA.UI.Screen.WorldToScreen(Character.Position + new Vector3(0, 0, 1.2f));

				    Vector3 targetPos;

				    if (!IsInVehicle && Character.HasBone("IK_Head"))
				        targetPos = Character.GetBoneCoord(Bone.IK_Head) + new Vector3(0, 0, 0.5f);
                    else
                        targetPos = Character.AttachedBlip.Position + new Vector3(0, 0, 1.2f);
                    

                    if (oldPos.X != 0 && oldPos.Y != 0)
					{
						Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);
						DEBUG_STEP = 6;
						var nameText = Name == null ? "<nameless>" : Name;

						if (TicksSinceLastUpdate > 10000)
							nameText = "~r~AFK~w~~n~" + nameText;

                        var dist = (GameplayCamera.Position - Character.Position).Length();
						var sizeOffset = Math.Max(1f - (dist/30f), 0.3f);

                        new UIResText(nameText, new Point(0, 0), 0.4f * sizeOffset, Color.WhiteSmoke,
                            GTA.UI.Font.ChaletLondon, UIResText.Alignment.Centered)
						{
							Outline = true,
						}.Draw();
						DEBUG_STEP = 7;
						if (Character != null)
						{
							var armorColor = Color.FromArgb(200, 220, 220, 220);
							var bgColor = Color.FromArgb(100, 0, 0, 0);
							var armorPercent = Math.Min(Math.Max(PedArmor / 100f, 0f), 1f);
							var armorBar = (int)Math.Round(150 * armorPercent);
							armorBar = (int)(armorBar * sizeOffset);

							new UIResRectangle(
								new Point(0, 0) - new Size((int)(75 * sizeOffset), (int)(-36 * sizeOffset)),
								new Size(armorBar, (int)(20 * sizeOffset)),
								armorColor).Draw();

							new UIResRectangle(
								new Point(0, 0) - new Size((int)(75 * sizeOffset), (int)(-36 * sizeOffset)) +
								new Size(armorBar, 0),
								new Size((int)(sizeOffset * 150) - armorBar, (int)(sizeOffset * 20)),
								bgColor).Draw();

							new UIResRectangle(
								new Point(0, 0) - new Size((int)(71 * sizeOffset), (int)(-40 * sizeOffset)),
								new Size(
									(int)((142 * Math.Min(Math.Max((PedHealth / 100f), 0f), 1f)) * sizeOffset),
									(int)(12 * sizeOffset)),
								Color.FromArgb(150, 50, 250, 50)).Draw();
						}
						DEBUG_STEP = 8;
						Function.Call(Hash.CLEAR_DRAW_ORIGIN);
					}
				}
			}
		}

        public int _debugVehicleHash;
	    bool CreateVehicle()
	    {
	        if (_isInVehicle && MainVehicle != null && Character.IsInVehicle(MainVehicle) && Game.Player.Character.IsInVehicle(MainVehicle) && VehicleSeat == -1 &&
	            Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER, Game.Player.Character) == -1 &&
	            Util.GetPedSeat(Game.Player.Character) == 0)
	        {
	            Character.Task.WarpOutOfVehicle(MainVehicle);
                Game.Player.Character.Task.WarpIntoVehicle(MainVehicle, GTA.VehicleSeat.Driver);
	            Main.LastCarEnter = DateTime.Now;
                Script.Yield();
	            return true;
	        }

			if ((!_lastVehicle && _isInVehicle) ||
					(_lastVehicle && _isInVehicle &&
					 (MainVehicle == null || (!Character.IsInVehicle(MainVehicle) && Game.Player.Character.VehicleTryingToEnter != MainVehicle) ||
					  Main.NetEntityHandler.EntityToNet(MainVehicle.Handle) != VehicleNetHandle ||
					  (VehicleSeat != Util.GetPedSeat(Character) && Game.Player.Character.VehicleTryingToEnter != MainVehicle))))
			{
			    if (Debug)
			    {
			        if (MainVehicle != null) MainVehicle.Delete();
			        MainVehicle = World.CreateVehicle(new Model(_debugVehicleHash), VehiclePosition, VehicleRotation.Z);
			        //MainVehicle.HasCollision = false;
			    }
			    else
			    {
			        MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);
			    }
				DEBUG_STEP = 10;

			    if (MainVehicle == null || MainVehicle.Handle == 0)
			    {
			        Character.Position = VehiclePosition;
			        return true;
			    }
                

                if (Game.Player.Character.IsInVehicle(MainVehicle) &&
					VehicleSeat == Util.GetPedSeat(Game.Player.Character))
				{
				    if (DateTime.Now.Subtract(Main.LastCarEnter).TotalMilliseconds < 1000)
				    {
				        return true;
				    }

					Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
					Util.SafeNotify("~r~Car jacked!");
				}
				DEBUG_STEP = 11;

				if (MainVehicle != null && MainVehicle.Handle != 0)
				{
				    if (VehicleSeat == -1)
				    {
				        MainVehicle.Position = VehiclePosition;
				    }
				    else
				    {
				        Character.PositionNoOffset = MainVehicle.Position;
				    }

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
			    var delta = Util.TickCount - LastUpdateReceived;
                if (Character != null && delta < 10000)
				{
				    Vector3 lastPos = _isInVehicle
				        ? _lastVehiclePos == null ? VehiclePosition : _lastVehiclePos.Value
				        : _lastPosition == null ? Position : _lastPosition;

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
            if (_isInVehicle && MainVehicle != null && (Character.AttachedBlip == null || (Character.AttachedBlip.Position - MainVehicle.Position).Length() > 70f) && _blip)
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

			if (VehicleMods != null && _modSwitch % 50 == 0 &&
				Game.Player.Character.IsInRangeOfEx(VehiclePosition, 30f))
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
		    MainVehicle.SteeringAngle = Util.ToRadians(SteeringScale);
	    }

        struct interpolation
        {
            public Vector3 vecStart;
            public Vector3 vecTarget;
            public Vector3 vecError;
            public long StartTime;
            public long FinishTime;
            public float LastAlpha;
        }

        private interpolation currentInterop = new interpolation();

        public void StartInterpolation()
        {
            currentInterop = new interpolation();

            if (_isInVehicle)
            {
                if (_lastVehiclePos == null) return;
                if (Main.VehicleLagCompensation)
                {

                    var dir = VehiclePosition - _lastVehiclePos.Value;

                    currentInterop.vecTarget = VehiclePosition + dir;
                    currentInterop.vecError = dir;
                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
                else
                {
                    var dir = VehiclePosition - _lastVehiclePos.Value;

                    currentInterop.vecTarget = VehiclePosition;
                    currentInterop.vecError = dir;
                    currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
            }
            else
            {
                if (Main.OnFootLagCompensation)
                {
                    var dir = Position - _lastPosition;

                    currentInterop.vecTarget = Position; // + dir;
                    currentInterop.vecError = dir;
                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
                else
                {
                    var dir = Position - _lastPosition;

                    currentInterop.vecTarget = Position;
                    currentInterop.vecError = dir;
                    currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                }
            }

            currentInterop.StartTime = Util.TickCount - DataLatency;
            currentInterop.FinishTime = currentInterop.StartTime + 100;
            currentInterop.LastAlpha = 0f;
        }

        private int m_uiForceLocalZCounter;
        void DisplayVehiclePosition()
        {
            var spazzout = (_spazzout_prevention != null &&
                            DateTime.Now.Subtract(_spazzout_prevention.Value).TotalMilliseconds > 200);

            if ((Speed > 0.2f || IsInBurnout) && currentInterop.FinishTime > 0 && _lastVehiclePos != null && spazzout)
            {
                Vector3 newPos;

                if (Main.VehicleLagCompensation)
                {
                    long currentTime = Util.TickCount;
                    float alpha = Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                    Vector3 comp = Util.Lerp(new Vector3(), alpha, currentInterop.vecError);
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
                    long currentTime = Util.TickCount;
                    float alpha = Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                    alpha = Util.Clamp(0f, alpha, 1.5f);

                    float cAlpha = alpha - currentInterop.LastAlpha;
                    currentInterop.LastAlpha = alpha;

                    Vector3 comp = Util.Lerp(new Vector3(), cAlpha, currentInterop.vecError);

                    if (alpha == 1.5f)
                    {
                        currentInterop.FinishTime = 0;
                    }

                    newPos = VehiclePosition + comp;
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

#if !DISABLE_UNDER_FLOOR_FIX

                const int VEHICLE_INTERPOLATION_WARP_THRESHOLD = 15;
                const int VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 10;

                float fThreshold = (VEHICLE_INTERPOLATION_WARP_THRESHOLD + VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * Speed);

                if (MainVehicle.Position.DistanceTo(currentInterop.vecTarget) > fThreshold)
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
                }
#endif

                //GTA.UI.Screen.ShowSubtitle("alpha: " + alpha);

                //MainVehicle.Alpha = 100;


                _stopTime = DateTime.Now;
                _carPosOnUpdate = MainVehicle.Position;
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && _lastVehiclePos != null && spazzout)
            {
                var dir = VehiclePosition - _lastVehiclePos.Value;
                var posTarget = Util.LinearVectorLerp(_carPosOnUpdate, VehiclePosition + dir,
                    (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0, 0);
            }
            else
            {
                MainVehicle.PositionNoOffset = VehiclePosition;
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

        public bool IsFriend()
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

                        Function.Call(Hash.TASK_PLAY_ANIM_ADVANCED, Character, Util.LoadDict(drivebyDict),
                            "sweep_low", Character.Position.X, Character.Position.Y, Character.Position.Z, Character.Rotation.X,
                            Character.Rotation.Y, Character.Rotation.Z, -8f, -8f, -1, 0, rightSide ? 0.6f : 0.3f, 0, 0);
                    }

                    if (IsShooting)
                    {
                        Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);
                        Function.Call(Hash.SET_PED_AMMO, Character, CurrentWeapon, 10);

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
			UpdateVehicleMountedWeapon();

	        if (IsCustomAnimationPlaying)
	        {
	            DisplayCustomAnimation();
	        }

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

	        _lastExitingVehicle = ExitingVehicle;

	        if (ExitingVehicle) return true;
            
	        if (GetResponsiblePed(MainVehicle).Handle == Character.Handle &&
                Environment.TickCount - LastUpdateReceived < 10000)
	        {
	            UpdateVehicleMainData();
				if (DisplayVehicleDriveBy()) return true;
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
                //Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
			    //Character.Weapons.Select((WeaponHash) CurrentWeapon);

			    var p = IsInVehicle ? VehiclePosition : Position;

			    var wObj = Function.Call<int>(Hash.CREATE_WEAPON_OBJECT, CurrentWeapon, 999, p.X, p.Y, p.Z, false, 0, 0);
                
                if (WeaponTints != null && WeaponTints.ContainsKey(CurrentWeapon))
			    {
			        var bitmap = WeaponTints[CurrentWeapon];

                    //Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, Character, CurrentWeapon, bitmap);
                    Function.Call(Hash.SET_WEAPON_OBJECT_TINT_INDEX, wObj, bitmap);
			    }

			    if (WeaponComponents != null && WeaponComponents.ContainsKey(CurrentWeapon))
			    {
			        foreach (var comp in WeaponComponents[CurrentWeapon])
			        {
                        //Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, Character, CurrentWeapon, comp);
                        Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_WEAPON_OBJECT, wObj, comp);
                    }
			    }

                Function.Call(Hash.GIVE_WEAPON_OBJECT_TO_PED, wObj, Character);

			    DirtyWeapons = false;
                /*
			    GTA.UI.Screen.ShowNotification("Updating weapons for " + Name);
                GTA.UI.Screen.ShowSubtitle("Updating weapons for " + Name, 500);
                */
			}

	        if (!_lastReloading && IsReloading && ((IsInCover && !IsInLowCover) || !IsInCover))
	        {
                Character.Task.ClearAll();
	            Character.Task.ReloadWeapon();
	        }
		}

	    void DisplayParachuteFreefall()
	    {
            Character.IsPositionFrozen = true;
			Character.CanRagdoll = false;

			if (!_lastFreefall)
			{
				Character.Task.ClearAllImmediately();
				Character.Task.ClearSecondary();
			}

			var target = Util.LinearVectorLerp(_lastPosition,
				_position,
                TicksSinceLastUpdate, (int)AverageLatency);

			Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
				0);
			DEBUG_STEP = 25;
#if !DISABLE_SLERP
            var latency = DataLatency + TicksSinceLastUpdate;
            Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, latency / (float)AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif

            if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
					"skydive@base", "free_idle",
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character,
					Util.LoadDict("skydive@base"), "free_idle",
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
				Character.Task.ClearAllImmediately();
				Character.Task.ClearSecondary();
			}

			Character.IsPositionFrozen = true;
			Character.CanRagdoll = false;

			var target = Util.LinearVectorLerp(_lastPosition,
				_position,
                TicksSinceLastUpdate, (int)AverageLatency);

			Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
				0);
			DEBUG_STEP = 25;

#if !DISABLE_SLERP
            var latency = DataLatency + TicksSinceLastUpdate;
            Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, latency / (float)AverageLatency));
#else
	        Character.Quaternion = Rotation.ToQuaternion();
#endif

            _parachuteProp.Position = Character.Position + new Vector3(0, 0, 3.7f) +
									  Character.ForwardVector * 0.5f;
			_parachuteProp.Quaternion = Character.Quaternion;
			if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character,
					"skydive@parachute@first_person", "chute_idle_right",
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character,
					Util.LoadDict("skydive@parachute@first_person"), "chute_idle_right",
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
                    Function.Call(Hash.TASK_PLAY_ANIM, Character,
                        Util.LoadDict(CustomAnimationDictionary), CustomAnimationName,
                        8f, 10f, -1, CustomAnimationFlag, -8f, 1, 1, 1);
                    CustomAnimationStartTime = Util.TickCount;
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
                    Util.TickCount - CustomAnimationStartTime >
                    Function.Call<float>(Hash.GET_ENTITY_ANIM_TOTAL_TIME, Character, CustomAnimationDictionary,
                        CustomAnimationName) &&
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
				var gunEntity = Function.Call<Entity>((Hash)0x3B390A939AF0B5FC, Character);
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
            
			if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
					8f, 10f, -1, 0, -8f, 1, 1, 1);
			}

			if (secondaryAnimDict != null &&
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, secAnim,
					3))
			{
                Character.Task.ClearSecondary();
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(secondaryAnimDict), secAnim,
					8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
			}

			UpdatePlayerPedPos();
		}

	    void DisplayAimingAnimation()
	    {
            var hands = GetWeaponHandsHeld(CurrentWeapon);
	        if (IsReloading)
	        {
                UpdatePlayerPedPos();
                return;
	        }

	        
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
                    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict("weapons@projectile@"), "aimlive_m",
                        8f, 10f, -1, 0, -8f, 1, 1, 1);
                }
            }
            else
#endif
                if (hands == 1 || hands == 2 || hands == 5 || hands == 6)
            {
                Character.Task.ClearSecondary();

                var latency = DataLatency + TicksSinceLastUpdate;
                var dir = Position - _lastPosition;
                var posTarget = Vector3.Lerp(Position, Position + dir,
                    latency / ((float)AverageLatency));

                var ndir = posTarget - Character.Position;
                
                if (ndir.LengthSquared() > 1e-3)
                {
                    if (Game.GameTime - _lastVehicleAimUpdate > 40)
                    {
                        ndir.Normalize();

                        var target = Character.Position + ndir*20f;

                        Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character, target.X, target.Y,
                            target.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 3f, false, 2f, 2f, true, 512, false,
                            unchecked ((int) FiringPattern.FullAuto));

                        _lastVehicleAimUpdate = Game.GameTime;
                    }
                }
                else
                {
                    Character.Task.AimAt(AimCoords, 100);
                }
			}

            UpdatePlayerPedPos();
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
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(ourAnim.Split()[0]),
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


	        if (!IsInCover)
	        {
                Character.Task.ClearSecondary();

	            if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
	            {
	                Character.Task.ClearAnimation(animDict, ourAnim);
	            }
	        }
	        else
	        {
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
                    8f, 10f, -1, 2, -8f, 1, 1, 1);
            }

	        Function.Call(Hash.SET_AI_WEAPON_DAMAGE_MODIFIER, 1f);
	        Function.Call(Hash.SET_PED_SHOOT_RATE, Character, 100);
            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);

            if (!IsInCover)
            if (Game.GameTime - _lastVehicleAimUpdate > 30)
            {
                //Character.Task.AimAt(AimCoords, -1);
                var latency = DataLatency + TicksSinceLastUpdate;
                var dir = Position - _lastPosition;
                var posTarget = Vector3.Lerp(Position, Position + dir,
                    latency / ((float)AverageLatency));

                var ndir = posTarget - Character.Position;
                ndir.Normalize();

                var target = Character.Position + ndir * 20f;

                Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character, target.X, target.Y,
                    target.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 3f, false, 2f, 2f, true, 512, false,
                    unchecked((int)FiringPattern.FullAuto));
                _lastVehicleAimUpdate = Game.GameTime;
            }


            var gunEnt = Function.Call<Entity>((Hash)0x3B390A939AF0B5FC, Character);
	        if (gunEnt != null)
	        {
	            var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, -0.01f));
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
            
            UpdatePlayerPedPos();

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

	    void DisplayWalkingAnimation()
	    {
	        if (IsReloading || (IsInCover && IsShooting && !IsAiming)) return;

            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
			var animDict = GetAnimDictionary(ourAnim);
			var secondaryAnimDict = GetSecondaryAnimDict();
	        var flag = GetAnimFlag();

			DEBUG_STEP = 34;

			if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
					8f, 10f, -1, flag, -8f, 1, 1, 1);
			}
            
			
			if (secondaryAnimDict != null &&
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, ourAnim,
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(secondaryAnimDict), ourAnim,
					8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
			}
            else if (secondaryAnimDict == null)
            {
                Character.Task.ClearSecondary();
            }
		}

		bool UpdateOnFootPosition()
		{
            UpdateProps();
			
			DEBUG_STEP = 23;

			UpdateCurrentWeapon();

			if (!_lastJumping && IsJumping)
			{
				//Character.FreezePosition = false;
				Character.Task.Jump();
			}

			if (!IsJumping && _lastJumping)
			{
				//Character.FreezePosition = true;
			}

		    var isonfire = Function.Call<bool>(Hash.IS_ENTITY_ON_FIRE, Character);
            
            if (IsOnFire && !isonfire)
            {
                Character.IsInvincible = false;
                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);
                _scriptFire = Function.Call<int>(Hash.START_ENTITY_FIRE, Character);
            }
            else if (!IsOnFire && isonfire)
            {
                Function.Call(Hash.STOP_ENTITY_FIRE, Character);
                Character.IsInvincible = true;
                if (Character.IsDead) Function.Call(Hash.RESURRECT_PED, Character);

                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);

                _scriptFire = 0;
            }

		    if (EnteringVehicle && !_lastEnteringVehicle)
		    {
		        Entity targetVeh = null;
		        if (Debug)
		        {
		             targetVeh = World.GetAllVehicles().OrderBy(v => v.Position.DistanceToSquared(Position)).FirstOrDefault();
		        }
		        else
		        {
		            targetVeh = Main.NetEntityHandler.NetToEntity(VehicleNetHandle);
		        }

		        if (targetVeh != null)
		        {
                    Character.Task.ClearAll();
                    Character.Task.ClearSecondary();
		            Character.IsPositionFrozen = false;
		            Character.Task.EnterVehicle(new Vehicle(targetVeh.Handle), (GTA.VehicleSeat) VehicleSeat, -1, 2f);
		        }
		    }

		    _lastEnteringVehicle = EnteringVehicle;

		    if (EnteringVehicle) return true;

            Character.CanBeTargetted = true;

            DEBUG_STEP = 24;
			if (IsFreefallingWithParachute)
			{
				DisplayParachuteFreefall();
			}
			else if (IsParachuteOpen)
			{
				DisplayOpenParachute();
			}
			else
			{
				Character.IsPositionFrozen = false;

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

                    var dir = Position - _lastPosition;
                    var vdir = PedVelocity - _lastPedVel;
                    var target = Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                        TicksSinceLastUpdate,
                        (int)AverageLatency);

                    var posTarget = Util.LinearVectorLerp(Position, Position + dir,
                        TicksSinceLastUpdate,
                        (int)AverageLatency);
                    
                    Character.Velocity = target + 2 * (posTarget - Character.Position);
                    _stopTime = DateTime.Now;
                    _carPosOnUpdate = Character.Position;
                    
                    return true;
			    }
                else if (!ragdoll && Character.IsRagdoll)
                {
                    Character.CanRagdoll = false;
                    Character.Task.ClearAllImmediately();
                    Character.PositionNoOffset = Position;

                    if (!IsPlayerDead)
                    {
                        Function.Call(Hash.TASK_PLAY_ANIM, Character,
                            Util.LoadDict("get_up@standard"), "back",
                            12f, 12f, -1, 0, -10f, 1, 1, 1);
                    }

                    return true;
                }

			    var getupAnim = GetAnimalGetUpAnimation().Split();

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, getupAnim[0], getupAnim[1], 3))
                {
                    UpdatePlayerPedPos();
                    var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character, getupAnim[0], getupAnim[1]);

                    if (currentTime >= 0.7f)
                    {
                        Character.Task.ClearAnimation(getupAnim[0], getupAnim[1]);
                        Character.Task.ClearAll();
                    }
                    else
                    {
                        return true;
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
				if (IsAiming && !IsShooting)
				{
					DisplayAimingAnimation();
				}

				DEBUG_STEP = 30;
                

				if (IsShooting)
				{
					DisplayShootingAnimation();
				}

			    if (IsCustomAnimationPlaying)
			    {
                    UpdatePlayerPedPos();

			        DisplayCustomAnimation();
			    }

                DEBUG_STEP = 32;
				if (!IsAiming && !IsShooting && !IsJumping && !IsInMeleeCombat && !IsCustomAnimationPlaying)
				{
					UpdatePlayerPedPos();

					DisplayWalkingAnimation();
				}
			}

			return false;
	    }


		private int DEBUG_STEP_backend;
        private bool _initialized;
        private bool _isReloading;

        private const float hRange = 1000f; // 1km
        private const float physicsRange = 175f;

        public void DisplayLocally()
        {
            try
            {
                if (IsSpectating || (Flag & (int) EntityFlag.PlayerSpectating) != 0 || ModelHash == 0 || string.IsNullOrEmpty(Name)) return;

                var gPos = _isInVehicle ? VehiclePosition : _position;
                var inRange = Game.Player.Character.IsInRangeOfEx(gPos, hRange);

                if (!StreamedIn)
                {
                    if (Character != null && Character.Exists())
                    {
                        Character.Delete();
                    }

                    if (_parachuteProp != null && _parachuteProp.Exists())
                    {
                        _parachuteProp.Delete();
                    }

                    if (_mainBlip == null || !_mainBlip.Exists())
                    {
                        _mainBlip = World.CreateBlip(gPos);
                        SetBlipNameFromTextFile(_mainBlip, Name);
                    }

                    if (BlipSprite != -1)
                        _mainBlip.Sprite = (BlipSprite)BlipSprite;
                    if (BlipColor != -1)
                        _mainBlip.Color = (BlipColor)BlipColor;
                    else
                        _mainBlip.Color = GTA.BlipColor.White;
                    _mainBlip.Scale = 0.8f;
                    _mainBlip.Alpha = BlipAlpha;

                    Vector3 lastPos = _isInVehicle
                        ? _lastVehiclePos == null ? VehiclePosition : _lastVehiclePos.Value
                        : _lastPosition == null ? Position : _lastPosition;
                    var delta = Util.TickCount - LastUpdateReceived;

                    _mainBlip.Position = Vector3.Lerp(lastPos, gPos,Math.Min(delta / 1000f, 1f));
                }
                else if (StreamedIn && _mainBlip != null && _mainBlip.Exists())
                {
                    _mainBlip.Remove();
                    _mainBlip = null;
                }


                DEBUG_STEP = 0;
                
                if (CrossReference.EntryPoint.CurrentSpectatingPlayer == this ||
                    (Character != null && Main.NetEntityHandler.NetToEntity(CrossReference.EntryPoint.SpectatingEntity)?.Handle == Character.Handle))
                    inRange = true;

                DEBUG_STEP = 1;
                
                if (Character != null && (Character.IsSubtaskActive(67) || IsBeingControlledByScript))
                {
                    DrawNametag();
                    return;
                }

                if (CreateCharacter(gPos, hRange)) return;

                DEBUG_STEP = 5;

	            DrawNametag();

                DEBUG_STEP = 9;

	            if (CreateVehicle()) return;
                
				_switch++;
                DEBUG_STEP = 15;

	            if (UpdatePlayerPosOutOfRange(gPos, inRange)) return;

                DEBUG_STEP = 16;

	            WorkaroundBlip();

                if (Character != null)
                {
                    Character.Health = PedHealth;
                    if (IsPlayerDead && !Character.IsDead && IsInVehicle)
                    {
                        Function.Call(Hash.SET_PED_PLAYS_HEAD_ON_HORN_ANIM_WHEN_DIES_IN_VEHICLE, Character, true);
                        Character.IsInvincible = false;
                        Character.Kill();
                    }
                }

                if (UpdatePosition()) return;


                _lastJumping = IsJumping;
                _lastFreefall = IsFreefallingWithParachute;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
                _lastVehicle = _isInVehicle;
                _lastEnteringVehicle = EnteringVehicle;
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
            if (veh == null || veh.Handle == 0 || !veh.Exists()) return new Ped(0);

            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
            }

            return new Ped(0);
        }

        public string GetAnimDictionary(string ourAnim = "")
        {
            if (IsInCover) return GetCoverIdleAnimDict();
            if (IsOnLadder) return "laddersbase";
            if (IsVaulting) return "move_climb";

            if (GetAnimalAnimationDictionary(ModelHash) != null)
                return GetAnimalAnimationDictionary(ModelHash);

            string dict = "move_m@generic";

            if (Character.Gender == Gender.Female)
                dict = "move_f@generic";

            dict = Character.IsInWater ? ourAnim == "idle" ? "swimming@base" : "swimming@swim" : dict;

            return dict;
        }

        public uint GetAnimFlag()
        {
            if (IsVaulting)
                return 2 | 2147483648;
            return 1 | 2147483648; // Loop + dont move
        }

        private int m_uiForceLocalCounter;
        private void UpdatePlayerPedPos()
        {
            Vector3 newPos;
            Vector3 velTarget = new Vector3();

            if (Main.OnFootLagCompensation)
            {
                long currentTime = Util.TickCount;

                float alpha = Util.Unlerp(currentInterop.StartTime, currentTime, currentInterop.FinishTime);

                Vector3 comp = Util.Lerp(new Vector3(), alpha, currentInterop.vecError);

                newPos = Position + comp;
            }
            else
            {
                var dir = Position - _lastPosition;
                var vdir = PedVelocity - _lastPedVel;
                
                var latency = DataLatency + TicksSinceLastUpdate;

                velTarget = Vector3.Lerp(PedVelocity, PedVelocity + vdir,
                    latency / ((float)AverageLatency));
                newPos = Vector3.Lerp(Position, Position + dir,
                    latency / ((float)AverageLatency));
            }

            if (OnFootSpeed > 0 || IsAnimal(ModelHash))
            {
                if (Game.Player.Character.IsInRangeOfEx(newPos, physicsRange))
                {
                    if (Main.OnFootLagCompensation)
                        Character.Velocity = PedVelocity + 10*(newPos - Character.Position);
                    else
                        Character.Velocity = velTarget + 2*(newPos - Character.Position);
                }
                else
                {
                    Character.PositionNoOffset = newPos;
                }


                _stopTime = DateTime.Now;
                _carPosOnUpdate = Character.Position;

#if !DISABLE_UNDER_FLOOR_FIX

                const int PED_INTERPOLATION_WARP_THRESHOLD = 5;
                const int PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 5;

                // Check if the distance to interpolate is too far.
                float fThreshold = (PED_INTERPOLATION_WARP_THRESHOLD +
                                    PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED*PedVelocity.Length());

                // There is a reason to have this condition this way: To prevent NaNs generating new NaNs after interpolating (Comparing with NaNs always results to false).
                if (Character.Position.DistanceTo(currentInterop.vecTarget) > fThreshold || Character.Position.DistanceToSquared(currentInterop.vecTarget) > 25)
                {
                    // Abort all interpolation
                    currentInterop.FinishTime = 0;
                    Character.PositionNoOffset = currentInterop.vecTarget;
                }

                // Calc remote movement
                var vecRemoteMovement = Position - _lastPosition;
                
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
                else
                if (m_uiForceLocalCounter++ > 1)
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
                }

#endif
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000)
            {
                var posTarget = Util.LinearVectorLerp(_carPosOnUpdate, Position + (Position - _lastPosition),
                    (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0);
            }
            else
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,
                    Position.Z, 0, 0, 0);
            }

            if (Debug)
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
#if !DISABLE_SLERP
            Character.Quaternion = GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(),
                Math.Min(1f, (DataLatency + TicksSinceLastUpdate) / (float)AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif
        }

        public string GetCoverIdleAnimDict()
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

        public string GetSecondaryAnimDict()
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
                weapon == unchecked((int)WeaponHash.SawnOffShotgun) ||
                weapon == unchecked((int)WeaponHash.CompactRifle))
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

        public string GetMovementAnim(int speed, bool inCover, bool coverFacingLeft)
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

            if (speed == 0) return "idle";
            if (speed == 1) return "walk";
            if (speed == 2) return "run";
            if (speed == 3) return "sprint";
            return "";
        }

        public static bool IsAnimal(int model)
        {
            return GetAnimalAnimationDictionary(model) != null;
        }

        public static string GetAnimalAnimationName(int modelhash, int speed)
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

        public static string GetAnimalAnimationDictionary(int modelhash)
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

        public string GetAnimalGetUpAnimation()
        {
            var hash = (PedHash) ModelHash;

            if (hash == PedHash.Boar)
                return "creatures@boar@getup getup_l";
            

            return "get_up@standard back";
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

#endregion

    }

}