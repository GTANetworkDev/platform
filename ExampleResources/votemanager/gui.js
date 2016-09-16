var mainMenu = null;

mainMenu = API.createMenu("VOTE", 0, 0, 6);
mainMenu.ResetKey(menuControl.Back);

mainMenu.OnItemSelect.connect(function(sender, item, index) {
    API.triggerServerEvent("cast_vote", index);
    API.sendChatMessage("You have voted for ~b~" + item.Text + "~w~.");
    mainMenu.Visible = false;    
});

API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "start_vote") {
		var txt = args[0];
        var numOfMaps = args[1];
        mainMenu.Clear();

        for (var i = 0; i < numOfMaps; i++) {
            var mapName = args[i+2];
            var mapItem = API.createMenuItem(mapName, "");
            mainMenu.AddItem(mapItem);
        }

        //API.sendNotification("Null?: " + (mainMenu.Subtitle == null));
        mainMenu.Subtitle.Caption = API.toString(txt);
        mainMenu.Visible = true;
    }
    else if (eventName == "end_vote") {
    	mainMenu.Visible = false;
    }
});

API.onUpdate.connect(function() {
	API.drawMenu(mainMenu);
});