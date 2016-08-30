using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;


public class HelpManager : Script
{
	public Dictionary<string, ResourceInfo> Information;

	public HelpManager()
	{
		Information = new Dictionary<string, ResourceInfo>();

		API.onResourceStart += OnThisResourceStart;
		API.onServerResourceStart += OnForeignResourceStart;
		API.onServerResourceStop += OnForeignResourceStop;
		API.onPlayerConnected += OnPlayerJoin;
	}

	public void OnThisResourceStart()
	{
		foreach (var res in API.getRunningResources())
		{
			ExtractResourceInfo(res);
		}

		foreach (var player in API.getAllPlayers())
		{
			DownloadClientData(player);
		}
	}

	public void OnPlayerJoin(Client player)
	{
		DownloadClientData(player);
	}

	public void OnForeignResourceStart(string resource)
	{
		API.consoleOutput(resource + " has started!");

		var r = ExtractResourceInfo(resource);

		foreach (var p in API.getAllPlayers())
		{
			DownloadPartialData(p, r);
		}
	}

	public void OnForeignResourceStop(string resource)
	{
		Information.Remove(resource);
		API.triggerClientEventForAll("helpmanager_removeresource", resource);
	}

	public void DownloadClientData(Client player)
	{
		var transferObj = new DataTransport();
		transferObj.Datatype = "complete";
		transferObj.Data = API.toJson(Information);

		string data = API.toJson(transferObj);
		API.downloadData(player, data);
	}

	public void DownloadPartialData(Client player, ResourceInfo info)
	{
		var transferObj = new DataTransport();
		transferObj.Datatype = "partial";
		transferObj.Data = API.toJson(info);

		string data = API.toJson(transferObj);
		API.downloadData(player, data);
	}

	public ResourceInfo ExtractResourceInfo(string resource)
	{
		if (Information.ContainsKey(resource)) return Information[resource];
		var rI = new ResourceInfo();
		rI.Name = resource;
		rI.CompleteName = API.getResourceName(resource);
		rI.Description = API.getResourceDescription(resource);
		rI.Author = API.getResourceAuthor(resource);
		rI.Version = API.getResourceVersion(resource);
		rI.Type = API.getResourceType(resource).ToString();
		rI.Commands = new List<CommandInfo>(API.getResourceCommandInfos(resource));

		Information.Add(resource, rI);
		return rI;
	}
}

public struct ResourceInfo
{
	public string Name;
	public string CompleteName;
	public string Description;
	public string Author;
	public string Version;
	public string Type;
	public List<CommandInfo> Commands;
}

public class DataTransport
{
	public string Datatype;
	public string Data;	
	public bool HelpManagerUniqueKey = true;
}