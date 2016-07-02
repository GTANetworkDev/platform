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
