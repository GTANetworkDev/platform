#define DISABLE_SLERP

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetworkShared;
using NativeUI;
using Font = GTA.Font;
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
        public bool IsInLowCover;
        public bool IsCoveringToLeft;
        public bool IsInMeleeCombat;
        public bool IsFreefallingWithParachute;
        public bool IsShooting;
        public float VehicleRPM;
	    public float SteeringScale;

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

        public int LastUpdateReceived
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

        private Dictionary<int, int> _pedTextures;
        public Dictionary<int, int> PedTextures
        {
            get { return _pedTextures; }
            set
            {
                if (value == null) return;
                _pedTextures = value;
            }
        }

        private Dictionary<int, Tuple<int, int>> _pedAccessories;
        public Dictionary<int, Tuple<int, int>> PedAccessories
        {
            get { return _pedAccessories; }
            set
            {
                if (value == null) return;
                _pedAccessories = value;
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
        public new Vector3 Position
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
        
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private bool _lastVehicleShooting;

        private Queue<double> _latencyAverager;

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
            
            _latencyAverager = new Queue<double>();
        }

        public SyncPed()
        {
            _blip = true;
            _latencyAverager = new Queue<double>();
        }

        public new int LocalHandle
        {
            get { return Character?.Handle ?? 0; }
            set { }
        }

        public void SetBlipNameFromTextFile(Blip blip, string text)
        {
            Function.Call(Hash._0xF9113A30DE5C6670, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0xBC38B49BCB83BC9B, blip);
        }

        private int _modSwitch = 0;
        private int _clothSwitch = 0;
        private int _lastUpdateReceived;
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

        public bool IsCustomScenarioPlaying;
        public bool HasCustomScenarioStarted;
        public bool IsCustomAnimationPlaying;
        public string CustomAnimationDictionary;
        public string CustomAnimationName;
        public int CustomAnimationFlag;

        #region NeoSyncPed

        bool CreateCharacter(Vector3 gPos, float hRange)
        {
			if (Character == null || !Character.Exists() || (!Character.IsInRangeOf(gPos, hRange) && Environment.TickCount - LastUpdateReceived < 5000) || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
			{
				//LogManager.DebugLog($"{Character == null}, {Character?.Exists()}, {Character?.IsInRangeOf(gPos, hRange)}, {Character?.Model.Hash}, {ModelHash}, {Character?.IsDead}, {PedHealth}");
                
				if (Character != null && Character.Exists()) Character.Delete();
                
				DEBUG_STEP = 3;

				LogManager.DebugLog("NEW PLAYER " + Name);

				var charModel = new Model(ModelHash);

				LogManager.DebugLog("REQUESTING MODEL FOR " + Name);

				charModel.Request(10000);

				LogManager.DebugLog("CREATING PED FOR " + Name);

				Character = World.CreatePed(charModel, gPos, _rotation.Z);
				charModel.MarkAsNoLongerNeeded();

				if (Character == null) return true;


			    Character.CanBeTargetted = true;


				DEBUG_STEP = 4;

				Character.BlockPermanentEvents = true;
				Character.IsInvincible = true;
				Character.CanRagdoll = false;

			    if (Team == -1 || Team != Main.LocalTeam)
			    {
			        Character.RelationshipGroup = Main.RelGroup;
                    Function.Call(Hash.SET_PED_AS_ENEMY, Character, true);
			    }
			    else
			    {
			        Character.RelationshipGroup = Main.FriendRelGroup;
			    }

				LogManager.DebugLog("SETTINGS FIRING PATTERN " + Name);

				Character.FiringPattern = FiringPattern.FullAuto;

				Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Character); //BUG: <- Maybe causes crash?

                Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, Character, false);

				LogManager.DebugLog("SETTING CLOTHES FOR " + Name);

				if (PedProps != null)
					foreach (var pair in PedProps)
					{
						Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value, PedTextures[pair.Key], 2);
					}

			    if (PedAccessories != null)
			    {
			        foreach (var pair in PedAccessories)
			        {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value.Item1, pair.Value.Item2, 2);
                    }
			    }

				LogManager.DebugLog("ATTACHING BLIP FOR " + Name);

				if (_blip)
				{
					Character.AddBlip();

					if (Character.CurrentBlip == null || !Character.CurrentBlip.Exists()) return true;

					LogManager.DebugLog("SETTING BLIP COLOR FOR" + Name);

                    if (BlipSprite != -1)
                        Character.CurrentBlip.Sprite = (BlipSprite)BlipSprite;

                    if (BlipColor != -1)
						Character.CurrentBlip.Color = (BlipColor)BlipColor;
					else
						Character.CurrentBlip.Color = GTA.BlipColor.White;

					LogManager.DebugLog("SETTING BLIP SCALE FOR" + Name);

					Character.CurrentBlip.Scale = 0.8f;

					LogManager.DebugLog("SETTING BLIP NAME FOR" + Name);

					SetBlipNameFromTextFile(Character.CurrentBlip, Name);

					
					if (BlipAlpha != -1)
						Character.CurrentBlip.Alpha = BlipAlpha;

					LogManager.DebugLog("BLIP DONE FOR" + Name);
				}

				return true;
			}
		    return false;
	    }

	    void DrawNametag()
	    {
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

							if (Environment.TickCount - LastUpdateReceived > 10000)
								nameText = "~r~AFK~w~~n~" + nameText;
                            var dist = (GameplayCamera.Position - Character.Position).Length();
							var sizeOffset = Math.Max(1f - (dist / 30f), 0.3f);

							new UIResText(nameText, new Point(0, 0), 0.4f * sizeOffset, Color.WhiteSmoke,
								Font.ChaletLondon, UIResText.Alignment.Centered)
							{
								Outline = true,
							}.Draw();
							DEBUG_STEP = 7;
							if (Character != null)
							{
								var armorColor = Color.FromArgb(100, 220, 220, 220);
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
										(int)((142 * Math.Min(Math.Max(2 * (PedHealth / 100f), 0f), 1f)) * sizeOffset),
										(int)(12 * sizeOffset)),
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
				    int offsetY = 0;
				    if (GetResponsiblePed(MainVehicle)?.Handle != Character.Handle)
				    {
				        offsetY = (VehicleSeat + 1)*20;
				    }

					var nameText = Name == null ? "<nameless>" : Name;

					if (Environment.TickCount - LastUpdateReceived > 10000)
						nameText = "~r~AFK~w~~n~" + nameText;

                    var dist = (GameplayCamera.Position - Character.Position).Length();
					var sizeOffset = Math.Max(1f - (dist / 100f), 0.3f);

					new UIResText(nameText, new Point(0, -offsetY), 0.4f * sizeOffset, Color.WhiteSmoke,
						Font.ChaletLondon, UIResText.Alignment.Centered)
					{
						Outline = true,
					}.Draw();
					DEBUG_STEP = 7;
					if (Character != null && offsetY == 0)
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
		}

	    bool CreateVehicle()
	    {
	        if (IsInVehicle && MainVehicle != null && Character.IsInVehicle(MainVehicle) && Game.Player.Character.IsInVehicle(MainVehicle) && VehicleSeat == -1 &&
	            Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER, Game.Player.Character) == -1 &&
	            Util.GetPedSeat(Game.Player.Character) == 0)
	        {
	            Character.Task.WarpOutOfVehicle(MainVehicle);
                Game.Player.Character.Task.WarpIntoVehicle(MainVehicle, GTA.VehicleSeat.Driver);
	            Main.LastCarEnter = DateTime.Now;
                Script.Yield();
	            return true;
	        }

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
				    //MainVehicle.HasCollision = false;
				}
				else
					MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);
				DEBUG_STEP = 10;

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
						MainVehicle.Position = VehiclePosition;
					MainVehicle.EngineRunning = true;
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
				if (Character != null && Environment.TickCount - LastUpdateReceived < 10000)
				{
				    if (!IsInVehicle)
				    {
				        Character.PositionNoOffset = gPos;
				    }
					else if (MainVehicle != null && GetResponsiblePed(MainVehicle).Handle == Character.Handle)
					{
						MainVehicle.Position = VehiclePosition;
						MainVehicle.Rotation = VehicleRotation;
                    }
				}
				return true;
			}
		    return false;
	    }

	    void WorkaroundBlip()
	    {
            if (IsInVehicle && MainVehicle != null && (Character.CurrentBlip == null || (Character.CurrentBlip.Position - MainVehicle.Position).Length() > 70f) && _blip)
			{
				LogManager.DebugLog("Blip was too far away -- deleting");
				Character.Delete();
			}
		}

	    bool UpdatePosition()
	    {
            return IsInVehicle ? UpdateVehiclePosition() : UpdateOnFootPosition();
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
				Game.Player.Character.IsInRangeOf(VehiclePosition, 30f))
			{
				var id = _modSwitch / 50;

				if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod((VehicleMod)id))
				{
					Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
					MainVehicle.SetMod((VehicleMod)id, VehicleMods[id], false);
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
		    MainVehicle.SteeringAngle = Util.ToRadians(SteeringScale);
	    }

        void DisplayVehiclePosition()
        {
            var dir = VehiclePosition - _lastVehiclePos;
            var vdir = VehicleVelocity - _lastVehVel;

            Vector3 target, posTarget;

            var latency = ((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2);
            
            if (Speed > 10)
            {
                target = Vector3.Lerp(VehicleVelocity, VehicleVelocity + vdir,
                    (latency) / ((float)AverageLatency));

                posTarget = Vector3.Lerp(VehiclePosition, VehiclePosition + dir,
                    (latency) / ((float)AverageLatency));
            }
            else
            {
                target = Util.LinearVectorLerp(VehicleVelocity, VehicleVelocity + vdir,
                    Environment.TickCount - LastUpdateReceived, (int)AverageLatency);

                posTarget = Util.LinearVectorLerp(VehiclePosition, VehiclePosition + dir,
                    Environment.TickCount - LastUpdateReceived, (int)AverageLatency);
            }

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
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y,
                    posTarget.Z, 0, 0, 0, 0);
            }
            else
            {
                MainVehicle.PositionNoOffset = VehiclePosition;
            }

            DEBUG_STEP = 21;
#if !DISABLE_SLERP
            MainVehicle.Quaternion = Quaternion.Slerp(_lastVehicleRotation.ToQuaternion(), _vehicleRotation.ToQuaternion(),
			        (float) Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
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
				else if ((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Savage ||
				         (VehicleHash) VehicleHash == GTA.Native.VehicleHash.Hydra ||
				         (VehicleHash) VehicleHash == GTA.Native.VehicleHash.Lazer)
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
					if (((VehicleHash)VehicleHash == GTA.Native.VehicleHash.Rhino &&
						 DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 1000) ||
						((VehicleHash)VehicleHash != GTA.Native.VehicleHash.Rhino))
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
						if ((VehicleHash)VehicleHash == GTA.Native.VehicleHash.Rhino)
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
						if ((VehicleHash)VehicleHash == GTA.Native.VehicleHash.Rhino)
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
					Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
				}

				if (IsShooting/* || IsAiming*/)
				{
					if (_lastShooting && Game.GameTime - _lastVehicleAimUpdate > 30)
					{
					    if (IsShooting)
					    {
					        Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);
					    }
					    else if (IsAiming)
					    {
                            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, false);
                            Function.Call(Hash.SET_PED_AMMO, Character, CurrentWeapon, 0);
                        }

					    Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z);
					}

					if (!_lastShooting)
					{
						Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z,
							0, 0, 0, unchecked((int)FiringPattern.FullAuto));
					}

					_lastVehicleAimUpdate = Game.GameTime;
					_lastDrivebyShooting = IsShooting || IsAiming;
				}

				if (!IsShooting && /*!IsAiming &&*/ _lastDrivebyShooting && Game.GameTime - _lastVehicleAimUpdate > 200)
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
		}

	    bool UpdateVehiclePosition()
	    {
			UpdateVehicleMountedWeapon();

	        if (IsCustomAnimationPlaying)
	        {
	            DisplayCustomAnimation();
	        }
            
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
            if (PedProps != null && _clothSwitch % 50 == 0 && Game.Player.Character.IsInRangeOf(IsInVehicle ? VehiclePosition : _position, 30f))
			{
				var id = _clothSwitch / 50;

				if (PedProps.ContainsKey(id) &&
					PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
				{
					Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
				}
			}
            */
			_clothSwitch++;
			if (_clothSwitch >= 750)
				_clothSwitch = 0;
		}

	    void UpdateCurrentWeapon()
	    {
            if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon)
			{
				//Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, -1, true, true);
				//Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);

				Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
			}

	        if (!_lastReloading && IsReloading && ((IsInCover && !IsInLowCover) || !IsInCover))
	        {
                Character.Task.ClearAll();
	            Character.Task.ReloadWeapon();
	        }
		}

	    void DisplayParachuteFreefall()
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
                Environment.TickCount - LastUpdateReceived, (int)AverageLatency);

			Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
				0);
			DEBUG_STEP = 25;
#if !DISABLE_SLERP
            Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
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
				_parachuteProp.FreezePosition = true;
				Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);
				Character.Task.ClearAllImmediately();
				Character.Task.ClearSecondary();
			}

			Character.FreezePosition = true;
			Character.CanRagdoll = false;

			var target = Util.LinearVectorLerp(_lastPosition,
				_position,
                Environment.TickCount - LastUpdateReceived, (int)AverageLatency);

			Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0,
				0);
			DEBUG_STEP = 25;

            #if !DISABLE_SLERP
            Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
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
                }

                var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character,
                    CustomAnimationDictionary, CustomAnimationName);

                if (currentTime >= .95f && (CustomAnimationFlag & 1) == 0)
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
				var gunEntity = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
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
                Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
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

                if (Game.GameTime - _lastVehicleAimUpdate > 40)
                {
                    //Character.Task.AimAt(AimCoords, -1);
                    var latency = ((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2);
                    var dir = Position - _lastPosition;
                    var posTarget = Vector3.Lerp(Position, Position + dir,
                        latency / ((float)AverageLatency));

                    var ndir = posTarget - Character.Position;
                    ndir.Normalize();

                    var target = Character.Position + ndir * 20f;

                    Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character, target.X, target.Y,
                        target.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 3f, false, 2f, 2f, true, 512, false,
                        unchecked ((int) FiringPattern.FullAuto));
                    _lastVehicleAimUpdate = Game.GameTime;
                }
			}

            UpdatePlayerPedPos();
            /*
			var dirVector = Position - _lastPosition;

			var target = Util.LinearVectorLerp(Position,
				(Position) + dirVector,
                Environment.TickCount - LastUpdateReceived, (int)AverageLatency);
			Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0,
				0, 0, 0);
                */
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
            Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif
        }

	    void DisplayWeaponShootingAnimation()
	    {
            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
			var animDict = GetAnimDictionary(ourAnim);
			var secondaryAnimDict = GetSecondaryAnimDict();

            Character.Task.ClearSecondary();
            
			if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
			{
				Character.Task.ClearAnimation(animDict, ourAnim);
			}

            Function.Call(Hash.SET_AI_WEAPON_DAMAGE_MODIFIER, 1f);
	        Function.Call(Hash.SET_PED_SHOOT_RATE, Character, 100);
            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);

            if (Game.GameTime - _lastVehicleAimUpdate > 30)
            {
                //Character.Task.AimAt(AimCoords, -1);
                var latency = ((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2);
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

            if (!WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
	        {
	            Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, Character, AimCoords.X, AimCoords.Y, AimCoords.Z, true);
	        }
	        else
	        {

	            var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Character);
	            if (gunEnt != null)
	            {
	                var start = gunEnt.GetOffsetInWorldCoords(new Vector3(0, 0, -0.01f));
	                var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash) CurrentWeapon);
	                var speed = 0xbf800000;
	                var weaponH = (WeaponHash) CurrentWeapon;


	                if (weaponH == WeaponHash.Minigun)
	                    weaponH = WeaponHash.CombatPDW;

	                var dir = (AimCoords - start);
	                dir.Normalize();
	                var end = start + dir*100f;

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
                Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
#else
                Character.Quaternion = Rotation.ToQuaternion();
#endif
            }
		}

	    void DisplayWalkingAnimation()
	    {
	        if (IsReloading) return;

            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
			var animDict = GetAnimDictionary(ourAnim);
			var secondaryAnimDict = GetSecondaryAnimDict();

			DEBUG_STEP = 34;
			if (
				!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim,
					3))
			{
				Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.LoadDict(animDict), ourAnim,
					8f, 10f, -1, 1 | 2147483648, -8f, 1, 1, 1);
			}

            

			// BUG: Animation doesn't clear for 1-2 seconds after aiming.

			
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
				Character.FreezePosition = false;

				if (_parachuteProp != null)
				{
					_parachuteProp.Delete();
					_parachuteProp = null;
				}
				DEBUG_STEP = 27;

			    if (IsRagdoll)
			    {
			        if (!Character.IsRagdoll)
			        {
			            Character.CanRagdoll = true;
                        Function.Call(Hash.SET_PED_TO_RAGDOLL, Character, 50000, 60000, 0, 1, 1, 1);
			        }

                    

                    var dir = Position - _lastPosition;
                    var vdir = PedVelocity - _lastPedVel;
                    var target = Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                        Environment.TickCount - LastUpdateReceived,
                        (int)AverageLatency);

                    var posTarget = Util.LinearVectorLerp(Position, Position + dir,
                        Environment.TickCount - LastUpdateReceived,
                        (int)AverageLatency);
                    
                    Character.Velocity = target + 2 * (posTarget - Character.Position);
                    _stopTime = DateTime.Now;
                    _carPosOnUpdate = Character.Position;
                    
                    return true;
			    }
                else if (!IsRagdoll && Character.IsRagdoll)
                {
                    Character.CanRagdoll = false;
                    Character.Task.ClearAllImmediately();

                    if (PedHealth > 0)
                    {
                        Function.Call(Hash.TASK_PLAY_ANIM, Character,
                            Util.LoadDict("get_up@standard"), "back",
                            12f, 12f, -1, 0, -10f, 1, 1, 1);
                    }

                    return true;
                }

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, "get_up@standard", "back", 3))
                {
                    UpdatePlayerPedPos();
                    var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character, "get_up@standard", "back");

                    if (currentTime >= 0.7f)
                    {
                        Character.Task.ClearAnimation("get_up@standard", "back");
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

        public void DisplayLocally()
        {
            try
            {
                if (IsSpectating || ModelHash == 0) return;


                DEBUG_STEP = 0;
                
                float hRange = IsInVehicle ? 100f : 200f;
                var gPos = IsInVehicle ? VehiclePosition : _position;
                var inRange = Game.Player.Character.IsInRangeOf(gPos, hRange);

                DEBUG_STEP = 1;
                

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

                if (CreateCharacter(gPos, hRange)) return;

                DEBUG_STEP = 5;

	            DrawNametag();

                DEBUG_STEP = 9;

	            if (CreateVehicle()) return;
                
				if (Character != null)
                {
                    Character.Health = (int) ((PedHealth/(float) 100)*Character.MaxHealth);
                }

                _switch++;
                DEBUG_STEP = 15;

	            if (UpdatePlayerPosOutOfRange(gPos, inRange)) return;

                DEBUG_STEP = 16;

	            WorkaroundBlip();

	            if (UpdatePosition()) return;

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
            if (IsInCover) return GetCoverIdleAnimDict();

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
                syncMode = SynchronizationMode.DeadReckoning;
            }
            
            LogManager.DebugLog("LASTPOS : " + _lastPosition);

            if (syncMode == SynchronizationMode.DeadReckoning)
            {
                var dir = Position - _lastPosition;
                var vdir = PedVelocity - _lastPedVel;

                Vector3 target, posTarget;

                if (Main.LagCompensation)
                {
                    var latency = ((Latency * 1000) / 2) + ((Main.Latency * 1000) / 2);

                    target = Vector3.Lerp(PedVelocity, PedVelocity + vdir,
                        latency/((float) AverageLatency));
                    posTarget = Vector3.Lerp(Position, Position + dir,
                        latency/((float) AverageLatency));
                }
                else
                {
                    target = Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir,
                    Environment.TickCount - LastUpdateReceived,
                    (int)AverageLatency);

                    posTarget = Util.LinearVectorLerp(Position, Position + dir,
                    Environment.TickCount - LastUpdateReceived,
                    (int)AverageLatency);
                    
                }

                if (OnFootSpeed > 0)
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
                        posTarget.Z, 0, 0, 0);
                }
                else
                {
                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y,
                        Position.Z, 0, 0, 0);
                }
            }
            else if (syncMode == SynchronizationMode.Teleport)
            {
                Character.PositionNoOffset = Position;
            }
            else if (syncMode == SynchronizationMode.TeleportRudimentary)
            {
                Character.Position = Position;
            }

            DEBUG_STEP = 33;
#if !DISABLE_SLERP
            Character.Quaternion = Quaternion.Slerp(_lastRotation.ToQuaternion(), _rotation.ToQuaternion(),
                    (float)Math.Min(1f, (Environment.TickCount - LastUpdateReceived) / AverageLatency));
#else
            Character.Quaternion = Rotation.ToQuaternion();
#endif
        }

        public string GetCoverIdleAnimDict()
        {
            if (!IsInCover) return "";
            var altitude = IsInLowCover ? "low" : "high";

            var hands = GetWeaponHandsHeld(CurrentWeapon);
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
                CurrentWeapon == unchecked((int) WeaponHash.SawnOffShotgun))
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

        public static string GetMovementAnim(int speed, bool inCover, bool coverFacingLeft)
        {
            if (inCover)
            {
                return coverFacingLeft ? "idle_l_corner" : "idle_r_corner";
            }

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

#endregion

    }

}