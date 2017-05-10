using System;
using GTA;
using GTANetwork.Streamer;
using GTANetwork.Util;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork
{
    internal partial class Main
    {

        private void HandlePedPacket(PedData fullPacket, bool pure)
        {
            if (fullPacket.NetHandle == null) return;
            var syncPed = NetEntityHandler.GetPlayer(fullPacket.NetHandle.Value);


            syncPed.IsInVehicle = false;
            syncPed.VehicleNetHandle = 0;

            if (fullPacket.Position != null) syncPed.Position = fullPacket.Position.ToVector();
            if (fullPacket.Speed != null) syncPed.OnFootSpeed = fullPacket.Speed.Value;
            if (fullPacket.PedArmor != null) syncPed.PedArmor = fullPacket.PedArmor.Value;
            if (fullPacket.PedModelHash != null) syncPed.ModelHash = fullPacket.PedModelHash.Value;
            if (fullPacket.Quaternion != null) syncPed.Rotation = fullPacket.Quaternion.ToVector();
            if (fullPacket.PlayerHealth != null) syncPed.PedHealth = fullPacket.PlayerHealth.Value;
            if (fullPacket.AimCoords != null) syncPed.AimCoords = fullPacket.AimCoords.ToVector();
            if (fullPacket.WeaponHash != null) syncPed.CurrentWeapon = fullPacket.WeaponHash.Value;
            if (fullPacket.Latency != null) syncPed.Latency = fullPacket.Latency.Value;
            if (fullPacket.Velocity != null) syncPed.PedVelocity = fullPacket.Velocity.ToVector();
            if (fullPacket.WeaponAmmo != null) syncPed.Ammo = fullPacket.WeaponAmmo.Value;

            if (fullPacket.Flag != null)
            {
                syncPed.IsFreefallingWithParachute = (fullPacket.Flag.Value & (int)PedDataFlags.InFreefall) >
                                                     0;
                syncPed.IsInMeleeCombat = (fullPacket.Flag.Value & (int)PedDataFlags.InMeleeCombat) > 0;
                syncPed.IsRagdoll = (fullPacket.Flag.Value & (int)PedDataFlags.Ragdoll) > 0;
                syncPed.IsAiming = (fullPacket.Flag.Value & (int)PedDataFlags.Aiming) > 0;
                syncPed.IsJumping = (fullPacket.Flag.Value & (int)PedDataFlags.Jumping) > 0;
                syncPed.IsParachuteOpen = (fullPacket.Flag.Value & (int)PedDataFlags.ParachuteOpen) > 0;
                syncPed.IsInCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCover) > 0;
                syncPed.IsInLowCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInLowerCover) > 0;
                syncPed.IsCoveringToLeft = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCoverFacingLeft) > 0;
                syncPed.IsOnLadder = (fullPacket.Flag.Value & (int)PedDataFlags.IsOnLadder) > 0;
                syncPed.IsReloading = (fullPacket.Flag.Value & (int)PedDataFlags.IsReloading) > 0;
                syncPed.IsVaulting = (fullPacket.Flag.Value & (int)PedDataFlags.IsVaulting) > 0;
                syncPed.IsOnFire = (fullPacket.Flag.Value & (int)PedDataFlags.OnFire) != 0;
                syncPed.IsPlayerDead = (fullPacket.Flag.Value & (int)PedDataFlags.PlayerDead) != 0;

                syncPed.EnteringVehicle = (fullPacket.Flag.Value & (int)PedDataFlags.EnteringVehicle) != 0;

                if ((fullPacket.Flag.Value & (int)PedDataFlags.ClosingVehicleDoor) != 0 && syncPed.MainVehicle != null && syncPed.MainVehicle.Model.Hash != (int)VehicleHash.CargoPlane)
                {
                    syncPed.MainVehicle.Doors[(VehicleDoorIndex)syncPed.VehicleSeat + 1].Close(true);
                }

                if (syncPed.EnteringVehicle)
                {
                    syncPed.VehicleNetHandle = fullPacket.VehicleTryingToEnter.Value;
                    syncPed.VehicleSeat = fullPacket.SeatTryingToEnter.Value;
                }
            }

            if (pure)
            {
                syncPed.LastUpdateReceived = Util.Util.TickCount;
                syncPed.StartInterpolation();
            }
        }

        private void HandleBasicPacket(int nethandle, Vector3 position)
        {
            var syncPed = NetEntityHandler.GetPlayer(nethandle);

            syncPed.Position = position;

            syncPed.LastUpdateReceived = Util.Util.TickCount;

            if (syncPed.VehicleNetHandle != 0)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    car.Position = position.ToLVector();
                    if (car.StreamedIn)
                    {
                        NetEntityHandler.NetToEntity(car).PositionNoOffset = position;
                    }
                }
            }
        }

        private void HandleVehiclePacket(VehicleData fullData, bool purePacket)
        {
            if (fullData.NetHandle == null) return;
            var syncPed = NetEntityHandler.GetPlayer(fullData.NetHandle.Value);

            syncPed.IsInVehicle = true;

            if (fullData.VehicleHandle != null) LogManager.DebugLog("RECEIVED LIGHT VEHICLE PACKET " + fullData.VehicleHandle);

            if (fullData.Position != null)
            {
                syncPed.Position = fullData.Position.ToVector();
            }

            if (fullData.VehicleHandle != null) syncPed.VehicleNetHandle = fullData.VehicleHandle.Value;
            if (fullData.Velocity != null) syncPed.VehicleVelocity = fullData.Velocity.ToVector();
            if (fullData.PedModelHash != null) syncPed.ModelHash = fullData.PedModelHash.Value;
            if (fullData.PedArmor != null) syncPed.PedArmor = fullData.PedArmor.Value;
            if (fullData.RPM != null) syncPed.VehicleRPM = fullData.RPM.Value;
            if (fullData.Quaternion != null)
            {
                syncPed.VehicleRotation = fullData.Quaternion.ToVector();
            }
            if (fullData.PlayerHealth != null) syncPed.PedHealth = fullData.PlayerHealth.Value;
            if (fullData.VehicleHealth != null) syncPed.VehicleHealth = fullData.VehicleHealth.Value;
            if (fullData.VehicleSeat != null) syncPed.VehicleSeat = fullData.VehicleSeat.Value;
            if (fullData.Latency != null) syncPed.Latency = fullData.Latency.Value;
            if (fullData.Steering != null) syncPed.SteeringScale = fullData.Steering.Value;
            if (fullData.Velocity != null) syncPed.Speed = fullData.Velocity.ToVector().Length();
            if (fullData.DamageModel != null && syncPed.MainVehicle != null) syncPed.MainVehicle.SetVehicleDamageModel(fullData.DamageModel);

            if (fullData.Flag != null)
            {
                syncPed.IsVehDead = (fullData.Flag.Value & (short)VehicleDataFlags.VehicleDead) > 0;
                syncPed.IsHornPressed = (fullData.Flag.Value & (short)VehicleDataFlags.PressingHorn) > 0;
                syncPed.Siren = (fullData.Flag.Value & (short)VehicleDataFlags.SirenActive) > 0;
                syncPed.IsShooting = (fullData.Flag.Value & (short)VehicleDataFlags.Shooting) > 0;
                syncPed.IsAiming = (fullData.Flag.Value & (short)VehicleDataFlags.Aiming) > 0;
                syncPed.IsInBurnout = (fullData.Flag.Value & (short)VehicleDataFlags.BurnOut) > 0;
                syncPed.ExitingVehicle = (fullData.Flag.Value & (short)VehicleDataFlags.ExitingVehicle) != 0;
                syncPed.IsPlayerDead = (fullData.Flag.Value & (int)VehicleDataFlags.PlayerDead) != 0;
                syncPed.Braking = (fullData.Flag.Value & (short)VehicleDataFlags.Braking) != 0;
            }

            if (fullData.WeaponHash != null)
            {
                syncPed.CurrentWeapon = fullData.WeaponHash.Value;
            }

            if (fullData.AimCoords != null) syncPed.AimCoords = fullData.AimCoords.ToVector();

            if (syncPed.VehicleNetHandle != 0 && fullData.Position != null)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    car.Position = fullData.Position;
                    car.Rotation = fullData.Quaternion;
                }

            }
            else if (syncPed.VehicleNetHandle != 00 && fullData.Position == null && fullData.Flag != null && !PacketOptimization.CheckBit(fullData.Flag.Value, VehicleDataFlags.Driver))
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    syncPed.Position = car.Position.ToVector();
                    syncPed.VehicleRotation = car.Rotation.ToVector();
                }
            }

            if (purePacket)
            {
                syncPed.LastUpdateReceived = Util.Util.TickCount;
                syncPed.StartInterpolation();
            }
        }

        private void HandleBulletPacket(int netHandle, bool shooting, Vector3 aim)
        {
            //Util.Util.SafeNotify("Handling Bullet - " + DateTime.Now.Millisecond);
            var syncPed = NetEntityHandler.GetPlayer(netHandle);

            syncPed.IsShooting = shooting;
            syncPed.AimedAtPlayer = false;

            if (shooting) syncPed.AimCoords = aim;
        }

        private void HandleBulletPacket(int netHandle, bool shooting, int netHandleTarget)
        {
            //Util.Util.SafeNotify("Handling PlayerBullet - " + DateTime.Now.Millisecond);
            var syncPed = NetEntityHandler.GetPlayer(netHandle);
            var syncPedTarget = NetEntityHandler.NetToEntity(netHandleTarget);

            syncPed.IsShooting = shooting;
            syncPed.AimedAtPlayer = true;

            if (shooting) syncPed.AimPlayer = new Ped(syncPedTarget.Handle);
        }
    }
}
