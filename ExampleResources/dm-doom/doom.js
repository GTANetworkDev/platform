var restartAudio = true;

API.onUpdate.connect(function(sender, e) {
	if (restartAudio && !API.isAudioPlaying()) {
		API.startAudio("e1m1.mp3");
	}
});

API.onChatCommand.connect(function (msg) {
   if (msg == "/imapussy") {
   		restartAudio = false;
       API.stopAudio();
   }   
});

API.onResourceStop.connect(function(s,e) {
	API.stopAudio();
});

API.onResourceStart.connect(function(s,e) {
	API.stopAudio();
	API.startAudio("e1m1.mp3");
});