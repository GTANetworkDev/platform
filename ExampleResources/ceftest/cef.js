var mainBrowser = null;

API.onChatCommand.connect(function(msg) {
	if (msg.indexOf("/goto") == 0) {
		var page = msg.substring(6);

		API.sendNotification("Page: " + page);

		if (mainBrowser == null) {
			API.sendNotification("Creating new browser...");
			var res = API.getScreenResolution();
			mainBrowser = API.createCefBrowser(500, 282);
			API.waitUntilCefBrowserInitalization(mainBrowser);
			API.setCefBrowserPosition(mainBrowser, res.Width - 505, 0);
			API.sendNotification("Browser created!");
		}

		API.loadPageCefBrowser(mainBrowser, page);
	}

	if (msg == "/gotohell") {
		if (mainBrowser != null) {
			API.destroyCefBrowser(mainBrowser);
			mainBrowser = null;
		}
	}
});

API.onResourceStop.connect(function(e, ev) {
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});