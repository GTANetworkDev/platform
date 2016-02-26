/*var mainBlip;
var nextCheckpoint;

script.onUpdate.connect(function (e, arg) {
    if (nextCheckpoint != null) {
        API.GTA.World.DrawMarker(MarkerType.VerticalCylinder, )
    }
});
*/

script.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName == "startCountdown") {                
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
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("go");
    }    
});

