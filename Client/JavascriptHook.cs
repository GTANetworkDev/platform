using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
using NativeUI;

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

        public static void InvokeServerEvent(string eventName, object[] arguments)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines) ScriptEngines.ForEach(en => en.Script.script.invokeServerEvent(eventName, arguments));
            });
        }

        public static void InvokeMessageEvent(string msg)
        {
            if (msg == null) return;
            ThreadJumper.Add(() =>
            {
                if (msg.StartsWith("/"))
                {
                    lock (ScriptEngines)
                    {
                        ScriptEngines.ForEach(en => en.Script.script.invokeChatCommand(msg));
                    }
                }
                else
                {
                    lock (ScriptEngines)
                    {
                        ScriptEngines.ForEach(en => en.Script.script.invokeChatMessage(msg));
                    }
                }
            });
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
                scriptEngine.AddHostType("Point", typeof(Point));
                scriptEngine.AddHostType("Size", typeof(Size));
                
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
                DownloadManager.Log(s);
            }

            var innEx = ex.InnerException;
            while (innEx != null)
            {
                foreach (var s in splitter(ex.InnerException.Message, 99))
                {
                    UI.Notify(s);
                    DownloadManager.Log(s);
                }

                innEx = innEx.InnerException;
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

        public string getResourceFilePath(string resourceName, string fileName)
        {
            return FileTransferId._DOWNLOADFOLDER_ + resourceName + "\\" + fileName;
        }
        
        public void dxDrawTexture(string path, Point pos, Size size)
        {
            Sprite.DrawTexture(path, pos, size);
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

        public float toFloat(double d)
        {
            return (float) d;
        }

        public void triggerServerEvent(string eventName, params object[] arguments)
        {
            Main.TriggerServerEvent(eventName, arguments);
        }

        public delegate void ServerEventTrigger(string eventName, object[] arguments);
        //public delegate void ChatEvent(string msg, CancelEventArgs cancelEv);
        public delegate void ChatEvent(string msg);

        public event EventHandler onResourceStart;
        public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event KeyEventHandler onKeyDown;
        public event KeyEventHandler onKeyUp;
        public event ServerEventTrigger onServerEventTrigger;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;

        internal void invokeChatMessage(string msg)
        {
            onChatMessage?.Invoke(msg);
        }

        internal void invokeChatCommand(string msg)
        {
            onChatCommand?.Invoke(msg);
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