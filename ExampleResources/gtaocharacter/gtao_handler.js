API.onResourceStart.connect(function() {
    var players = API.getStreamedPlayers();

    for (var i = players.Length - 1; i >= 0; i--) {
        setPedCharacter(players[i]);
    }
});


API.onServerEventTrigger.connect(function(name, args) {
    if (name == "UPDATE_CHARACTER") {
        setPedCharacter(args[0]);
    }
});

API.onEntityStreamIn.connect(function(ent, entType) {
    if (entType == 6 || entType == 8) {// Player or ped
        setPedCharacter(ent);
    }
});


function setPedCharacter(ent) {
    if (API.isPed(ent) &&
        API.getEntitySyncedData(ent, "GTAO_HAS_CHARACTER_DATA") === true &&
        (API.getEntityModel(ent) == 1885233650 || // FreemodeMale
         API.getEntityModel(ent) == -1667301416)) // FreemodeFemale
    {
        // FACE
        var shapeFirstId = API.getEntitySyncedData(ent, "GTAO_SHAPE_FIRST_ID");
        var shapeSecondId = API.getEntitySyncedData(ent, "GTAO_SHAPE_SECOND_ID");

        var skinFirstId = API.getEntitySyncedData(ent, "GTAO_SKIN_FIRST_ID");
        var skinSecondId = API.getEntitySyncedData(ent, "GTAO_SKIN_SECOND_ID");

        var shapeMix = API.f(API.getEntitySyncedData(ent, "GTAO_SHAPE_MIX"));
        var skinMix = API.f(API.getEntitySyncedData(ent, "GTAO_SKIN_MIX"));

        API.callNative("SET_PED_HEAD_BLEND_DATA", ent, shapeFirstId, shapeSecondId, 0, skinFirstId, skinSecondId, 0, shapeMix, skinMix, 0, false);

        // HAIR COLOR
        var hairColor = API.getEntitySyncedData(ent, "GTAO_HAIR_COLOR");
        var highlightColor = API.getEntitySyncedData(ent, "GTAO_HAIR_HIGHLIGHT_COLOR");

        API.callNative("_SET_PED_HAIR_COLOR", ent, hairColor, highlightColor);

        // EYE COLOR

        var eyeColor = API.getEntitySyncedData(ent, "GTAO_EYE_COLOR");

        API.callNative("_SET_PED_EYE_COLOR", ent, eyeColor);

        // EYEBROWS, MAKEUP, LIPSTICK
        var eyebrowsStyle = API.getEntitySyncedData(ent, "GTAO_EYEBROWS");
        var eyebrowsColor = API.getEntitySyncedData(ent, "GTAO_EYEBROWS_COLOR");
        var eyebrowsColor2 = API.getEntitySyncedData(ent, "GTAO_EYEBROWS_COLOR2");

        API.callNative("SET_PED_HEAD_OVERLAY", ent, 2, eyebrowsStyle, API.f(1));

        API.callNative("_SET_PED_HEAD_OVERLAY_COLOR", ent, 2, 1, eyebrowsColor, eyebrowsColor2);

        if (API.hasEntitySyncedData(ent, "GTAO_LIPSTICK"))
        {
            var lipstick = API.getEntitySyncedData(ent, "GTAO_LIPSTICK");
            var lipstickColor = API.getEntitySyncedData(ent, "GTAO_LIPSTICK_COLOR");
            var lipstickColor2 = API.getEntitySyncedData(ent, "GTAO_LIPSTICK_COLOR2");

            API.callNative("SET_PED_HEAD_OVERLAY", ent, 8, lipstick, API.f(1));
            API.callNative("_SET_PED_HEAD_OVERLAY_COLOR", ent, 8, 2, lipstickColor, lipstickColor2);
        }

        if (API.hasEntitySyncedData(ent, "GTAO_MAKEUP"))
        {
            var makeup = API.getEntitySyncedData(ent, "GTAO_MAKEUP");
            var makeupColor = API.getEntitySyncedData(ent, "GTAO_MAKEUP_COLOR");
            var makeupColor2 = API.getEntitySyncedData(ent, "GTAO_MAKEUP_COLOR2");
                    
            API.callNative("SET_PED_HEAD_OVERLAY", ent, 4, makeup, API.f(1));
            API.callNative("SET_PED_HEAD_OVERLAY", ent, 8, lipstick, API.f(1));        
            API.callNative("_SET_PED_HEAD_OVERLAY_COLOR", ent, 4, 0, makeupColor, makeupColor2);
        }

        // FACE FEATURES (e.g. nose length, chin shape, etc)

        var faceFeatureList = API.getEntitySyncedData(ent, "GTAO_FACE_FEATURES_LIST");

        for (var i = 0; i < 21; i++) {
            API.callNative("_SET_PED_FACE_FEATURE", ent, i, API.f(faceFeatureList[i]));
        }
    }
}