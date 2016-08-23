using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class DoorManager : Script
{
	public DoorManager()
	{
		API.onEntityEnterColShape += ColShapeTrigger;
		API.onClientEventTrigger += ClientEventTrigger;
	}

	private int _doorCounter;
	private Dictionary<int, ColShape> _doorColShapes = new Dictionary<int, ColShape>();

	private bool _debugStatus = true;

	public const ulong SET_STATE_OF_CLOSEST_DOOR_OF_TYPE = 0xF82D8F1926A02C3D;

	[Command("getdoor")]
	public void Debug_GetDoorCMD(Client sender)
	{
		if (!_debugStatus) return;

		API.triggerClientEvent(sender, "doormanager_debug");
	}

	[Command("getdoorex")]
	public void Debug_GetDoorExCMD(Client sender, string modelName)
	{
		if (!_debugStatus) return;

		var pos = API.getEntityPosition(sender);

		API.triggerClientEvent(sender, "doormanager_finddoor_return", API.getHashKey(modelName), pos);
	}

	[Command("createDoor")]
	public void Debug_CreateDoorCMD(Client sender, int model)
	{
		if (!_debugStatus) return;

		var pos = API.getEntityPosition(sender);

		var id = registerDoor(model, pos);

		API.sendChatMessageToPlayer(sender, "Your door id is " + id);
	}

	[Command("createDoorEx")]
	public void Debug_CreateDoorExCMD(Client sender, string modelName, float x, float y, float z)
	{
		if (!_debugStatus) return;

		var pos = new Vector3(x, y, z);
		var model = API.getHashKey(modelName);

		var id = registerDoor(model, pos);

		API.sendChatMessageToPlayer(sender, "Your door id is " + id);
	}

	[Command("setdoorstate")]
	public void Debug_SetDoorStateCMD(Client sender, int doorId, bool locked, float heading)
	{
		if (!_debugStatus) return;

		setDoorState(doorId, locked, heading);
	}

	[Command("findobj")]
	public void Debug_FindDoorCMD(Client sender, int model)
	{
		if (!_debugStatus) return;

		var pos = API.getEntityPosition(sender);

		API.triggerClientEvent(sender, "doormanager_finddoor", model, pos);
	}

	[Command("findobjname")]
	public void Debug_FindDoorByNameCMD(Client sender, string modelName)
	{
		if (!_debugStatus) return;

		var model = API.getHashKey(modelName);

		var pos = API.getEntityPosition(sender);

		API.triggerClientEvent(sender, "doormanager_finddoor", model, pos);
	}

	[Command("transition")]
	public void Debug_TransitionDoorCMD(Client sender, int doorid, float target, int time)
	{
		transitionDoor(doorid, target, time);
	}

	private void ClientEventTrigger(Client sender, string eventName, object[] args)
	{
		if (eventName == "doormanager_debug_createdoor")
		{
			if (!_debugStatus) return;

			var model = (int) args[0];
			var pos = (Vector3) args[1];

			var id = registerDoor(model, pos);

			API.sendChatMessageToPlayer(sender, "Your door id is " + id);
		}
	}

	private void ColShapeTrigger(ColShape colshape, NetHandle entity)
	{
		var player = API.getPlayerFromHandle(entity);

		if (player == null) return;

		if (colshape != null && colshape.getData("IS_DOOR_TRIGGER") == true)
		{
			var id = colshape.getData("DOOR_ID");
			var info = colshape.getData("DOOR_INFO");

			float heading = 0f;

			if (info.State != null) heading = info.State;

			API.sendNativeToPlayer(player, SET_STATE_OF_CLOSEST_DOOR_OF_TYPE,
				info.Hash, info.Position.X, info.Position.Y, info.Position.Z,
				info.Locked, heading, false);
		}
	}

	/* EXPORTED METHODS */
	public int registerDoor(int modelHash, Vector3 position)
	{
		var colShapeId = ++_doorCounter;

		var info = new DoorInfo();
		info.Hash = modelHash;
		info.Position = position;
		info.Locked = false; // Open by default;
		info.Id = colShapeId;
		info.State = 0;

		var colShape = API.createSphereColShape(position, 35f);		
		colShape.setData("DOOR_INFO", info);
		colShape.setData("DOOR_ID", colShapeId);
		colShape.setData("IS_DOOR_TRIGGER", true);

		_doorColShapes.Add(colShapeId, colShape);

		return colShapeId;
	}

	public void transitionDoor(int doorId, float finish, int ms)
	{
		if (_doorColShapes.ContainsKey(doorId))
		{
			var info = _doorColShapes[doorId].getData("DOOR_INFO");

			info.Locked = true;
			
			foreach (var entity in _doorColShapes[doorId].getAllEntities())
			{
				var player = API.getPlayerFromHandle(entity);

				if (player == null) continue;

				API.triggerClientEvent(player, "doormanager_transitiondoor",
					info.Hash, info.Position, info.State, finish, ms);
			}

			info.State = finish;
		}
	}

	public void refreshDoorState(int doorId)
	{
		if (_doorColShapes.ContainsKey(doorId))
		{
			var info = _doorColShapes[doorId].getData("DOOR_INFO");

			float heading = info.State;

			foreach (var entity in _doorColShapes[doorId].getAllEntities())
			{
				var player = API.getPlayerFromHandle(entity);

				if (player == null) continue;

				API.sendNativeToPlayer(player, SET_STATE_OF_CLOSEST_DOOR_OF_TYPE,
					info.Hash, info.Position.X, info.Position.Y, info.Position.Z,
					info.Locked, heading, false);
			}
		}
	}

	public void removeDoor(int id)
	{
		if (_doorColShapes.ContainsKey(id))
		{
			API.deleteColShape(_doorColShapes[id]);
			_doorColShapes.Remove(id);
		}
	}

	public void setDoorState(int doorId, bool locked, float heading)
	{
		if (_doorColShapes.ContainsKey(doorId))
		{
			var door = _doorColShapes[doorId];
			var data = door.getData("DOOR_INFO");
			data.Locked = locked;
			data.State = heading;

			door.setData("DOOR_INFO", data);

			foreach (var entity in door.getAllEntities())
			{
				var player = API.getPlayerFromHandle(entity);

				if (player == null) continue;

				float cH = data.State;

				API.sendNativeToPlayer(player, SET_STATE_OF_CLOSEST_DOOR_OF_TYPE,
					data.Hash, data.Position.X, data.Position.Y, data.Position.Z,
					data.Locked, cH, false);
			}
		}
	}

	public int getCloseDoor(Client player)
	{
		var localCopy = new Dictionary<int, ColShape>(_doorColShapes);
		return localCopy.FirstOrDefault(pair => pair.Value.containsEntity(player)).Key;
	}

	public int[] getAllCloseDoors(Client player)
	{
		var localCopy = new Dictionary<int, ColShape>(_doorColShapes);
		var list = new List<int>();
		foreach (var sh in localCopy)
		{
			if (sh.Value.containsEntity(player))
				list.Add(sh.Key);
		}

		return list.ToArray();
	}

	public void setDebug(bool status)
	{
		_debugStatus = status;
	}
}

public struct DoorInfo
{
	public int Hash;
	public Vector3 Position;
	public int Id;

	public bool Locked;
	public float State;
}