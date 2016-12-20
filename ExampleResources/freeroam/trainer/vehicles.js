var mainWindow = null;
var invincible = false;

API.onResourceStart.connect(function (sender, e) {
	mainWindow = API.createMenu("VEHICLES", 0, 0, 6);

	var linkItem = API.createMenuItem("Vehicles", "");
	resource.trainer.mainWindow.AddItem(linkItem);
	resource.trainer.mainWindow.BindMenuToItem(mainWindow, linkItem);
	resource.trainer.menuPool.Add(mainWindow);

	mainWindow.AddItem(addVehicleItem("T20", 1663218586));
	mainWindow.AddItem(addVehicleItem("Futo", 2016857647));
	mainWindow.AddItem(addVehicleItem("Burrito", -1346687836));
	mainWindow.AddItem(addVehicleItem("Sanchez", 788045382));
	mainWindow.AddItem(addVehicleItem("Maverick", -1660661558));
	mainWindow.AddItem(addVehicleItem("Buzzard", 788747387));
	mainWindow.AddItem(addVehicleItem("Hydra", 970385471));
	mainWindow.AddItem(addVehicleItem("Seashark", -1030275036));

	mainWindow.RefreshIndex();
});

function addVehicleItem(gun, hash) {
	var suicide = API.createMenuItem(gun, "");

	suicide.Activated.connect(function (menu, item) {
		API.triggerServerEvent("CREATE_VEHICLE", hash);
	});

	return suicide;
}


API.onUpdate.connect(function (s, e) {
	
})