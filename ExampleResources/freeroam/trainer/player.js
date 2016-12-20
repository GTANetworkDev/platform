var mainWindow = null;

API.onResourceStart.connect(function (sender, e) {
	mainWindow = API.createMenu("PLAYER", 0, 0, 6);

	var linkItem = API.createMenuItem("Player Options", "");
	resource.trainer.mainWindow.AddItem(linkItem);
	resource.trainer.mainWindow.BindMenuToItem(mainWindow, linkItem);
	resource.trainer.menuPool.Add(mainWindow);

	var invin = API.createCheckboxItem("Invincibility", "", false);

	invin.CheckboxEvent.connect(function (item, newChecked) {
		API.setPlayerInvincible(newChecked);
		API.sendNotification("Invincibility " + (newChecked ? "~g~on" : "~r~off"));
	});

	mainWindow.AddItem(invin);


	var ragdoll = API.createCheckboxItem("Ragdoll", "", true);

	ragdoll.CheckboxEvent.connect(function (item, newChecked) {
		API.callNative("SET_PED_CAN_RAGDOLL", API.getLocalPlayer().Value, newChecked);
		API.sendNotification("Ragdoll " + (newChecked ? "~g~on" : "~r~off"));		
	});

	mainWindow.AddItem(ragdoll);

	var hp = API.createMenuItem("Restore Health", "");

	hp.Activated.connect(function (menu, item) {
		API.setPlayerHealth(100);
		API.sendNotification("Health ~g~restored.");		
	});

	mainWindow.AddItem(hp);

	var armor = API.createMenuItem("Restore Armor", "");

	armor.Activated.connect(function (menu, item) {
		API.setPlayerArmor(100);
		API.sendNotification("Armor ~g~restored.");		
	});

	mainWindow.AddItem(armor);

	var suicide = API.createMenuItem("Suicide", "");

	suicide.Activated.connect(function (menu, item) {
		API.setPlayerHealth(-1);		
	});

	mainWindow.AddItem(suicide);

	mainWindow.RefreshIndex();
});