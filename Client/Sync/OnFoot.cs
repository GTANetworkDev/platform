
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
    internal partial class SyncPed
    {
        private bool CreateCharacter()
        {
            var gPos = Position;
            if (Character != null && Character.Exists() && Character.Model.Hash == ModelHash && (!Character.IsDead || PedHealth <= 0)) return false;

            if (Character != null && Character.Exists()) Character.Delete();


            var charModel = new Model(ModelHash);
            Util.Util.LoadModel(charModel);
            Character = World.CreatePed(charModel, gPos, _rotation.Z);
            charModel.MarkAsNoLongerNeeded();

            if (Character == null) return true;

            lock (Main.NetEntityHandler.ClientMap) Main.NetEntityHandler.HandleMap.Set(RemoteHandle, Character.Handle);

            Character.CanBeTargetted = true;
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

            foreach (var source in Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemoteParticle && ((RemoteParticle)item).EntityAttached == RemoteHandle).Cast<RemoteParticle>())
            {
                Main.NetEntityHandler.StreamOut(source);
                Main.NetEntityHandler.StreamIn(source);
            }

            if (PacketOptimization.CheckBit(Flag, EntityFlag.Collisionless))
            {
                Character.IsCollisionEnabled = false;
            }

            JavascriptHook.InvokeStreamInEvent(new LocalHandle(Character.Handle), (int)GTANetworkShared.EntityType.Player);
            return true;
        }

        private void UpdateOnFootPosition()
        {
            if (!Character.IsOnScreen())
            {
                BasicPosSync();
                return;
            }

            //UpdateProps();

            UpdateCurrentWeapon();

            #region IsJumping
            if (!_lastJumping && IsJumping)
            {
                Character.Task.Jump();
            }
            #endregion

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
                Character.IsInvincible = true;
                if (Character.IsDead) Function.Call(Hash.RESURRECT_PED, Character);

                if (_scriptFire != 0) Function.Call(Hash.REMOVE_SCRIPT_FIRE, _scriptFire);

                _scriptFire = 0;
            }
            _lastFire = IsOnFire;
            #endregion

            #region EnteringVehicles
            if (EnteringVehicle)
            {
                var targetVeh = Main.NetEntityHandler.NetToEntity(VehicleNetHandle);

                if (targetVeh != null)
                {
                    //Character.Task.ClearAll();
                    //Character.Task.ClearSecondary();
                    //Character.Task.ClearAllImmediately();
                    //Character.IsPositionFrozen = false;
                    Game.Player.Character.StaysInVehicleWhenJacked = false;
                    Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, Game.Player.Character.Handle, true); // 7A6535691B477C48 8A251612
                    Character.Task.EnterVehicle(new Vehicle(targetVeh.Handle), (GTA.VehicleSeat)VehicleSeat, -1, 2f);

                    //if(Game.Player.Character.SeatIndex == (VehicleSeat)VehicleSeat)
                    //_seatEnterStart = Util.Util.TickCount;
                }
                return;
            }
            #endregion

            Character.CanBeTargetted = true;

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

            //TODO: CHECK
            if (ragdoll)
            {
                if (!Character.IsRagdoll)
                {
                    Character.CanRagdoll = true;
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, Character, 50000, 60000, 0, 1, 1, 1);
                }

                var dir = Position - (_lastPosition ?? Position);
                var vdir = PedVelocity - _lastPedVel;
                var target = Util.Util.LinearVectorLerp(PedVelocity, PedVelocity + vdir, TicksSinceLastUpdate, (int)AverageLatency);

                var posTarget = Util.Util.LinearVectorLerp(Position, Position + dir, TicksSinceLastUpdate, (int)AverageLatency);

                const int PED_INTERPOLATION_WARP_THRESHOLD = 15;
                const int PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 5;

                var fThreshold = PED_INTERPOLATION_WARP_THRESHOLD + PED_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * PedVelocity.Length();

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

                return;
            }

            if (Character.IsRagdoll)
            {
                Character.CanRagdoll = false;
                Character.Task.ClearAllImmediately();
                //Character.PositionNoOffset = Position;

                if (IsPlayerDead) return;
                Function.Call(Hash.TASK_PLAY_ANIM, Character, Util.Util.LoadDict("anim@sports@ballgame@handball@"), "ball_get_up", 12f, 12f, -1, 0, -10f, 1, 1, 1);

                _playingGetupAnim = true;

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

            #region Melee
            //if (lastMeleeAnim != null)
            //{
            //    DisplayMeleeAnimation();
            //}
            //else if (IsInMeleeCombat)
            //{
            //    DisplayMeleeCombat();
            //}
            #endregion

            #region CustomAnimation // WalkAnim // IsAiming // Shooting
            if (!IsCustomAnimationPlaying)
            {
                if (IsAiming) DisplayAimingAnimation();

                if (IsShooting) DisplayShootingAnimation();

                if (!IsAiming && !IsShooting && !IsJumping && !IsInMeleeCombat) WalkAnimation();


            }
            else
            {
                //if ((CustomAnimationFlag & 48) == 48)
                //{
                //    VMultiOnfootPosition();
                //}
                //else
                //{
                //    UpdatePlayerPedPos();
                //}

                //DisplayCustomAnimation();
            }
            BasicPosSync();

            #endregion

            //else if ( && !IsCustomAnimationPlaying)
            //{
            //    //UpdatePlayerPedPos();

            //    //Walk();

            //    //DisplayWalkingAnimation();

            //}
        }
        
        private void WalkAnimation()
        {
            var predictPosition = Position + (Position - Character.Position) + PedVelocity;
            var range = predictPosition.DistanceToSquared(Character.Position);

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
                    lastMoving = true;
                    break;
                case 2:
                    if (!Character.IsRunning || range > 0.50f)
                    {
                        Character.Task.RunTo(predictPosition, true);
                        Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 2.0f);
                    }
                    lastMoving = true;
                    break;

                case 3:
                    if (!Character.IsSprinting || range > 0.75f)
                    {
                        Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character, predictPosition.X, predictPosition.Y, predictPosition.Z, 3.0f, -1, 0.0f, 0.0f);
                        Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Character, 1.49f);
                        Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, 3.0f);
                    }
                    lastMoving = true;
                    break;

                default:
                    if (lastMoving)
                    {
                        //Character.Task.ClearAllImmediately();
                        Character.Task.StandStill(2000);
                        lastMoving = false;
                    }
                    break;
            }
        }


        private void BasicPosSync(bool updateRotation = true)
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
                newPos = Position + PedVelocity * latency / 1000;
            }
            var playerChar = FrameworkData.PlayerChar.Ex();

            if ((OnFootSpeed > 0 || IsAnimal(ModelHash)) && currentInterop.FinishTime != 0)
            {
                // (PlayerChar.IsInRangeOfEx(newPos, StreamerThread.CloseRange))
                if (playerChar.IsOnScreen())
                {
                    Character.Velocity = PedVelocity + 10 * (newPos - Character.Position);
                }
                else
                {
                    Character.PositionNoOffset = newPos;
                }

                //StuckDetection();
                _stopTime = DateTime.Now;
                _carPosOnUpdate = Character.Position;
            }

            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && currentInterop.FinishTime != 0)
            {
                var posTarget = Util.Util.LinearVectorLerp(_carPosOnUpdate, Position + (Position - (_lastPosition ?? Position)), (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0);
            }
            else
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, Character, Position.X, Position.Y, Position.Z, 0, 0, 0);
            }

            if (updateRotation)
            {
            #if !DISABLE_SLERP
                Character.Quaternion = !Character.IsSwimmingUnderWater ? GTA.Math.Quaternion.Slerp(Character.Quaternion, _rotation.ToQuaternion(), Math.Min(1f, (DataLatency + TicksSinceLastUpdate) / (float)AverageLatency)) : Rotation.ToQuaternion();
            #else
                Character.Quaternion = Rotation.ToQuaternion();
            #endif
            }
        }

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

            if (length > 0.05f * 0.05f && length < StreamerThread.CloseRange * StreamerThread.CloseRange)
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
                            Position.X + ((PedVelocity.X / 7)),
                            Position.Y + ((PedVelocity.Y / 7)),
                            Position.Z + ((PedVelocity.Z / 10))),
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

            if (length < StreamerThread.CloseRange * StreamerThread.CloseRange)
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
                    if (IsAiming && !IsReloading && !IsInMeleeCombat)
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
                        if (length < (StreamerThread.CloseRange * StreamerThread.CloseRange) / 2)
                        {
                            if ((!isAiming || Environment.TickCount % 25 == 0) && OnFootSpeed == 0)
                            {
                                Function.Call(Hash.TASK_AIM_GUN_AT_ENTITY, Character, _aimingProp, -1, false);
                                _lastAimCoords = AimCoords;
                            }
                            else if ((!isAiming || Environment.TickCount % 25 == 0) && OnFootSpeed > 0)
                            {
                                Function.Call(Hash.TASK_GO_TO_ENTITY_WHILE_AIMING_AT_ENTITY, Character, _followProp, _aimingProp, (float)OnFootSpeed, false, 10000, 10000, true, true, (uint)FiringPattern.FullAuto);
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, Character, (float)OnFootSpeed);
                                _lastAimCoords = AimCoords;
                            }
                        }
                    }
                }
                //StuckDetection();
            }
        }

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
    }
}
