var mainBrowser = null;
var lastUpdate = 0;

API.onResourceStart.connect(function() {
	// init browser
	var res = API.getScreenResolution();
	mainBrowser = API.createCefBrowser(500, 400);
	API.waitUntilCefBrowserInit(mainBrowser);
	API.setCefBrowserPosition(mainBrowser, res.Width - 505, res.Height - 405);
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
		API.getGlobalTime() - lastUpdate > 1000) { // update every 1 s
		lastUpdate = API.getGlobalTime();
		mainBrowser.call("updateLines", API.getBytesSentPerSecond(), API.getBytesReceivedPerSecond());		
	}
});