using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkShared;

namespace GTANetworkServer
{
    public enum ScriptingEngineLanguage
    {
        javascript,
        compiled,
        csharp,
        vbasic,
    }

    internal class ScriptingEngine
    {
        public ScriptingEngineLanguage Language { get; private set; }
        public string Filename { get; set; }
        public Resource ResourceParent { get; set; }
        public List<Thread> ActiveThreads = new List<Thread>();
        public bool Async = false;

        internal Script _compiledScript;
        private Thread _workerThread;
        private Thread _blockingThread;
        private Queue _mainQueue;
        private Queue _secondaryQueue;
        public bool HasTerminated = false;
        
        public Script GetAssembly
        {
            get
            {
                return _compiledScript;
            }
        }

        public ScriptingEngine(Script sc, string name, Resource parent, bool async)
        {
            Async = async;
            ResourceParent = parent;
            _mainQueue = Queue.Synchronized(new Queue());
            _secondaryQueue = Queue.Synchronized(new Queue());

            Language = ScriptingEngineLanguage.compiled;
            Filename = name;

            _compiledScript = sc;
            _compiledScript.API.ResourceParent = this;

            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            _blockingThread = new Thread(SecondaryThreadLoop);
            _blockingThread.IsBackground = true;
            _blockingThread.Start();
        }
        
        private void MainThreadLoop()
        {
            while (!HasTerminated)
            {
                Queue localCopy;
                lock (_mainQueue.SyncRoot)
                {
                    localCopy = new Queue(_mainQueue);
                    _mainQueue.Clear();
                }


                while (localCopy.Count > 0)
                {
                    Action mainAction;

                    mainAction = (localCopy.Dequeue() as Action);

                    if (mainAction != null)
                    {
                        try
                        {
                            if (Async)
                            {
                                ThreadPool.QueueUserWorkItem((WaitCallback) delegate
                                {
                                    try
                                    {
                                        mainAction?.Invoke();
                                    }
                                    catch (ResourceAbortedException)
                                    {}
                                    catch (ThreadAbortException)
                                    {}
                                    catch (Exception ex)
                                    {
                                        Program.Output("EXCEPTION IN RESOURCE " + ResourceParent.DirectoryName + " INSIDE SCRIPTENGINE " + Filename);
                                        Program.Output(ex.ToString());
                                    }
                                });
                            }
                            else
                            {
                                mainAction?.Invoke();
                            }
                        }
                        catch (ThreadAbortException) { }
                        catch (ResourceAbortedException) { }
                        catch (Exception ex)
                        {
                            Program.Output("EXCEPTION IN RESOURCE " + ResourceParent.DirectoryName + " INSIDE SCRIPTENGINE " + Filename);
                            Program.Output(ex.ToString());
                        }
                    }
                }

                Thread.Sleep(10);
                lock (ActiveThreads) ActiveThreads.RemoveAll(t => t == null || !t.IsAlive);
            }
        }

        private void SecondaryThreadLoop()
        {
            if (!Async) return;

            while (!HasTerminated)
            {
                try
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokeUpdate();
                }
                catch (ThreadAbortException) { }
                catch (ResourceAbortedException) { }
                catch (Exception ex)
                {
                    Program.Output("EXCEPTION IN RESOURCE " + ResourceParent.DirectoryName + " INSIDE SCRIPTENGINE " + Filename);
                    Program.Output(ex.ToString());
                }

                Thread.Sleep(16);
            }
        }

        internal void AddTrackedThread(Thread th)
        {
            lock (ActiveThreads)
            {
                ActiveThreads.Add(th);
            }
        }

        #region Interface

        public void InvokeVoidMethod(string method, object[] args)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                {
                    var mi = _compiledScript.GetType().GetMethod(method);
                    if (mi == null)
                    {
                        Program.Output("METHOD NOT ACCESSIBLE OR NOT FOUND: " + method);
                        return;
                    }

                    mi.Invoke(_compiledScript, args == null ? null : args.Length == 0 ? null : args);
                }
          }));
            
        }

        public dynamic InvokeMethod(string method, object[] args)
        {
            try
            {
                object objectToReturn = null;
                bool hasValue = false;
                lock (_mainQueue.SyncRoot)
                _mainQueue.Enqueue(new Action(() =>
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                    {
                        var mi = _compiledScript.GetType().GetMethod(method);
                        if (mi == null)
                        {
                            Program.Output("METHOD NOT ACCESSIBLE OR NOT FOUND: " + method);
                            hasValue = true;
                            return;
                        }
                        
                        objectToReturn = mi.Invoke(_compiledScript, args == null ? null : args.Length == 0 ? null : args);
                        hasValue = true;
                    }
                }));

                while (!hasValue) { }

                return objectToReturn;
            }
            catch (Exception e)
            {
                Program.Output("ERROR: Method invocation failed for method " + method + "! (" + e.Message + ")");
            }
            return null;
        }

        public void InvokeResourceStart()
        {
            /*
            // Sync resourceStart to make sure dependencies are ready?
            Task shutdownTask = new Task(() =>
            {
                try
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokeResourceStart();
                }
                catch (Exception ex)
                {
                    Program.Output("Unhandled exception caught in " + Filename + " from resource " +
                                   ResourceParent.DirectoryName + "\r\n" + ex.ToString());
                }
            });

            shutdownTask.Start();
            shutdownTask.Wait(20000);
            */
            //*
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeResourceStart();
            }));
            //*/
        }

        public void InvokeEntityDataChange(NetHandle ent, string key, object oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeOnEntityDataChange(ent, key, oldValue);
            }));
        }

        public void InvokeResourceStop()
        {
            _workerThread.Abort();
            _blockingThread.Abort();

            HasTerminated = true;

            lock (ActiveThreads)
            {
                ActiveThreads.Where(t => t != null && t.IsAlive).ToList().ForEach(t => t.Abort());
                ActiveThreads.Clear();
            }

            Task shutdownTask = new Task(() =>
            {
                try
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokeResourceStop();
                }
                catch (Exception ex)
                {
                    Program.Output("Unhandled exception caught in " + Filename + " from resource " +
                                   ResourceParent.DirectoryName + "\r\n" + ex.ToString());
                }
            });

            shutdownTask.Start();
            shutdownTask.Wait(5000);
        }

        public void InvokePlayerBeginConnect(Client client, CancelEventArgs e)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerBeginConnect(client, e);
            }));
        }

        public void InvokePlayerEnterVehicle(Client client, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerEnterVeh(client, veh);
            }));
        }

        public void InvokeServerResourceStart(string resource)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeServerResourceStart(resource);
            }));
        }

        public void InvokeVehicleTrailerChange(NetHandle entity, NetHandle trailer)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleTrailerChange(entity, trailer);
            }));
        }

        public void InvokePlayerDetonateStickies(Client player)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDetonateStickies(player);
            }));
        }

        public void InvokePlayerModelChange(Client player, int oldModel)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerModelChange(player, oldModel);
            }));
        }

        public void InvokeVehicleTyreBurst(NetHandle entity, int index)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleTyreBurst(entity, index);
            }));
        }

        public void InvokeVehicleDoorBreak(NetHandle entity, int index)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleDoorBreak(entity, index);
            }));
        }

        public void InvokeVehicleWindowBreak(NetHandle entity, int index)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleWindowBreak(entity, index);
            }));
        }

        public void InvokeVehicleSirenToggle(NetHandle entity, bool oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleSirenToggle(entity, oldValue);
            }));
        }

        public void InvokeVehicleHealthChange(NetHandle player, float oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleHealthChange(player, oldValue);
            }));
        }

        public void InvokePlayerWeaponChange(Client player, int oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerWeaponSwitch(player, oldValue);
            }));
        }

        public void InvokePlayerWeaponAmmoChange(Client player, int weapon, int oldValue)
        {
            lock (_mainQueue.SyncRoot)
                _mainQueue.Enqueue(new Action(() =>
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokePlayerWeaponAmmoChange(player, weapon, oldValue);
                }));
        }

        public void InvokePlayerArmorChange(Client player, int oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerArmorChange(player, oldValue);
            }));
        }

        public void InvokePlayerHealthChange(Client player, int oldValue)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerHealthChange(player, oldValue);
            }));
        }

        public void InvokeServerResourceStop(string resource)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeServerResourceStop(resource);
            }));
        }

        public void InvokeCustomDataReceive(string resource)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeCustomDataReceive(resource);
            }));
        }

        public void InvokePlayerExitVehicle(Client client, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerExitVeh(client, veh);
            }));
        }

        public void InvokeVehicleDeath(NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleDeath(veh);
            }));
        }

        public void InvokeColshapeEnter(ColShape shape, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeColShapeEnter(shape, veh);
            }));
        }

        public void InvokeColshapeExit(ColShape shape, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeColShapeExit(shape, veh);
            }));
        }

        public void InvokeMapChange(string mapName, XmlGroup map)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeMapChange(mapName, map);
            }));
        }

        public void InvokePlayerDisconnected(Client client, string reason)
        {
            Task shutdownTask = new Task(() =>
            {
                try
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokePlayerDisconnected(client, reason);
                }
                catch (Exception ex)
                {
                    Program.Output("Unhandled exception caught in " + Filename + " from resource " +
                                   ResourceParent.DirectoryName + "\r\n" + ex.ToString());
                }
            });

            shutdownTask.Start();
            shutdownTask.Wait(5000);
        }

        public void InvokePlayerDownloadFinished(Client client)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeFinishedDownload(client);
            }));
        }

        public void InvokePlayerPickup(Client client, NetHandle pickup)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerPickup(client, pickup);
            }));
        }

        public void InvokePickupRespawn(NetHandle pickup)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePickupRespawn(pickup);
            }));
        }

        public void InvokeChatCommand(Client sender, string command, CancelEventArgs ce)
        {
            Task cmdTask = new Task(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                {
                    _compiledScript.API.invokeChatCommand(sender, command, ce);
                }
            });

            cmdTask.Start();
            cmdTask.Wait(5000);
        }

        public bool InvokeChatMessage(Client sender, string cmd)
        {
            Task<bool> shutdownTask = new Task<bool>(() =>
            {
                try
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        return _compiledScript.API.invokeChatMessage(sender, cmd);
                }
                catch (Exception ex)
                {
                    Program.Output("Unhandled exception caught in " + Filename + " from resource " +
                                   ResourceParent.DirectoryName + "\r\n" + ex.ToString());
                }
                return true;
            });

            shutdownTask.Start();
            shutdownTask.Wait(5000);
            return shutdownTask.Result;
        }

        public void InvokeClientEvent(Client sender, string eventName, object[] args)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeClientEvent(sender, eventName, args);
            }));
        }

        public void InvokePlayerConnected(Client sender)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerConnected(sender);
            }));
        }

        public void InvokePlayerDeath(Client killed, int reason, int weapon)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDeath(killed, new NetHandle(reason), weapon);
            }));
        }

        public void InvokePlayerRespawn(Client sender)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerRespawn(sender);
            }));
        }

        public void InvokeUpdate()
        {
            if (Async) return;
            lock (_mainQueue.SyncRoot)
            {
                _mainQueue.Enqueue(new Action(() =>
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokeUpdate();
                }));
            }
        }

        #endregion
    }

    internal class Resource
    {
        public string DirectoryName { get; set; }
        public ResourceInfo Info { get; set; }
        public List<ScriptingEngine> Engines { get; set; }
        public List<ClientsideScript> ClientsideScripts { get; set; }
        public XmlGroup Map { get; set; }

        internal List<NetHandle> MapEntities { get; set; }
        internal Dictionary<string, CustomSetting> Settings { get; set; }
    }

    public struct CustomSetting
    {
        public string Value;
        public string DefaultValue;
        public string Description;

        public object CastObject;
        public bool HasValue;
    }


    public enum ScriptType
    {
        server,
        client
    }

    public enum ResourceType
    {
        script,
        gamemode,
        map
    }

    [XmlRoot("meta"), Serializable]
    public class ResourceInfo
    {
        [XmlElement("info")]
        public ResourceMetaInfo Info { get; set; }

        [XmlElement("script")]
        public List<ResourceScript> Scripts { get; set; }

        [XmlElement("file")]
        public List<FilePath> Files { get; set; }

        [XmlElement("assembly")]
        public List<AssemblyReferences> References { get; set; }

        [XmlElement("include")]
        public List<RequiredResource> Includes { get; set; }

        [XmlElement("map")]
        public MapSource Map { get; set; }

        [XmlElement("export")]
        public List<MethodExport> ExportedFunctions { get; set; }

        [XmlElement("acl")]
        public ResourceAcl ResourceACL { get; set; }

        public ResourceSettingsMeta settings { get; set; }

        [XmlElement("config")]
        public List<ResourceConfigFile> ConfigFiles { get; set; }
    }

    public class ResourceConfigFile
    {
        [XmlAttribute("src")]
        public string Path { get; set; }

        [XmlAttribute("type")]
        public ScriptType Type { get; set; }
    }

    [XmlRoot("settings")]
    public class ResourceSettingsMeta
    {
        [XmlAttribute("src")]
        public string Path { get; set; }

        // OR

        [XmlElement("setting")]
        public List<MetaSetting> Settings { get; set; }
    }

    [XmlRoot("settings")]
    public class ResourceSettingsFile
    {
        [XmlElement("setting")]
        public List<MetaSetting> Settings { get; set; }
    }

    public class MetaSetting
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }

        [XmlAttribute("default")]
        public string DefaultValue { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }
    }

    [XmlRoot("acl")]
    public class ResourceAcl
    {
        [XmlAttribute("src")]
        public string Path { get; set; }
    }

    [XmlRoot("export")]
    public class MethodExport
    {
        [XmlAttribute("class")]
        public string Path { get; set; }

        [XmlAttribute("function")]
        public string Name { get; set; }

        [XmlAttribute("event")]
        public string EventName { get; set; }
    }

    [XmlRoot("map")]
    public class MapSource
    {
        [XmlAttribute("src")]
        public string Path { get; set; }

        [XmlAttribute("dimension")]
        public int Dimension { get; set; }
    }

    [XmlRoot("assembly")]
    public class AssemblyReferences
    {
        [XmlAttribute("ref")]
        public string Name { get; set; }
    }

    [XmlRoot("include")]
    public class RequiredResource
    {
        [XmlAttribute("resource")]
        public string Resource { get; set; }
    }

    [XmlRoot("script")]
    public class ResourceScript
    {
        public ResourceScript()
        {
            Language = ScriptingEngineLanguage.javascript;
        }

        [XmlAttribute("src")]
        public string Path { get; set; }
        [XmlAttribute("type")]
        public ScriptType Type { get; set; }
        [XmlAttribute("lang")]
        public ScriptingEngineLanguage Language { get; set; }
    }

    [XmlRoot("file")]
    public class FilePath
    {
        [XmlAttribute("src")]
        public string Path { get; set; }
    }


    [XmlRoot("info")]
    public class ResourceMetaInfo
    {
        public ResourceMetaInfo()
        {
            Type = ResourceType.script;
            Multithreaded = false;
            Name = "";
        }

        [XmlAttribute("author")]
        public string Author { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }

        [XmlAttribute("type")]
        public ResourceType Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlAttribute("gamemodes")]
        public string Gamemodes { get; set; }

        [XmlAttribute("async")]
        public bool Multithreaded { get; set; }

        [XmlAttribute("shadowcopy")]
        public bool Shadowcopy { get; set; }
    }
}