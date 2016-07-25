API.onUpdate.connect(function (l,r) {
	API.callNative("DISABLE_CONTROL_ACTION", 0, 75, true);
	API.callNative("DISABLE_CONTROL_ACTION", 0, 24, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 25, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 68, true);
    API.callNative("DISABLE_CONTROL_ACTION", 0, 91, true);
});