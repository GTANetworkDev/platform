
class cashGrab {
	
	constructor(target, pos, bag, owner, id) {
		this._id = id;
		this._target = target;
		this._pos = pos;
		this._bag = bag;
		this._owner = owner;
		this.finished = false;
		this._stage = "";
		this._scene = 0;
		this._disposed = false;	
	}

	tick() {
		if (this._stage == "intro") {
			if (API.returnNative("GET_SYNCHRONIZED_SCENE_PHASE", 7, this._scene) >= 1) {
		    	API.callNative("_0xCD9CC7E200A52A6F", this._scene); // Warn: cant call _0x natives in scripthookvdotnet 3.0

				if (this._owner)
					API.triggerServerEvent("cashgrab_intro_finished", this._id);

				this._stage = "";
			}
		}
		else if (this._stage == "exit") {
			if (API.returnNative("GET_SYNCHRONIZED_SCENE_PHASE", 7, this._scene) >= 1) {
		    	API.callNative("_0xCD9CC7E200A52A6F", this._scene); // Warn: cant call _0x natives in scripthookvdotnet 3.0

				if (this._owner)
					API.triggerServerEvent("cashgrab_exit_finished", this._id);
				else
					API.stopControlOfPlayer(this._target);

				this.finished = true;
				this._stage = "";
			}
		}
		else if (this._stage == "grab") {
			if (API.returnNative("GET_SYNCHRONIZED_SCENE_PHASE", 7, this._scene) < 1) {

				if (API.returnNative("HAS_ANIM_EVENT_FIRED", 8, this._target, API.getHashKey("CASH_APPEAR")))
		        {            
		            if (API.getEntityTransparency(this._cashpile) == 0)
		            {
		                API.setEntityTransparency(this._cashpile, 255);
		            }            
		        }

		        if (API.returnNative("HAS_ANIM_EVENT_FIRED", 8, this._target, API.getHashKey("RELEASE_CASH_DESTROY")))
		        {
		            if (API.getEntityTransparency(this._cashpile) == 255)
		            {
		                API.setEntityTransparency(this._cashpile, 0);
		                // Increase the take?
		                API.playSoundFrontEnd("HUD_FRONTEND_CUSTOM_SOUNDSET", "ROBBERY_MONEY_TOTAL");
		            }
		        }
			} else {				
		    	API.callNative("_0xCD9CC7E200A52A6F", this._scene); // Warn: cant call _0x natives in scripthookvdotnet 3.0

				if (this._owner)
					API.triggerServerEvent("cashgrab_grab_finished", this._id);
				this._stage = "";
			}

		}
	}

	startIntroSequence() {
		if (!this._owner)
		{
			API.requestControlOfPlayer(this._target);
		}

		this._stage = "intro";

		this.CreateCashBlockingGrabbingScene(this._target.Value, this._pos, "intro", this._bag.Value);
	}

	startGrabbingSequence(cart, cashpile) {
		this._stage = "grab";
		this._cart = cart;
		this._cashpile = cashpile;

		this.CreateCashBlockingGrabbingSceneWithCart(this._target.Value, this._pos, "grab", this._bag.Value, cart.Value);
	}

	endSequence() {
		this._stage = "exit";

		this.CreateCashBlockingGrabbingScene(this._target.Value, this._pos, "exit", this._bag.Value);		
	}

	dispose() {
		if (!this._disposed) {			
			if (!this._owner)
				API.stopControlOfPlayer(this._target);

			this._disposed = true;
		}
	}


	CreateCashBlockingGrabbingScene(ped, pos, type, bagProp)
	{
		// ped -> int (game handle)
		// pos -> Vector3
		// type -> string

		API.loadAnimationDict("anim@heists@ornate_bank@grab_cash");

	    this._scene = API.returnNative("CREATE_SYNCHRONIZED_SCENE", /*return type*/ 0, pos.X, pos.Y, pos.Z, 0, 0, 0 /* heading */, 2);

	    API.callNative("TASK_SYNCHRONIZED_SCENE", ped, this._scene, "anim@heists@ornate_bank@grab_cash", type, 8.01, -8.01, 3341, 16, 1148846080, 0);
	    API.callNative("PLAY_SYNCHRONIZED_ENTITY_ANIM", bagProp, this._scene, "bag_" +  type, "anim@heists@ornate_bank@grab_cash", 8.01, -8.01, 0, 1148846080);

	    API.callNative("FORCE_ENTITY_AI_AND_ANIMATION_UPDATE", bagProp);	    
	}

	CreateCashBlockingGrabbingSceneWithCart(ped, pos, type, bagProp, cart)
	{
	    API.loadAnimationDict("anim@heists@ornate_bank@grab_cash");

	    this._scene = API.returnNative("CREATE_SYNCHRONIZED_SCENE", /*return type*/ 0, pos.X, pos.Y, pos.Z, 0, 0, 0 /* heading */, 2);

	    API.callNative("TASK_SYNCHRONIZED_SCENE", ped, this._scene, "anim@heists@ornate_bank@grab_cash", type, 8.01, -8.01, 3341, 16, 1148846080, 0);
	    API.callNative("PLAY_SYNCHRONIZED_ENTITY_ANIM", bagProp, this._scene, "bag_" +  type, "anim@heists@ornate_bank@grab_cash", 8.01, -8.01, 0, 1148846080);

	    
	    API.callNative("PLAY_SYNCHRONIZED_ENTITY_ANIM", cart, this._scene, "cart_cash_dissapear", "anim@heists@ornate_bank@grab_cash", 8.01, -8.01, 0, 1148846080);
	   

	    API.callNative("FORCE_ENTITY_AI_AND_ANIMATION_UPDATE", bagProp);	    
	}
}

var cashGrabs = {};

API.onResourceStop.connect(function(sender, args) {
	for (key in cashGrabs) {
		cashGrabs[key].dispose();
	}
});


API.onServerEventTrigger.connect(function(eventName, args) {
	if (eventName == "cashgrab_intro") {
		target = args[0];
		pos = args[1];
		bag = args[2];
		owner = args[3];
		id = args[4];

		cashGrabs[id] = new cashGrab(target, pos, bag, owner, id);
		cashGrabs[id].startIntroSequence();
	}
	else if (eventName == "cashgrab_grab") {
		var cart = args[0];
		var cashpile = args[1];
		var id = args[2];
		if (id in cashGrabs) {
			cashGrabs[id].startGrabbingSequence(cart, cashpile);
		}
	}
	else if (eventName == "cashgrab_exit") {
		var id = args[0];
		if (id in cashGrabs) {
			cashGrabs[id].endSequence();
		}
	}
});

API.onUpdate.connect(function(sender, args) {
	for (key in cashGrabs) {
		cashGrabs[key].tick();

		if (cashGrabs[key].finished) {
			delete cashGrabs[key];
		}
	}
});