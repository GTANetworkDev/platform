API.onLocalPlayerDamaged.connect(function(enemy, weapon, bone) {
	if (bone == 31086) // head
	{
		API.setPlayerHealth(-1);
	}
});
