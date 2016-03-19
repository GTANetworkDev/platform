var mainBlip = null;


API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName === "clearAllBlips") {
		if (mainBlip != null) {
			API.removeBlip(mainBlip);
			mainBlip = null;
		}
	}

	if (eventName === "createLocalBlip") {
		if (mainBlip != null) {
			API.removeBlip(mainBlip);
			mainBlip = null;
		}

		mainBlip = API.createBlip(args[0]);
	}
});