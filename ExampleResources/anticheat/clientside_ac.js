var airbreakStrikes = 0;


API.onUpdate.connect(function() {
    var player = API.getLocalPlayer();

    if (!API.getEntityInvincible(player) && 
         API.getLocalPlayerInvincible())
    {
        API.triggerServerEvent("CHEAT_GODMODE");
    }

    if (API.isPlayerInAnyVehicle(player))
    {
        var car = API.getPlayerVehicle(player);
        var height = API.returnNative("GET_ENTITY_HEIGHT_ABOVE_GROUND", 7, car);
        var speed = API.getEntityVelocity(car).Length();
        var model = API.getEntityModel(car);

        var airVeh = API.returnNative("IS_THIS_MODEL_A_PLANE", 8, model) || API.returnNative("IS_THIS_MODEL_A_HELI", 8, model);

        if (height > 10 && !airVeh && speed < 5) {
            airbreakStrikes++;

            if (airbreakStrikes > 5) {
                API.triggerServerEvent("CHEAT_AIRBREAK");
            }
        }
        else {
            airbreakStrikes = 0;
        }
    }
});
