var mainBrowser = null;
var lastInVehicle = false;

API.onResourceStart.connect(function() {
	var res = API.getScreenResolution();
	mainBrowser = API.createCefBrowser(500, 500);
	API.setCefBrowserPosition(mainBrowser, res.Width - 500, res.Height - 500);
	API.setCefBrowserHeadless(mainBrowser, true);
	API.waitUntilCefBrowserInitalization(mainBrowser);
	API.loadPageCefBrowser(mainBrowser, "main.html");
});

API.onResourceStop.connect(function() {
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});

API.onUpdate.connect(function() {
	// TODO: move into a vehicle enter/exit event
	var player = API.getLocalPlayer();
	var inVeh = API.isPlayerInAnyVehicle(player);

	if (inVeh) {
		var car = API.getPlayerVehicle(player);
		var health = API.getVehicleHealth(car);
		var velocity = API.getEntityVelocity(car);
		var speed = Math.sqrt(
			velocity.X * velocity.X +
			velocity.Y * velocity.Y +
			velocity.Z * velocity.Z
			);

		mainBrowser.call("updateSpeed", speed * 3.6); // from m/s to km/h
		mainBrowser.call("updateHealth", health);
	}

	if (inVeh && !lastInVehicle) {
		API.setCefBrowserHeadless(mainBrowser, false);
	}
	if (!inVeh && lastInVehicle) {
		API.setCefBrowserHeadless(mainBrowser, true);
	}
	
	lastInVehicle = inVeh;
});