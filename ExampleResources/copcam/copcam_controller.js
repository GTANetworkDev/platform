var inCamera = false;
var mainCamera = null;
var currentChopper = null;

API.onKeyDown.connect(function(sender, key) {
	if (key.KeyCode == Keys.E && mainCamera != null && API.getActiveCamera() == mainCamera) {
		API.triggerServerEvent("switch_searchlight", currentChopper);
	}
});

API.onUpdate.connect(function() {
	if (mainCamera != null && API.getActiveCamera() == mainCamera) {
		API.disableAllControlsThisFrame();

		var x = API.returnNative("GET_DISABLED_CONTROL_NORMAL", 7, 0, 1);
		var y = API.returnNative("GET_DISABLED_CONTROL_NORMAL", 7, 0, 2);

		var currentRot = API.getCameraRotation(mainCamera);

		currentRot = new Vector3(currentRot.X + y, 0, currentRot.Z + x);

		API.setCameraRotation(mainCamera, currentRot);

		var aimP = API.getPlayerAimingPoint();

		API.callNative("TASK_VEHICLE_AIM_AT_COORD", currentChopper, aimP.X, aimP.Y, aimP.Z);
	}

	
});

API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "start_police_cam") {
		currentChopper = args[0];
		var playa = API.getLocalPlayer();
		mainCamera = API.createCamera(API.getEntityPosition(playa), new Vector3(-90, 0, 0));
		API.attachCameraToEntity(mainCamera, currentChopper, new Vector3(0, 5, -2));

		API.setActiveCamera(mainCamera);

		API.forceSendAimData(true);
	}
});
