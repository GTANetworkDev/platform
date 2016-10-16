var mainBrowser = null;
var selectingPos = false;

var topLeft = new Vector3(-1419.58, -258.72, 22.91707);
var topRight = new Vector3(-1433.43, -258.70, 22.91268);
var bottomRight = new Vector3(-1433.43, -258.70, 17.59536);
var bottomLeft = new Vector3(-1419.58, -258.72, 17.56603);

API.onChatCommand.connect(function(msg) {
	if (msg.indexOf("/goto") == 0) {
		var page = msg.substring(6);

		API.sendNotification("Page: " + page);

		if (mainBrowser == null) {
			API.sendNotification("Creating new browser...");
			var res = API.getScreenResolution();
			mainBrowser = API.createCefBrowser(515, 315, false);
			API.waitUntilCefBrowserInit(mainBrowser);
			API.sendNotification("Browser created!");
		}

		API.loadPageCefBrowser(mainBrowser, page);
	}

	if (msg == "/stopbrowser") {
		if (mainBrowser != null) {
			API.destroyCefBrowser(mainBrowser);
			mainBrowser = null;
		}
	}

	if (msg == "/mouseon") {
		API.showCursor(true);
	}

	if (msg == "/mouseoff") {
		API.showCursor(false);
	}
});

API.onKeyDown.connect(function(sender, args) {
	if (args.KeyCode == Keys.F12) {
		selectingPos = !selectingPos;
		API.sendNotification("Setpos: " + selectingPos);
	}
});

API.onResourceStop.connect(function(e, ev) {
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});

API.onUpdate.connect(function() {
	if (selectingPos) {
		var cursOp = API.getCursorPositionMantainRatio();
		var s2w = API.screenToWorldMantainRatio(cursOp);
		API.displaySubtitle(API.toString(s2w));
	}

	if (mainBrowser != null) {
		var tl = API.worldToScreen(topLeft);
		var tr = API.worldToScreen(topRight);
		var br = API.worldToScreen(bottomRight);
		var bl = API.worldToScreen(bottomLeft);

		API.pinCefBrowser(mainBrowser, tl.X, tl.Y, tr.X, tr.Y, br.X, br.Y, bl.X, bl.Y);

		/*
		API.displaySubtitle(
			API.toString(tl) + "\n" +
			API.toString(tr) + "\n" +
			API.toString(br) + "\n" + 
			API.toString(bl) + "\n");
		*/
	}
});