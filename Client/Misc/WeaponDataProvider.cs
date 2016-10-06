using GTA;
using GTA.Math;

namespace GTANetwork.Misc
{

    public static class WeaponDataProvider
    {
        public static bool IsWeaponAutomatic(GTANetworkShared.WeaponHash hash)
        {
            switch (hash)
            {
                default:
                    return true;
                case GTANetworkShared.WeaponHash.Molotov:
                case GTANetworkShared.WeaponHash.BZGas:
                case GTANetworkShared.WeaponHash.SmokeGrenade:
                case GTANetworkShared.WeaponHash.ProximityMine:
                case GTANetworkShared.WeaponHash.StickyBomb:
                case GTANetworkShared.WeaponHash.Grenade:
                case GTANetworkShared.WeaponHash.Flare:
                case GTANetworkShared.WeaponHash.Snowball:
                case GTANetworkShared.WeaponHash.Ball:
                case GTANetworkShared.WeaponHash.Pipebomb:

                case GTANetworkShared.WeaponHash.RPG:
                case GTANetworkShared.WeaponHash.HomingLauncher:
                case GTANetworkShared.WeaponHash.Railgun:
                case GTANetworkShared.WeaponHash.GrenadeLauncher:
                case GTANetworkShared.WeaponHash.Firework:
                case GTANetworkShared.WeaponHash.Musket:
                case GTANetworkShared.WeaponHash.FlareGun:
                case GTANetworkShared.WeaponHash.CompactLauncher:

                    return false;
            }
        }

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

        public static bool NeedsFakeBullets(int wephash)
        {
            var uW = unchecked((uint) wephash);

            switch ((GTANetworkShared.WeaponHash) uW)
            {
                default:
                    return false;
                case GTANetworkShared.WeaponHash.Molotov:
                case GTANetworkShared.WeaponHash.BZGas:
                case GTANetworkShared.WeaponHash.SmokeGrenade:
                case GTANetworkShared.WeaponHash.ProximityMine:
                case GTANetworkShared.WeaponHash.StickyBomb:
                case GTANetworkShared.WeaponHash.Grenade:
                case GTANetworkShared.WeaponHash.Flare:
                case GTANetworkShared.WeaponHash.Snowball:
                case GTANetworkShared.WeaponHash.Ball:
                case GTANetworkShared.WeaponHash.Pipebomb:
                    return true;
            }
        }

        public static bool NeedsManualRotation(int wephash)
        {
            return NeedsFakeBullets(wephash);
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
                return new Vector3(0f, -1.359f, 1.799f);

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
            if (vehicle == VehicleHash.Valkyrie && (vehiclepos == (int)VehicleSeat.Passenger || anySeat)) return true;
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
                case (WeaponHash)1649403952:
                    return 36;
                case (WeaponHash)4019527611:
                    return 165;
            }
        }
    }

}