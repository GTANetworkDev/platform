API.onPlayerDeath.connect(function (player, reason, weapon) {
    var killer = "";
    API.consoleOutput("killed: " + player.Name + " reason: " + reason.Value + " weapon: " + weapon);
    if (reason.IsNull)
    {
    }
    else
    {
        var players = API.getAllPlayers();
        for (var i = 0; i < players.Count; i++) {
            if (players[i].CharacterHandle == reason.Value) {
                killer = players[i].Name;
                break;
            }            
        }        
    }
    
    var players = API.getAllPlayers();
    for (var i = 0; i < players.Count; i++) {
        API.triggerClientEvent(players[i], "addKillToKillchat", player.Name, killer, weapon);
    }    
});
