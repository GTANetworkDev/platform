var pool = null;

API.onResourceStart.connect(function() {
    
});

API.onServerEventTrigger.connect(function (name, args) {
    if (name == "menu_handler_create_menu") {
        pool = API.getMenuPool();

        var callbackId = args[0];
        var banner = args[1];
        var subtitle = args[2];
        var noExit = args[3];

        var menu = null;
        if (banner == null)
            menu = API.createMenu(subtitle, 0, 0, 6);
        else menu = API.createMenu(banner, subtitle, 0, 0, 6);

        if (noExit) {
            menu.ResetKey(menuControl.Back);
        }
        
        var itemsLen = args[4];

        for (var i = 0; i < itemsLen; i++) {
            var item = API.createMenuItem(args[5 + i], "");
            menu.AddItem(item);
        }

        menu.OnItemSelect.connect(function(sender, item, index) {
            API.triggerServerEvent("menu_handler_select_item", callbackId, index);
        });

        menu.Visible = true;

        pool.Add(menu);
    }
    else if (name === "menu_handler_close_menu") {
        pool = null;
    }
});

API.onUpdate.connect(function() {
    if (pool != null) {
        pool.ProcessMenus();
    }
});