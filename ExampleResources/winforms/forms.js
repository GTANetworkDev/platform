var mainBrowser = null;
var lastInVehicle = false;

API.onResourceStart.connect(function() {
	var res = API.getScreenResolution();
	mainBrowser = API.createCefBrowser(res.Width, res.Height);
	API.setCefBrowserPosition(mainBrowser, 0, 0);
	API.setCefBrowserHeadless(mainBrowser, true);
	API.waitUntilCefBrowserInit(mainBrowser);
	API.loadPageCefBrowser(mainBrowser, "index.html");
});

API.onResourceStop.connect(function() {
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});

API.onKeyDown.connect(function(sender, arg) {
	if (arg.KeyCode == Keys.F5) {
		API.setCefBrowserHeadless(mainBrowser, !API.getCefBrowserHeadless(mainBrowser));
		API.showCursor(!API.getCefBrowserHeadless(mainBrowser));
	}
});