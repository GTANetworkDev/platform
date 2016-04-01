
var lastPositions = [];

API.onPlayerRespawn.connect(function (sender) {
    API.consoleOutput(sender.Name + " has respawned.");
});


API.onChatCommand.connect(function (sender, command) {
    if (command.substring(0, 5) == "/spec")   {
      if (command.length > 5) {
        var target = null;
        var targetName = command.substring(6);
        var players = API.getAllPlayers();        
        for (var i = 0; i < players.Count; i++) {          
          if (players[i].Name == targetName) {
            target = players[i];
            break;
          }
        }

        if (target != null) {
          API.setPlayerToSpectatePlayer(sender, target);
        } else {
          API.sendNotificationToPlayer(sender, "No such player found.");
        }
      } else {
        API.sendNotificationToPlayer(sender, "USAGE: /spec [player name]");
      }
    }

    if (command === "/unspec") {
      API.unspectatePlayer(sender);
    }

    if (command.substring(0, 4) == "/car") {
        if (command.length > 4) {
            var vehName = command.substring(5);
            var vehModel = API.vehicleNameToModel(vehName);
            if (vehModel == 0) {
                API.sendNotificationToPlayer(sender, "No such model found!");
            } else {
                var veh = API.createVehicle(vehModel, sender.Position, new Vector3(), 0, 0);
                var c = 0;
                while (!API.doesEntityExistForPlayer(sender, veh) && c < 40){
                  c++;
                }
                API.setPlayerIntoVehicle(sender, veh, -1);
            }
        } else {
            API.sendNotificationToPlayer(sender, "USAGE: /car [model name]");
        }
   }

   if (command.substring(0, 4) == "/pic") {
        if (command.length > 4)
        {
            var vehName = command.substring(5);
            var vehModel = API.pickupNameToModel(vehName);
            if (vehModel == 0) {
                API.sendNotificationToPlayer(sender, "No such model found!");
            } else {
                var veh = API.createPickup(vehModel, new Vector3(sender.Position.X + 10, sender.Position.Y, sender.Position.Z), new Vector3(), 10);                
            }
        } else {
            API.sendNotificationToPlayer(sender, "USAGE: /pic [model name]");
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