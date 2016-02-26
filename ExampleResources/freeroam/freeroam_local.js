
var drawSkeletor = false;

script.onUpdate.connect(function (sender, args) {    
    if (drawSkeletor)
    {
        var pont = new Point(0, 1080 - 295);
        var siz = new Size(500, 295);
        script.dxDrawTexture(script.getResourceFilePath("freeroam", "skeletor.png"), pont, siz);        
    }
});

script.onChatCommand.connect(function (msg) {
   if (msg == "/spooky") {
       if (drawSkeletor) {
           drawSkeletor = false;
       } else {
           drawSkeletor = true;
       }
   }   
});

script.onServerEventTrigger.connect(function (evName, args) {
    if (evName == "startCountdown") {                
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
        API.NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("go!", 2000);       
    }
});