
var lastPositions = [];

API.onChatCommand.connect(function (sender, command) {
    if (command.substring(0, 4) == "/car") {
        if (command.length > 4)
        {
            var vehName = command.substring(5);
            var vehModel = API.vehicleNameToModel(vehName);
            if (vehModel == 0) {
                API.sendNotificationToPlayer(sender, "No such model found!");
            } else {
                var veh = API.createVehicle(vehModel, sender.Position, new Vector3(), 0, 0);
                API.setPlayerIntoVehicle(sender, veh, -1);
            }
        } else {
            API.sendNotificationToPlayer(sender, "USAGE: /car [model name]");
        }
   }
   
   if (command == "/countdown") {
       var players = API.getAllPlayers();
        for (var i = 0; i < players.Count; i++) {
            API.triggerClientEvent(players[i], "startCountdown");
        }
   }
   
   if (command == "/tp") {
       API.setEntityPosition(sender.CharacterHandle, new Vector3(0, 0, 100));
   }
});