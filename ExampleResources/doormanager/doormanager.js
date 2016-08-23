var selectingDoor = false;
var lastDoor = null;
var lastDoorV = 0;

API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "doormanager_debug") {
		selectingDoor = true;
		API.showCursor(true);
	}
	else if (eventName == "doormanager_finddoor") {
		var doorHash = args[0];
		var pos = args[1];
		var handle = API.returnNative("GET_CLOSEST_OBJECT_OF_TYPE", 9, pos.X, pos.Y, pos.Z, 10.5, doorHash, false, true, true);

		if (!handle.IsNull) {
			API.sendChatMessage("Found model at " + API.getEntityPosition(handle).ToString());
			var mark = API.createMarker(28, API.getEntityPosition(handle),
				new Vector3(), new Vector3(), new Vector3(0.1, 0.1, 0.1),
				255, 255, 255, 100);
			API.sleep(3000);
			API.deleteEntity(mark);
		}
	}
	else if (eventName == "doormanager_finddoor_return") {
		var doorHash = args[0];
		var pos = args[1];
		var handle = API.returnNative("GET_CLOSEST_OBJECT_OF_TYPE", 9, pos.X, pos.Y, pos.Z, 10.5, doorHash, false, true, true);

		if (!handle.IsNull) {
			API.triggerServerEvent("doormanager_debug_createdoor",
				API.getEntityModel(handle), API.getEntityPosition(handle));
		}
	}
});

API.onUpdate.connect(function() {
	if (selectingDoor) {
		var cursOp = API.getCursorPositionMantainRatio();
		var s2w = API.screenToWorldMantainRatio(cursOp);
		var rayCast = API.createRaycast(API.getGameplayCamPos(), s2w, -1, null);
		var localH = null;
		var localV = 0;
		if (rayCast.didHitEntity) {
			localH = rayCast.hitEntity;					
			localV = localH.Value;
		}

		API.displaySubtitle("Object Handle: " + localV);

		if (localV != lastDoorV) {
			if (localH != null) API.setEntityTransparency(localH, 50);
			if (lastDoor != null) API.setEntityTransparency(lastDoor, 255);
			lastDoor = localH;
			lastDoorV = localV;
		}		

		if (API.isDisabledControlJustPressed(24)) {
			API.showCursor(false);
			selectingDoor = false;

			if (localH != null) {
				API.sendChatMessage("Object model is " + API.getEntityModel(localH));
				API.triggerServerEvent("doormanager_debug_createdoor",
					API.getEntityModel(localH), API.getEntityPosition(localH));
			}
		}
	}
	else if (lastDoor != null)
	{
		API.setEntityTransparency(lastDoor, 255);
		lastDoor = null;
	}
});