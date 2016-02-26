function readMap(xmlText) {
    var xmlDoc = new clr.System.Xml.XmlDocument();
    xmlDoc.Load(xmlText);
    var mainDict = xmlParser.Parse(xmlDoc);
    var name = mainDict["Name"]["Value"];
    var desc = mainDict["Description"]["Value"];
    var lapsAvailable = mainDict["LapsAvailable"]["Value"];
    
    var availableCars = new Array();
    
    for (var i = 0; i < mainDict["AvailableVehicles"]["Children"].Count; i++) {
        var nodeName;
        if (i == 0)
            nodeName = "VehicleHash";
        else
            nodeName = "VehicleHash" + i;
        
        var hash = mainDict["AvailableVehicles"]["Children"][nodeName]["Value"];        
        availableCars.push(hash);
    }
    
    var spawnPoints = new Array();
    
    for (var i = 0; i < mainDict["SpawnPoints"]["Children"].Count; i++) {
        var nodeName;
        if (i == 0)
            nodeName = "SpawnPoint";
        else
            nodeName = "SpawnPoint" + i;
        
        var heading = Number(mainDict["SpawnPoints"]["Children"][nodeName]["Children"]["Heading"]["Value"]);
        var xPos = Number(mainDict["SpawnPoints"]["Children"][nodeName]["Children"]["Position"]["Children"]["X"]);
        var yPos = Number(mainDict["SpawnPoints"]["Children"][nodeName]["Children"]["Position"]["Children"]["Y"]);
        var zPos = Number(mainDict["SpawnPoints"]["Children"][nodeName]["Children"]["Position"]["Children"]["Z"]);
        
        spawnPoints.push({head:heading, x:xPos, y:yPos, z:zPos});
    }
    
    var checkpoints = new Array();
    
    for (var i = 0; i < mainDict["Checkpoints"]["Children"].Count; i++) {
        var nodeName;
        if (i == 0)
            nodeName = "Vector3";
        else
            nodeName = "Vector3" + i;
        
        var xPos = Number(mainDict["Checkpoints"]["Children"][nodeName]["X"]);
        var yPos = Number(mainDict["Checkpoints"]["Children"][nodeName]["Y"]);
        var zPos = Number(mainDict["Checkpoints"]["Children"][nodeName]["Z"]);
        
        checkpoints.push({x:xPos, y:yPos, z:zPos});
    }
    
    return {RaceName:name, RaceDescription:desc, AvailableVehicles:availableCars, RaceSpawnPoints:spawnPoints, RaceCheckPoints:checkpoints};
}


API.onPlayerConnected.connect(function (player) {
    API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~joined.");
});

API.onPlayerDisconnected.connect(function (player) {
    API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~quit.");
});

API.onPlayerDeath.connect(function (player) {
   API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~died.");
   console.WriteLine(player.DisplayName + " has died");
});

var vehicles = new List(Int32);
var currentMap;
var raceGoingOn = false;
var playerCheckpoints;

API.onClientEventTrigger.connect(function (sender,  eventName, args) {
    
});

API.onChatCommand.connect(function (sender, command) {
    if (command.substring(0, 5) == "/race") {
        var trackName = command.substring(6);
        var xmlFile = clr.System.IO.File.ReadAllText(trackName);
        var map = readMap(xmlFile);
        raceGoingOn = true;
        currentMap = map;
        playerCheckpoints = {};
        
        for (int i = 0; i < vehicles.Count; i++)
        {
            API.deleteEntity(vehicles[i]);
        }
        
        vehicles = new List(Int32);
        
        var players = API.getAllPlayers();
        
        for (int i = 0; i < players.Count; i++) {
            var pos = new API.Vector3(map.RaceSpawnPoints[i].x, map.RaceSpawnPoints[i].y, map.RaceSpawnPoints[i].z);
            var rot = new API.Vector3(0, 0, map.RaceSpawnPoints[i].head);
            var playerCar = API.createVehicle(vehicleNameToModel(map.AvailableVehicles[0]), pos, rot, 0, 0);
            playerCheckpoints[players[i]] = 0;
            API.setPlayerIntoVehicle(players[i], playerCar, -1);
            vehicles.push(playerCar);
            API.triggerClientEvent(players[i], "race_prepare");
            var nextChkpt = new API.Vector3(map.RaceCheckPoints[0].x, map.RaceCheckPoints[0].y, map.RaceCheckPoints[0].z);
            API.triggerClientEvent(players[i], "setNextCheckpoint", nextChkpt);
        }
        
        
    }
});