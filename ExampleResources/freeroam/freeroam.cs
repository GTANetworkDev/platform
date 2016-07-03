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
    public FreeroamScript()
    {
        API.onPlayerRespawn += onPlayerRespawn;     
        API.onChatCommand += onPlayerCommand;
    }

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
    };

    private void onPlayerCommand(Client sender, string cmd, CancelEventArgs cancel)
    {
        var args = cmd.Split();

        if (args[0] == "/me")
        {
            API.sendChatMessageToAll("~#CB15EB~", sender.Name + " " + cmd.Substring(4) + ".");
        }

        if (args[0] == "/spec")
        {
            if (args.Length >= 2)
            {
                var targetName = args[1];
                var target = API.getPlayerFromName(targetName);
                if (target != null)
                {
                    API.setPlayerToSpectatePlayer(sender, target);
                }
                else
                {
                    API.sendNotificationToPlayer(sender, "No such player found.");
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /spec [player name]");
            }
        }

        if (args[0] == "/loadipl")
        {
            if (args.Length >= 2)
            {
                var ipl = args[1];
                API.requestIpl(ipl);
                API.consoleOutput("LOADED IPL " + ipl);
                API.sendChatMessageToPlayer(sender, "Loaded IPL ~b~" + ipl + "~w~.");
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /loadipl [IPL name]");
            }
        }

        if (args[0] == "/blackout")
        {
            if (args.Length >= 2)
            {
                bool blackout;
                if (!bool.TryParse(args[1], out blackout))                
                {
                    API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ wrong input!");
                }
                else
                {
                    API.sendNativeToAllPlayers(0x1268615ACE24D504, blackout);
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /blackout [true/false]");
            }
        }

        if (args[0] == "/removeipl")
        {
            if (args.Length >= 2)
            {
                var ipl = args[1];
                API.removeIpl(ipl);
                API.consoleOutput("REMOVED IPL " + ipl);
                API.sendChatMessageToPlayer(sender, "Removed IPL ~b~" + ipl + "~w~.");
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /removeipl [IPL name]");
            }
        }

        if (cmd == "/unspec")
        {
            API.unspectatePlayer(sender);
        }

        if (args[0] == "/mod")
        {
            if (args.Length < 3)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~ /mod [modIndex] [modvariation]");
            }
            else
            {
                int modIndex, modVar;
                if (!int.TryParse(args[1], out modIndex) || !int.TryParse(args[2], out modVar))
                {
                        API.sendChatMessageToPlayer(sender, "~r~ERROR: Wrong input!");
                        return;
                }

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
        }

        if (args[0] == "/clothes")
        {
            if (args.Length < 4)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~ /clothes [slot] [drawable] [texture]");
            }
            else
            {
                int slot, drawable, texture;
                if (!int.TryParse(args[1], out slot) || !int.TryParse(args[2], out drawable) || !int.TryParse(args[3], out texture))
                {
                        API.sendChatMessageToPlayer(sender, "~r~ERROR: Wrong input!");
                        return;
                }

                API.setPlayerProp(sender, slot, drawable, texture);
                API.sendChatMessageToPlayer(sender, "Clothes applied successfully!");                
            }
        }

        if (args[0] == "/props")
        {
            if (args.Length < 4)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~ /props [slot] [drawable] [texture]");
            }
            else
            {
                int slot, drawable, texture;
                if (!int.TryParse(args[1], out slot) || !int.TryParse(args[2], out drawable) || !int.TryParse(args[3], out texture))
                {
                        API.sendChatMessageToPlayer(sender, "~r~ERROR: Wrong input!");
                        return;
                }

                if (drawable == -1)
                {
                    API.clearPlayerAccessory(sender, slot);
                    return;
                }

                API.setPlayerAccessory(sender, slot, drawable, texture);
                API.sendChatMessageToPlayer(sender, "Props applied successfully!");                
            }
        }

        if (args[0] == "/colors")
        {
            if (args.Length < 3)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~ /colors [primaryColor] [secondaryColor]");
            }
            else
            {
                int primaryColor, secondaryColor;
                if (!int.TryParse(args[1], out primaryColor) || !int.TryParse(args[2], out secondaryColor))
                {
                        API.sendChatMessageToPlayer(sender, "~r~ERROR: Wrong input!");
                        return;
                }

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
        }

        if (args[0] == "/anim")
        {
            if (args.Length < 2)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~/anim [animation]");
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~/anim help for animation list.");                
            }
            else
            {
                if (args[1] == "help")
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
                else if (!AnimationList.ContainsKey(args[1]))
                {
                    API.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~Animation not found!");                    
                }
                else
                {
                    API.playPlayerAnimation(sender, 0, AnimationList[args[1]].Split()[0], AnimationList[args[1]].Split()[1]);
                }
            }
        }

        if (args[0] == "/rawanim")
        {
            if (args.Length < 4)
            {
                API.sendChatMessageToPlayer(sender, "~y~USAGE: ~w~ /rawanim [flag] [animDict] [animName]");
            }
            else
            {
                int flag;
                if (!int.TryParse(args[1], out flag))
                {
                        API.sendChatMessageToPlayer(sender, "~r~ERROR: Wrong input!");
                        return;
                }

                API.playPlayerAnimation(sender, flag, args[2], args[3]);
            }
        }

        if (args[0] == "/stopanim")
        {
            API.stopPlayerAnimation(sender);
        }

        if (args[0] == "/car")
        {
            if (args.Length >= 2)
            {
                var vehName = args[1];
                var vehModel = API.vehicleNameToModel(vehName);
                if (vehModel == 0)
                {
                    API.sendNotificationToPlayer(sender, "No such model found!");
                }
                else
                {
                    var rot = API.getEntityRotation(sender.CharacterHandle);
                    var veh = API.createVehicle(vehModel, sender.Position, new Vector3(0, 0, rot.Z), 0, 0);
                    API.setPlayerIntoVehicle(sender, veh, -1);

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
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /car [model name]");
            }
       }

       if (args[0] == "/skin")
        {
            if (args.Length >= 2)
            {
                var vehName = args[1];
                var vehModel = API.pedNameToModel(vehName);
                if (vehModel == 0)
                {
                    API.sendNotificationToPlayer(sender, "No such model found!");
                }
                else
                {
                    API.setPlayerSkin(sender, vehModel);
                    API.sendNativeToPlayer(sender, 0x45EEE61580806D63, sender.CharacterHandle);
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /skin [model name]");
            }
       }

       if (args[0] == "/pic")
       {
            if (args.Length >= 2)
            {
                var vehName = args[1];
                var vehModel = API.pickupNameToModel(vehName);
                if (vehModel == 0)
                {
                    API.sendNotificationToPlayer(sender, "No such model found!");
                }
                else
                {
                    var veh = API.createPickup(vehModel, new Vector3(sender.Position.X + 10, sender.Position.Y, sender.Position.Z), new Vector3(), 10);                
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /pic [model name]");
            }
       }
       
       if (cmd == "/countdown")
       {
            API.triggerClientEventForAll("startCountdown");
       }
   
        if (args[0] == "/tp")
        {
            if (args.Length >= 2)
            {
                var targetName = args[1];
                var target = API.getPlayerFromName(targetName);

                if (target != null)
                {
                    API.consoleOutput("Name: " + target.Name + " ID: " + target.CharacterHandle.Value + " pos: " + (API.getEntityPosition(target.CharacterHandle) == null));
                    API.setEntityPosition(sender.CharacterHandle, API.getEntityPosition(target.CharacterHandle));
                }
                else
                {
                    API.sendNotificationToPlayer(sender, "No such player found.");
                }
            }
            else
            {
                API.sendNotificationToPlayer(sender, "USAGE: /tp [player name]");
            }               
        }
    }

    public void onPlayerRespawn(Client player)
    {
        API.consoleOutput(player.Name + " has respawned.");
    }
}
