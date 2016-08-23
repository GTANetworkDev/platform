q.ready(function() {
	// create the notification popup
	var popup;

	// create the notification API
	var notify = function(message, delay, callback) {
	  popup.setHtml(message);
	  popup.fadeIn();
	};

	// DEMO
	var popup = q.create("<div>").appendTo(document.body).addClass("popup").setStyle("border-radius", "5px");

	notify("This is ...", 1000, function() {
	 notify("... a qx.Website notification demo.", 2000);
	});

});