using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTANetworkShared;
using Microsoft.ClearScript;
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

        private Thread _workerThread;
        private JScriptEngine _jsEngine;
        private Script _compiledScript;
        private Queue<Action> _mainQueue;
        private bool _hasToStop = false;
        private TaskFactory _factory;

        public ScriptingEngine(string javascript, string name, string[] references)
        {
            _mainQueue = new Queue<Action>();
            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            Language = ScriptingEngineLanguage.javascript;
            Filename = name;
            
            _mainQueue.Enqueue(() =>
            {
                _jsEngine = InstantiateScripts(javascript, name, references);
            });
        }

        public ScriptingEngine(Script sc, string name)
        {
            _factory = new TaskFactory();
            _mainQueue = new Queue<Action>();
            _workerThread = new Thread(MainThreadLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            Language = ScriptingEngineLanguage.compiled;
            Filename = name;

            _mainQueue.Enqueue(() =>
            {
                _compiledScript = sc;
            });
        }

        private JScriptEngine InstantiateScripts(string script, string resourceName, string[] refs)
        {
            var scriptEngine = new JScriptEngine();

            var collect = new HostTypeCollection(refs);

            scriptEngine.AddHostObject("clr", collect);
            scriptEngine.AddHostObject("API", new API());
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostType("Dictionary", typeof(Dictionary<,>));
            scriptEngine.AddHostType("xmlParser", typeof(RetardedXMLParser));
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
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
                _hasToStop = true;
                return null;
            }

            return scriptEngine;
        }


        private void MainThreadLoop()
        {
            while (!_hasToStop)
            {
                if (_mainQueue.Count > 0)
                {
                    lock (_mainQueue)
                    {
                        if (Language == ScriptingEngineLanguage.compiled)
                        {
                            _factory.StartNew(() =>
                            {
                                _mainQueue.Dequeue()?.Invoke();
                            });
                        }
                        else
                            _mainQueue.Dequeue()?.Invoke();
                    }
                }

                Thread.Sleep(10);
            }
        }


        #region Interface

        public object InvokeMethod(string method, object[] args)
        {
            try
            {
                object objectToReturn = null;
                _mainQueue.Enqueue(() =>
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
                });

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
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeResourceStart();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeResourceStart();
            });
        }

        public void InvokeResourceStop()
        {
            bool canContinue = false;
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeResourceStop();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeResourceStop();
                canContinue = true;
            });
            while (!canContinue) { Thread.Sleep(10); }
        }

        public void InvokePlayerBeginConnect(Client client)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerBeginConnect(client);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerBeginConnect(client);
            });
        }

        public void InvokePlayerDisconnected(Client client, string reason)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerDisconnected(client, reason);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDisconnected(client, reason);
            });
        }

        public void InvokePlayerDownloadFinished(Client client)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeFinishedDownload(client);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeFinishedDownload(client);
            });
        }

        public void InvokeChatCommand(Client sender, string command)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                {
                    _jsEngine.Script.API.invokeChatCommand(sender, command);
                }
                else if (Language == ScriptingEngineLanguage.compiled)
                {
                    _compiledScript.API.invokeChatCommand(sender, command);
                }
            });
        }

        public bool InvokeChatMessage(Client sender, string cmd)
        {
            bool? passThroughMessage = null;
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    passThroughMessage = _jsEngine.Script.API.invokeChatMessage(sender, cmd);
                else if (Language == ScriptingEngineLanguage.compiled)
                    passThroughMessage = _compiledScript.API.invokeChatMessage(sender, cmd);
            });
            var counter = 0;
            while (counter < 50 && !passThroughMessage.HasValue) { }

            return passThroughMessage ?? true;
        }

        public void InvokeClientEvent(Client sender, string eventName, object[] args)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeClientEvent(sender, eventName, args);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeClientEvent(sender, eventName, args);
            });
        }

        public void InvokePlayerConnected(Client sender)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerConnected(sender);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerConnected(sender);
            });
        }

        public void InvokePlayerDeath(Client killed, int reason, int weapon)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerDeath(killed, reason, weapon);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerDeath(killed, reason, weapon);
            });
        }

        public void InvokePlayerRespawn(Client sender)
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokePlayerRespawn(sender);
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokePlayerRespawn(sender);
            });
        }

        public void InvokeUpdate()
        {
            _mainQueue.Enqueue(() =>
            {
                if (Language == ScriptingEngineLanguage.javascript)
                    _jsEngine.Script.API.invokeUpdate();
                else if (Language == ScriptingEngineLanguage.compiled)
                    _compiledScript.API.invokeUpdate();
            });
        }

        #endregion
    }

    public class Resource
    {
        public string DirectoryName { get; set; }
        public ResourceInfo Info { get; set; }
        public List<ScriptingEngine> Engines { get; set; }
    }

    public enum ResourceType
    {
        server,
        client
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
        public ResourceType Type { get; set; }
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
            Type = "script";
        }

        [XmlAttribute("author")]
        public string Author { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }
    }
}