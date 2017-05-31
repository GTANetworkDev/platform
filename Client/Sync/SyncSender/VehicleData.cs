using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetworkShared;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Streamer
{
    public partial class SyncCollector
    {
        private static void VehicleData(Ped player)
        {
            var veh = player.CurrentVehicle;

            var obj = new VehicleData
            {
                Position = veh.Position.ToLVector(),
                VehicleHandle = Main.NetEntityHandler.EntityToNet(veh.Handle),
                Quaternion = veh.Rotation.ToLVector(),
                PedModelHash = player.Model.Hash,
                PlayerHealth = (byte)Util.Util.Clamp(0, player.Health, 255),
                VehicleHealth = veh.EngineHealth,
                Velocity = veh.Velocity.ToLVector(),
                PedArmor = (byte)player.Armor,
                RPM = veh.CurrentRPM,
                VehicleSeat = (short)Util.Util.GetPedSeatAtVehicle(player, veh),
                Flag = 0,
                Steering = veh.SteeringAngle,
            };

            if (Game.Player.IsPressingHorn)
                obj.Flag |= (byte)VehicleDataFlags.PressingHorn;
            if (veh.SirenActive)
                obj.Flag |= (byte)VehicleDataFlags.SirenActive;
            if (veh.IsDead)
                obj.Flag |= (byte)VehicleDataFlags.VehicleDead;
            if (player.IsDead)
                obj.Flag |= (short)VehicleDataFlags.PlayerDead;
            if (Util.Util.GetResponsiblePed(veh).Handle == player.Handle)
                obj.Flag |= (byte)VehicleDataFlags.Driver;
            if (veh.IsInBurnout)
                obj.Flag |= (byte)VehicleDataFlags.BurnOut;
            if (ForceAimData)
                obj.Flag |= (byte)VehicleDataFlags.HasAimData;
            if (player.IsSubtaskActive(167) || player.IsSubtaskActive(168))
                obj.Flag |= (short)VehicleDataFlags.ExitingVehicle;
            if (Game.IsEnabledControlPressed(0, Control.VehicleBrake) || Game.IsEnabledControlPressed(0, Control.VehicleHandbrake))
                obj.Flag |= (short)VehicleDataFlags.Braking;

            if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.Util.GetPedSeat(player)) &&
                WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash) &&
                Util.Util.GetPedSeat(player) == -1)
            {
                obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                obj.AimCoords = new GTANetworkShared.Vector3(0, 0, 0);
                obj.WeaponHash = Main.GetCurrentVehicleWeaponHash(player);
                if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                    obj.Flag |= (byte)VehicleDataFlags.Shooting;
            }
            else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.Util.GetPedSeat(player)))
            {
                obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                obj.WeaponHash = 0;
                obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();
                if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                    obj.Flag |= (byte)VehicleDataFlags.Shooting;
            }
            else
            {
                bool usingVehicleWeapon = player.IsSubtaskActive(200) || player.IsSubtaskActive(190);

                if (usingVehicleWeapon &&
                    Game.IsEnabledControlPressed(0, Control.Attack) &&
                    player.Weapons.Current?.AmmoInClip != 0)
                {
                    obj.Flag |= (byte)VehicleDataFlags.Shooting;
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                }

                if ((usingVehicleWeapon &&
                     player.Weapons.Current?.AmmoInClip != 0) ||
                    (player.Weapons.Current?.Hash == WeaponHash.Unarmed &&
                     player.IsSubtaskActive(200)))
                {
                    obj.Flag |= (byte)VehicleDataFlags.Aiming;
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                }

                var outputArg = new OutputArgument();
                Function.Call(Hash.GET_CURRENT_PED_WEAPON, player, outputArg, true);
                obj.WeaponHash = outputArg.GetResult<int>();

                lock (Lock)
                {
                    if (LastSyncPacket != null && LastSyncPacket is VehicleData &&
                        WeaponDataProvider.NeedsFakeBullets(obj.WeaponHash.Value) &&
                        (((VehicleData)LastSyncPacket).Flag & (byte)VehicleDataFlags.Shooting) != 0)
                    {
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                        obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    }
                }

                obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();
            }

            Vehicle trailer;

            switch ((VehicleHash)veh.Model.Hash)
            {
                case VehicleHash.TowTruck:
                case VehicleHash.TowTruck2:
                    trailer = veh.TowedVehicle;
                    break;
                case VehicleHash.Cargobob:
                case VehicleHash.Cargobob2:
                case VehicleHash.Cargobob3:
                case VehicleHash.Cargobob4:
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                    break;
                default:
                    trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);
                    break;
            }

            if (trailer != null && trailer.Exists())
            {
                obj.Trailer = trailer.Position.ToLVector();
            }

            if (Util.Util.GetResponsiblePed(veh) == player)
            {
                obj.DamageModel = veh.GetVehicleDamageModel();
            }

            lock (Lock)
            {
                LastSyncPacket = obj;
            }
        }

    }
}
