var currentMoney = null;
var res_X = API.getScreenResolutionMantainRatio().Width;

API.onServerEventTrigger.connect(function (name, args) {
    if (name === "update_money_display") {
        currentMoney = args[0];
    }
});

API.onUpdate.connect(function() {
    if (currentMoney != null) {
        API.drawText("$" + currentMoney, res_X - 15, 50, 1, 115, 186, 131, 255, 4, 2, false, true, 0);
    }
});