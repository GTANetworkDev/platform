using System;
using System.Collections.Generic;
using System.Xml.Serialization;
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

        private JScriptEngine _jsEngine;
        private Script _compiledScript;

        public ScriptingEngine(JScriptEngine en, string name)
        {
            _jsEngine = en;
            Language = ScriptingEngineLanguage.javascript;
            Filename = name;
        }

        public ScriptingEngine(Script sc, string name)
        {
            _compiledScript = sc;
            Language = ScriptingEngineLanguage.compiled;
            Filename = name;
        }

        #region Interface

        public object InvokeMethod(string method, object[] args)
        {
            try
            {
                if (Language == ScriptingEngineLanguage.compiled)
                {
                    var mi = _compiledScript.GetType().GetMethod(method);
                    return mi.Invoke(_compiledScript, args.Length == 0 ? null : args);
                }
                else if (Language == ScriptingEngineLanguage.javascript)
                {
                    var mi = ((object)_jsEngine.Script).GetType().GetMethod(method);
                    return mi.Invoke(_compiledScript, args.Length == 0 ? null : args);
                }
            }
            catch (Exception e)
            {
                Program.Output("ERROR: Method invocation failed for method " + method + "! (" + e.Message + ")");
            }
            return null;
        }

        public void InvokeResourceStart()
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokeResourceStart();
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokeResourceStart();
        }

        public void InvokeResourceStop()
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokeResourceStop();
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokeResourceStop();
        }

        public void InvokePlayerBeginConnect(Client client)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokePlayerBeginConnect(client);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokePlayerBeginConnect(client);
        }

        public void InvokePlayerDisconnected(Client client, string reason)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokePlayerDisconnected(client, reason);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokePlayerDisconnected(client, reason);
        }

        public void InvokeChatCommand(Client sender, string command)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokeChatCommand(sender, command);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokeChatCommand(sender, command);
        }

        public bool InvokeChatMessage(Client sender, string cmd)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                return _jsEngine.Script.API.invokeChatMessage(sender, cmd);
            if (Language == ScriptingEngineLanguage.compiled)
                return _compiledScript.API.invokeChatMessage(sender, cmd);
            return true;
        }

        public void InvokeClientEvent(Client sender, string eventName, object[] args)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokeClientEvent(sender, eventName, args);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokeClientEvent(sender, eventName, args);
        }

        public void InvokePlayerConnected(Client sender)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokePlayerConnected(sender);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokePlayerConnected(sender);
        }

        public void InvokePlayerDeath(Client killed, int reason, int weapon)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokePlayerDeath(killed, reason, weapon);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokePlayerDeath(killed, reason, weapon);
        }

        public void InvokePlayerRespawn(Client sender)
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokePlayerRespawn(sender);
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokePlayerRespawn(sender);
        }

        public void InvokeUpdate()
        {
            if (Language == ScriptingEngineLanguage.javascript)
                _jsEngine.Script.API.invokeUpdate();
            else if (Language == ScriptingEngineLanguage.compiled)
                _compiledScript.API.invokeUpdate();
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