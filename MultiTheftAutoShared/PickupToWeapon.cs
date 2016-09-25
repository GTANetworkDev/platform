namespace GTANetworkShared
{
    public static class PickupToWeapon
    {
        public static int Translate(int pickup)
        {
            switch (pickup)
            {
                default: return 0;
                case 157823901: return -1063057011; // WeaponSpecialCarbine -> SpecialCarbine
                case 483787975: return -37975472; // WeaponSmokeGrenade -> SmokeGrenade
                case 496339155: return 324215364; // WeaponMicroSMG -> MicroSMG
                case 663586612: return -1716189206; // WeaponKnife -> Knife
                case 746606563: return 741814745; // VehicleWeaponStickyBomb -> StickyBomb
                case 768803961: return 615608432; // WeaponMolotov -> Molotov
                case 772217690: return 2017895192; // VehicleWeaponSawnoffShotgun -> SawnoffShotgun
                case 779501861: return -1568386805; // WeaponGrenadeLauncher -> GrenadeLauncher
                case 792114228: return 1119849093; // WeaponMinigun -> Minigun
                case 978070226: return 736523883; // WeaponSMG -> SMG
                case 996550793: return 584646201; // WeaponAPPistol -> APPistol
                case 1295434569: return -1312131151; // WeaponRPG -> RPG
                case 1577485217: return -1813897027; // WeaponGrenade -> Grenade
                case 1587637620: return 1737195953; // WeaponNightstick -> Nightstick
                case 1705498857: return -37975472; // VehicleWeaponSmokeGrenade -> SmokeGrenade
                case 1735599485: return -72657034; // Parachute
                case 1765114797: return 205991906; // WeaponHeavySniper -> HeavySniper
                case 2081529176: return 741814745; // WeaponStickyBomb -> StickyBomb
                case -2124585240: return 2132975508; // WeaponBullpupRifle -> BullpupRifle
                case -2115084258: return -1786099057; // WeaponBat -> Bat
                case -2066319660: return 615608432; // VehicleWeaponMolotov -> Molotov
                case -2050315855: return -1660422300; // WeaponMG -> MG
                case -2027042680: return -2067956739; // WeaponCrowbar -> Crowbar
                case -1997886297: return 1141786504; // Golf Club
                case -1989692173: return 1593441988; // WeaponCombatPistol -> CombatPistol
                case -1835415205: return -494615257; // WeaponAssaultShotgun -> AssaultShotgun
                case -1766583645: return 2017895192; // WeaponSawnoffShotgun -> SawnoffShotgun
                case -1661912808: return -771403250; // WeaponHeavyPistol -> HeavyPistol
                case -1521817673: return 453432689; // VehicleWeaponPistol -> Pistol
                case -1491601256: return -1813897027; // VehicleWeaponGrenade -> Grenade
                case -1456120371: return 487013001; // WeaponPumpShotgun -> PumpShotgun
                case -1298986476: return 2144741730; // WeaponCombatMG -> CombatMG
                case -1296747938: return -1357824103; // WeaponAdvancedRifle -> AdvancedRifle
                case -1200951717: return 324215364; // VehicleWeaponMicroSMG -> MicroSMG
                case -977852653: return -1076751822; // WeaponSNSPistol -> SNSPistol
                case -962731009: return 883325847; // WeaponPetrolCan -> PetrolCan
                case -863291131: return 584646201; // VehicleWeaponAPPistol -> APPistol
                case -794112265: return 1593441988; // VehicleWeaponCombatPistol -> CombatPistol
                case -546236071: return -2084633992; // WeaponCarbineRifle -> CarbineRifle
                case -214137936: return -1074790547; // WeaponAssaultRifle -> AssaultRifle
                case -105925489: return 453432689; // WeaponPistol -> Pistol
                case -95310859: return -102323637; // WeaponBottle -> Bottle
                case -30788308: return 100416529; // WeaponSniperRifle -> SniperRifle


            }
        }
    }
}