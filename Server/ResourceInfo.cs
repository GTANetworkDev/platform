using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkShared;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript.Windows;

namespace GTANetworkServer
{
    public enum ScriptingEngineLanguage
    {
        javascript,
        compiled,
        csharp,
        vbasic,
    }

    public class ScriptingEngine
    {
        public ScriptingEngineLanguage Language { get; private set; }
        public string Filename { get; set; }
        public Resource ResourceParent { get; set; }
        public List<Thread> ActiveThreads = new List<Thread>();
        public bool Async = false;

        private Thread _workerThread;
        private Thread _blockingThread;
        private JScriptEngine _jsEngine;
        private Script _compiledScript;
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
        public ScriptingEngine(string javascript, string name, Resource parent, string[] references, bool async)
        {
            Async = async;
            ResourceParent = parent;
            _mainQueue = Queue.Synchronized(new Queue());
            _secondaryQueue = Queue.Synchronized(new Queue());

            Language = ScriptingEngineLanguage.javascript;
            Filename = name;
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                _jsEngine = InstantiateScripts(javascript, name, references);
            }));

            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            _blockingThread = new Thread(SecondaryThreadLoop);
            _blockingThread.IsBackground = true;
            _blockingThread.Start();
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

        private JScriptEngine InstantiateScripts(string script, string resourceName, string[] refs)
        {
            var scriptEngine = new JScriptEngine();
            var collect = new HostTypeCollection(refs);

            scriptEngine.AddHostObject("clr", collect);
            scriptEngine.AddHostObject("API", new API() { ResourceParent = this});
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostType("Dictionary", typeof(Dictionary<,>));
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
            scriptEngine.AddHostType("NetHandle", typeof(NetHandle));
            scriptEngine.AddHostType("String", typeof(string));
            scriptEngine.AddHostType("List", typeof(List<>));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("Vector3", typeof(Vector3));
            scriptEngine.AddHostType("Quaternion", typeof(Vector3));
            scriptEngine.AddHostType("Client", typeof(Client));
            scriptEngine.AddHostType("LocalPlayerArgument", typeof(LocalPlayerArgument));
            scriptEngine.AddHostType("LocalGamePlayerArgument", typeof(LocalGamePlayerArgument));
            scriptEngine.AddHostType("EntityArgument", typeof(EntityArgument));
            scriptEngine.AddHostType("EntityPointerArgument", typeof(EntityPointerArgument));
            scriptEngine.AddHostType("console", typeof(Console));
            scriptEngine.AddHostType("VehicleHash", typeof(VehicleHash));
            scriptEngine.AddHostType("Int32", typeof(int));
            scriptEngine.AddHostType("EntityArgument", typeof(EntityArgument));
            scriptEngine.AddHostType("EntityPtrArgument", typeof(EntityPointerArgument));

            try
            {
                scriptEngine.Execute(script);
            }
            catch (ScriptEngineException ex)
            {
                Program.Output("EXCEPTION WHEN COMPILING JAVASCRIPT " + Filename);
                Program.Output(ex.Message);
                Program.Output(ex.StackTrace);
                HasTerminated = true;
                throw;
            }

            return scriptEngine;
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
                        if (Async)
                        {
                            ThreadPool.QueueUserWorkItem((WaitCallback) delegate
                            {
                                mainAction?.Invoke();
                            });
                        }
                        else
                        {
                            mainAction?.Invoke();
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
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeUpdate();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeUpdate();

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
                else if (Language == ScriptingEngineLanguage.javascript)
                {
                    var mi = ((object)_jsEngine.Script).GetType().GetMethod(method);
                    mi.Invoke(_compiledScript, args == null ? null : args.Length == 0 ? null : args);
                }
            }));
            
        }

        public dynamic InvokeMethod(string method, object[] args)
        {
            try
            {
                dynamic objectToReturn = null;
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
                    else if (Language == ScriptingEngineLanguage.javascript)
                    {
                        var mi = ((object) _jsEngine.Script).GetType().GetMethod(method);
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
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeResourceStart();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeResourceStart();
            }));
        }

        public void InvokeResourceStop()
        {
            bool canContinue = false;
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeResourceStop();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeResourceStop();
                canContinue = true;
            }));
            DateTime start = DateTime.Now;
            while (!canContinue && DateTime.Now.Subtract(start).TotalMilliseconds < 10000) { Thread.Sleep(10); }
            lock (ActiveThreads)
            {
                ActiveThreads.Where(t => t != null && t.IsAlive).ToList().ForEach(t => t.Abort());
                ActiveThreads.Clear();
            }
            HasTerminated = true;
        }

        public void InvokePlayerBeginConnect(Client client, CancelEventArgs e)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerBeginConnect(client);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerBeginConnect(client, e);
            }));
        }

        public void InvokePlayerEnterVehicle(Client client, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerEnterVeh(client, veh);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerEnterVeh(client, veh);
            }));
        }

        public void InvokePlayerExitVehicle(Client client, NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerExitVeh(client, veh);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerExitVeh(client, veh);
            }));
        }

        public void InvokeVehicleDeath(NetHandle veh)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeVehicleDeath(veh);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeVehicleDeath(veh);
            }));
        }

        public void InvokeMapChange(string mapName, XmlGroup map)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeMapChange(mapName, map);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeMapChange(mapName, map);
            }));
        }

        public void InvokePlayerDisconnected(Client client, string reason)
        {
            bool canContinue = false;

            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerDisconnected(client, reason);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDisconnected(client, reason);

                canContinue = true;
            }));

            DateTime start = DateTime.Now;
            while (!canContinue && DateTime.Now.Subtract(start).TotalMilliseconds < 10000) { Thread.Sleep(10); }
        }

        public void InvokePlayerDownloadFinished(Client client)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeFinishedDownload(client);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeFinishedDownload(client);
            }));
        }

        public void InvokePlayerPickup(Client client, NetHandle pickup)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerPickup(client, pickup);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerPickup(client, pickup);
            }));
        }

        public void InvokePickupRespawn(NetHandle pickup)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePickupRespawn(pickup);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePickupRespawn(pickup);
            }));
        }

        public void InvokeChatCommand(Client sender, string command)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                {
                    _jsEngine.Script.API.invokeChatCommand(sender, command);
                }
                else if (Language == ScriptingEngineLanguage.compiled)
                {
                    _compiledScript.API.invokeChatCommand(sender, command);
                }
            }));
        }

        public bool InvokeChatMessage(Client sender, string cmd)
        {
            bool? passThroughMessage = null;
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    passThroughMessage = _jsEngine.Script.API.invokeChatMessage(sender, cmd);
                else if (Language == ScriptingEngineLanguage.compiled)
                    passThroughMessage = _compiledScript.API.invokeChatMessage(sender, cmd);
            }));
            int counter = Environment.TickCount;
            while (Environment.TickCount - counter < 5000 && !passThroughMessage.HasValue) { }

            return passThroughMessage ?? true;
        }

        public void InvokeClientEvent(Client sender, string eventName, object[] args)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeClientEvent(sender, eventName, args);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeClientEvent(sender, eventName, args);
            }));
        }

        public void InvokePlayerConnected(Client sender)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerConnected(sender);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerConnected(sender);
            }));
        }

        public void InvokePlayerDeath(Client killed, int reason, int weapon)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerDeath(killed, new NetHandle(reason), weapon);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDeath(killed, new NetHandle(reason), weapon);
            }));
        }

        public void InvokePlayerRespawn(Client sender)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerRespawn(sender);
                else if (Language == ScriptingEngineLanguage.compiled)
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
                    if (Language == ScriptingEngineLanguage.javascript)
                        _jsEngine.Script.API.invokeUpdate();
                    else if (Language == ScriptingEngineLanguage.compiled)
                        _compiledScript.API.invokeUpdate();
                }));
            }
        }

        #endregion
    }

    public class Resource
    {
        public string DirectoryName { get; set; }
        public ResourceInfo Info { get; set; }
        public List<ScriptingEngine> Engines { get; set; }
        public List<ClientsideScript> ClientsideScripts { get; set; }
        public XmlGroup Map { get; set; }

        internal List<NetHandle> MapEntities { get; set; }
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
        public List<AssemblyReferences> Referenceses { get; set; }

        [XmlElement("include")]
        public List<RequiredResource> Includes { get; set; }

        [XmlElement("map")]
        public MapSource Map { get; set; }

        [XmlElement("export")]
        public List<MethodExport> ExportedFunctions { get; set; }

        [XmlElement("acl")]
        public ResourceAcl ResourceACL { get; set; }
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
    }
}