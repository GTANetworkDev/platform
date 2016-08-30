var False = false;
var True = true;

function updateScroll() {
	if ($("#chat-body").scrollTop() >= $("#chat-body")[0].scrollHeight - 400) {
		$("#chat-body").scrollTop($("#chat-body")[0].scrollHeight);
	}		
}

function formatMsg(input) {
	var start = '<span style="color: white;">';
	var output = start;
	
	var pass1 = input.replace("~r~", '</span><span style="color: red;">');
	var pass2 = pass1.replace("~b~", '</span><span style="color: blue;">');
	var pass3 = pass2.replace("~g~", '</span><span style="color: green;">');
	var pass4 = pass3.replace("~p~", '</span><span style="color: purple;">');

	return output + pass4 + "</span>";
}

function addMessage(msg) {
	var child = $("<p>" + formatMsg(msg) + "</p>");
	child.hide();
	$("#chat-body").append(child);
	child.fadeIn();

	updateScroll();
}

function addColoredMessage(msg, r,g,b) {
	var child = $('<p style="color: rgb(' + r + ', ' + g + ', ' + b + ');">' + formatMsg(msg) + '</p>');
	child.hide();
	$("#chat-body").append(child);
	child.fadeIn();

	updateScroll();
}

function setFocus(focus) {
	if (focus) {
		$("#main-input").show();
		$("#main-input").val("");
		$("#main-input").focus();
	} else {
		$("#main-input").hide();
		$("#main-input").val("");
	}
}

function onKeyUp(event) {
	if (event.keyCode == 13) {
		var m = $("#main-input").val();
		try
		{
			resource.call("commitMessage", [m]);
		}
		catch(err) {
			$("body").text(err);
		}
		setFocus(false);		
	}
}
/*
window.setInterval(function () {
	addMessage($("#chat-body").scrollTop() + " / " + $("#chat-body")[0].scrollHeight);
}, 500);
*/