using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class HeistScript : Script
{
	public HeistScript()
	{
		_crossReference = this;

		API.onClientEventTrigger += OnClientScriptEvent;		
	}

	private Dictionary<int, Cashgrab> CashgrabDict = new Dictionary<int, Cashgrab>();
	private int _cashgrabCount = 0;

	public void OnClientScriptEvent(Client sender, string eventName, object[] args)
	{
		lock (CashgrabDict)
		{
			for (int i = CashgrabDict.Count - 1; i >= 0; i--)
			{
				var pair = CashgrabDict.ElementAt(i);

				if (pair.Value.Finished) CashgrabDict.Remove(pair.Key);
				else pair.Value.ReceiveEvent(eventName, args);
			}
		}
	}

	[Command("test")]
	public void StartTest(Client sender)
	{
		lock (CashgrabDict)
		{
			var newId = ++_cashgrabCount;
			CashgrabDict.Add(newId, new Cashgrab(sender, newId));
		}
	}

	private static HeistScript _crossReference;
	public static API CAPI
	{ 
		get
		{
			return _crossReference.API;
		}
	}
}

public class Cashgrab
{
	private NetHandle cashPile;
	private NetHandle cashGrabTray2;
	private NetHandle _bagProp;
	private Vector3 startPos;
	private List<Client> playerList;
	private int _id;
	private Client _owner;

	public bool Finished;

	public Cashgrab(Client owner, int id)
	{
		_id = id;
		_owner = owner;
		startPos = HeistScript.CAPI.getEntityPosition(owner) - new Vector3(0, 0, 0.55f);

		var bagMod = HeistScript.CAPI.getHashKey("hei_p_m_bag_var22_arm_s");
		_bagProp = HeistScript.CAPI.createObject(bagMod, startPos, new Vector3());

		HeistScript.CAPI.setEntityPositionFrozen(_bagProp, true);
		HeistScript.CAPI.setEntityCollisionless(_bagProp, true);
		HeistScript.CAPI.setEntityInvincible(_bagProp, true);
		
		cashGrabTray2 = HeistScript.CAPI.createObject(269934519, startPos, new Vector3());

		HeistScript.CAPI.setPlayerClothes(owner, 5, 0, 0);

		playerList = HeistScript.CAPI.getAllPlayers();

		foreach (var c in playerList)
		{
			bool isOwner = c == owner;
			HeistScript.CAPI.triggerClientEvent(c, "cashgrab_intro", owner.handle, startPos, _bagProp, isOwner, _id);
		}
	}

	public void ReceiveEvent(string eventName, object[] args)
	{
		if (eventName == "cashgrab_intro_finished")
		{
			if ((int)args[0] != _id) return;
		
			var cashMod = HeistScript.CAPI.getHashKey("hei_prop_heist_cash_pile");
			cashPile = HeistScript.CAPI.createObject(cashMod, startPos, new Vector3());

			HeistScript.CAPI.setEntityPositionFrozen(cashPile, true);
			HeistScript.CAPI.setEntityCollisionless(cashPile, true);
			HeistScript.CAPI.setEntityInvincible(cashPile, true);
			HeistScript.CAPI.setEntityTransparency(cashPile, 0);
			HeistScript.CAPI.attachEntityToEntity(cashPile, _owner, "PH_L_Hand", new Vector3(), new Vector3());

			foreach (var c in playerList)
			{
				HeistScript.CAPI.triggerClientEvent(c, "cashgrab_grab", cashGrabTray2, cashPile, _id);
			}
		}
		else if (eventName == "cashgrab_grab_finished")
		{
			if ((int)args[0] != _id) return;
		
			HeistScript.CAPI.deleteEntity(cashPile);
			HeistScript.CAPI.deleteEntity(cashGrabTray2);

			var newMod = HeistScript.CAPI.getHashKey("hei_prop_hei_cash_trolly_03");

			var cashGrabTray1 = HeistScript.CAPI.createObject(newMod, startPos, new Vector3());

			foreach (var c in playerList)
			{
				HeistScript.CAPI.triggerClientEvent(c, "cashgrab_exit", _id);
			}
		}
		else if (eventName == "cashgrab_exit_finished")
		{
			if ((int)args[0] != _id) return;

			HeistScript.CAPI.deleteEntity(_bagProp);
			HeistScript.CAPI.setPlayerClothes(_owner, 5, 45, 0);

			HeistScript.CAPI.clearPlayerTasks(_owner);
			Finished = true;
		}
	}
}