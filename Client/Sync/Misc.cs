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

        internal bool IsRagdoll
        {
            get { return _isRagdoll; }
            set { _isRagdoll = value; }
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

        internal bool IsFriend()
        {
            return (Team != -1 && Team == Main.LocalTeam);
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

            if (hands == 1) return "cover@idles@1h@" + altitude + "@_a";
            if (hands == 2 || hands == 5) return "cover@idles@2h@" + altitude + "@_a";
            if (hands == 3 || hands == 4 || hands == 0) return "cover@idles@unarmed@" + altitude + "@_a";
            return "";
        }

        internal string GetSecondaryAnimDict()
        {
            if (CurrentWeapon == unchecked((int)WeaponHash.Unarmed)) return null;
            if (CurrentWeapon == unchecked((int)WeaponHash.RPG) ||
                CurrentWeapon == unchecked((int)WeaponHash.HomingLauncher) ||
                CurrentWeapon == unchecked((int)WeaponHash.Firework))
                return "weapons@heavy@rpg";
            if (CurrentWeapon == unchecked((int)WeaponHash.Minigun))
                return "weapons@heavy@minigun";
            if (CurrentWeapon == unchecked((int)WeaponHash.GolfClub) ||
                CurrentWeapon == unchecked((int)WeaponHash.Bat))
                return "weapons@melee_2h";
            if (Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, CurrentWeapon) ==
                     Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, unchecked((int)WeaponHash.Bat)))
                return "weapons@melee_1h";
            if (CurrentWeapon == -1357824103 || CurrentWeapon == -1074790547 ||
                (CurrentWeapon == 2132975508 || CurrentWeapon == -2084633992) ||
                (CurrentWeapon == -952879014 || CurrentWeapon == 100416529) ||
                CurrentWeapon == unchecked((int)WeaponHash.Gusenberg) ||
                CurrentWeapon == unchecked((int)WeaponHash.MG) || CurrentWeapon == unchecked((int)WeaponHash.CombatMG) ||
                CurrentWeapon == unchecked((int)WeaponHash.CombatPDW) ||
                CurrentWeapon == unchecked((int)WeaponHash.AssaultSMG) ||
                CurrentWeapon == unchecked((int)WeaponHash.SMG) ||
                CurrentWeapon == unchecked((int)WeaponHash.HeavySniper) ||
                CurrentWeapon == unchecked((int)WeaponHash.PumpShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.HeavyShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.Musket) ||
                CurrentWeapon == unchecked((int)WeaponHash.AssaultShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.BullpupShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.SawnOffShotgun) ||
                CurrentWeapon == unchecked((int)WeaponHash.GrenadeLauncher) ||
                CurrentWeapon == unchecked((int)WeaponHash.Railgun))
                return "move_weapon@rifle@generic";
            return null;
        }

        internal int GetWeaponHandsHeld(int weapon)
        {
            if (weapon == unchecked((int)WeaponHash.Unarmed)) return 0;
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
            if (weapon == unchecked((int)WeaponHash.Knife) || weapon == unchecked((int)WeaponHash.Nightstick) ||
                weapon == unchecked((int)WeaponHash.Hammer) || weapon == unchecked((int)WeaponHash.Crowbar) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Wrench) ||
                weapon == unchecked((int)GTANetworkShared.WeaponHash.Battleaxe) ||
                weapon == unchecked((int)WeaponHash.Dagger) || weapon == unchecked((int)WeaponHash.Hatchet) ||
                weapon == unchecked((int)WeaponHash.KnuckleDuster) || weapon == -581044007 || weapon == -102323637 || weapon == -538741184)
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

            if (GetAnimalAnimationName(ModelHash, speed) != null)
                return GetAnimalAnimationName(ModelHash, speed);
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
            var hash = (PedHash)ModelHash;

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
    }
}
