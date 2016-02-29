var mainBlip = null;
var secondBlip = null;
var nextCheckpointMarker = null;
var nextCheckpointDir = null;
var racePosition = null;

script.onUpdate.connect(function(sender, args) {
    if (racePosition != null) {
        //script.drawText(racePosition, 1900, 1000, 0.5, 255, 255, 255, 255, 0, 2, false, false, false);
    }
});

script.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === "startRaceCountdown") {
        script.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("3");
        API.GTA.Script.Wait(1000);
        script.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("2");
        API.GTA.Script.Wait(1000);
        script.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("1");
        API.GTA.Script.Wait(1000);
        script.callNative("REQUEST_SCRIPT_AUDIO_BANK", "HUD_MINI_GAME_SOUNDSET", true);
        script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("go", 1000);
    }

    if (eventName === "updatePosition") {
        racePosition = args[0] + " / " + args[1];
    }

    if (eventName === "setNextCheckpoint") {
        var newPos = new Vector3(args[0], args[1], args[2]);
        var isFinishLine = args[3];
        var newDir;
        var playSound = args[4];
        var secondNextBlip;


        if (!isFinishLine) {
            newDir = new Vector3(args[5], args[6], args[7]);
            secondNextBlip = new Vector3(args[8], args[9], args[10]);
        }

        if (playSound) {
            script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        }

        if (mainBlip == null) {
            mainBlip = script.createBlip(newPos);
        } else {
            script.setBlipPosition(mainBlip, newPos);
        }

        if (nextCheckpointMarker == null) {
            nextCheckpointMarker = script.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        } else {
            script.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = script.createMarker(1, newPos, new Vector3(), new Vector3(), new Vector3(10, 10, 2), 241, 247, 57, 180);
        }
        
        if (!isFinishLine) {
            if (nextCheckpointDir == null) {
                nextCheckpointDir = script.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            } else {
                script.deleteMarker(nextCheckpointDir);
                nextCheckpointDir = script.createMarker(20, new Vector3(newPos.X, newPos.Y, newPos.Z + 2), newDir, new Vector3(60, 0, 0), new Vector3(4, 4, 4), 87, 193, 250, 100);
            }

            if (secondBlip == null) {
                secondBlip = script.createBlip(secondNextBlip);
                script.setBlipScale(secondBlip, 0.6);
            } else {
                script.setBlipPosition(secondBlip, secondNextBlip);
            }
        }

        if (isFinishLine && nextCheckpointDir != null) {
            script.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }

        if (isFinishLine && secondBlip != null) {
            script.removeBlip(secondBlip);
            secondBlip = null;
        }
    }

    if (eventName === "finishRace") {
        script.callNative("PLAY_SOUND_FRONTEND", 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("finished");

        if (mainBlip != null) {
            script.removeBlip(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            script.removeBlip(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            script.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            script.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }
    }

    if (eventName === "resetRace") {
        if (mainBlip != null) {
            script.removeBlip(mainBlip);
            mainBlip = null;
        }

        if (secondBlip != null) {
            script.removeBlip(secondBlip);
            secondBlip = null;
        }

        if (nextCheckpointMarker != null) {
            script.deleteMarker(nextCheckpointMarker);
            nextCheckpointMarker = null;
        }

        if (nextCheckpointDir != null) {
            script.deleteMarker(nextCheckpointDir);
            nextCheckpointDir = null;
        }
    }
});

