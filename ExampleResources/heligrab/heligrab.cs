using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class HeligrabScript : Script
{
	public HeligrabScript()
	{
		API.onClientEventTrigger += ScriptEvent;
	}

	public List<Chopper> Choppers = new List<Chopper>();

	public void ScriptEvent(Client sender, string eventName, object[] args)
	{
		if (eventName == "heligrab_requestGrab")
		{
			var chopperHandle = (NetHandle) args[0];
			var right = (bool) args[1];

			if (API.getEntityModel(chopperHandle) != (int) VehicleHash.Maverick) return;

			lock (Choppers)
			{
				// Player already hanging
				if (Choppers.Any(c => c.Hangers.Contains(sender))) return;

				var ourchopper = Choppers.FirstOrDefault(c => c.Vehicle == chopperHandle);

				if (ourchopper == null)
				{
					ourchopper = new Chopper();
					ourchopper.Vehicle = chopperHandle;
					ourchopper.Hangers = new List<Client>();
				}
				else
				{
					if (ourchopper.Hangers.Count >= 2) return;
				}

				ourchopper.Hangers.Add(sender);

				API.setEntityPosition(sender.handle, API.getEntityPosition(chopperHandle));

				if (right)
				{
					API.attachEntityToEntity(sender.handle, chopperHandle, null,
						new Vector3(1.0402, 0.91039, -2.25), new Vector3(0, 0, 270));
				}
				else	
				{
					API.attachEntityToEntity(sender.handle, chopperHandle, null,
						new Vector3(-1.0402, 0.91039, -2.25), new Vector3(0, 0, 90));
				}

				API.sleep(1000);

				API.playPlayerAnimation(sender, 1, "missfam1_yachtbattleonyacht02_", "onboom_twohand_hang_idle");

				API.triggerClientEvent(sender, "heligrab_confirm", chopperHandle);

				if (right)
				{
					API.attachEntityToEntity(sender.handle, chopperHandle, null,
						new Vector3(1.0402, 0.91039, -2.25), new Vector3(0, 0, 270));
				}
				else	
				{
					API.attachEntityToEntity(sender.handle, chopperHandle, null,
						new Vector3(-1.0402, 0.91039, -2.25), new Vector3(0, 0, 90));
				}
			}

		}
		else if (eventName == "heligrab_stop")
		{
			lock (Choppers)
			{
				var ourchopper = Choppers.FirstOrDefault(c => c.Hangers.Contains(sender));
				if (ourchopper != null)
				{
					ourchopper.Hangers.Remove(sender);
				}
			}

			API.stopPlayerAnimation(sender);
			API.detachEntity(sender.handle, true);
		}
	}
}

public class Chopper
{
	public NetHandle Vehicle;
	public List<Client> Hangers;
}