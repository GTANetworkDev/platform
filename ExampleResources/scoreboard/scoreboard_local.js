function disableControls() {
	API.disableControlThisFrame(14);
	API.disableControlThisFrame(15);
}


API.onUpdate.connect(function(s,e) {
	
	if (API.isControlPressed(19)) {
		//disableControls();

		API.disableControlThisFrame(14);
		API.disableControlThisFrame(15);

		var res = API.getScreenResolutionMantainRatio();
		var players = API.getAllPlayers();

		var columnList = API.getWorldSyncedData("scoreboard_column_names");
		if (columnList == null) return;
		var columnNames = API.getWorldSyncedData("scoreboard_column_friendlynames");
		var columnWidths = API.getWorldSyncedData("scoreboard_column_widths");

		var columnLen = columnList.Count;
		var totalWidth = 300;
		var totalHeight = 80 + players.Length * 40;

		var activeArea = 0;
		
		for (var i = columnWidths.Count - 1; i >= 0; i--) {
			activeArea += columnWidths[i];
		};

		totalWidth += activeArea;

		var startX = res.Width / 2;
		startX -= totalWidth / 2;

		var startY = res.Height / 2;
		startY -= totalHeight / 2;

		// Column drawing
		API.drawRectangle(startX, startY, totalWidth, 40, 0, 0, 0, 200);
		API.drawText("Players", startX + 10, startY + 5, 0.35, 255, 255, 255, 255, 0, 0, false, true, 0);

		var currentCW = 0;
		for (var j = 0; j < columnList.Count; j++) {			
			var value = columnNames[j];			
			API.drawText(value, res.Width - startX - currentCW - 5, startY + 5, 0.35, 255, 255, 255, 255, 0, 2, false, true, 0);
			currentCW += columnWidths[j];
		}

		API.drawRectangle(startX, startY + 40, totalWidth, 40, 50, 50, 50, 200);
		API.drawText(API.getPlayerName(API.getLocalPlayer()), startX + 10, startY + 45, 0.4, 255, 255, 255, 255, 4, 0, false, true, 0);

		currentCW = 0;
		for (var j = 0; j < columnList.Count; j++) {
			var columnData = API.getEntitySyncedData(API.getLocalPlayer(), columnList[j]);

			if (columnList[j] == "scoreboard_ping")
				columnData = API.toString(API.getPlayerPing(API.getLocalPlayer()));

			if (columnData != null) {
				API.drawText(API.toString(columnData), res.Width - startX - currentCW - 5, startY + 45, 0.4, 255, 255, 255, 255, 4, 2, false, true, 0);
				currentCW += columnWidths[j];
			}
		}

		
		for (var i = 0; i < players.Length; i++) {
			if (API.getPlayerName(players[i]) == null) continue;

			var color = 50;
			if (i % 2 == 0)
				color = 70;

			API.drawRectangle(startX, startY + 80 + 40 * i, totalWidth, 40, color, color, color, 200);
			API.drawText(API.getPlayerName(players[i]), startX + 10, startY + 85 + 40 * i, 0.4, 255, 255, 255, 255, 4, 0, false, true, 0);

			currentCW = 0;
			for (var j = 0; j < columnList.Count; j++) {
				var columnData = API.getEntitySyncedData(players[i], columnList[j]);

				if (columnList[j] == "scoreboard_ping")
					columnData = API.toString(API.getPlayerPing(players[i]));

				if (columnData != null) {
					API.drawText(API.toString(columnData), res.Width - startX - currentCW - 5, startY + 85 + 40 * i, 0.4, 255, 255, 255, 255, 4, 2, false, true, 0);

					currentCW += columnWidths[j];
				}
			}
		}
	}
});