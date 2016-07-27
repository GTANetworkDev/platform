var voteMenu = null;
var ghostMode = false;
var lastCollisionUpdate = 0;


voteMenu = API.createMenu("VOTE FOR NEXT MAP", 0, 0, 6);
voteMenu.ResetKey(menuControl.Back);


function disableControls() {
    API.callNative("DISABLE_CONTROL_ACTION", 0, 75, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 25, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 68, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 91, true);
}

voteMenu.OnItemSelect.connect(function(sender, item, index) {
    API.triggerServerEvent("race_castVote", index+1);
    API.sendNotification("You have voted for ~b~" + item.Text + "~w~.");
    voteMenu.Visible = false;    
});


API.onUpdate.connect(function(sender, args) {
    disableControls();
    API.drawMenu(voteMenu);    
    
    if (ghostMode && API.getGlobalTime() - lastCollisionUpdate > 5000 && API.isPlayerInAnyVehicle(API.getLocalPlayer()) ) {
        lastCollisionUpdate = API.getGlobalTime();
        var players = API.getAllPlayers();
        var playerCar = API.getPlayerVehicle(API.getLocalPlayer());

        for (var i = players.Length - 1; i >= 0; i--) {
            if (API.isPlayerInAnyVehicle(players[i])) {
                var car = API.getPlayerVehicle(players[i]);
                API.callNative("SET_ENTITY_NO_COLLISION_ENTITY", playerCar.Value, car.Value, false);
            }
        };
    }
});

API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === "startRaceCountdown") {
        var countdown = API.requestScaleform("countdown");

        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        countdown.CallFunction("FADE_MP", "3", 241, 247, 57);

        var start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }

        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        countdown.CallFunction("FADE_MP", "2", 241, 247, 57);
        
        start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }


        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        countdown.CallFunction("FADE_MP", "1", 241, 247, 57);
        
        start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }

        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        countdown.CallFunction("FADE_MP", "go", 49, 235, 126);

        start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }
    }

    if (eventName === "race_toggleGhostMode") {
        ghostMode = args[0];
    }

    if (eventName === "race_startVotemap") {
        var numOfMaps = args[0];
        voteMenu.Clear();

        for (var i = 0; i < numOfMaps; i++) {
            var mapName = args[i+1];
            var mapItem = API.createMenuItem(mapName, "");
            voteMenu.AddItem(mapItem);
        }

        voteMenu.Visible = true;
    }
});

