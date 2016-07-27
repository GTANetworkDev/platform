using System;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

public struct RespawnablePickup
{
    public int Hash;
    public int Amount;
    public int PickupTime;
    public int RespawnTime;
    public Vector3 Position;
}

public class Deathmatch : Script
{
    private List<Vector3> spawns;
    private List<int> weapons;
    private Dictionary<Client, int> Killstreaks;
    private Random rInst;
    
    public Deathmatch()
    {
        spawns = new List<Vector3>();
        spawns.Add(new Vector3(1482.36, 3587.45, 35.39));
        spawns.Add(new Vector3(1613.67, 3560.03, 35.42));
        spawns.Add(new Vector3(1533.44, 3581.24, 38.73));
        spawns.Add(new Vector3(1576.09, 3607.35, 38.73));
        spawns.Add(new Vector3(1596.88, 3590.43, 42.12));

        weapons = new List<int>();
        weapons.Add(324215364);
        weapons.Add(487013001);
        weapons.Add(-2084633992);
        
        rInst = new Random();

               
        Killstreaks = new Dictionary<Client, int>();

        API.onPlayerConnected += OnPlayerConnected;
        API.onPlayerRespawn += OnPlayerRespawn;
        API.onResourceStop += onResourceStop;
        API.onResourceStart += onResourceStart;
        API.onMapChange += onMapChange;
        API.onPlayerDeath += PlayerKilled;
    }

    private void onMapChange(string mapName, XmlGroup map)
    {     
        spawns.Clear();
        weapons.Clear();
        Killstreaks.Clear();
        var spawnpoints = map.getElementsByType("spawnpoint");
        foreach(var point in spawnpoints)
        {
            spawns.Add(new Vector3(point.getElementData<float>("posX"),
                point.getElementData<float>("posY"),
                point.getElementData<float>("posZ")));
        }

        var availableGuns = map.getElementsByType("weapon");
        foreach(var point in availableGuns)
        {
            weapons.Add(API.weaponNameToModel(point.getElementData<string>("model")));
        }

        API.resetIplList();

        var neededInteriors = map.getElementsByType("ipl");
        foreach(var point in neededInteriors)
        {
            API.requestIpl(point.getElementData<string>("name"));
        }

        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            API.setPlayerBlipSprite(player, 1);
            API.setPlayerBlipColor(player, 0);
            Respawn(player);
        }
    }

    private void onResourceStart(object sender, EventArgs e)
    {
        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            Respawn(player);
        }
    }

    private void onResourceStop(object sender, EventArgs e)
    {
        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            API.setPlayerBlipSprite(player, 1);
            API.setPlayerBlipColor(player, 0);
        }
    }

    // Exported
    public void Respawn(Client player)
    {
        API.sendNativeToPlayer(player, 17464388802800305651, new EntityArgument(player.CharacterHandle.Value), true);
        var rand = spawns[rInst.Next(spawns.Count)];
        API.setEntityPosition(player.CharacterHandle, rand);
        foreach(var gun in weapons)
        {
            API.givePlayerWeapon(player, gun, 500, false, true);
        }
        
        API.setPlayerHealth(player, 100);
    }
    
    public void OnPlayerConnected(Client player)
    {
        Respawn(player);
    }
    
    public void OnPlayerRespawn(Client player)
    {
        API.setPlayerBlipSprite(player, 1);
        API.setPlayerBlipColor(player, 0);

        Respawn(player);        
    }    

    public void PlayerKilled(Client player, NetHandle reason, int weapon)
    {
        Client killer = null; 

        if (!reason.IsNull)     
        {
            var players = API.getAllPlayers();
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].CharacterHandle == reason) {
                    killer = players[i];
                    break;
                }            
            }        
        }

        if (killer != null)
        {
            if (Killstreaks.ContainsKey(killer))
            {
                Killstreaks[killer]++;
                if (Killstreaks[killer] >= 3)
                {
                    API.sendChatMessageToAll("~b~" + killer.Name + "~w~ is on a killstreak! ~r~" + Killstreaks[killer] + "~w~ kills and counting!");
                    API.setPlayerBlipSprite(killer, 303);
                    API.setPlayerBlipColor(killer, 1);

                    if (Killstreaks[killer] == 4)
                    {
                        API.setPlayerHealth(killer, Math.Min(100, API.getPlayerHealth(killer) + 25));
                        API.sendChatMessageToPlayer(killer, "~g~Health bonus!");
                    }
                    else if (Killstreaks[killer] == 6)
                    {
                        API.setPlayerHealth(killer, Math.Min(100, API.getPlayerHealth(killer) + 50));
                        API.sendChatMessageToPlayer(killer, "~g~Health bonus!");
                    }
                    else if (Killstreaks[killer] == 8)
                    {
                        API.setPlayerHealth(killer, Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, Math.Min(100, API.getPlayerArmor(killer) + 25));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                    else if (Killstreaks[killer] == 12)
                    {
                        API.setPlayerHealth(killer, Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, Math.Min(100, API.getPlayerArmor(killer) + 50));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                    else if (Killstreaks[killer] >= 16 && Killstreaks[killer] % 4 == 0)
                    {
                        API.setPlayerHealth(killer, Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, Math.Min(100, API.getPlayerArmor(killer) + 75));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                }
            }
            else
            {
                Killstreaks.Add(killer, 1);
            }
        }

        if (Killstreaks.ContainsKey(player))
        {
            if (Killstreaks[player] >= 3 && killer != null)
            {
                API.sendChatMessageToAll("~b~" + killer.Name + "~w~ ruined ~r~" + player.Name + "~w~'s killstreak!");                
                API.setPlayerBlipColor(player, 0);
                API.setPlayerBlipSprite(player, 1);                
            }
            Killstreaks[player] = 0;
        }
        else
        {
            Killstreaks.Add(player, 0);
        }

        API.setPlayerBlipSprite(player, 274);
    }
}