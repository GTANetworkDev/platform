case (byte)DefaultMessageEventType.ID_CEF_EVENT:
{
	var len = packet.ReadInt32();
	var ceftem = DeserializeBinary<List<String>>(packet.ReadBytes(len)) as List<String>;
	if (ceftem == null) return;

	PlayerInfo player = PlayerInfo.GetPlayerObject(packet.SenderConnection.RemoteUniqueIdentifier);
	if (player != null)
	{
		startScriptAPI.API._gamemodes.ForEach(fs =>
		{
			fs.OnCEFEventTrigger(player, ceftem);
		});
	}
}
break;