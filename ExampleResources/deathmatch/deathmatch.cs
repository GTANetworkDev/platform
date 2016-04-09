using System;
using System.Collections.Generic;
using System.ComponentModel;
using GTANetworkServer;
using GTANetworkShared;

public class Deathmatch : Script
{
    private List<Vector3> spawns;
    private Random rInst;
    
    public Deathmatch()
    {
        spawns = new List<Vector3>();
        spawns.Add(new Vector3(1482.36, 3587.45, 35.39));
        spawns.Add(new Vector3(1613.67, 3560.03, 35.42));
        spawns.Add(new Vector3(1533.44, 3581.24, 38.73));
        spawns.Add(new Vector3(1576.09, 3607.35, 38.73));
        spawns.Add(new Vector3(1596.88, 3590.43, 42.12));
        
        rInst = new Random();
        
        API.onPlayerConnected += OnPlayerConnected;
        API.onPlayerRespawn += OnPlayerRespawn;
        API.onResourceStart += onResourceStart;
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
        API.givePlayerWeapon(player, 324215364, 500, false, true);
        API.givePlayerWeapon(player, 487013001, 500, false, true);
        API.givePlayerWeapon(player, -2084633992, 500, false, true);
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