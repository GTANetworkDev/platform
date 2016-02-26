API.onPlayerDeath.connect(function (player, reason, weapon) {
    var killer = "";
    
    if (reason == 0)
    {        
    }
    else
    {
        var players = API.getAllPlayers();
        for (var i = 0; i < players.Count; i++) {
            if (players[i].CharacterHandle == reason) {
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
