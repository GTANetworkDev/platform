var mainBrowser = null;
var lastUpdate = 0;

API.onResourceStart.connect(function() {
	// init browser
	var res = API.getScreenResolution();
	//mainBrowser = API.createCefBrowser(500, 400);
	mainBrowser = API.createCefBrowser(1920, 1080);
	API.waitUntilCefBrowserInitalization(mainBrowser);
	//API.setCefBrowserPosition(mainBrowser, res.Width - 500, res.Height - 400);
	API.setCefBrowserPosition(mainBrowser, 0, 0);
	API.setCefBrowserHeadless(mainBrowser, true);
	API.loadPageCefBrowser(mainBrowser, "main.html");
});

API.onResourceStop.connect(function() {
	// destroy browser
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});

API.onKeyDown.connect(function(sender, args) {
	if (args.KeyCode == Keys.F12 && mainBrowser != null) {
		API.setCefBrowserHeadless(mainBrowser, !API.getCefBrowserHeadless(mainBrowser));		
	}
});

API.onUpdate.connect(function(eventName, args) {
	if (mainBrowser != null &&
		!API.getCefBrowserHeadless(mainBrowser) &&
		API.getGlobalTime() - lastUpdate > 100) { // update every 100 ms
		lastUpdate = API.getGlobalTime();
		mainBrowser.call("updateLines", API.getBytesSentPerSecond(), API.getBytesReceivedPerSecond());		
	}
});