var mainWindow = null;
var invincible = false;

API.onResourceStart.connect(function (sender, e) {
	mainWindow = API.createMenu("WEAPONS", 0, 0, 6);

	var linkItem = API.createMenuItem("Weapons", "");
	resource.trainer.mainWindow.AddItem(linkItem);
	resource.trainer.mainWindow.BindMenuToItem(mainWindow, linkItem);
	resource.trainer.menuPool.Add(mainWindow);

	mainWindow.AddItem(addGunItem("SniperRifle", 100416529));
	mainWindow.AddItem(addGunItem("FireExtinguisher", 101631238));
	mainWindow.AddItem(addGunItem("CompactLauncher", 125959754));
	mainWindow.AddItem(addGunItem("Snowball", 126349499));
	mainWindow.AddItem(addGunItem("VintagePistol", 137902532));
	mainWindow.AddItem(addGunItem("CombatPDW", 171789620));
	mainWindow.AddItem(addGunItem("HeavySniper", 205991906));
	mainWindow.AddItem(addGunItem("Autoshotgun", 317205821));
	mainWindow.AddItem(addGunItem("MicroSMG", 324215364));
	mainWindow.AddItem(addGunItem("Wrench", 419712736));
	mainWindow.AddItem(addGunItem("Pistol", 453432689));
	mainWindow.AddItem(addGunItem("PumpShotgun", 487013001));
	mainWindow.AddItem(addGunItem("APPistol", 584646201));
	mainWindow.AddItem(addGunItem("Ball", 600439132));
	mainWindow.AddItem(addGunItem("Molotov", 615608432));
	mainWindow.AddItem(addGunItem("SMG", 736523883));
	mainWindow.AddItem(addGunItem("StickyBomb", 741814745));
	mainWindow.AddItem(addGunItem("PetrolCan", 883325847));
	mainWindow.AddItem(addGunItem("StunGun", 911657153));
	mainWindow.AddItem(addGunItem("HeavyShotgun", 984333226));
	mainWindow.AddItem(addGunItem("Minigun", 1119849093));
	mainWindow.AddItem(addGunItem("Golfclub", 1141786504));
	mainWindow.AddItem(addGunItem("FlareGun", 1198879012));
	mainWindow.AddItem(addGunItem("Flare", 1233104067));
	mainWindow.AddItem(addGunItem("GrenadeLauncherSmoke", 1305664598));
	mainWindow.AddItem(addGunItem("Hammer", 1317494643));
	mainWindow.AddItem(addGunItem("CombatPistol", 1593441988));
	mainWindow.AddItem(addGunItem("Gusenberg", 1627465347));
	mainWindow.AddItem(addGunItem("CompactRifle", 1649403952));
	mainWindow.AddItem(addGunItem("HomingLauncher", 1672152130));
	mainWindow.AddItem(addGunItem("Nightstick", 1737195953));
	mainWindow.AddItem(addGunItem("Railgun", 1834241177));
	mainWindow.AddItem(addGunItem("SawnoffShotgun", 2017895192));
	mainWindow.AddItem(addGunItem("BullpupRifle", 2132975508));
	mainWindow.AddItem(addGunItem("Firework", 2138347493));
	mainWindow.AddItem(addGunItem("CombatMG", 2144741730));
	mainWindow.AddItem(addGunItem("CarbineRifle", -2084633992));
	mainWindow.AddItem(addGunItem("Crowbar", -2067956739));
	mainWindow.AddItem(addGunItem("Flashlight", -1951375401));
	mainWindow.AddItem(addGunItem("Dagger", -1834847097));
	mainWindow.AddItem(addGunItem("Grenade", -1813897027));
	mainWindow.AddItem(addGunItem("Poolcue", -1810795771));
	mainWindow.AddItem(addGunItem("Bat", -1786099057));
	mainWindow.AddItem(addGunItem("Pistol50", -1716589765));
	mainWindow.AddItem(addGunItem("Knife", -1716189206));
	mainWindow.AddItem(addGunItem("MG", -1660422300));
	mainWindow.AddItem(addGunItem("BullpupShotgun", -1654528753));
	mainWindow.AddItem(addGunItem("BZGas", -1600701090));
	mainWindow.AddItem(addGunItem("Unarmed", -1569615261));
	mainWindow.AddItem(addGunItem("GrenadeLauncher", -1568386805));
	mainWindow.AddItem(addGunItem("Musket", -1466123874));
	mainWindow.AddItem(addGunItem("ProximityMine", -1420407917));
	mainWindow.AddItem(addGunItem("AdvancedRifle", -1357824103));
	mainWindow.AddItem(addGunItem("RPG", -1312131151));
	mainWindow.AddItem(addGunItem("Pipebomb", -1169823560));
	mainWindow.AddItem(addGunItem("MiniSMG", -1121678507));
	mainWindow.AddItem(addGunItem("SNSPistol", -1076751822));
	mainWindow.AddItem(addGunItem("AssaultRifle", -1074790547));
	mainWindow.AddItem(addGunItem("SpecialCarbine", -1063057011));
	mainWindow.AddItem(addGunItem("Revolver", -1045183535));
	mainWindow.AddItem(addGunItem("MarksmanRifle", -952879014));
	mainWindow.AddItem(addGunItem("Battleaxe", -853065399));
	mainWindow.AddItem(addGunItem("HeavyPistol", -771403250));
	mainWindow.AddItem(addGunItem("KnuckleDuster", -656458692));
	mainWindow.AddItem(addGunItem("MachinePistol", -619010992));
	mainWindow.AddItem(addGunItem("MarksmanPistol", -598887786));
	mainWindow.AddItem(addGunItem("Machete", -581044007));
	mainWindow.AddItem(addGunItem("SwitchBlade", -538741184));
	mainWindow.AddItem(addGunItem("AssaultShotgun", -494615257));
	mainWindow.AddItem(addGunItem("DoubleBarrelShotgun", -275439685));
	mainWindow.AddItem(addGunItem("AssaultSMG", -270015777));
	mainWindow.AddItem(addGunItem("Hatchet", -102973651));
	mainWindow.AddItem(addGunItem("Bottle", -102323637));
	mainWindow.AddItem(addGunItem("Parachute", -72657034));
	mainWindow.AddItem(addGunItem("SmokeGrenade", -37975472));

	mainWindow.RefreshIndex();
});

function addGunItem(gun, hash) {

	var suicide = API.createMenuItem(gun, "");

	suicide.Activated.connect(function (menu, item) {
		API.givePlayerWeapon(hash, 100, true, true);
	});

	return suicide;
}


API.onUpdate.connect(function (s, e) {
	
})