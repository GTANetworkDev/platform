
var drawSkeletor = false;

API.onUpdate.connect(function (sender, args) {    
    if (drawSkeletor)
    {
        var pont = new Point(0, 1080 - 295);
        var siz = new Size(500, 295);
        API.dxDrawTexture("skeletor.png", pont, siz);        
    }
});

API.onChatCommand.connect(function (msg) {
   if (msg == "/spooky") {
       if (drawSkeletor) {
           drawSkeletor = false;
       } else {
           drawSkeletor = true;
       }
   }   
});

API.onServerEventTrigger.connect(function (evName, args) {
    if (evName == "startCountdown") {                
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
});