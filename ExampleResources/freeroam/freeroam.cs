using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class FreeroamScript : Script
{
    public Dictionary<Client, List<NetHandle>> VehicleHistory = new Dictionary<Client, List<NetHandle>>();
    public Dictionary<string, string> AnimationList = new Dictionary<string, string>
    {
        {"finger", "mp_player_intfinger mp_player_int_finger"},
        {"guitar", "anim@mp_player_intcelebrationmale@air_guitar air_guitar"},
        {"shagging", "anim@mp_player_intcelebrationmale@air_shagging air_shagging"},
        {"synth", "anim@mp_player_intcelebrationmale@air_synth air_synth"},
        {"kiss", "anim@mp_player_intcelebrationmale@blow_kiss blow_kiss"},        
        {"bro", "anim@mp_player_intcelebrationmale@bro_love bro_love"},
        {"chicken", "anim@mp_player_intcelebrationmale@chicken_taunt chicken_taunt"},
        {"chin", "anim@mp_player_intcelebrationmale@chin_brush chin_brush"},
        {"dj", "anim@mp_player_intcelebrationmale@dj dj"},
        {"dock", "anim@mp_player_intcelebrationmale@dock dock"},
        {"facepalm", "anim@mp_player_intcelebrationmale@face_palm face_palm"},
        {"fingerkiss", "anim@mp_player_intcelebrationmale@finger_kiss finger_kiss"},
        {"freakout", "anim@mp_player_intcelebrationmale@freakout freakout"},
        {"jazzhands", "anim@mp_player_intcelebrationmale@jazz_hands jazz_hands"},
        {"knuckle", "anim@mp_player_intcelebrationmale@knuckle_crunch knuckle_crunch"},
        {"nose", "anim@mp_player_intcelebrationmale@nose_pick nose_pick"},
        {"no", "anim@mp_player_intcelebrationmale@no_way no_way"},
        {"peace", "anim@mp_player_intcelebrationmale@peace peace"},
        {"photo", "anim@mp_player_intcelebrationmale@photography photography"},
        {"rock", "anim@mp_player_intcelebrationmale@rock rock"},
        {"salute", "anim@mp_player_intcelebrationmale@salute salute"},
        {"shush", "anim@mp_player_intcelebrationmale@shush shush"},
        {"slowclap", "anim@mp_player_intcelebrationmale@slow_clap slow_clap"},
        {"surrender", "anim@mp_player_intcelebrationmale@surrender surrender"},
        {"thumbs", "anim@mp_player_intcelebrationmale@thumbs_up thumbs_up"},
        {"taunt", "anim@mp_player_intcelebrationmale@thumb_on_ears thumb_on_ears"},
        {"vsign", "anim@mp_player_intcelebrationmale@v_sign v_sign"},
        {"wank", "anim@mp_player_intcelebrationmale@wank wank"},
        {"wave", "anim@mp_player_intcelebrationmale@wave wave"},
        {"loco", "anim@mp_player_intcelebrationmale@you_loco you_loco"},
        {"handsup", "missminuteman_1ig_2 handsup_base"},
    };


    [Command("me", GreedyArg = true)]
    public void MeCommand(Client sender, string text)
    {
        API.sendChatMessageToAll("~#C2A2DA~", sender.Name + " " + text);
    }

    [Command("spec")]
    public void SpectatorCommand(Client sender, Client target)
    {
        API.setPlayerToSpectatePlayer(sender, target);
    }

    [Command("unspec")]
    public void StopSpectatingCommand(Client sender)
    {
        API.unspectatePlayer(sender);
    }

    [Command("loadipl")]
    public void LoadIplCommand(Client sender, string ipl)
    {
        API.requestIpl(ipl);
        API.consoleOutput("LOADED IPL " + ipl);
        API.sendChatMessageToPlayer(sender, "Loaded IPL ~b~" + ipl + "~w~.");
    }

    [Command("removeipl")]
    public void RemoveIplCommand(Client sender, string ipl)
    {
        API.removeIpl(ipl);
        API.consoleOutput("REMOVED IPL " + ipl);
        API.sendChatMessageToPlayer(sender, "Removed IPL ~b~" + ipl + "~w~.");
    }

    [Command("blackout")]
    public void BlackoutCommand(Client sender, bool blackout)
    {
        API.sendNativeToAllPlayers(0x1268615ACE24D504, blackout);
    }

    [Command("mod")]
    public void SetCarModificationCommand(Client sender, int modIndex, int modVar)
    {
        if (!sender.CurrentVehicle.IsNull)
        {
                API.setVehicleMod(sender.CurrentVehicle, modIndex, modVar);
                API.sendChatMessageToPlayer(sender, "Mod applied successfully!");
        }
        else
        {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~You're not in a vehicle!");
        }
    }

    [Command("clothes")]
    public void SetPedClothesCommand(Client sender, int slot, int drawable, int texture)
    {
        API.setPlayerProp(sender, slot, drawable, texture);
        API.sendChatMessageToPlayer(sender, "Clothes applied successfully!");
    }

    [Command("props")]
    public void SetPedAccessoriesCommand(Client sender, int slot, int drawable, int texture)
    {
        API.setPlayerAccessory(sender, slot, drawable, texture);
        API.sendChatMessageToPlayer(sender, "Props applied successfully!");
    }

    [Command("colors")]
    public void GameVehicleColorsCommand(Client sender, int primaryColor, int secondaryColor)
    {
        if (!sender.CurrentVehicle.IsNull)
        {
                API.setVehiclePrimaryColor(sender.CurrentVehicle, primaryColor);
                API.setVehicleSecondaryColor(sender.CurrentVehicle, secondaryColor);
                API.sendChatMessageToPlayer(sender, "Colors applied successfully!");
        }
        else
        {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~You're not in a vehicle!");
        }
    }

    [Command("colorsrgb")]
    public void CustomVehicleColorsCommand(Client sender, int primaryRed, int primaryGreen, int primaryBlue, int secondaryRed, int secondaryGreen, int secondaryBlue)
    {
        if (!sender.CurrentVehicle.IsNull)
        {
                API.setVehicleCustomPrimaryColor(sender.CurrentVehicle, primaryRed, primaryGreen, primaryBlue);
                API.setVehicleCustomSecondaryColor(sender.CurrentVehicle, secondaryRed, secondaryGreen, secondaryBlue);
                API.sendChatMessageToPlayer(sender, "Colors applied successfully!");
        }
        else
        {
                API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~You're not in a vehicle!");
        }
    }

    [Command("anim", "~y~USAGE: ~w~/anim [animation]\n" +
                     "~y~USAGE: ~w~/anim help for animation list.\n" +
                     "~y~USAGE: ~w~/anim stop to stop current animation.")]
    public void SetPlayerAnim(Client sender, string animation)
    {
        if (animation == "help")
        {
            string helpText = AnimationList.Aggregate(new StringBuilder(),
                            (sb, kvp) => sb.Append(kvp.Key + " "), sb => sb.ToString());
            API.sendChatMessageToPlayer(sender, "~b~Available animations:");
            var split = helpText.Split();
            for (int i = 0; i < split.Length; i += 5)
            {
                string output = "";
                if (split.Length > i)
                    output += split[i] + " ";
                if (split.Length > i + 1)
                    output += split[i + 1] + " ";
                if (split.Length > i + 2)
                    output += split[i + 2] + " ";
                if (split.Length > i + 3)
                    output += split[i + 3] + " ";
                if (split.Length > i + 4)
                    output += split[i + 4] + " ";
                if (!string.IsNullOrWhiteSpace(output))
                    API.sendChatMessageToPlayer(sender, "~b~>> ~w~" + output);
            }
        }
        else if (animation == "stop")
        {
            API.stopPlayerAnimation(sender);
        }
        else if (!AnimationList.ContainsKey(animation))
        {
            API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~Animation not found!");                    
        }
        else
        {   
            var flag = 0;
            if (animation == "handsup") flag = 1;

            API.playPlayerAnimation(sender, flag, AnimationList[animation].Split()[0], AnimationList[animation].Split()[1]);
        }
    }

    [Command("car")]
    public void SpawnCarCommand(Client sender, VehicleHash model)
    {
        var rot = API.getEntityRotation(sender.CharacterHandle);
        var veh = API.createVehicle(model, sender.Position, new Vector3(0, 0, rot.Z), 0, 0);

        if (VehicleHistory.ContainsKey(sender))
        {
            VehicleHistory[sender].Add(veh);
            if (VehicleHistory[sender].Count > 3)
            {
                API.deleteEntity(VehicleHistory[sender][0]);
                VehicleHistory[sender].RemoveAt(0);
            }
        }
        else
        {
            VehicleHistory.Add(sender, new List<NetHandle> { veh });
        }

        var start = Environment.TickCount;
        while (!API.doesEntityExistForPlayer(sender, veh) && Environment.TickCount - start < 1000) {}
        API.setPlayerIntoVehicle(sender, veh, -1);        
    }

    [Command("skin")]
    public void ChangeSkinCommand(Client sender, PedHash model)
    {
        API.setPlayerSkin(sender, model);
        API.sendNativeToPlayer(sender, 0x45EEE61580806D63, sender.CharacterHandle);        
    }

    [Command("pic")]
    public void SpawnPickupCommand(Client sender, PickupHash pickup)
    {
        API.createPickup(pickup, new Vector3(sender.Position.X + 10, sender.Position.Y, sender.Position.Z), new Vector3(), 10);
    }

    [Command("countdown")]
    public void StartGlobalCountdownCommand(Client sender)
    {
        API.triggerClientEventForAll("startCountdown");
    }

    [Command("tp")]
    public void TeleportPlayerToPlayerCommand(Client sender, Client target)
    {
        var pos = API.getEntityPosition(sender.CharacterHandle);                    
        API.sendNativeToAllPlayers(0xB80D8756B4668AB6, "scr_rcbarry1");
        API.sendNativeToAllPlayers(0x6C38AF3693A69A91, "scr_rcbarry1");
        API.sendNativeToAllPlayers(0x25129531F77B9ED3, "scr_alien_teleport", pos.X, pos.Y, pos.Z, 0, 0, 0, 1f, 0, 0, 0);

        API.setEntityPosition(sender.CharacterHandle, API.getEntityPosition(target.CharacterHandle));
    }
}
