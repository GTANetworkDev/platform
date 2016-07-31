var currentChopper = null;


API.onUpdate.connect(function(s, e) {
	if (!API.isChatOpen() && currentChopper == null && API.isControlJustPressed(22)) {
		var cars = API.getAllVehicles();

		if (cars.Length == 0) return;

		var player = API.getLocalPlayer();
		var playerPos = API.getEntityPosition(player);

		var closestVeh = null;
		var closestDistance = 999999;

		for (var i = 0; i < cars.Length; i++) {
			var carPos = API.getEntityPosition(cars[i]);
			var dist = carPos.DistanceToSquared(playerPos);			
			if (dist < closestDistance) {
				closestVeh = cars[i];
				closestDistance = dist;
			}
		}

		if (closestDistance > 30) return;

		var relativePosition = API.returnNative(
			"GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS", 5,
			closestVeh.Value, playerPos.X, playerPos.Y, playerPos.Z
		);

		if (relativePosition.Z < -1 && relativePosition.Z > -3 && 
			relativePosition.Y < 2 && relativePosition.Y > -1 &&
			relativePosition.X > -3 && relativePosition.X < 3) {
			var rightSide = relativePosition.X > 0;
			API.triggerServerEvent("heligrab_requestGrab", closestVeh, rightSide);
		}
	}
	else if (!API.isChatOpen() && currentChopper != null && API.isControlJustPressed(22)) {
		API.triggerServerEvent("heligrab_stop");
		currentChopper = null;
	}
});

API.onServerEventTrigger.connect(function (evName, args) {
	if (evName == "heligrab_confirm") {
		currentChopper = args[0];
	}
});