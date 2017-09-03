
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
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
    internal partial class SyncPed
    {
        private bool CreateCharacter()
        {
            if (Character == null || !Character.Exists() || Character.Model.Hash != ModelHash)
            {
                if (Character != null && Character.Exists())
                {
                    Character?.Delete();
                }

                var gPos = Position;
                var charModel = new Model(ModelHash);
                Util.Util.LoadModel(charModel);
                //Character = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, charModel.Hash, gPos.X, gPos.Y, gPos.Z, _rotation.Z, false, false));
                Character = World.CreatePed(charModel, gPos, _rotation.Z);
                charModel.MarkAsNoLongerNeeded();

                if (Character == null) return true;

                lock (Main.NetEntityHandler.ClientMap) Main.NetEntityHandler.HandleMap.Set(RemoteHandle, Character.Handle);

                Character.CanBeTargetted = true;
                Character.BlockPermanentEvents = true;
                //Function.Call(Hash.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, Character, true);
                //Character.IsInvincible = true;
                Character.CanRagdoll = false;
                Character.CanSufferCriticalHits = false;

                if (Team == -1 || Team != Main.LocalTeam)
                {
                    Character.RelationshipGroup = Main.RelGroup;
                    //Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH, Character, Main.RelGroup);
                }
                else
                {
                    Character.RelationshipGroup = Main.FriendRelGroup;
                    //Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH, Character, Main.FriendRelGroup);
                }

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

                if (Props != null)
                {
                    foreach (var pair in Props)
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value, Textures[pair.Key], 2);
                    }
                }

                if (Accessories != null)
                {
                    foreach (var pair in Accessories)
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character, pair.Key, pair.Value.Item1, pair.Value.Item2, 2);
                    }
                }

                Main.NetEntityHandler.ReattachAllEntities(this, false);

                foreach (var source in Main.NetEntityHandler.ClientMap.Values
                    .Where(item => item is RemoteParticle && ((RemoteParticle) item).EntityAttached == RemoteHandle)
                    .Cast<RemoteParticle>())
                {
                    Main.NetEntityHandler.StreamOut(source);
                    Main.NetEntityHandler.StreamIn(source);
                }

                if (PacketOptimization.CheckBit(Flag, EntityFlag.Collisionless))
                {
                    Character.IsCollisionEnabled = false;
                }

                JavascriptHook.InvokeStreamInEvent(new LocalHandle(Character.Handle), (int) GTANetworkShared.EntityType.Player);
                return true;
            }

            return false;
        }

        private bool _init;
        internal bool IsJumping;
        private bool _lastJumping;
        private void UpdateOnFootAnimation()
        {
            if (EnteringVehicle/* || !Character.IsRendered*/) return;

            if (!_init)
            {
                _init = true;
                Character.PositionNoOffset = Position;
            }

            //if (Character.IsSwimming || Character.IsInWater)
            //{
            //    var dim = Character.Model.GetDimensions();
            //    var waterheight = World.GetWaterHeight(Position); //Expensive and risky
            //    var height = Vector2.Lerp(new Vector2(0f, Character.Position.Z), new Vector2(0f, waterheight), 0.8f);
            //    if (Position.Z < height.Y + dim.Z - 0.2f)
            //    {
            //        Character.PositionNoOffset = new Vector3(biDimensionalPos.X, biDimensionalPos.Y, triDimensionalPos.Z);
            //    }
            //    else
            //    {
            //        Character.PositionNoOffset = new Vector3(biDimensionalPos.X, biDimensionalPos.Y, height.Y + dim.Z - 0.1f);
            //    }
            //}
            //else
            //{

            // If remote z higher by too much and remote not doing any z movement, warp local z coord
            var fDeltaZ = Position.Z - Character.Position.Z;

            if (fDeltaZ > 0.4f && fDeltaZ < 10.0f)
            {
                if (Math.Abs(Character.Velocity.Z) < 0.01f)
                {
                    Character.Position = new Vector3(Position.X, Position.Y, Position.Z);
                    //Character.Velocity = new Vector3(Character.Velocity.X, Character.Velocity.Y, 0);
                }
            }

            #region SwimFix
            if (!Character.IsSwimming && _lastSwimming)
            {
                Character.Task.ClearAllImmediately();
            }
            _lastSwimming = Character.IsSwimming;
            #endregion

            #region DeadAnimationFix
            if ((Character.IsSubtaskActive(ESubtask.DEAD) || Character.IsSubtaskActive(107)) && !IsPlayerDead)
            {
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                Character.Task.ClearAllImmediately();
                Character.Quaternion = Rotation.ToQuaternion();
            }
            #endregion

            UpdateProps();

            UpdateCurrentWeapon();

            #region IsOnFire
            if (!_lastFire && IsOnFire)
            {
                Character.IsInvincible = false;
                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);
                _scriptFire = Function.Call<int>(Hash.START_ENTITY_FIRE, Character);
            }
            else if (_lastFire && !IsOnFire)
            {
                Function.Call(Hash.STOP_ENTITY_FIRE, Character);
                //Character.IsInvincible = true;
                if (Character.IsDead) Function.Call(Hash.RESURRECT_PED, Character);

                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);

                _scriptFire = 0;
            }
            _lastFire = IsOnFire;
            #endregion

            #region ParachuteFreefall
            if (IsFreefallingWithParachute)
            {
                DisplayParachuteFreefall();
                return;
            }
            #endregion

            #region IsParachuteOpen
            if (IsParachuteOpen)
            {
                DisplayOpenParachute();
                return;
            }

            if (_parachuteProp != null)
            {
                _parachuteProp.Delete();
                _parachuteProp = null;
            }

            #endregion

            #region Ragdoll
            var ragdoll = IsRagdoll || IsPlayerDead;

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
                return;
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

                return;
            }
            #endregion

            #region GetUp
            if (_playingGetupAnim)
            {
                var getupAnim = GetAnimalGetUpAnimation().Split();

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, getupAnim[0], getupAnim[1], 3))
                {
                    //UpdatePlayerPedPos();
                    var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character, getupAnim[0], getupAnim[1]);

                    if (!(currentTime >= 0.7f)) return;
                    Character.Task.ClearAnimation(getupAnim[0], getupAnim[1]);
                    Character.Task.ClearAll();
                    _playingGetupAnim = false;
                }
            }
            #endregion

            if (!IsCustomAnimationPlaying)
            {
                if (!IsReloading)
                {
                    if (lastMeleeAnim != null)
                    {
                        var currentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, Character, lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);
                        if (currentTime >= meleeanimationend)
                        {
                            lastMeleeAnim = null;
                        }
                    }

                    if (IsInMeleeCombat && !IsShooting && lastMeleeAnim == null && ((IsInCover && !IsInLowCover) || !IsInCover))
                    {
                        DisplayMeleeCombat();
                    }
                    else if (lastMeleeAnim == null && ((IsInCover && !IsInLowCover) || !IsInCover))
                    {
                        Character.Task.ClearSecondary();
                    }

                    if (IsAiming)
                    {
                        DisplayAimAnimation();
                    }

                    if (IsShooting)
                    {
                        DisplayShootingAnimation();
                    }
                }

                if (IsJumping && !_lastJumping)
                {
                    Character.Task.Jump();
                }

                if (IsReloading && !_lastReloading && !Character.IsSubtaskActive(ESubtask.RELOADING))
                {
                    Character.Task.ClearAll();
                    Character.Task.ReloadWeapon();

                }
                if (!IsAiming && !IsShooting && !IsReloading && !IsJumping && !IsRagdoll && ((IsInCover && !IsInLowCover) || !IsInCover))
                {
                    WalkAnimation();
                }
                else if ((IsInCover || IsInLowCover) && !IsShooting)
                {
                    DisplayWalkingAnimation();
                }
            }
            else
            {
                DisplayCustomAnimation();
            }
        }

        #region Melee/Combat/Aim/Shoot
        private string lastMeleeAnim;
        private float meleeanimationend;
        private float meleeDamageStart;
        private float meleeDamageEnd;
        private bool meleeSwingDone;
        private bool meleeSwingEnd;

        private void DisplayMeleeAnimation()
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
                    var ray = World.RaycastCapsule(start, end, (int)Math.Abs(end.X - start.X), IntersectOptions.Peds1, Character);
                    //Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
                    if (ray.DidHit && ray.DidHitEntity && ray.HitEntity.Handle == PlayerChar.Handle)
                    {
                        LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                        JavascriptHook.InvokeCustomEvent(api => api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));

                        if (!Main.NetEntityHandler.LocalCharacter.IsInvincible) PlayerChar.ApplyDamage(25);

                        meleeSwingDone = true;
                        meleeSwingEnd = false;
                    }
                }
            }
            else if (!meleeSwingDone && CurrentWeapon == unchecked((int)WeaponHash.Unarmed))
            {
                var rightfist = Character.GetBonePosition((int)Bone.IK_R_Hand);
                var start = rightfist - new Vector3(0, 0, 0.5f);
                var end = rightfist + new Vector3(0, 0, 0.5f);
                var ray = World.RaycastCapsule(start, end, (int)Math.Abs(end.X - start.X), IntersectOptions.Peds1, Character);
                if (ray.DidHit && ray.DidHitEntity && ray.HitEntity.Handle == PlayerChar.Handle)
                {
                    LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                    JavascriptHook.InvokeCustomEvent(api =>
                        api.invokeonLocalPlayerMeleeHit(them, CurrentWeapon));
                    if (!Main.NetEntityHandler.LocalCharacter.IsInvincible)
                        PlayerChar.ApplyDamage(25);
                    meleeSwingDone = true;
                    meleeSwingEnd = false;
                }
            }

            if (currentTime >= 0.95f)
            {
                lastMeleeAnim = null;
                meleeSwingDone = false;
                meleeSwingEnd = false;
            }

            if (currentTime >= meleeanimationend)
            {
                if (lastMeleeAnim != null) Character.Task.ClearAnimation(lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);
                lastMeleeAnim = null;
                meleeSwingDone = false;
                meleeSwingEnd = true;
            }
        }

        private void DisplayMeleeAnimation(int hands)
        {
            //if (lastMeleeAnim != null) Character.Task.ClearAnimation(lastMeleeAnim.Split()[0], lastMeleeAnim.Split()[1]);
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

        private void DisplayMeleeCombat()
        {
            string secondaryAnimDict = null;
            var ourAnim = GetMovementAnim(OnFootSpeed, false, false);
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
                //Character.Task.ClearSecondary();
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(secondaryAnimDict), secAnim, 8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
            }
        }

        private bool _steadyAim;
        private Prop _entityToAimAt;
        private Prop _entityToWalkTo;
        private bool _lastReloading;
        private bool _isReloading;
        internal bool IsReloading
        {
            get => _isReloading;
            set
            {
                _lastReloading = _isReloading;
                _isReloading = value;
            }
        }

        private void DisplayAimAnimation()
        {
            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
            var animDict = GetAnimDictionary(ourAnim);
            if (ourAnim != null && animDict != null)
            {
                var flag = GetAnimFlag();
                if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
                {
                    Character.Task.ClearAll();
                    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim, 8f, 10f, -1, flag, -8f, 1, 1, 1);
                }
            }
            else
            {
                #region Init

                if (_entityToAimAt != null && _entityToAimAt.Exists())
                {
                    _entityToAimAt.Position = Vector3.Lerp(_entityToAimAt.Position, AimCoords, 0.10f);
                }
                else
                {
                    _entityToAimAt = World.CreateProp(new Model(-512779781), this.AimCoords, false, false);
                    _entityToAimAt.IsCollisionEnabled = false;
                    _entityToAimAt.Opacity = 0;
                }

                if (_entityToWalkTo != null && _entityToWalkTo.Exists())
                {
                    _entityToWalkTo.Position = Vector3.Lerp(_entityToWalkTo.Position, Position + PedVelocity * 1.25f,
                        0.10f);
                }
                else
                {
                    _entityToWalkTo = World.CreateProp(new Model(-512779781), Position, false, false);
                    _entityToWalkTo.IsCollisionEnabled = false;
                    _entityToWalkTo.Opacity = 0;
                }

                #endregion

                if (_entityToAimAt != null && _entityToAimAt.Exists() && _entityToWalkTo != null && _entityToWalkTo.Exists() && Character.Exists())
                {
                    if (!Character.IsSubtaskActive(ESubtask.AIMING_GUN))
                    {
                        if (OnFootSpeed > 0)
                        {
                            Function.Call(Hash.TASK_GO_TO_ENTITY_WHILE_AIMING_AT_ENTITY, Character, _entityToWalkTo, _entityToAimAt, (float)OnFootSpeed, false, 10000, 10000, true, true, (uint)FiringPattern.FullAuto);
                            Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 0.25f);
                        }
                        else
                        {
                            _steadyAim = true;
                            Function.Call(Hash.TASK_AIM_GUN_AT_ENTITY, Character, _entityToAimAt, -1, true);
                        }
                    }
                    else
                    {
                        if (OnFootSpeed > 0 && _steadyAim)
                        {
                            _steadyAim = false;
                            Character.Task.ClearAll();
                        }
                        else if (OnFootSpeed == 0 && !_steadyAim)
                        {
                            Character.Task.ClearAll();
                            _steadyAim = true;
                            Function.Call(Hash.TASK_AIM_GUN_AT_ENTITY, Character, _entityToAimAt, -1, true);
                        }
                    }
                }   
            }
        }

        private void DisplayAimingAnimation()
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
            //else
#endif
            //if (hands == 1 || hands == 2 || hands == 5 || hands == 6)
            //{
            //    //UpdatePlayerPedPos(false);
            //    //VMultiOnfootPosition();
            //}

        }

        private void DisplayShootingAnimation()
        {
            var hands = GetWeaponHandsHeld(CurrentWeapon);
            if (hands == 3 || hands == 4 || hands == 0)
            {
                DisplayMeleeAnimation(hands);
                DisplayMeleeAnimation();
            }
            else
            {
                DisplayWeaponShootingAnimation();
            }

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

        private void DisplayWeaponShootingAnimation()
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
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim, 8f, 10f, -1, 2, -8f, 1, 1, 1);
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
                Vector3 dir;
                if (AimedAtPlayer)
                {
                    var us = Game.Player.Character;
                    if (us == AimPlayer)
                    {
                        dir = (Game.Player.Character.Position - start);
                    }
                    else
                    {
                        dir = (AimPlayer.Position - start);
                    }
                }
                else
                {
                    dir = (AimCoords - start);
                }
                
                dir.Normalize();
                var end = start + dir * 100f;

                if (IsInCover) // Weapon spread
                {
                    end += Vector3.RandomXYZ() * 2f;
                }

                if (!WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
                {
                    Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, Character, end.X, end.Y, end.Z, true);
                }
                else
                {
                    var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash)CurrentWeapon);
                    var speed = 0xbf800000;
                    var weaponH = (WeaponHash)CurrentWeapon;

                    if (weaponH == WeaponHash.Minigun) weaponH = WeaponHash.CombatPDW;

                    if (IsFriend()) damage = 0;

                    Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X, end.Y, end.Z, damage, true, (int)weaponH, Character, false, true, speed);

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
                    JavascriptHook.InvokeCustomEvent(api => api.invokeonLocalPlayerDamaged(them, CurrentWeapon, boneHit/*, playerHealth, playerArmor*/));
                }
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, PlayerChar);
            }
        }

#endregion

        private void DisplayParachuteFreefall()
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

        private void DisplayOpenParachute()
        {
            CallCollection thisCall = new CallCollection();

            if (_parachuteProp == null)
            {
                _parachuteProp = World.CreateProp(new Model(1740193300), Character.Position, Character.Rotation, false, false);
                _parachuteProp.IsPositionFrozen = true;
                Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);

                _parachuteProp.AttachTo(Character, Character.GetBoneIndex(Bone.SKEL_Spine2), new Vector3(3.6f, 0, 0f), new Vector3(0, 90, 0));

                Character.Task.ClearAllImmediately();
                Character.Task.ClearSecondary();
            }


            var target = Util.Util.LinearVectorLerp(_lastPosition ?? Position, _position, TicksSinceLastUpdate, (int)AverageLatency);

            thisCall.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, target.X, target.Y, target.Z, 0, 0, 0, 0);

            Character.Quaternion = _rotation.ToQuaternion();

            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, "skydive@parachute@first_person", "chute_idle_right", 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict("skydive@parachute@first_person"), "chute_idle_right", 8f, 10f, -1, 0, -8f, 1, 1, 1);
            }

            //thisCall.Execute();
        }

        private void DisplayCustomAnimation()
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

        private bool _lastMoving;
        private void WalkAnimation()
        {
            var predictPosition = Position + (Position - Character.Position) + PedVelocity;
            var range = predictPosition.DistanceToSquared(Character.Position);

            if (Character.IsSubtaskActive(ESubtask.AIMING_GUN))
            {
                Character.Task.ClearAll();
            }

            switch (OnFootSpeed)
            {
                case 1:
                    if (!Character.IsWalking || range > 0.25f)
                    {
                        var nrange = range * 2;
                        if (nrange > 1.0f) nrange = 1.0f;
                        Character.Task.GoTo(predictPosition, true);
                        Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, nrange);
                    }
                    _lastMoving = true;
                    break;
                case 2:
                    if (!Character.IsRunning || range > 0.50f)
                    {
                        Character.Task.RunTo(predictPosition, true);
                        Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 1.0f);
                    }
                    _lastMoving = true;
                    break;

                case 3:
                    if (!Character.IsSprinting || range > 0.75f)
                    {
                        Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, predictPosition.X, predictPosition.Y, predictPosition.Z, 3.0f, -1, 0.0f, 0.0f);
                        Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Character, 1.49f);
                        Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 1.0f);
                    }
                    _lastMoving = true;
                    break;

                default:
                    if (_lastMoving)
                    {
                        //Character.Task.ClearAll();
                        Character.Task.StandStill(2000);
                        _lastMoving = false;
                    }
                    break;
            }
            UpdatePosition();
        }

        private void UpdatePosition(bool updatePosition = true, bool updateRotation = true, bool updateVelocity = true)
        {
            if (updatePosition)
            {
                var lerpValue = (DataLatency * 2) / 50000f;

                var biDimensionalPos = Vector2.Lerp(new Vector2(Character.Position.X, Character.Position.Y), new Vector2(Position.X + (PedVelocity.X / 5), Position.Y + (PedVelocity.Y / 5)), lerpValue);
                var zPos = Util.Util.Lerp(Character.Position.Z, Position.Z, 0.1f);

                Character.PositionNoOffset = new Vector3(biDimensionalPos.X, biDimensionalPos.Y, zPos);
            }

            if (updateRotation) Character.Quaternion = GTA.Math.Quaternion.Lerp(Character.Quaternion, Rotation.ToQuaternion(), 0.10f);

            if (updateVelocity) Character.Velocity = PedVelocity;
        }

        private void DisplayWalkingAnimation(bool displaySecondary = true)
        {
            if (IsReloading || (IsInCover && IsShooting && !IsAiming)) return;

            var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
            var animDict = GetAnimDictionary(ourAnim);
            var secondaryAnimDict = GetSecondaryAnimDict();
            var flag = GetAnimFlag();

            if (animDict != null && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim, 8f, 10f, -1, flag, -8f, 1, 1, 1);
            }

            if (displaySecondary)
            {
                if (secondaryAnimDict != null && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, secondaryAnimDict, ourAnim, 3))
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(secondaryAnimDict), ourAnim, 8f, 10f, -1, 32 | 16 | 1, -8f, 1, 1, 1);
                }
                else if (secondaryAnimDict == null)
                {
                    Character.Task.ClearSecondary();
                }
            }
        }

        //var dim = Character.Model.GetDimensions();
        //var waterheight = World.GetWaterHeight(Position); //Expensive
        //var pos = Vector2.Lerp(new Vector2(Character.Position.X, Character.Position.Y), new Vector2(Position.X + (PedVelocity.X / 5), Position.Y + (PedVelocity.Y / 5)), lerpValue);
        ////var height = Vector2.Lerp(new Vector2(0f, Character.Position.Z), new Vector2(0f, waterheight), 0.5f);
        //if (Position.Z < waterheight + dim.Z)
        //{
        //}
        //else
        //{
        //    Character.PositionNoOffset = new Vector3(pos.X, pos.Y, waterheight + dim.Z + 0.04f);
        //}

        //var ourAnim = GetMovementAnim(OnFootSpeed, IsInCover, IsCoveringToLeft);
        //var animDict = GetAnimDictionary(ourAnim);
        //if (ourAnim != null && animDict != null)
        //{
        //    var flag = GetAnimFlag();
        //    if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Character, animDict, ourAnim, 3))
        //    {
        //        Character.Task.ClearAll();
        //        //playerChar.Task.ClearSecondary();
        //        Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict(animDict), ourAnim, 8f, 10f, -1, flag, -8f, 1, 1, 1);
        //    }
        //}

        private void UpdateCurrentWeapon()
        {
            if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon || DirtyWeapons)
            {
                //Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, -1, true, true);
                //Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);

                //Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
                //Character.Weapons.Select((WeaponHash)CurrentWeapon);

                Character.Weapons.RemoveAll();
                var p = Position;

                Util.Util.LoadWeapon(CurrentWeapon);

                var wObj = Function.Call<int>(Hash.CREATE_WEAPON_OBJECT, CurrentWeapon, 999, p.X, p.Y, p.Z, true, 0, 0);

                if (WeaponTints != null && WeaponTints.ContainsKey(CurrentWeapon))
                {
                    var bitmap = WeaponTints[CurrentWeapon];
                    Function.Call(Hash.SET_WEAPON_OBJECT_TINT_INDEX, wObj, bitmap);
                }

                if (WeaponComponents != null && WeaponComponents.ContainsKey(CurrentWeapon) && WeaponComponents[CurrentWeapon] != null)
                {
                    for (var index = WeaponComponents[CurrentWeapon].Count - 1; index >= 0; index--)
                    {
                        var comp = WeaponComponents[CurrentWeapon][index];
                        Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_WEAPON_OBJECT, wObj, comp);
                    }
                }

                Function.Call(Hash.GIVE_WEAPON_OBJECT_TO_PED, wObj, Character);

                DirtyWeapons = false;
            }
        }
    }
}
