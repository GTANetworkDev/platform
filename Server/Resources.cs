using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkServer.Constant;
using GTANetworkServer.Managers;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using ProtoBuf;

namespace GTANetworkServer
{
    internal partial class GameServer
    {
        public bool StartResource(string resourceName, string father = null)
        {
            try
            {
                if (RunningResources.Any(res => res.DirectoryName == resourceName)) return false;

                Program.Output("Starting " + resourceName);

                if (!Directory.Exists("resources" + Path.DirectorySeparatorChar + resourceName))
                    throw new FileNotFoundException("Resource does not exist.");

                var baseDir = "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar;

                if (!File.Exists(baseDir + "meta.xml"))
                    throw new FileNotFoundException("meta.xml has not been found.");

                var xmlSer = new XmlSerializer(typeof(ResourceInfo));
                ResourceInfo currentResInfo;
                using (var str = File.OpenRead(baseDir + "meta.xml"))
                    currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);

                var ourResource = new Resource();
                ourResource.Info = currentResInfo;
                ourResource.DirectoryName = resourceName;
                ourResource.Engines = new List<ScriptingEngine>();
                ourResource.ClientsideScripts = new List<ClientsideScript>();

                if (ourResource.Info.Info != null && ourResource.Info.Info.Type == ResourceType.gamemode)
                {
                    if (Gamemode != null)
                        StopResource(Gamemode.DirectoryName);
                    Gamemode = ourResource;
                }

                if (currentResInfo.ResourceACL != null && ACLEnabled)
                {
                    var aclHead = AccessControlList.ParseXml("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + currentResInfo.ResourceACL.Path);
                    ACL.MergeACL(aclHead);
                }

                if (currentResInfo.Includes != null)
                    foreach (var resource in currentResInfo.Includes)
                    {
                        if (string.IsNullOrWhiteSpace(resource.Resource) || resource.Resource == father) continue;
                        StartResource(resource.Resource, resourceName);
                    }

                FileModule.ExportedFiles.Set(resourceName, new List<FileDeclaration>());

                foreach (var filePath in currentResInfo.Files)
                {
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + filePath.Path))
                    {
                        var myData = md5.ComputeHash(stream);

                        var keyName = ourResource.DirectoryName + "_" + filePath.Path;

                        string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                        if (FileHashes.ContainsKey(keyName))
                            FileHashes[keyName] = hash;
                        else
                            FileHashes.Add(keyName, hash);

                        FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(filePath.Path, hash, FileType.Normal));
                    }
                }

                if (currentResInfo.ConfigFiles != null)
                    foreach (var filePath in currentResInfo.ConfigFiles.Where(cfg => cfg.Type == ScriptType.client))
                    {
                        using (var md5 = MD5.Create())
                        using (var stream = File.OpenRead("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + filePath.Path))
                        {
                            var myData = md5.ComputeHash(stream);

                            var keyName = ourResource.DirectoryName + "_" + filePath.Path;

                            string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                            if (FileHashes.ContainsKey(keyName))
                                FileHashes[keyName] = hash;
                            else
                                FileHashes.Add(keyName, hash);

                            FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(filePath.Path, hash, FileType.Normal));
                        }
                    }

                if (currentResInfo.settings != null)
                {
                    if (string.IsNullOrEmpty(currentResInfo.settings.Path))
                    {
                        ourResource.Settings = LoadSettings(currentResInfo.settings.Settings);
                    }
                    else
                    {
                        var ser2 = new XmlSerializer(typeof(ResourceSettingsFile));

                        ResourceSettingsFile file;

                        using (var stream = File.Open(currentResInfo.settings.Path, FileMode.Open))
                            file = ser2.Deserialize(stream) as ResourceSettingsFile;

                        if (file != null)
                        {
                            ourResource.Settings = LoadSettings(file.Settings);
                        }
                    }
                }

                // Load assembly references
                if (currentResInfo.References != null)
                    foreach (var ass in currentResInfo.References)
                    {
                        AssemblyReferences.Set(ass.Name,
                            "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + ass.Name);
                    }


                var csScripts = new List<ClientsideScript>();

                var cSharp = new List<string>();
                var vBasic = new List<string>();

                bool multithreaded = false;

                if (ourResource.Info.Info != null)
                {
                    multithreaded = ourResource.Info.Info.Multithreaded;
                }

                foreach (var script in currentResInfo.Scripts)
                {
                    if (script.Language == ScriptingEngineLanguage.javascript)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        if (script.Type == ScriptType.client)
                        {
                            var csScript = new ClientsideScript()
                            {
                                ResourceParent = resourceName,
                                Script = scrTxt,
                                //Filename = Path.GetFileNameWithoutExtension(script.Path)?.Replace('.', '_'),
                                Filename = script.Path,
                            };

                            string hash;

                            using (var md5 = MD5.Create())
                            {
                                var myData = md5.ComputeHash(Encoding.UTF8.GetBytes(scrTxt));
                                hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
                                csScript.MD5Hash = hash;

                                if (FileHashes.ContainsKey(ourResource.DirectoryName + "_" + script.Path))
                                    FileHashes[ourResource.DirectoryName + "_" + script.Path] = hash;
                                else
                                    FileHashes.Add(ourResource.DirectoryName + "_" + script.Path, hash);
                            }

                            FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(script.Path, hash, FileType.Script));

                            ourResource.ClientsideScripts.Add(csScript);
                            csScripts.Add(csScript);
                            continue;
                        }
                    }
                    else if (script.Language == ScriptingEngineLanguage.compiled)
                    {
                        try
                        {
                            Program.DeleteFile(baseDir + script.Path + ":Zone.Identifier");
                        }
                        catch
                        {
                        }
                        Assembly ass;

                        if (ourResource.Info.Info.Shadowcopy)
                        {
                            byte[] bytes = File.ReadAllBytes(baseDir + script.Path);
                            ass = Assembly.Load(bytes);
                        }
                        else
                        {
                            ass = Assembly.LoadFrom(baseDir + script.Path);
                        }

                        var instances = InstantiateScripts(ass);
                        ourResource.Engines.AddRange(instances.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                    }
                    else if (script.Language == ScriptingEngineLanguage.csharp)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        cSharp.Add(scrTxt);
                    }
                    else if (script.Language == ScriptingEngineLanguage.vbasic)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        vBasic.Add(scrTxt);
                    }
                }



                if (cSharp.Count > 0)
                {
                    var csharpAss = CompileScript(cSharp.ToArray(), currentResInfo.References.Select(r => r.Name).ToArray(), false);
                    ourResource.Engines.AddRange(csharpAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                }

                if (vBasic.Count > 0)
                {
                    var vbasicAss = CompileScript(vBasic.ToArray(), currentResInfo.References.Select(r => r.Name).ToArray(), true);
                    ourResource.Engines.AddRange(vbasicAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                }

                CommandHandler.Register(ourResource);

                var randGen = new Random();

                if (ourResource.ClientsideScripts.Count > 0 || currentResInfo.Files.Count > 0)
                    foreach (var client in Clients)
                    {
                        var downloader = new StreamingClient(client);

                        if (!UseHTTPFileServer)
                        {
                            foreach (var file in currentResInfo.Files)
                            {
                                var fileData = new StreamedData();
                                fileData.Id = randGen.Next(int.MaxValue);
                                fileData.Type = FileType.Normal;
                                fileData.Data =
                                    File.ReadAllBytes("resources" + Path.DirectorySeparatorChar +
                                                      ourResource.DirectoryName +
                                                      Path.DirectorySeparatorChar +
                                                      file.Path);
                                fileData.Name = file.Path;
                                fileData.Resource = ourResource.DirectoryName;
                                fileData.Hash = FileHashes.ContainsKey(ourResource.DirectoryName + "_" + file.Path)
                                    ? FileHashes[ourResource.DirectoryName + "_" + file.Path]
                                    : null;

                                downloader.Files.Add(fileData);
                            }
                        }
                        else
                        {
                            var msg = Server.CreateMessage();
                            msg.Write((byte)PacketType.RedownloadManifest);
                            client.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);
                        }

                        foreach (var script in ourResource.ClientsideScripts)
                        {
                            var scriptData = new StreamedData();
                            scriptData.Id = randGen.Next(int.MaxValue);
                            scriptData.Data = Encoding.UTF8.GetBytes(script.Script);
                            scriptData.Type = FileType.Script;
                            scriptData.Resource = script.ResourceParent;
                            scriptData.Hash = script.MD5Hash;
                            scriptData.Name = script.Filename;
                            downloader.Files.Add(scriptData);
                        }

                        var endStream = new StreamedData();
                        endStream.Id = randGen.Next(int.MaxValue);
                        endStream.Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D };
                        endStream.Type = FileType.EndOfTransfer;
                        downloader.Files.Add(endStream);

                        Downloads.Add(downloader);
                    }

                if (ourResource.Info.Map != null && !string.IsNullOrWhiteSpace(ourResource.Info.Map.Path))
                {
                    ourResource.Map = new XmlGroup();
                    ourResource.Map.Load("resources\\" + ourResource.DirectoryName + "\\" + ourResource.Info.Map.Path);

                    LoadMap(ourResource, ourResource.Map, ourResource.Info.Map.Dimension);

                    if (ourResource.Info.Info.Type == ResourceType.gamemode)
                    {
                        if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                        ourResource.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                    }
                    else if (ourResource.Info.Info.Type == ResourceType.map)
                    {
                        if (string.IsNullOrWhiteSpace(ourResource.Info.Info.Gamemodes))
                        { }
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length != 1 && Gamemode == null)
                        { }
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length == 1 && (Gamemode == null || !ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName)))
                        {
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            StartResource(ourResource.Info.Info.Gamemodes?.Split(',')[0]);

                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                        else if (Gamemode != null && ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName))
                        {
                            Program.Output("Starting map " + ourResource.DirectoryName + "!");
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                    }
                }

                if (ourResource.Info.ExportedFunctions != null)
                {
                    var gPool = ExportedFunctions as IDictionary<string, object>;
                    dynamic resPool = new System.Dynamic.ExpandoObject();
                    var resPoolDict = resPool as IDictionary<string, object>;

                    foreach (var func in ourResource.Info.ExportedFunctions)
                    {
                        ScriptingEngine engine;
                        if (string.IsNullOrEmpty(func.Path))
                            engine = ourResource.Engines.SingleOrDefault();
                        else
                            engine = ourResource.Engines.FirstOrDefault(en => en.Filename == func.Path);

                        if (engine == null) continue;

                        if (string.IsNullOrWhiteSpace(func.EventName))
                        {
                            ExportedFunctionDelegate punchthrough = new ExportedFunctionDelegate((ExportedFunctionDelegate)
                                delegate (object[] parameters)
                                {
                                    return engine.InvokeMethod(func.Name, parameters);
                                });
                            resPoolDict.Add(func.Name, punchthrough);
                        }
                        else
                        {
                            var eventInfo = engine._compiledScript.GetType().GetEvent(func.EventName);

                            if (eventInfo == null)
                            {
                                Program.Output("WARN: Exported event " + func.EventName + " has not been found!");
                                if (LogLevel > 0)
                                {
                                    Program.Output("Available events:");
                                    Program.Output(string.Join(", ", engine._compiledScript.GetType().GetEvents().Select(ev => ev.Name)));
                                }
                            }
                            else
                            {

                                resPoolDict.Add(func.EventName, null);

                                ExportedEvent punchthrough = new ExportedEvent((ExportedEvent)
                                    delegate (dynamic[] parameters)
                                    {
                                        ExportedEvent e = resPoolDict[func.EventName] as ExportedEvent;

                                        if (e != null)
                                        {
                                            e.Invoke(parameters);
                                        }
                                    });

                                eventInfo.AddEventHandler(engine._compiledScript, punchthrough);
                            }
                        }
                    }

                    gPool.Add(ourResource.DirectoryName, resPool);
                }

                foreach (var engine in ourResource.Engines)
                {
                    engine.InvokeResourceStart();
                }

                var oldRes = new List<Resource>(RunningResources);
                lock (RunningResources) RunningResources.Add(ourResource);

                foreach (var resource in oldRes)
                {
                    resource.Engines.ForEach(en => en.InvokeServerResourceStart(ourResource.DirectoryName));
                }

                Program.Output("Resource " + ourResource.DirectoryName + " started!");
                return true;
            }
            catch (Exception ex)
            {
                Program.Output("ERROR STARTING RESOURCE " + resourceName);
                Program.Output(ex.ToString());
                return false;
            }
        }

        public bool StopResource(string resourceName, Resource[] resourceParent = null)
        {
            Resource ourRes;
            lock (RunningResources)
            {
                ourRes = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName);
                if (ourRes == null) return false;

                Program.Output("Stopping " + resourceName);

                RunningResources.Remove(ourRes);
            }

            ourRes.Engines.ForEach(en => en.InvokeResourceStop());

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.StopResource);
            msg.Write(resourceName);
            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            if (Gamemode == ourRes)
            {
                if (CurrentMap != null && CurrentMap != ourRes)
                {
                    StopResource(CurrentMap.DirectoryName);
                    CurrentMap = null;
                }

                Gamemode = null;
            }

            if (ourRes.MapEntities != null)
                foreach (var entity in ourRes.MapEntities)
                {
                    PublicAPI.deleteEntity(entity);
                }

            if (CurrentMap == ourRes) CurrentMap = null;

            var gPool = ExportedFunctions as IDictionary<string, object>;
            if (gPool != null && gPool.ContainsKey(ourRes.DirectoryName)) gPool.Remove(ourRes.DirectoryName);
            CommandHandler.Unregister(ourRes.DirectoryName);
            FileModule.ExportedFiles.Remove(resourceName);
            lock (RunningResources)
            {
                foreach (var resource in RunningResources)
                {
                    resource.Engines.ForEach(en => en.InvokeServerResourceStop(ourRes.DirectoryName));
                }
            }

            Program.Output("Stopped " + resourceName + "!");
            return true;
        }

        public IEnumerable<ClientsideScript> GetAllClientsideScripts()
        {
            var allScripts = new List<ClientsideScript>();

            lock (RunningResources)
            {
                foreach (var resource in RunningResources)
                {
                    allScripts.AddRange(resource.ClientsideScripts);
                }
            }

            return allScripts;
        }

        public static ResourceInfo GetStoppedResourceInfo(string resourceName)
        {
            if (!Directory.Exists("resources" + Path.DirectorySeparatorChar + resourceName))
                throw new FileNotFoundException("Resource does not exist.");

            var baseDir = "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar;

            if (!File.Exists(baseDir + "meta.xml"))
                throw new FileNotFoundException("meta.xml has not been found.");

            var xmlSer = new XmlSerializer(typeof(ResourceInfo));
            ResourceInfo currentResInfo;
            using (var str = File.OpenRead(baseDir + "meta.xml"))
                currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);

            return currentResInfo;
        }

        public ResourceInfo GetResourceInfo(string resourceName)
        {
            lock (RunningResources)
            {
                Resource runningResource;

                return (runningResource = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName)) != null ? runningResource.Info : GetStoppedResourceInfo(resourceName);
            }
        }

    }
}
