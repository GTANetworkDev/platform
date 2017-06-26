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
            switch (CurrentWeapon)
            {
                case unchecked((int)WeaponHash.Unarmed):
                    return null;

                case unchecked((int)WeaponHash.RPG):
                case unchecked((int)WeaponHash.HomingLauncher):
                case unchecked((int)WeaponHash.Firework):
                    return "weapons@heavy@rpg";

                case unchecked((int)WeaponHash.Minigun):
                    return "weapons@heavy@minigun";

                case unchecked((int)WeaponHash.GolfClub):
                case unchecked((int)WeaponHash.Bat):
                    return "weapons@melee_2h";

                case -1357824103:
                case -1074790547:
                case 2132975508:
                case -2084633992:
                case -952879014:
                case 100416529:
                case unchecked((int)WeaponHash.Gusenberg):
                case unchecked((int)WeaponHash.MG):
                case unchecked((int)WeaponHash.CombatMG):
                case unchecked((int)WeaponHash.CombatPDW):
                case unchecked((int)WeaponHash.AssaultSMG):
                case unchecked((int)WeaponHash.SMG):
                case unchecked((int)WeaponHash.HeavySniper):
                case unchecked((int)WeaponHash.PumpShotgun):
                case unchecked((int)WeaponHash.HeavyShotgun):
                case unchecked((int)WeaponHash.Musket):
                case unchecked((int)WeaponHash.AssaultShotgun):
                case unchecked((int)WeaponHash.BullpupShotgun):
                case unchecked((int)WeaponHash.SawnOffShotgun):
                case unchecked((int)WeaponHash.GrenadeLauncher):
                case unchecked((int)WeaponHash.Railgun):
                    return "move_weapon@rifle@generic";
            }

            if (Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, CurrentWeapon) ==
                     Function.Call<int>(Hash.GET_WEAPONTYPE_SLOT, unchecked((int)WeaponHash.Bat)))
                return "weapons@melee_1h";

            return null;
        }

        internal int GetWeaponHandsHeld(int weapon)
        {
            switch (weapon)
            {
                case unchecked((int)WeaponHash.Unarmed):
                    return 0;

                case unchecked((int)WeaponHash.RPG):
                case unchecked((int)WeaponHash.HomingLauncher):
                case unchecked((int)WeaponHash.Firework):
                    return 5;

                case unchecked((int)WeaponHash.Minigun):
                    return 5;

                case unchecked((int)WeaponHash.GolfClub):
                case unchecked((int)GTANetworkShared.WeaponHash.Poolcue):
                case unchecked((int)WeaponHash.Bat):
                    return 4;

                case unchecked((int)WeaponHash.Knife):
                case unchecked((int)WeaponHash.Nightstick):
                case unchecked((int)WeaponHash.Hammer):
                case unchecked((int)WeaponHash.Crowbar):
                case unchecked((int)GTANetworkShared.WeaponHash.Wrench):
                case unchecked((int)GTANetworkShared.WeaponHash.Battleaxe):
                case unchecked((int)WeaponHash.Dagger):
                case unchecked((int)WeaponHash.Hatchet):
                case unchecked((int)WeaponHash.KnuckleDuster):
                case -581044007:
                case -102323637:
                case -538741184:
                    return 3;

                case -1357824103:
                case -1074790547:
                case 2132975508:
                case -2084633992:
                case -952879014:
                case 100416529:
                case unchecked((int)WeaponHash.Gusenberg):
                case unchecked((int)WeaponHash.MG):
                case unchecked((int)WeaponHash.CombatMG):
                case unchecked((int)WeaponHash.CombatPDW):
                case unchecked((int)WeaponHash.AssaultSMG):
                case unchecked((int)WeaponHash.SMG):
                case unchecked((int)WeaponHash.HeavySniper):
                case unchecked((int)WeaponHash.PumpShotgun):
                case unchecked((int)WeaponHash.HeavyShotgun):
                case unchecked((int)WeaponHash.Musket):
                case unchecked((int)WeaponHash.AssaultShotgun):
                case unchecked((int)WeaponHash.BullpupShotgun):
                case unchecked((int)WeaponHash.SawnOffShotgun):
                case unchecked((int)GTANetworkShared.WeaponHash.Autoshotgun):
                case unchecked((int)WeaponHash.CompactRifle):
                    return 2;
            }

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
            if (speed == 0) return "idle";

            var hash = (PedHash)modelhash;

            switch (hash)
            {
                case PedHash.Cat:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Boar:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "trot";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.ChickenHawk:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "glide";
                        if (speed == 3) return "flapping";
                    }
                    break;
                case PedHash.Chop:
                case PedHash.Shepherd:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Cormorant:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "glide";
                        if (speed == 3) return "flapping";
                    }
                    break;
                case PedHash.Cow:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "trot";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Coyote:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "trot";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Crow:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "glide";
                        if (speed == 3) return "flapping";
                    }
                    break;
                case PedHash.Deer:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "trot";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Dolphin:
                    {
                        if (speed == 1) return "swim";
                        if (speed == 2) return "accelerate";
                        if (speed == 3) return "accelerate";
                    }
                    break;
                case PedHash.Fish:
                    {
                        if (speed == 1) return "swim";
                        if (speed == 2) return "accelerate";
                        if (speed == 3) return "accelerate";
                    }
                    break;
                case PedHash.Hen:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "run";
                        if (speed == 3) return "run";
                    }
                    break;
                case PedHash.Humpback:
                    {
                        if (speed == 1) return "swim";
                        if (speed == 2) return "accelerate";
                        if (speed == 3) return "accelerate";
                    }
                    break;
                case PedHash.Husky:
                    {
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
                        if (speed == 1) return "swim";
                        if (speed == 2) return "accelerate";
                        if (speed == 3) return "accelerate";
                    }
                    break;
                case PedHash.Pig:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "trot";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Seagull:
                case PedHash.Pigeon:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "glide";
                        if (speed == 3) return "flapping";
                    }
                    break;
                case PedHash.Pug:
                case PedHash.Poodle:
                case PedHash.Westy:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Rabbit:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Rat:
                    {
                        if (speed == 1) return "walk";
                        if (speed == 2) return "canter";
                        if (speed == 3) return "gallop";
                    }
                    break;
                case PedHash.Rottweiler:
                case PedHash.Retriever:
                    {
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
            switch ((PedHash)modelhash)
            {
                case PedHash.Boar:
                    return "creatures@boar@move";
                case PedHash.Cat:
                    return "creatures@cat@move";
                case PedHash.ChickenHawk:
                    return "creatures@chickenhawk@move";
                case PedHash.Chop:
                case PedHash.Shepherd:
                    return "creatures@dog@move";
                case PedHash.Cormorant:
                    return "creatures@cormorant@move";
                case PedHash.Cow:
                    return "creatures@cow@move";
                case PedHash.Coyote:
                    return "creatures@coyote@move";
                case PedHash.Crow:
                    return "creatures@crow@move";
                case PedHash.Deer:
                    return "creatures@deer@move";
                case PedHash.Dolphin:
                    return "creatures@dolphin@move";
                case PedHash.Fish:
                    return "creatures@fish@move";
                case PedHash.Hen:
                    return "creatures@hen@move";
                case PedHash.Humpback:
                    return "creatures@humpback@move";
                case PedHash.Husky:
                    return "creatures@husky@move";
                case PedHash.KillerWhale:
                    return "creatures@killerwhale@move";
                case PedHash.Pig:
                    return "creatures@pig@move";
                case PedHash.Pigeon:
                    return "creatures@pigeon@move";
                case PedHash.Poodle:
                case PedHash.Pug:
                case PedHash.Westy:
                    return "creatures@pug@move";
                case PedHash.Rabbit:
                    return "creatures@rabbit@move";
                case PedHash.Rat:
                    return "creatures@rat@move";
                case PedHash.Retriever:
                    return "creatures@retriever@move";
                case PedHash.Rottweiler:
                    return "creatures@rottweiler@move";
                case PedHash.Seagull:
                    return "creatures@pigeon@move";
                case PedHash.HammerShark:
                case PedHash.TigerShark:           
                    return "creatures@shark@move";
                case PedHash.Stingray:
                    return "creatures@stingray@move";

                default:
                    return null;
            }
        }

        internal string GetAnimalGetUpAnimation()
        {
            return (PedHash)ModelHash == PedHash.Boar ? "creatures@boar@getup getup_l" : "anim@sports@ballgame@handball@ ball_get_up";
        }

        internal void Clear()
        {
            if (_entityToAimAt != null)
            {
                _entityToAimAt.Delete();
                _entityToAimAt = null;
            }

            LogManager.DebugLog("CLEAR FOR " + Name);
            if (Character != null)
            {
                Character.Model.MarkAsNoLongerNeeded();
                Character.Delete();
            }

            if (_mainBlip != null && _mainBlip.Exists())
            {
                _mainBlip.Delete();
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
