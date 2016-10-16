var mainBrowser = null;

API.onResourceStart.connect(function() {
	// init browser
	var res = API.getScreenResolution();
	mainBrowser = API.createCefBrowser(res.Width - 300, res.Height - 300);
	API.setCefBrowserPosition(mainBrowser, 150, 150);
	API.setCefBrowserHeadless(mainBrowser, true);
	API.waitUntilCefBrowserInit(mainBrowser);
	API.loadPageCefBrowser(mainBrowser, "main.html");
});

API.onResourceStop.connect(function() {
	// destroy browser
	if (mainBrowser != null) {
		API.destroyCefBrowser(mainBrowser);
	}
});

API.onKeyDown.connect(function(sender, args) {
	if (args.KeyCode == Keys.F9 && mainBrowser != null) {
		API.setCefBrowserHeadless(mainBrowser, !API.getCefBrowserHeadless(mainBrowser));
		API.showCursor(!API.isCursorShown());
		API.setCanOpenChat(!API.getCanOpenChat());
	}
});

API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "helpmanager_removeresource") {
		// communicate to browser
		if (mainBrowser != null) {
			mainBrowser.call("removeTab", args[0]);
		}
	}

});

function debugOutput(text) {
	API.sendChatMessage(text);
}

API.onCustomDataReceived.connect(function(data) {
	if (mainBrowser != null) {
		while (API.isCefBrowserLoading(mainBrowser)) {
			API.sleep(100);
		}
	}

	var deJsonified = null;
	try
	{
		deJsonified = API.fromJson(data);		
		if (deJsonified.HelpManagerUniqueKey == undefined) // we know its our json
			return;
	}
	catch(err)
	{
		API.sendNotification("error: " + err);
		return;
	}

	if (API.toString(deJsonified.Datatype) == "partial") {
		var d = deJsonified.Data;
		// communicate d to browser
		if (mainBrowser != null) {
			mainBrowser.call("addSingleTab", API.toString(d).toString()); // toString!
		}
	}
	else if (API.toString(deJsonified.Datatype) == "complete") {
		if (mainBrowser != null) {
			var data = API.toString(deJsonified.Data).toString();
			mainBrowser.call("addMultipleTabs", data);
		}
	}
});