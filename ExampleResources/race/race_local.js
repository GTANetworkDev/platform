var mainBlip = null;
var secondBlip = null;
var nextCheckpointMarker = null;
var nextCheckpointDir = null;
var racePosition = null;
var voteMenu = null;

voteMenu = API.createMenu("VOTE FOR NEXT MAP", 0, 0, 6);
voteMenu.ResetKey(menuControl.Back);

voteMenu.OnItemSelect.connect(function(sender, item, index) {
    API.triggerServerEvent("race_castVote", index+1);
    API.sendNotification("You have voted for ~b~" + item.Text + "~w~.");
    voteMenu.Visible = false;    
})


API.onUpdate.connect(function(sender, args) {
    API.callNative("DISABLE_CONTROL_ACTION", 0, 75, true);
    API.drawMenu(voteMenu);
});

API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === "startRaceCountdown") {
        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.showShard("3");
        API.wait(1000);
        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.showShard("2");
        API.wait(1000);
        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.showShard("1");
        API.wait(1000);
        API.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.showShard("go!", 2000);
    }

    if (eventName === "updatePosition") {
        racePosition = args[0] + " / " + args[1];
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
            API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        }

        if (mainBlip == null) {
            mainBlip = API.createBlip(newPos);
        } else {
            API.setBlipPosition(mainBlip, newPos);
        }

        if (nextCheckpointMarker == null) {
            nextCheckpointMarker = API.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        } else {
            API.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = API.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        }
        
        if (!isFinishLine) {
            if (nextCheckpointDir == null) {
                nextCheckpointDir = API.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            } else {
                API.deleteMarker(nextCheckpointDir);
                nextCheckpointDir = API.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            }

            if (secondBlip == null) {
                secondBlip = API.createBlip(secondNextBlip);
                API.setBlipScale(secondBlip, 0.6);
            } else {
                API.setBlipPosition(secondBlip, secondNextBlip);
            }
        }

        if (isFinishLine && nextCheckpointDir != null) {
            API.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }

        if (isFinishLine && secondBlip != null) {
            API.removeBlip(secondBlip);
            secondBlip = null;
        }
    }

    if (eventName === "finishRace") {
        API.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.showShard("finished");

        if (mainBlip != null) {
            API.removeBlip(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            API.removeBlip(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            API.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            API.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }
    }

    if (eventName === "resetRace") {
        if (mainBlip != null) {
            API.removeBlip(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            API.removeBlip(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            API.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            API.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }
    }
});

