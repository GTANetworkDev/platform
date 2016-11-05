var currentSpherePos = null;
var currentSphereScale = null;
var marker = null;

var destinationPos = null;
var destinationScale = null;

var interpolatingPos = false;
var interpolation = 0.0;
var interpolationStart = 0;
var interpolationEnd = 0;

var isInsideSphere = true;
var lastSphereLeave = 0;

var roundEnd = false;

//"Explosion_Countdown", NETWORK::NET_TO_VEH(l_181/*2*/]._f1), "GTAO_FM_Events_Soundset", 0, 

API.onServerEventTrigger.connect(function (eventName, args) {
	if (eventName == "pennedin_roundend") {
		if (marker != null)	{
			API.deleteEntity(marker);
		}

		roundEnd = true;
	}

	if (eventName == "pennedin_roundstart") {

		currentSpherePos = args[0];
		currentSphereScale = args[1];

		roundEnd = false;

		destinationPos = null;
		destinationScale = null;

		interpolation = 0;
		interpolationStart = 0;
		interpolationEnd = 0;
		interpolatingPos = false;

		lastSphereLeave = 0;
		isInsideSphere = true;


		if (marker != null)	{
			API.deleteEntity(marker);
		}

		marker = API.createMarker(28, currentSpherePos,
			new Vector3(), new Vector3(),
			new Vector3(currentSphereScale, currentSphereScale, currentSphereScale),
			255, 0, 0, 100);
	}

	if (eventName == "pennedin_setposdestination") {
		if (marker == null) return;
		if (destinationScale != null)
		{
			currentSphereScale = destinationScale;
			destinationScale = null;
		}

		currentSpherePos = args[0];
		destinationPos = args[1];

		API.setEntityPosition(marker, currentSpherePos);

		interpolatingPos = true;
		interpolation = 0;
		interpolationStart = API.getGlobalTime();
		interpolationEnd = args[2];
	}

	if (eventName == "pennedin_setscaledestination") {		
		if (marker == null) return;
		if (destinationPos != null)
		{
			currentSpherePos = destinationPos;
			destinationPos = null;
		}

		currentSphereScale = args[0];
		destinationScale = args[1];

		API.setMarkerScale(marker, new Vector3(currentSphereScale, currentSphereScale, currentSphereScale));

		interpolatingPos = false;
		interpolation = 0;
		interpolationStart = API.getGlobalTime();
		interpolationEnd = args[2];
	}

});


API.onUpdate.connect(function(sender, e) {
	API.callNative("DISABLE_CONTROL_ACTION", 0, 75, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 25, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 68, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 91, true);

    if (roundEnd) return;

    if (interpolationStart != 0 && marker != null) {
    	var cTime = API.getGlobalTime() - interpolationStart;
    	var dur = interpolationEnd;

    	var newScale = currentSphereScale;
    	var newPos = currentSpherePos;

    	if (interpolatingPos) {
    		newPos = API.lerpVector(currentSpherePos, destinationPos, cTime, dur);
    		API.setEntityPosition(marker, newPos);
    	} else {
    		newScale = API.lerpFloat(currentSphereScale, destinationScale, cTime, dur);
    		API.setMarkerScale(marker, new Vector3(newScale, newScale, newScale));
    	}

    	var player = API.getLocalPlayer();
    	var playerPos = API.getEntityPosition(player);
    	var isInRange = API.isInRangeOf(playerPos, newPos, newScale);

    	if (!isInRange && isInsideSphere) {
    		lastSphereLeave = API.getGlobalTime();
    	} else if (isInRange && !isInsideSphere) {
    		API.displaySubtitle("");
    	}

    	isInsideSphere = isInRange;

    	if (!isInRange) {
    		var timeLeft = 10000 - (API.getGlobalTime() - lastSphereLeave);
	    	API.displaySubtitle("~r~Return to the sphere or explode in ~w~" + API.formatTime(timeLeft, "ss\\.fff"));

	    	if (API.getGlobalTime() - lastSphereLeave > 10000) {
	    		API.explodeVehicle(API.getPlayerVehicle(player));
	    		API.setPlayerHealth(player, -1);
	    		API.deleteEntity(marker);
	    		marker = null;
	    		roundEnd = true;
	    	}
	    }
    }

});

API.onResourceStop.connect(function(sender, e) {
	if (marker != null)	{
		API.deleteEntity(marker);
	}
});