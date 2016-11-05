API.onServerEventTrigger.connect(function (name, args) {
    if (name == "createCamera") {
        var pos = args[0];
        var target = args[1];

        var newCam = API.createCamera(pos, new Vector3());
        API.pointCameraAtPosition(newCam, target);
        API.setActiveCamera(newCam);
    }
});