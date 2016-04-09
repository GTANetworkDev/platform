var mainWindow = API.createMenu("FREEROAM", 0, 0, 6);
var menuPool = API.getMenuPool();

menuPool.Add(mainWindow);

API.onKeyDown.connect(function(sender, keyEventArgs) {
	if (keyEventArgs.KeyCode == Keys.F1) {
		mainWindow.Visible = !mainWindow.Visible;
	}
});


API.onUpdate.connect(function(sender, events) {
	menuPool.ProcessMenus();
});