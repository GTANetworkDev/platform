var mainWindow = null;
var invincible = false;

API.onResourceStart.connect(function (sender, e) {
	mainWindow = API.createMenu("CLOTHES", 0, 0, 6);

	var linkItem = API.createMenuItem("Clothes", "");
	resource.trainer.mainWindow.AddItem(linkItem);
	resource.trainer.mainWindow.BindMenuToItem(mainWindow, linkItem);
	resource.trainer.menuPool.Add(mainWindow);

	
});


API.onUpdate.connect(function (s, e) {
	
})