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
    private List<WeaponHash> weapons;
    private Dictionary<Client, int> Killstreaks;
    private Random rInst;

    private int killTarget;
    
    public Deathmatch()
    {
        spawns = new List<Vector3>();
        spawns.Add(new Vector3(1482.36, 3587.45, 35.39));
        spawns.Add(new Vector3(1613.67, 3560.03, 35.42));
        spawns.Add(new Vector3(1533.44, 3581.24, 38.73));
        spawns.Add(new Vector3(1576.09, 3607.35, 38.73));
        spawns.Add(new Vector3(1596.88, 3590.43, 42.12));

        weapons = new List<WeaponHash>();
        weapons.Add(WeaponHash.MicroSMG);
        weapons.Add(WeaponHash.PumpShotgun);
        weapons.Add(WeaponHash.CarbineRifle);
        
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
            var pBlip = API.exported.playerblips.getPlayerBlip(player);

            API.setBlipSprite(pBlip, 1);
            API.setBlipColor(pBlip, 0);
            Respawn(player);
        }
    }

    private void onResourceStart()
    {
        var players = API.getAllPlayers();

        API.exported.scoreboard.addScoreboardColumn("dm_score", "Score", 80);
        API.exported.scoreboard.addScoreboardColumn("dm_kdr", "Ratio", 80);
        API.exported.scoreboard.addScoreboardColumn("dm_deaths", "Deaths", 80);        
        API.exported.scoreboard.addScoreboardColumn("dm_kills", "Kills", 80);

        foreach (var player in players)
        {
            Respawn(player);

            API.setEntitySyncedData(player.handle, "dm_score", 0);
            API.setEntitySyncedData(player.handle, "dm_deaths", 0);
            API.setEntitySyncedData(player.handle, "dm_kills", 0);
            API.setEntitySyncedData(player.handle, "dm_kdr", 0);

            API.exported.scoreboard.setPlayerScoreboardData(player, "dm_score", "0");
            API.exported.scoreboard.setPlayerScoreboardData(player, "dm_deaths", "0");
            API.exported.scoreboard.setPlayerScoreboardData(player, "dm_kills", "0");
            API.exported.scoreboard.setPlayerScoreboardData(player, "dm_kdr", "0");
        }

        killTarget = API.getSetting<int>("victory_kills");
    }

    private void onResourceStop()
    {
        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            var pBlip = API.exported.playerblips.getPlayerBlip(player);

            API.setBlipSprite(pBlip, 1);
            API.setBlipColor(pBlip, 0);

            API.setEntitySyncedData(player.handle, "dm_score", 0);
            API.setEntitySyncedData(player.handle, "dm_deaths", 0);
            API.setEntitySyncedData(player.handle, "dm_kills", 0);
            API.setEntitySyncedData(player.handle, "dm_kdr", 0);

            UpdateScoreboardData(player);
        }

        API.exported.scoreboard.removeScoreboardColumn("dm_score");
        API.exported.scoreboard.removeScoreboardColumn("dm_kdr");
        API.exported.scoreboard.removeScoreboardColumn("dm_deaths");
        API.exported.scoreboard.removeScoreboardColumn("dm_kills");
    }

    // Exported
    public void Respawn(Client player)
    {
        API.removeAllPlayerWeapons(player);
        var rand = spawns[rInst.Next(spawns.Count)];
        API.setEntityPosition(player.handle, rand);
        foreach(var gun in weapons)
        {
            API.givePlayerWeapon(player, gun, 500, false, true);
        }
        
        API.setPlayerHealth(player, 100);
    }
    
    public void OnPlayerConnected(Client player)
    {
        API.setEntitySyncedData(player.handle, "dm_score", 0);
        API.setEntitySyncedData(player.handle, "dm_deaths", 0);
        API.setEntitySyncedData(player.handle, "dm_kills", 0);
        API.setEntitySyncedData(player.handle, "dm_kdr", 0);

        UpdateScoreboardData(player);

        Respawn(player);
    }
    
    public void OnPlayerRespawn(Client player)
    {
        var pBlip = API.exported.playerblips.getPlayerBlip(player);

        API.setBlipSprite(pBlip, 1);
        API.setBlipColor(pBlip, 0);

        Respawn(player);        
    }    

    private void UpdateScoreboardData(Client player)
    {
        API.exported.scoreboard.setPlayerScoreboardData(player, "dm_score", API.getEntitySyncedData(player.handle, "dm_score").ToString());
        API.exported.scoreboard.setPlayerScoreboardData(player, "dm_deaths", API.getEntitySyncedData(player.handle, "dm_deaths").ToString());
        API.exported.scoreboard.setPlayerScoreboardData(player, "dm_kills", API.getEntitySyncedData(player.handle, "dm_kills").ToString());
        API.exported.scoreboard.setPlayerScoreboardData(player, "dm_kdr", API.getEntitySyncedData(player.handle, "dm_kdr").ToString("F2"));
    }

    public void PlayerKilled(Client player, NetHandle reason, int weapon)
    {
        Client killer = null; 

        if (!reason.IsNull)     
        {
            var players = API.getAllPlayers();
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].handle == reason) {
                    killer = players[i];
                    break;
                }            
            }        
        }

        API.setEntitySyncedData(player.handle, "dm_score", API.getEntitySyncedData(player.handle, "dm_score") - 1);
        API.setEntitySyncedData(player.handle, "dm_deaths", API.getEntitySyncedData(player.handle, "dm_deaths") + 1);

        API.setEntitySyncedData(player.handle, "dm_kdr", API.getEntitySyncedData(player.handle, "dm_kills") / (float)API.getEntitySyncedData(player.handle, "dm_deaths"));

        UpdateScoreboardData(player);

        if (killer != null)
        {
            API.setEntitySyncedData(killer.handle, "dm_kills", API.getEntitySyncedData(killer.handle, "dm_kills") + 1);
            API.setEntitySyncedData(killer.handle, "dm_score", API.getEntitySyncedData(killer.handle, "dm_score") + 1);
            if (API.getEntitySyncedData(killer.handle, "dm_deaths") != 0)
            API.setEntitySyncedData(killer.handle, "dm_kdr", API.getEntitySyncedData(killer.handle, "dm_kills") / (float)API.getEntitySyncedData(killer.handle, "dm_deaths"));

            UpdateScoreboardData(killer);

            /*if (API.getEntitySyncedData(killer.handle, "dm_kills") >= killTarget)
            {
                API.sendChatMessageToAll("~b~~h~" + killer.name + "~h~~w~ has won the round with " + killTarget + " kills and " + API.getEntitySyncedData(player.handle, "dm_deaths") + " deaths!");
                API.exported.mapcycler.endRound();
            }
*/
            if (Killstreaks.ContainsKey(killer))
            {
                Killstreaks[killer]++;
                if (Killstreaks[killer] >= 3)
                {
                    var kBlip = API.exported.playerblips.getPlayerBlip(killer);

                    API.sendChatMessageToAll("~b~" + killer.name + "~w~ is on a killstreak! ~r~" + Killstreaks[killer] + "~w~ kills and counting!");
                    API.setBlipSprite(kBlip, 303);
                    API.setBlipColor(kBlip, 1);

                    if (Killstreaks[killer] == 4)
                    {
                        API.setPlayerHealth(killer, (int)Math.Min(100, API.getPlayerHealth(killer) + 25));
                        API.sendChatMessageToPlayer(killer, "~g~Health bonus!");
                    }
                    else if (Killstreaks[killer] == 6)
                    {
                        API.setPlayerHealth(killer, (int)Math.Min(100, API.getPlayerHealth(killer) + 50));
                        API.sendChatMessageToPlayer(killer, "~g~Health bonus!");
                    }
                    else if (Killstreaks[killer] == 8)
                    {
                        API.setPlayerHealth(killer, (int)Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, (int)Math.Min(100, API.getPlayerArmor(killer) + 25));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                    else if (Killstreaks[killer] == 12)
                    {
                        API.setPlayerHealth(killer, (int)Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, (int)Math.Min(100, API.getPlayerArmor(killer) + 50));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                    else if (Killstreaks[killer] >= 16 && Killstreaks[killer] % 4 == 0)
                    {
                        API.setPlayerHealth(killer, (int)Math.Min(100, API.getPlayerHealth(killer) + 75));
                        API.setPlayerArmor(killer, (int)Math.Min(100, API.getPlayerArmor(killer) + 75));
                        API.sendChatMessageToPlayer(killer, "~g~Health and armor bonus!");
                    }
                }
            }
            else
            {
                Killstreaks.Add(killer, 1);
            }
        }

        var pBlip = API.exported.playerblips.getPlayerBlip(player);

        if (Killstreaks.ContainsKey(player))
        {
            if (Killstreaks[player] >= 3 && killer != null)
            {
                API.sendChatMessageToAll("~b~" + killer.name + "~w~ ruined ~r~" + player.name + "~w~'s killstreak!");                
                API.setBlipColor(pBlip, 0);
                API.setBlipSprite(pBlip, 1);                
            }
            Killstreaks[player] = 0;
        }
        else
        {
            Killstreaks.Add(player, 0);
        }

        //API.setBlipSprite(pBlip, 274); // why is it here?        
    }
}