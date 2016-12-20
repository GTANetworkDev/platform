var mainBlip = null;
var secondBlip = null;
var nextCheckpointMarker = null;
var nextCheckpointDir = null;
var racePosition = null;
var voteMenu = null;
var respawnKeyStart = null;
var racePositionScaleform = null;
var ghostMode = false;
var lastCollisionUpdate = 0;


voteMenu = API.createMenu("VOTE FOR NEXT MAP", 0, 0, 6);
voteMenu.ResetKey(menuControl.Back);
racePositionScaleform = API.requestScaleform("race_position");
racePositionScaleform.CallFunction("SET_RACE_LABELS", "POSITION","","","CHECKPOINTS","RACE RESULTS");
racePositionScaleform.CallFunction("SET_RACE_POSITION", 4, 10);
racePositionScaleform.CallFunction("SHOW_RACE_MODULE", 1, false);
racePositionScaleform.CallFunction("SHOW_RACE_MODULE", 2, false);
racePositionScaleform.CallFunction("SHOW_RACE_MODULE", 4, false);
racePositionScaleform.CallFunction("SET_SAFE");
racePositionScaleform.CallFunction("SET_RATIO", 1);


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
})

API.onKeyDown.connect(function (sender, keyEventArgs) {
    if (keyEventArgs.KeyCode == Keys.F && respawnKeyStart == null) {
        respawnKeyStart = API.getGameTime();
    }
});

API.onKeyUp.connect(function (sender, keyEventArgs) {
    if (keyEventArgs.KeyCode == Keys.F) {
        respawnKeyStart = null;
    }
});


API.onUpdate.connect(function(sender, args) {
    disableControls();
    API.drawMenu(voteMenu);    
    
    if (racePosition != null) {        
        API.renderScaleform(racePositionScaleform, 80, 150, 1280, 720);
    }

    if (respawnKeyStart != null) {
        var res = API.getScreenResolutionMantainRatio();
        var elapsed = API.getGameTime() - respawnKeyStart;

        API.drawRectangle(0, res.Height - 10, res.Width * (elapsed / 3000.0), 10, 255, 0, 0, 100);

        if (elapsed > 3000) {
            API.triggerServerEvent("race_requestRespawn");
            respawnKeyStart = null;
        }
    }

    if (ghostMode && API.getGlobalTime() - lastCollisionUpdate > 5000 && API.isPlayerInAnyVehicle(API.getLocalPlayer()) ) {
        lastCollisionUpdate = API.getGlobalTime();
        var players = API.getAllPlayers();
        var playerCar = API.getPlayerVehicle(API.getLocalPlayer());

        for (var i = players.Length - 1; i >= 0; i--) {
            if (API.isPlayerInAnyVehicle(players[i])) {
                var car = API.getPlayerVehicle(players[i]);
                API.callNative("SET_ENTITY_NO_COLLISION_ENTITY", playerCar.Value, car.Value, false);
                API.setEntityTransparency(car, 150);
            }
        };
    }
});

API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === "startRaceCountdown") {
        var countdown = API.requestScaleform("countdown");
        
        API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
        countdown.CallFunction("FADE_MP", "3", 241, 247, 57);

        var start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }
        
        API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
        countdown.CallFunction("FADE_MP", "2", 241, 247, 57);
        
        start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }


        API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
        countdown.CallFunction("FADE_MP", "1", 241, 247, 57);
        
        start = API.getGlobalTime();
        while (API.getGlobalTime() - start < 1000) {
            disableControls();
            countdown.Render2D();
            API.sleep(0);
        }

        API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
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

    if (eventName === "updatePosition") {
        racePosition = "~h~Position~h~: " + args[0] + " / " + args[1] + "~n~~h~Checkpoints~h~: " + args[2] + " / " + args[3];

        racePositionScaleform.CallFunction("SET_RACE_POSITION", args[0], args[1]);
        racePositionScaleform.CallFunction("SET_GATES_POSITION", args[2], args[3]);
        racePositionScaleform.CallFunction("SET_RACE_LABELS", "POSITION","","","CHECKPOINTS","RACE RESULTS");
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

    if (eventName === "setNextCheckpoint") {
        var newPos = args[0];
        var isFinishLine = args[1];
        var newDir;
        var playSound = args[2];
        var secondNextBlip;

        if (!isFinishLine) {
            newDir = args[3];
            secondNextBlip = args[4];
        }

        if (playSound) {
            API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
        }

        if (mainBlip == null) {
            mainBlip = API.createBlip(newPos);
            API.setBlipColor(mainBlip, 66);
        } else {
            API.setBlipPosition(mainBlip, newPos);
        }

        if (nextCheckpointMarker == null) {
            nextCheckpointMarker = API.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        } else {
            API.deleteEntity(nextCheckpointMarker);
            nextCheckpointMarker = API.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        }
        
        if (!isFinishLine) {
            if (nextCheckpointDir == null) {
                nextCheckpointDir = API.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            } else {
                API.deleteEntity(nextCheckpointDir);
                nextCheckpointDir = API.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            }

            if (secondBlip == null) {
                secondBlip = API.createBlip(secondNextBlip);
                API.setBlipColor(secondBlip, 66);
                API.setBlipScale(secondBlip, 0.6);
            } else {
                API.setBlipPosition(secondBlip, secondNextBlip);
            }
        }

        if (isFinishLine && nextCheckpointDir != null) {
            API.deleteEntity(nextCheckpointDir);
            nextCheckpointDir = null;
        }

        if (isFinishLine && secondBlip != null) {
            API.deleteEntity(secondBlip);
            secondBlip = null;
        }
    }

    if (eventName === "finishRace") {
        API.playSoundFrontEnd("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_NORMAL");
        API.showShard("finished");

        if (mainBlip != null) {
            API.deleteEntity(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            API.deleteEntity(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            API.deleteEntity(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            API.deleteEntity(nextCheckpointDir);
            nextCheckpointDir = null;
        }
        racePosition = null;
    }

    if (eventName === "resetRace") {
        if (mainBlip != null) {
            API.deleteEntity(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            API.deleteEntity(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            API.deleteEntity(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            API.deleteEntity(nextCheckpointDir);
            nextCheckpointDir = null;
        }
        racePosition = null;
    }
});

