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
        python
    }

    public class ScriptingEngine
    {
        public ScriptingEngineLanguage Language { get; private set; }
        public string Filename { get; set; }

        private List<Thread> ActiveThreads;
        private Thread _workerThread;
        private V8ScriptEngine _jsEngine;
        private Script _compiledScript;
        private Queue _mainQueue;
        public bool HasTerminated = false;

        public ScriptingEngine(string javascript, string name, string[] references)
        {
            ActiveThreads = new List<Thread>();
            _mainQueue = Queue.Synchronized(new Queue());
            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            Language = ScriptingEngineLanguage.javascript;
            Filename = name;
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                _jsEngine = InstantiateScripts(javascript, name, references);
                _jsEngine.Script.API.ResourceParent = name;
            }));
        }

        public ScriptingEngine(Script sc, string name)
        {
            ActiveThreads = new List<Thread>();
            _mainQueue = Queue.Synchronized(new Queue());
            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            Language = ScriptingEngineLanguage.compiled;
            Filename = name;
            
            
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                _compiledScript = sc;
                _compiledScript.API.ResourceParent = name;
            }));
        }

        private V8ScriptEngine InstantiateScripts(string script, string resourceName, string[] refs)
        {
            var scriptEngine = new V8ScriptEngine();

            var collect = new HostTypeCollection(refs);

            scriptEngine.AddHostObject("clr", collect);
            scriptEngine.AddHostObject("API", new API());
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostType("Dictionary", typeof(Dictionary<,>));
            scriptEngine.AddHostType("xmlParser", typeof(RetardedXMLParser));
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
                while (_mainQueue.Count > 0)
                {
                    Action mainAction;
                    lock (_mainQueue.SyncRoot)
                    {
                       mainAction = (_mainQueue.Dequeue() as Action);
                    }

                    if (mainAction != null)
                    {
                        var t = new Thread(mainAction.Invoke);
                        t.IsBackground = true;
                        lock (ActiveThreads) ActiveThreads.Add(t);
                        t.Start();
                    }
                    //mainAction?.Invoke();
                }

                Thread.Sleep(10);
                lock (ActiveThreads) ActiveThreads.RemoveAll(t => !t.IsAlive);
            }
        }


        #region Interface

        public object InvokeMethod(string method, object[] args)
        {
            try
            {
                object objectToReturn = null;
                lock (_mainQueue.SyncRoot)
                _mainQueue.Enqueue(new Action(() =>
                {
                    if (Language == ScriptingEngineLanguage.compiled)
                    {
                        var mi = _compiledScript.GetType().GetMethod(method);
                        objectToReturn =  mi.Invoke(_compiledScript, args.Length == 0 ? null : args);
                    }
                    else if (Language == ScriptingEngineLanguage.javascript)
                    {
                        var mi = ((object) _jsEngine.Script).GetType().GetMethod(method);
                        objectToReturn = mi.Invoke(_compiledScript, args.Length == 0 ? null : args);
                    }
                }));

                var counter = 0;
                while (counter < 50 && objectToReturn == null)
                    counter++;

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
                ActiveThreads.ForEach(t => t.Abort());
                ActiveThreads.Clear();
            }
            HasTerminated = true;
        }

        public void InvokePlayerBeginConnect(Client client)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerBeginConnect(client);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerBeginConnect(client);
            }));
        }

        public void InvokePlayerDisconnected(Client client, string reason)
        {
            lock (_mainQueue.SyncRoot)
            _mainQueue.Enqueue(new Action(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerDisconnected(client, reason);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDisconnected(client, reason);
            }));
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
            var counter = 0;
            while (counter < 50 && !passThroughMessage.HasValue) { }

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
    }

    [XmlRoot("assembly")]
    public class AssemblyReferences
    {
        [XmlAttribute("ref")]
        public string Name { get; set; }
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
    }
}