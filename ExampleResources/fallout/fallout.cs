using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class Fallout : Script
{
    public Fallout()
    {
        API.onPlayerConnected += join;
        API.onUpdate += update;
        API.onPlayerRespawn += respawn;
        API.onPlayerDeath += killed;
        API.onResourceStart += start;
    }

    public void start()
    {
        createFallingPanels();
    }

    public void join(Client player)
    {
        if (roundStarted)
        {
            API.setPlayerToSpectator(player);
        }
    }

    public void killed(Client player, NetHandle killer, int reason)
    {        
        Survivors.Remove(player);
    }

    public void respawn(Client player)
    {
        if (!roundStarted) return;
        Survivors.Remove(player);

        if (Survivors.Count == 1)
        {
            var winner = Survivors[0];
            API.sendNotificationToAll("~b~~h~" + winner.name + "~h~ ~w~has won! Restarting round in 15 seconds...");
            foreach (var c in API.getAllPlayers())
            {
                API.unspectatePlayer(c);
            }
            roundStarted = false;

            API.sleep(15000);
            createFallingPanels();
        }
        else if (Survivors.Count == 0) {
            API.sendNotificationToAll("No winners! Restarting round in 15 seconds...");
            foreach (var c in API.getAllPlayers())
            {
                API.unspectatePlayer(c);
            }

            roundStarted = false;
            API.sleep(15000);
            createFallingPanels();
        }
        else
        {
            API.setPlayerToSpectator(player);
        }
    }

    [Command("panel")]
    public void restartroundcmd(Client sender)
    {
        createFallingPanels();
    }


    private Random r = new Random();
    public void update()
    {
        if (roundStarted) 
        {
            roundStart = roundStart + 1;
            if (roundStart > 300 && objects.Count > 0) {
                var rand = objects[r.Next(objects.Count)];
                API.setEntityTransparency(rand, 100);
                API.sleep(2000);
                API.deleteEntity(rand);
                objects.Remove(rand);
                roundStart = 0;
            }

            for (int i = Survivors.Count - 1; i >= 0; i--)
            {
                if (API.getEntityPosition(Survivors[i].handle).Z < 327f)
                {
                    API.setPlayerHealth(Survivors[i], -1);
                    Survivors.Remove(Survivors[i]);
                }
            }
        }
    }

// someone rewrite me
    private int roundStart;
    private bool roundStarted;
    private List<NetHandle> objects = new List<NetHandle>();
    private List<Client> Survivors = new List<Client>();

    private Vector3 firstPanel = new Vector3(-66.4266739, -764.013062, 337.5375);
    private float distance = 2.6466739f;


    public void createFallingPanels()
    {
        roundStart = 0;
           
        for (var i = 0; i < objects.Count; i++) {
            API.deleteEntity(objects[i]);
        }

        objects.Clear();
        
        var rows = 10;
        var cols = 10;
        for (var i = 0; i < rows * cols; i++) {
            var currentColumn = i % cols;
            var currentRow = i / cols;
            objects.Add(API.createObject(1022953480, new Vector3(-66.4266739 - (distance * currentColumn), -764.013062 - (distance * currentRow), 337.5375), new Vector3(90, 0, 0)));
        }   
        
           
        var players = API.getAllPlayers();
        Survivors = new List<Client>(players);
        for (var i = 0; i < players.Count; i++) {
            Survivors.Add(players[i]);
            API.setEntityPosition(players[i].handle, new Vector3(-77.02, -780.12, 344.64));
        }
        
        API.sleep(3000);
        roundStarted = true;
    }

}