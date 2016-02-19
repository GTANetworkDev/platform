using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using Microsoft.ClearScript;
using Microsoft.ClearScript.Windows;

namespace MTAV
{
    public class JavascriptHook : Script
    {
        public JavascriptHook()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            ScriptEngines = new List<JScriptEngine>();
            ThreadJumper = new List<Action>();
        }

        public static List<JScriptEngine> ScriptEngines;

        public static List<Action> ThreadJumper;

        public static bool InvokeMessageEvent(string msg)
        {
            bool cont = false;
            bool output = true;
            ThreadJumper.Add(() =>
            {
                bool allow = true;
                if (msg.StartsWith("/"))
                {
                    lock (ScriptEngines)
                    {
                        allow = ScriptEngines.Aggregate(allow, (current, engine) => current && engine.Script.script.invokeChatCommand(msg));
                    }
                }
                else
                {
                    lock (ScriptEngines)
                    {
                        allow = ScriptEngines.Aggregate(allow, (current, engine) => current && engine.Script.script.invokeChatMessage(msg));
                    }
                }

                cont = true;
                output = allow;
            });

            while (!cont) Script.Yield();
            return output;
        }

        public void OnTick(object sender, EventArgs e)
        {
            if (ThreadJumper.Count > 0)
            {
                ThreadJumper.ForEach(a => a.Invoke());
                ThreadJumper.Clear();
            }

            lock (ScriptEngines)
            {
                foreach (var engine in ScriptEngines)
                {
                    try
                    {
                        engine.Script.script.invokeUpdate();
                    }  
                    catch (ScriptEngineException ex)
                    {
                        LogException(ex);
                    }
                }
            }
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            lock (ScriptEngines)
            {
                foreach (var engine in ScriptEngines)
                {
                    try
                    {
                        engine.Script.script.invokeKeyDown(sender, e);
                    }
                    catch (ScriptEngineException ex)
                    {
                        LogException(ex);
                    }
                }
            }
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            lock (ScriptEngines)
            {
                foreach (var engine in ScriptEngines)
                {
                    try
                    {
                        engine.Script.script.invokeKeyUp(sender, e);
                    }
                    catch (ScriptEngineException ex)
                    {
                        LogException(ex);
                    }
                }
            }
        }

        public static void StartScript(string script)
        {
            ThreadJumper.Add((() =>
            {
                var scriptEngine = new JScriptEngine();
                var collection = new HostTypeCollection(Assembly.LoadFrom("scripthookvdotnet.dll"),
                    Assembly.LoadFrom("scripts\\NativeUI.dll"));
                scriptEngine.AddHostObject("API", collection);
                scriptEngine.AddHostObject("host", new HostFunctions());
                scriptEngine.AddHostObject("script", new ScriptContext());
                scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
                scriptEngine.AddHostType("List", typeof(IList));
                scriptEngine.AddHostType("KeyEventArgs", typeof(KeyEventArgs));
                scriptEngine.AddHostType("Keys", typeof(Keys));
                scriptEngine.AddHostObject("Network", Main.NetEntityHandler);
                
                try
                {
                    scriptEngine.Execute(script);
                }
                catch (ScriptEngineException ex)
                {
                    LogException(ex);
                }
                finally
                {
                    scriptEngine.Script.script.invokeResourceStart();
                    lock (ScriptEngines) ScriptEngines.Add(scriptEngine);
                }
            }));
        }

        public static void StopAllScripts()
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    foreach (var engine in ScriptEngines)
                    {
                        engine.Script.script.invokeResourceStop();
                        engine.Dispose();
                    }

                    ScriptEngines.Clear();
                }
            });
        }

        private static void LogException(Exception ex)
        {
            Func<string, int, string[]> splitter = (string input, int everyN) =>
            {
                var list = new List<string>();
                for (int i = 0; i < input.Length; i += everyN)
                {
                    list.Add(input.Substring(i, Math.Min(everyN, input.Length - i)));
                }
                return list.ToArray();
            };

            UI.Notify("~r~~h~Clientside Javascript Error~h~~w~");

            foreach (var s in splitter(ex.Message, 99))
            {
                UI.Notify(s);
            }

            if (ex.InnerException != null)
                foreach (var s in splitter(ex.InnerException.Message, 99))
                {
                    UI.Notify(s);
                }
        }
    }

    public class ScriptContext
    {
        public enum ReturnType
        {
            Int = 0,
            UInt = 1,
            Long = 2,
            ULong = 3,
            String = 4,
            Vector3 = 5,
            Vector2 = 6,
            Float = 7
        }

        public void callNative(string hash, params object[] args)
        {
            Hash ourHash;
            if (!Hash.TryParse(hash, out ourHash))
                return;
            Function.Call(ourHash, args.Select(o => new InputArgument(o)).ToArray());
        }

        public object returnNative(string hash, int returnType, params object[] args)
        {
            Hash ourHash;
            if (!Hash.TryParse(hash, out ourHash))
                return null;
            var fArgs = args.Select(o => new InputArgument(o)).ToArray();
            switch ((ReturnType)returnType)
            {
                case ReturnType.Int:
                    return Function.Call<int>(ourHash, fArgs);
                case ReturnType.UInt:
                    return Function.Call<uint>(ourHash, fArgs);
                case ReturnType.Long:
                    return Function.Call<long>(ourHash, fArgs);
                case ReturnType.ULong:
                    return Function.Call<ulong>(ourHash, fArgs);
                case ReturnType.String:
                    return Function.Call<string>(ourHash, fArgs);
                case ReturnType.Vector3:
                    return Function.Call<Vector3>(ourHash, fArgs);
                case ReturnType.Vector2:
                    return Function.Call<Vector2>(ourHash, fArgs);
                case ReturnType.Float:
                    return Function.Call<float>(ourHash, fArgs);
                default:
                    return null;
            }
        }
        
        public bool isPed(int ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent);
        }

        public bool isVehicle(int ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_VEHICLE, ent);
        }

        public bool isProp(int ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_AN_OBJECT, ent);
        }

        public void triggerServerEvent(string eventName, params object[] arguments)
        {
            Main.TriggerServerEvent(eventName, arguments);
        }

        public delegate void ServerEventTrigger(string eventName, object[] arguments);
        public delegate void ChatEvent(string msg, CancelEventArgs cancelEv);

        public event EventHandler onResourceStart;
        public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event KeyEventHandler onKeyDown;
        public event KeyEventHandler onKeyUp;
        public event ServerEventTrigger onServerEventTrigger;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;

        internal bool invokeChatMessage(string msg)
        {
            var cancelEvent = new CancelEventArgs(false);
            onChatMessage?.Invoke(msg, cancelEvent);
            return !cancelEvent.Cancel;
        }

        internal bool invokeChatCommand(string msg)
        {
            var cancelEvent = new CancelEventArgs(false);
            onChatCommand?.Invoke(msg, cancelEvent);
            return !cancelEvent.Cancel;
        }

        internal void invokeServerEvent(string eventName, object[] arsg)
        {
            onServerEventTrigger?.Invoke(eventName, arsg);
        }

        internal void invokeResourceStart()
        {
            onResourceStart?.Invoke(this, EventArgs.Empty);
        }

        internal void invokeUpdate()
        {
            onUpdate?.Invoke(this, EventArgs.Empty);
        }

        internal void invokeResourceStop()
        {
            onResourceStop?.Invoke(this, EventArgs.Empty);
        }

        internal void invokeKeyDown(object sender, KeyEventArgs e)
        {
            onKeyDown?.Invoke(sender, e);
        }

        internal void invokeKeyUp(object sender, KeyEventArgs e)
        {
            onKeyUp?.Invoke(sender, e);
        }
    }

}