API.onServerEventTrigger.connect(function(eventName, args) {
    if (eventName == "display_subtitle") {
        API.displaySubtitle(args[0], args[1]);
    } else if (eventName == "display_shard") {
        API.displayShard(args[0], args[1]);
    } else if (eventName == "set_marker_color") {
        API.setMarkerColor(args[0], 100, args[1], args[2], args[3]);
    } else if (eventName == "set_blip_color") {
        API.setBlipColor(args[0], args[1]);
    }

});