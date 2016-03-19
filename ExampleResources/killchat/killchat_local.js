var weaponNames = new Object();
var weaponDicts = new Object();

weaponNames["137902532"] = "weap_hg_7"; // VintagePistol
weaponDicts["137902532"] = "srange_weap"; // VintagePistol


weaponNames["324215364"] = "weap_smg_1"; // MicroSMG
weaponDicts["324215364"] = "srange_weap2"; // MicroSMG

weaponNames["453432689"] = "weap_hg_1"; // Pistol
weaponDicts["453432689"] = "srange_weap"; // Pistol

weaponNames["487013001"] = "weap_sg_1"; // PumpShotgun
weaponDicts["487013001"] = "srange_weap2"; // PumpShotgun

weaponNames["584646201"] = "weap_hg_3"; // APPistol
weaponDicts["584646201"] = "srange_weap"; // APPistol


weaponNames["736523883"] = "weap_smg_2"; // SMG
weaponDicts["736523883"] = "srange_weap2"; // SMG


weaponNames["1119849093"] = "weap_hvy_1"; // Minigun
weaponDicts["1119849093"] = "srange_weap"; // Minigun

weaponNames["1593441988"] = "weap_hg_2"; // CombatPistol
weaponDicts["1593441988"] = "srange_weap"; // CombatPistol

weaponNames["1627465347"] = "weap_lmg_4"; // Gusenberg
weaponDicts["1627465347"] = "srange_weap2"; // Gusenberg


weaponNames["1834241177"] = "weap_rg_1"; // Railgun
weaponDicts["1834241177"] = "srange_weap"; // Railgun

weaponNames["2017895192"] = "weap_sg_2"; // SawnOffShotgun
weaponDicts["2017895192"] = "srange_weap2"; // SawnOffShotgun

weaponNames["2132975508"] = "weap_ar_5"; // BullpupRifle
weaponDicts["2132975508"] = "srange_weap"; // BullpupRifle


weaponNames["2144741730"] = "weap_lmg_3"; // CombatMG
weaponDicts["2144741730"] = "srange_weap2"; // CombatMG

weaponNames["-2084633992"] = "weap_ar_2"; // CarbineRifle
weaponDicts["-2084633992"] = "srange_weap"; // CarbineRifle

weaponNames["-1716589765"] = "weap_hg_4"; // Pistol50
weaponDicts["-1716589765"] = "srange_weap"; // Pistol50


weaponNames["-1660422300"] = "weap_lmg_1"; // MG
weaponDicts["-1660422300"] = "srange_weap2"; // MG

weaponNames["-1654528753"] = "weap_sg_"; // BullpupShotgun
weaponDicts["-1654528753"] = "srange_weap2"; // BullpupShotgun

weaponNames["-1357824103"] = "weap_ar_3"; // AdvancedRifle
weaponDicts["-1357824103"] = "srange_weap"; // AdvancedRifle

weaponNames["-1076751822"] = "weap_hg_6"; // SNSPistol
weaponDicts["-1076751822"] = "srange_weap"; // SNSPistol

weaponNames["-1074790547"] = "weap_ar_1"; // AssaultRifle
weaponDicts["-1074790547"] = "srange_weap"; // AssaultRifle

weaponNames["-1063057011"] = "weap_ar_6"; // SpecialCarbine
weaponDicts["-1063057011"] = "srange_weap"; // SpecialCarbine

weaponNames["-771403250"] = "weap_hg_5"; // HeavyPistol
weaponDicts["-771403250"] = "srange_weap"; // HeavyPistol

weaponNames["-494615257"] = "weap_sg_3"; // AssaultShotgun
weaponDicts["-494615257"] = "srange_weap2"; // AssaultShotgun

weaponNames["-270015777"] = "weap_smg_3"; // AssaultSMG
weaponDicts["-270015777"] = "srange_weap2"; // AssaultSMG

weaponNames["0"] = "deathmatch";
weaponDicts["0"] = "commonmenutu";

var mainArr = new Array();
var show = true;

API.onUpdate.connect(function (sender, args) {
    if (show) {
        var res = API.getScreenResolutionMantainRatio();
        for (var i = 0; i < mainArr.length; i++) {
            API.drawText(mainArr[i].k, host.toInt32(res.Width - 275), 300 + 70 * i, 0.4, 255, 255, 255, 255, 0, 2, false, true, 0);        
            API.drawText(mainArr[i].v, host.toInt32(res.Width - 200), 300 + 70 * i, 0.4, 255, 255, 255, 255, 0, 0, false, true, 0);
            
            var dct = weaponDicts[mainArr[i].w];
            var nam = weaponNames[mainArr[i].w];
            
            if (dct != null && nam != null) {
                API.drawGameTexture(dct, nam, host.toInt32(res.Width - 270), 290 + 70 * i, 70, 70, 0, 255, 255, 255, 255);
            } else {
                API.drawGameTexture("commonmenutu", "deathmatch", host.toInt32(res.Width - 270), 290 + 70 * i, 70, 70, 0, 255, 255, 255, 255);
            }
            
        }
    }
});

API.onChatCommand.connect(function (cmd){
    if (cmd == "/togglekillchat") {
        show = !show;
    }
});

API.onServerEventTrigger.connect(function (evName, args) {
    if (evName == "addKillToKillchat") {        
        var victim = args[0];
        var killer = args[1];
        var weapon = args[2];
        mainArr.push({v:victim, k:killer, w:weapon.toString()});
        if (mainArr.length > 5)
        {
            mainArr.shift();
        }
    }
});