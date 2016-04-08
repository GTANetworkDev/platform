// someone rewrite me
var roundStart;
var objects;
var playersLeft;
var roundStarted = false;

var firstPanel = new Vector3(-66.4266739, -764.013062, 337.5375);
var distance = 2.6466739;


API.onPlayerRespawn.connect(function (player) {
    if (playersLeft == null || !roundStarted) return;
    playersLeft.Remove(player);
    if (playersLeft.Count == 1) {
        API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~has won!.");
        createFallingPanels();
    }
    else if (playersLeft.Count == 0) {
        API.sendNotificationToAll("No winners!.");
        createFallingPanels();
    }
});

API.onChatCommand.connect(function (sender, command) {
    if (command == "/panel") {
        createFallingPanels();
    }
});

API.onUpdate.connect(function (e, args) {
    if (roundStarted) {
        roundStart = roundStart + 1;
        if (roundStart > 300 && objects != null && objects.Count > 0) {
            var rand = objects[Math.floor(Math.random() * objects.Count)];
            API.deleteEntity(rand);
            objects.Remove(rand);
            roundStart = 0;
        }
    }
});

function createFallingPanels() {
    roundStart = 0;
    console.WriteLine("Starting falling panels");
       
    if (objects != null) {
        for (var i = 0; i < objects.Count; i++) {
            API.deleteEntity(objects[i]);
        }
    }
    
    objects = new List(Int32);
    
    var rows = 10;
    var cols = 10;
    for (var i = 0; i < rows * cols; i++) {
        var currentColumn = i % cols;
        var currentRow = i / cols;
        objects.Add(API.createObject(-384237829, new Vector3(-66.4266739 - (distance * currentColumn), -764.013062 - (distance * currentRow), 337.5375), new Vector3(90, 0, 0)));
    }   
    
       
    var players = API.getAllPlayers();
    playersLeft = new List(Client);    
    for (var i = 0; i < players.Count; i++) {
        playersLeft.Add(players[i]);
        console.WriteLine("player: " + players[i].Name);
        API.setEntityPosition(players[i].CharacterHandle, new Vector3(-77.02, -780.12, 344.64));
    }
    
    roundStarted = true;
}