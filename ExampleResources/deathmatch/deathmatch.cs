using System;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

public class Deathmatch : Script
{
    private List<Vector3> spawns;
    private List<int> weapons;
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
        
        API.onPlayerConnected += OnPlayerConnected;
        API.onPlayerRespawn += OnPlayerRespawn;
        API.onResourceStart += onResourceStart;
        API.onMapChange += onMapChange;
    }

    private void onMapChange(string mapName, Map map)
    {     
        spawns.Clear();
        weapons.Clear();

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
    
    private void Respawn(Client player)
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
        Respawn(player);
    }    
}