API.onPlayerConnected.connect(function (player) {
    API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~joined.");
    API.sendChatMessageToAll("~b~~h~" + player.Name + "~h~~w~ has joined the server.");
});

API.onPlayerDisconnected.connect(function (player, reason) {
    API.sendNotificationToAll("~b~~h~" + player.Name + "~h~ ~w~quit.");
    API.sendChatMessageToAll("~b~~h~" + player.Name + "~h~~w~ has quit the server. (" + reason + ")");
});