var ourBlip = null;

API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "SET_PLAYER_BLIP") {
		ourBlip = args[0];
	}
});

API.onUpdate.connect(function() {
	if (ourBlip != null) {
		API.setBlipTransparency(ourBlip, 0);
	}
});
