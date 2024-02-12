﻿//#define RELATIVE_CEF_POS

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.GUI;
using GTANetwork.Streamer;
using GTANetwork.Util;
using GTANetwork.Sync;
using GTANetworkShared;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NativeUI;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Control = GTA.Control;
using Vector3 = GTANetworkShared.Vector3;
using VehicleHash = GTANetworkShared.VehicleHash;
using WeaponHash = GTANetworkShared.WeaponHash;
using System.ComponentModel;
//using WeaponComponent = GTANetwork.Util.WeaponComponent;
using WeaponComponent = GTA.WeaponComponentHash;


namespace GTANetwork.Javascript
{
    internal class ClientsideScriptWrapper
    {
        internal ClientsideScriptWrapper(V8ScriptEngine en, string rs, string filename)
        {
            Engine = en;
            ResourceParent = rs;
            Filename = filename;
        }

        internal V8ScriptEngine Engine { get; set; }
        internal string ResourceParent { get; set; }
        internal string Filename { get; set; }
    }

    internal static class AudioThread
    {
        internal static void StartAudio(string path, bool looped)
        {
            try
            {
                JavascriptHook.AudioDevice?.Stop();
                JavascriptHook.AudioDevice?.Dispose();
                JavascriptHook.AudioReader?.Dispose();

                if (path.EndsWith(".wav"))
                {
                    JavascriptHook.AudioReader = new WaveFileReader(path);
                }
                else
                {
                    JavascriptHook.AudioReader = new Mp3FileReader(path);
                }

                if (looped)
                {
                    JavascriptHook.AudioReader = new LoopStream(JavascriptHook.AudioReader);
                }

                JavascriptHook.AudioDevice = new WaveOutEvent();
                JavascriptHook.AudioDevice.Init(JavascriptHook.AudioReader);
                JavascriptHook.AudioDevice.Play();
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "STARTAUDIO");
            }

            /*
            // BUG: very buggy, for some reason
            var t = new Thread((ThreadStart) delegate
            {
                try
                {
                    JavascriptHook.AudioDevice?.Stop();
                    JavascriptHook.AudioDevice?.Dispose();
                    JavascriptHook.AudioReader?.Dispose();

                    JavascriptHook.AudioReader = new Mp3FileReader(path);
                    JavascriptHook.AudioDevice = new WaveOutEvent();
                    JavascriptHook.AudioDevice.Init(JavascriptHook.AudioReader);
                    JavascriptHook.AudioDevice.Play();
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "STARTAUDIO");
                }
            });
            t.IsBackground = true;
            t.Start();
            */
        }

        internal static void DisposeAudio()
        {
            var t = new Thread((ThreadStart) delegate
            {
                try
                {
                    JavascriptHook.AudioDevice?.Stop();
                    JavascriptHook.AudioDevice?.Dispose();
                    JavascriptHook.AudioReader?.Dispose();
                    JavascriptHook.AudioDevice = null;
                    JavascriptHook.AudioReader = null;
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "DISPOSEAUDIO");
                }
            }) {IsBackground = true};
            t.Start();
        }
    }

    public class JavascriptHook : Script
    {
        public JavascriptHook()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            ScriptEngines = new List<ClientsideScriptWrapper>();
            ThreadJumper = new List<Action>();
            TextElements = new List<UIResText>();
            Exported = new ExpandoObject();
        }

        internal static PointF MousePosition { get; set; }
        internal static bool MouseClick { get; set; }

        internal static List<UIResText> TextElements { get; set; }

        internal static List<ClientsideScriptWrapper> ScriptEngines;

        internal static List<Action> ThreadJumper;

        internal static WaveOutEvent AudioDevice { get; set; }
        internal static WaveStream AudioReader { get; set; }

        internal static ExpandoObject Exported { get; set; }

        internal static void InvokeServerEvent(string eventName, string resource, object[] arguments)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                for (int i = 0; i < ScriptEngines.Count; i++)
                {
                    try
                    {
                        if (resource != "*" && ScriptEngines[i].ResourceParent != resource) continue;
                        ScriptEngines[i].Engine.Script.API.invokeServerEvent(eventName, arguments);
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                    }
                }
            });
        }

        internal static void InvokeMessageEvent(string msg)
        {
            if (msg == null) return;
            ThreadJumper.Add(() =>
            {
                if (msg.StartsWith("/"))
                {
                    lock (ScriptEngines)
                    {
                        for (var index = ScriptEngines.Count - 1; index >= 0; index--)
                        {
                            ScriptEngines[index].Engine.Script.API.invokeChatCommand(msg);
                        }
                    }
                }
                else
                {
                    lock (ScriptEngines)
                    {
                        for (var index = 0; index < ScriptEngines.Count; index++)
                        {
                            ScriptEngines[index].Engine.Script.API.invokeChatMessage(msg);
                        }
                    }
                }
            });
        }

        internal static void InvokeCustomEvent(Action<dynamic> func)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    for (var index = ScriptEngines.Count - 1; index >= 0; index--)
                    {
                        func(ScriptEngines[index].Engine.Script.API);
                    }
                }
            });
        }

        internal static void InvokeStreamInEvent(LocalHandle handle, int type)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    for (var index = 0; index < ScriptEngines.Count; index++)
                    {
                        ScriptEngines[index].Engine.Script.API.invokeEntityStreamIn(handle, type);
                    }
                }
            });
        }

        internal static void InvokeStreamOutEvent(LocalHandle handle, int type)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    for (var index = 0; index < ScriptEngines.Count; index++)
                    {
                        ScriptEngines[index].Engine.Script.API.invokeEntityStreamOut(handle, type);
                    }
                }
            });
        }

        internal static void InvokeDataChangeEvent(LocalHandle handle, string key, object oldValue)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    for (var index = 0; index < ScriptEngines.Count; index++)
                    {
                        ScriptEngines[index].Engine.Script.API.invokeEntityDataChange(handle, key, oldValue);
                    }
                }
            });
        }

        internal static void InvokeCustomDataReceived(string resource, string data)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    foreach (var res in ScriptEngines.Where(en => en.ResourceParent == resource))
                    {
                        res.Engine.Script.API.invokeCustomDataReceived(data);
                    }
                }
            });
        }

        internal static void OnTick(object sender, EventArgs e)
        {
            var tmpList = new List<Action>(ThreadJumper);
            ThreadJumper.Clear();
            try
            {
                for (var i = 0; i < tmpList.Count; i++)
                {
                    tmpList[i].Invoke();
                }

                lock (ScriptEngines)
                {
                    for (var i = 0; i < ScriptEngines.Count; i++)
                    {
                        ScriptEngines[i].Engine.Script.API.invokeUpdate();
                        ScriptEngines[i].Engine.Script.API.processCoroutines();
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        internal static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Main.Chat == null || Main.Chat.IsFocused || Main.MainMenu.Visible) return;

            lock (ScriptEngines)
            {
                for (var i = 0; i < ScriptEngines.Count; i++)
                {
                    //try
                    //{
                        ScriptEngines[i].Engine.Script.API.invokeKeyDown(sender, e);
                    //}
                    //catch (ScriptEngineException ex)
                    //{
                    //    LogException(ex);
                    //}
                }
            }
        }

        internal static void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (Main.Chat == null || Main.Chat.IsFocused) return;

            lock (ScriptEngines)
            {
                for (var i = 0; i < ScriptEngines.Count; i++)
                { 
                    //try
                    //{
                    ScriptEngines[i].Engine.Script.API.invokeKeyUp(sender, e);
                    //}
                    //catch (ScriptEngineException ex)
                    //{
                    //    LogException(ex);
                    //}
                }
            }
        }

        internal static void StartScripts(ScriptCollection sc)
        {
            var localSc = new List<ClientsideScript>(sc.ClientsideScripts);

            ThreadJumper.Add(() =>
            {
                var scripts = localSc.Select(StartScript).ToList();

                var exportedDict = Exported as IDictionary<string, object>;

                foreach (var group in scripts.GroupBy(css => css.ResourceParent))
                {
                    dynamic thisRes = new ExpandoObject();
                    var thisResDict = (IDictionary<string, object>) thisRes;

                    foreach (var compiledResources in group)
                    {
                        thisResDict.Add(compiledResources.Filename, compiledResources.Engine.Script);
                    }

                    foreach (var wrapper in group)
                    {
                        wrapper.Engine.AddHostObject("resource", thisRes);
                    }

                    exportedDict.Add(group.Key, thisRes);
                }

                for (var index = scripts.Count - 1; index >= 0; index--)
                {
                    var cr = scripts[index];
                    cr.Engine.AddHostObject("exported", Exported);

                    cr.Engine.Script.API.invokeResourceStart();
                }
            });
        }

        internal static ClientsideScriptWrapper StartScript(ClientsideScript script)
        {
            ClientsideScriptWrapper csWrapper;
            var scriptEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging);
            //scriptEngine.AddHostObject("host", new HostFunctions()); // Disable an exploit where you could get reflection
            scriptEngine.AddHostObject("API", new ScriptContext(scriptEngine));
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
            scriptEngine.AddHostType("List", typeof(List<>));
            scriptEngine.AddHostType("Dictionary", typeof(Dictionary<,>));
            scriptEngine.AddHostType("String", typeof(string));
            scriptEngine.AddHostType("Int32", typeof(int));
            scriptEngine.AddHostType("Bool", typeof(bool));
            scriptEngine.AddHostType("Double", typeof(double));
            scriptEngine.AddHostType("Float", typeof(float));
            scriptEngine.AddHostType("KeyEventArgs", typeof(KeyEventArgs));
            scriptEngine.AddHostType("CancelEventArgs", typeof(CancelEventArgs));
            scriptEngine.AddHostType("Keys", typeof(Keys));
            scriptEngine.AddHostType("Point", typeof(Point));
            scriptEngine.AddHostType("PointF", typeof(PointF));
            scriptEngine.AddHostType("Size", typeof(Size));
            scriptEngine.AddHostType("Size2", typeof(SharpDX.Size2));
            scriptEngine.AddHostType("Vector3", typeof(Vector3));
            scriptEngine.AddHostType("Matrix4", typeof(Matrix4));
            scriptEngine.AddHostType("menuControl", typeof(UIMenu.MenuControls));
            scriptEngine.AddHostType("BadgeStyle", typeof(UIMenuItem.BadgeStyle));
            scriptEngine.AllowReflection = false;

            try
            {
                scriptEngine.Execute(script.Script);
                scriptEngine.Script.API.ParentResourceName = script.ResourceParent;
            }
            catch (ScriptEngineException ex)
            {
                LogException(ex);
            }
            finally
            {
                csWrapper = new ClientsideScriptWrapper(scriptEngine, script.ResourceParent, script.Filename);
                lock (ScriptEngines) ScriptEngines.Add(csWrapper);
            }
            return csWrapper;
        }

        internal static void StopAllScripts()
        {
            lock (ScriptEngines)
            {
                foreach (var t in ScriptEngines)
                {
                    t.Engine.Script.API.isDisposing = true;
                }
            }

            lock (ScriptEngines)
            {
                foreach (var t in ScriptEngines)
                {
                    t.Engine.Interrupt();
                    t.Engine.Script.API.invokeResourceStop();
                    t.Engine.Dispose();
                }
                ScriptEngines.Clear();
            }

            AudioDevice?.Stop();
            AudioDevice?.Dispose();
            AudioReader?.Dispose();
            AudioDevice = null;
            AudioReader = null;
            Exported = new ExpandoObject();

            lock (CEFManager.Browsers)
            {
                foreach (var t in CEFManager.Browsers)
                {
                    t.Close();
                    t.Dispose();
                }

                CEFManager.Browsers.Clear();
            }
        }

        internal static void StopScript(string resourceName)
        {
            lock (ScriptEngines)
            {
                for (int i = 0; i < ScriptEngines.Count; i++)
                {
                    if (ScriptEngines[i].ResourceParent != resourceName) continue;
                    ScriptEngines[i].Engine.Script.API.isDisposing = true;
                }
            }

            var dict = Exported as IDictionary<string, object>;
            dict.Remove(resourceName);

            ThreadJumper.Add(delegate
            {
                lock (ScriptEngines)
                {
                    for (int i = 0; i < ScriptEngines.Count; i++)
                    {
                        if (ScriptEngines[i].ResourceParent != resourceName) continue;
                        ScriptEngines[i].Engine.Script.API.invokeResourceStop();
                        ScriptEngines[i].Engine.Dispose();
                        ScriptEngines.RemoveAt(i);
                    }
                }

            });
        }


        private static void LogException(Exception ex)
        {
            Func<string, int, string[]> splitter = (string input, int everyN) =>
            {
                var list = new List<string>();
                for (var i = 0; i < input.Length; i += everyN)
                {
                    list.Add(input.Substring(i, Math.Min(everyN, input.Length - i)));
                }
                return list.ToArray();
            };

            Util.Util.SafeNotify("~r~~h~Clientside Javascript Error~h~~w~");

            var count = splitter(ex.Message, 99).Length;
            for (var index = 0; index < count; index++)
            {
                Util.Util.SafeNotify(splitter(ex.Message, 99)[index]);
            }

            LogManager.LogException(ex, "CLIENTSIDE SCRIPT ERROR");
        }
    }

    public class ScriptContext
    {
        public ScriptContext(V8ScriptEngine engine)
        {
            Engine = engine;
        }

        internal bool isDisposing;
        internal string ParentResourceName;
        internal V8ScriptEngine Engine;

        List<dynamic> unblockedCoroutines = new List<dynamic>();
        List<dynamic> shouldRunNextFrame = new List<dynamic>();
        SortedList<int, dynamic> shouldRunInTime = new SortedList<int, dynamic>();
        Dictionary<LocalHandle, int> Latencies = new Dictionary<LocalHandle, int>();
        Dictionary<LocalHandle, string> Names = new Dictionary<LocalHandle, string>();

        private const float height = 1080f;
        private static float ratio = (float)Main.screen.Width / Main.screen.Height;
        private static float width = height * ratio;

        internal void processCoroutines()
        {
            for (int i = 0; i < unblockedCoroutines.Count; i++)
            {
                var coroutine = unblockedCoroutines[i];
                dynamic result;
                if ((result = coroutine.next()).done) continue;
                dynamic value = result.value;
                if (value is int)
                {
                    int offset = (int) value;
                    int nextRun = Environment.TickCount + offset;
                    if (!shouldRunInTime.ContainsKey(nextRun))
                    {
                        shouldRunInTime.Add(nextRun, coroutine);
                    }
                }
                else
                {
                    shouldRunNextFrame.Add(coroutine);
                }
            }

            unblockedCoroutines = new List<dynamic>(shouldRunNextFrame);
            shouldRunNextFrame.Clear();

            while (shouldRunInTime.Count > 0 && Environment.TickCount - shouldRunInTime.ElementAt(0).Key > 0)
            {
                unblockedCoroutines.Add(shouldRunInTime.ElementAt(0).Value);
                shouldRunInTime.RemoveAt(0);
            }
        }

        public void startCoroutine(dynamic target)
        {
            dynamic iterator = target();
            shouldRunNextFrame.Add(iterator);
        }

        internal bool isPathSafe(string path)
        {
            if (ParentResourceName == null) throw new NullReferenceException("Illegal call to isPathSafe inside constructor!");

            var absPath = System.IO.Path.GetFullPath(FileTransferId._DOWNLOADFOLDER_ + ParentResourceName + Path.DirectorySeparatorChar + path);
            var resourcePath = System.IO.Path.GetFullPath(FileTransferId._DOWNLOADFOLDER_ + ParentResourceName);

            return absPath.StartsWith(resourcePath);
        }

        internal void checkPathSafe(string path)
        {
            if (!isPathSafe(path))
            {
                throw new AccessViolationException("Illegal path to file!");
            }
        }

        public enum ReturnType
        {
            Int = 0,
            UInt = 1,
            Long = 2,
            ULong = 3,
            String = 4,
            Vector3 = 5,
            Vector2 = 6,
            Float = 7,
            Bool = 8,
            Handle = 9,
        }
        
        public void showCursor(bool show)
        {
            CefController.ShowCursor = show;
        }

        public bool isCursorShown()
        {
            return CefController.ShowCursor;
        }

        private Dictionary<string, object> Settings;

        private void loadSettings()
        {
            if (Settings != null)
                return;

            if (!File.Exists(getResourceFilePath(".settings")))
            {
                Settings = new Dictionary<string, object>();
                return;
            }

            var settings = File.ReadAllBytes(getResourceFilePath(".settings"));
            var sets = (ClientResourceSettings)Main.DeserializeBinary<ClientResourceSettings>(settings);

            Settings = sets.Settings.ToDictionary((pair) => pair.Key,
                (pair) => Main.DecodeArgumentListPure(pair.Value).FirstOrDefault());
        }

        private void saveSettings()
        {
            if (Settings == null) return;

            var crs = new ClientResourceSettings()
            {
                Settings = Settings.ToDictionary(
                    (pair) => pair.Key,
                    (pair) => Main.ParseNativeArguments(pair.Value).FirstOrDefault()),
            };

            var bin = Main.SerializeBinary(crs);

            File.WriteAllBytes(getResourceFilePath(".settings"), bin);
        }
        

        // TODO: Limit the number of setting the programmer can set.
        public void setSetting(string name, object value)
        {
            if (Settings == null)
            {
                loadSettings();
            }

            Settings.Set(name, value);

            saveSettings();
        }

        public object getSetting(string name)
        {
            if (Settings == null)
            {
                loadSettings();
            }

            return Settings.Get(name);
        }

        public bool doesSettingExist(string name)
        {
            if (Settings == null)
            {
                loadSettings();
            }

            return Settings.ContainsKey(name);
        }

        public void removeSetting(string name)
        {
            if (Settings == null)
            {
                loadSettings();
            }

            Settings.Remove(name);

            saveSettings();
        }

        public JavascriptChat registerChatOverride()
        {
            var c = new JavascriptChat();
            Main.Chat = c;
            c.OnComplete += Main.ChatOnComplete;
            return c;
        }

        public GlobalCamera createCamera(Vector3 position, Vector3 rotation)
        {
            return Main.CameraManager.Create(position, rotation);
        }

        public void setActiveCamera(GlobalCamera camera)
        {
            Main.CameraManager.SetActive(camera);
        }

        public void setGameplayCameraActive()
        {
            Main.CameraManager.SetActive(null);
        }

        public GlobalCamera getActiveCamera()
        {
            return Main.CameraManager.GetActive();
        }

        public void setCameraShake(GlobalCamera cam, string shakeType, double amplitute)
        {
            cam.Shake = shakeType;
            cam.ShakeAmp = (float)amplitute;

            if (cam.CamObj != null && cam.Active)
            {
                Function.Call(Hash.SHAKE_CAM, cam.CamObj.Handle, cam.Shake, cam.ShakeAmp);
            }
        }

        public void stopCameraShake(GlobalCamera cam)
        {
            cam.Shake = null;
            cam.ShakeAmp = 0;

            if (cam.CamObj != null && cam.Active)
            {
                Function.Call(Hash.STOP_CAM_SHAKING, cam.CamObj.Handle, true);
            }
        }

        public bool isCameraShaking(GlobalCamera cam)
        {
            return !string.IsNullOrEmpty(cam.Shake);
        }

        public void setCameraPosition(GlobalCamera cam, Vector3 pos)
        {
            cam.Position = pos;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.Position = pos.ToVector();
            }
        }

        public Vector3 getCameraPosition(GlobalCamera cam)
        {
            return cam.Position;
        }

        public void setCameraRotation(GlobalCamera cam, Vector3 rotation)
        {
            cam.Rotation = rotation;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.Rotation = rotation.ToVector();
            }
        }

        public Vector3 getCameraRotation(GlobalCamera cam)
        {
            return cam.Rotation;
        }

        public void setCameraFov(GlobalCamera cam, double fov)
        {
            cam.Fov = (float)fov;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.FieldOfView = (float)fov;
            }
        }

        public float getCameraFov(GlobalCamera cam)
        {
            return cam.Fov;
        }

        public void pointCameraAtPosition(GlobalCamera cam, Vector3 pos)
        {
            cam.EntityPointing = 0;
            cam.VectorPointing = pos;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.PointAt(pos.ToVector());
            }
        }

        public void pointCameraAtEntity(GlobalCamera cam, LocalHandle ent, Vector3 offset)
        {
            cam.VectorPointing = null;
            cam.EntityPointing = ent.Value;
            cam.PointOffset = offset;
            cam.BonePointing = 0;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.PointAt(new Prop(ent.Value), offset.ToVector());
            }
        }

        public void pointCameraAtEntityBone(GlobalCamera cam, LocalHandle ent, int bone, Vector3 offset)
        {
            cam.VectorPointing = null;
            cam.EntityPointing = ent.Value;
            cam.BonePointing = bone;
            cam.PointOffset = offset;


            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.PointAt(new Ped(ent.Value), bone, offset.ToVector());
            }
        }

        public void stopCameraPointing(GlobalCamera cam)
        {
            cam.VectorPointing = null;
            cam.EntityPointing = 0;
            cam.BonePointing = 0;
            cam.PointOffset = null;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.StopPointing();
            }
        }

        public void attachCameraToEntity(GlobalCamera cam, LocalHandle ent, Vector3 offset)
        {
            cam.EntityAttached = ent.Value;
            cam.BoneAttached = 0;
            cam.AttachOffset = offset;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.AttachTo(new Prop(ent.Value), offset.ToVector());
            }
        }

        public void attachCameraToEntityBone(GlobalCamera cam, LocalHandle ent, int bone, Vector3 offset)
        {
            cam.EntityAttached = ent.Value;
            cam.BoneAttached = bone;
            cam.AttachOffset = offset;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.AttachTo(new Ped(ent.Value), bone, offset.ToVector());
            }
        }

        public void detachCamera(GlobalCamera cam)
        {
            cam.EntityAttached = 0;
            cam.BoneAttached = 0;
            cam.AttachOffset = null;

            if (cam.CamObj != null && cam.Active)
            {
                cam.CamObj.Detach();
            }
        }

        public void interpolateCameras(GlobalCamera from, GlobalCamera to, double duration, bool easepos, bool easerot)
        {
            if (!from.Active) Main.CameraManager.SetActive(from);

            Main.CameraManager.SetActiveWithInterp(to, (int)duration, easepos, easerot);
        }

        public PointF getCursorPosition()
        {
            return CefController._lastMousePoint;
        }

        public PointF getCursorPositionMantainRatio()
        {
            var res = getScreenResolutionMaintainRatio();

            var mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorX) * res.Width;
            var mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)GTA.Control.CursorY) * res.Height;

            return new PointF(mouseX, mouseY);
        }

        public PointF worldToScreen(Vector3 pos)
        {
            var p = Main.WorldToScreen(pos.ToVector());
            var res = getScreenResolution();

            return new PointF(p.X * res.Width, p.Y * res.Height);
        }

        public PointF worldToScreenMantainRatio(Vector3 pos)
        {
            var p = Main.WorldToScreen(pos.ToVector());
            var res = getScreenResolutionMaintainRatio();

            return new PointF(p.X * res.Width, p.Y * res.Height);
        }

        public string getCurrentResourceName()
        {
            return ParentResourceName;
        }

        public Vector3 screenToWorld(PointF pos)
        {
            var res = getScreenResolution();
            var norm = new Vector2(pos.X / res.Width, pos.Y / res.Height);
            var norm2 = new Vector2((norm.X - 0.5f) * 2f, (norm.Y - 0.5f) * 2f);

            var p = Main.RaycastEverything(norm2);

            return p.ToLVector();
        }

        public Vector3 screenToWorldMantainRatio(PointF pos)
        {
            var res = getScreenResolutionMaintainRatio();
            var norm = new Vector2(pos.X / res.Width, pos.Y / res.Height);
            var norm2 = new Vector2((norm.X - 0.5f) * 2f, (norm.Y - 0.5f) * 2f);

            var p = Main.RaycastEverything(norm2);

            return p.ToLVector();
        }

        public Vector3 screenToWorld(PointF pos, Vector3 camPos, Vector3 camRot) // TODO: replace this with a camera object
        {
            var res = getScreenResolution();
            var norm = new Vector2(pos.X / res.Width, pos.Y / res.Height);
            var norm2 = new Vector2((norm.X - 0.5f) * 2f, (norm.Y - 0.5f) * 2f);

            var p = Main.RaycastEverything(norm2, camPos.ToVector(), camRot.ToVector());

            return p.ToLVector();
        }

        public Vector3 screenToWorldMantainRatio(PointF pos, Vector3 camPos, Vector3 camrot) // TODO: replace this with a camera object
        {
            var res = getScreenResolutionMaintainRatio();
            var norm = new Vector2(pos.X / res.Width, pos.Y / res.Height);
            var norm2 = new Vector2((norm.X - 0.5f) * 2f, (norm.Y - 0.5f) * 2f);

            var p = Main.RaycastEverything(norm2, camPos.ToVector(), camrot.ToVector());

            return p.ToLVector();
        }

        public class Raycast
        {
            internal Raycast(RaycastResult res)
            {
                wrapper = res;
            }

            private RaycastResult wrapper;

            public bool didHitAnything => wrapper.DidHit;

            public bool didHitEntity => wrapper.DidHitEntity;

            public LocalHandle hitEntity => new LocalHandle(wrapper.HitEntity?.Handle ?? 0);

            public Vector3 hitCoords => wrapper.HitPosition.ToLVector();
        }

        public Raycast createRaycast(Vector3 start, Vector3 end, int flag, LocalHandle? ignoreEntity)
        {
            return ignoreEntity != null ? new Raycast(World.Raycast(start.ToVector(), end.ToVector(), (IntersectOptions) flag, new Prop(ignoreEntity.Value.Value))) : new Raycast(World.Raycast(start.ToVector(), end.ToVector(), (IntersectOptions)flag));
        }

        public Vector3 getGameplayCamPos()
        {
            return GameplayCamera.Position.ToLVector();
        }

        public Vector3 getGameplayCamRot()
        {
            return GameplayCamera.Rotation.ToLVector();
        }

        public Vector3 getGameplayCamDir()
        {
            return GameplayCamera.Direction.ToLVector();
        }

        public void setCanOpenChat(bool show)
        {
            Main.CanOpenChatbox = show;
        }

        public bool getCanOpenChat()
        {
            return Main.CanOpenChatbox;
        }

        public void setDisplayWastedShard(bool show)
        {
            Main.DisplayWastedMessage = show;
        }

        public bool getDisplayWastedShard()
        {
            return Main.DisplayWastedMessage;
        }

        public Browser createCefBrowser(double width, double height, bool local = true, bool focus = true)
        {
#if RELATIVE_CEF_POS
            var rat = getScreenResolutionMaintainRatio();
            var ramp = getScreenResolution();

            int w = (int) ((width / rat.Width) * ramp.Width);
            int h = (int) ((height / rat.Height) * ramp.Height);
#else
            int w = (int) width;
            int h = (int) height;
#endif

            var newBrowser = new Browser(Engine, new Size(w, h), local);
            CEFManager.Browsers.Add(newBrowser);

            //if (!newBrowser._hasFocused && focus)
            //{
            //    newBrowser._browser.GetHost().SetFocus(true);
            //    newBrowser._browser.GetHost().SendFocusEvent(true);
            //    newBrowser._hasFocused = true;
            //}
            return newBrowser;
        }

        public void setCefBrowserFocus(Browser browser, bool focus)
        {
            browser.GetHost().SetFocus(focus);
            browser.GetHost().SendFocusEvent(focus);
        }

        public void destroyCefBrowser(Browser browser)
        {
            lock (CEFManager.Browsers)
            {
                CEFManager.Browsers.Remove(browser);
            }
            
            try
            {
                CefUtil._cachedReferences.Remove(CefUtil._cachedReferences.FirstOrDefault(pair => pair.Value == browser).Key);
                browser.Close();
                browser.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "DESTROYCEFBROWSER");
            }
        }

        public bool isCefBrowserInitialized(Browser browser)
        {
            return browser.IsInitialized();
        }
        
        public void waitUntilCefBrowserInit(Browser browser)
        {
            while (!browser.IsInitialized())
            {
                Script.Yield();
            }
        }

        public void waitUntilCefBrowserLoaded(Browser browser)
        {
            while (!browser.IsLoading())
            {
                Script.Yield();
            }
            Script.Wait(50);
        }

        public void setCefBrowserSize(Browser browser, double width, double height)
        {
#if RELATIVE_CEF_POS
            var rat = getScreenResolutionMaintainRatio();
            var ramp = getScreenResolution();

            int w = (int)((width / rat.Width) * ramp.Width);
            int h = (int)((height / rat.Height) * ramp.Height);
#else
            int w = (int) width;
            int h = (int) height;
#endif
            browser.Size = new Size(w,h);
        }

        public Size getCefBrowserSize(Browser browser)
        {
            return browser.Size;
        }

        public void setCefBrowserHeadless(Browser browser, bool headless)
        {
            browser.Headless = headless;
        }

        public bool getCefBrowserHeadless(Browser browser)
        {
            return browser.Headless;
        }

        public void setCefBrowserPosition(Browser browser, double xPos, double yPos)
        {
#if RELATIVE_CEF_POS
            var rat = getScreenResolutionMaintainRatio();
            var ramp = getScreenResolution();

            int w = (int)((xPos / rat.Width) * ramp.Width);
            int h = (int)((yPos / rat.Height) * ramp.Height);
#else
            int w = (int) xPos;
            int h = (int) yPos;
#endif
            browser.Position = new Point(w, h);
        }

        public Point getCefBrowserPosition(Browser browser)
        {
            return browser.Position;
        }

        internal PointF ratioToRealRes(double x, double y)
        {
            var rat = getScreenResolutionMaintainRatio();
            var ramp = getScreenResolution();

            float w = (float)((x / rat.Width) * ramp.Width);
            float h = (float)((y / rat.Height) * ramp.Height);

            return new PointF(w, h);
        }

//        public void pinCefBrowser(Browser browser, double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
//        {
//            browser.Pinned = new PointF[4];
//#if RELATIVE_CEF_POS
//            browser.Pinned[0] = ratioToRealRes(x1, y1);
//            browser.Pinned[1] = ratioToRealRes(x2, y2);
//            browser.Pinned[2] = ratioToRealRes(x3, y3);
//            browser.Pinned[3] = ratioToRealRes(x4, y4);
//#else
//            browser.Pinned[0] = new PointF((float)x1, (float)y1);
//            browser.Pinned[1] = new PointF((float)x2, (float)y2);
//            browser.Pinned[2] = new PointF((float)x3, (float)y3);
//            browser.Pinned[3] = new PointF((float)x4, (float)y4);
//#endif
//        }

        //public void clearCefPinning(Browser browser)
        //{
        //    browser.Pinned = null;
        //}

        public void verifyIntegrityOfCache()
        {
            if (DownloadManager.CheckFileIntegrity()) return;
            Main.Client.Disconnect("Quit");
            DownloadManager.FileIntegrity.Clear();
        }

        public void loadPageCefBrowser(Browser browser, string uri)
        {
            if (browser == null) return;

            if (browser._localMode)
            {
                checkPathSafe(uri);

                if (!DownloadManager.CheckFileIntegrity())
                {
                    Main.Client.Disconnect("Quit");
                    DownloadManager.FileIntegrity.Clear();
                    return;
                }

                var fullUri = "https://" + ParentResourceName + "/" + uri.TrimStart('/');

                browser.GoToPage(fullUri);
            }
            else
            {
                // TODO: Check whitelist domains

                browser.GoToPage(uri);
            }
        }

        public void loadHtmlCefBrowser(Browser browser, string html)
        {
            if (browser == null) return;
            if (browser._localMode)
            {
                browser.LoadHtml(html);
            }
        }

        public void goBackCefBrowser(Browser browser)
        {
            browser?.GoBack();
        }

        public bool isCefBrowserLoading(Browser browser)
        {
            return browser.IsLoading();
        }

        public void setCefDrawState(bool state)
        {
            CEFManager.Draw = state;
        }

        public void setCefFramerate(Browser browser, int fps)
        {
            if (fps > 60) fps = 60;
            else if (fps < 1) fps = 1;

            browser.GetHost().SetWindowlessFrameRate(fps);
        }

        private DateTime _last;
        private static int _fps = 30;
        public int getGameFramerate()
        {
            if (DateTime.Now.Subtract(_last).TotalMilliseconds <= 500)
            {
                return _fps;
            }
            _fps = Convert.ToInt32(Game.FPS);
            return _fps;
        }

        public bool isCefDrawEnabled()
        {
            return CEFManager.Draw;
        }

        private bool parseHash(string hash, out Hash result)
        {
            if (!hash.StartsWith("0x")) return Hash.TryParse(hash, out result);
            result = (Hash) ulong.Parse(hash.Substring(2), NumberStyles.HexNumber);
            return true;
        }

        public void callNative(string hash, params object[] args)
        {
            if (!parseHash(hash, out Hash ourHash))
                throw new ArgumentException("Hash \"" + hash + "\" has not been found!");

            if (!NativeWhitelist.IsAllowed((ulong) ourHash))
                throw new ArgumentException("Hash \"" + hash + "\" is not allowed!");

            Function.Call(ourHash, args.Select(o =>
            {
                if (o is LocalHandle)
                    return new InputArgument(((LocalHandle) o).Value);
                else if (o is fArg)
                    return new InputArgument(((fArg) o).Value);
                return new InputArgument(o);
            }).ToArray());
        }

        public object returnNative(string hash, int returnType, params object[] args)
        {
            if (!parseHash(hash, out Hash ourHash))
                throw new ArgumentException("Hash \"" + hash + "\" has not been found!");

            if (!NativeWhitelist.IsAllowed((ulong) ourHash))
                throw new ArgumentException("Hash \"" + hash + "\" is not allowed!");

            var fArgs = args.Select(o =>
            {
                if (o is LocalHandle)
                    return new InputArgument(((LocalHandle) o).Value);
                else if (o is fArg)
                    return new InputArgument(((fArg)o).Value);
                return new InputArgument(o);
            }).ToArray();

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
                    return Function.Call<GTA.Math.Vector3>(ourHash, fArgs).ToLVector();
                case ReturnType.Vector2:
                    return Function.Call<Vector2>(ourHash, fArgs);
                case ReturnType.Float:
                    return Function.Call<float>(ourHash, fArgs);
                case ReturnType.Bool:
                    return Function.Call<bool>(ourHash, fArgs);
                case ReturnType.Handle:
                    return new LocalHandle(Function.Call<int>(ourHash, fArgs));
                default:
                    return null;
            }
        }

        public void setUiColor(int r, int g, int b)
        {
            Main.UIColor = Color.FromArgb(r, g, b);
        }

        public int getHashKey(string input)
        {
            return Game.GenerateHash(input);
        }

        public bool setEntitySyncedData(LocalHandle entity, string key, object data)
        {
            return Main.SetEntityProperty(entity, key, data);
        }

        public void resetEntitySyncedData(LocalHandle entity, string key)
        {
            Main.ResetEntityProperty(entity, key);
        }

        public bool hasEntitySyncedData(LocalHandle entity, string key)
        {
            return Main.HasEntityProperty(entity, key);
        }

        public object getEntitySyncedData(LocalHandle entity, string key)
        {
            return Main.GetEntityProperty(entity, key);
        }

        public string[] getAllEntitySyncedData(LocalHandle entity)
        {
            return Main.GetEntityAllProperties(entity);
        }

        public bool setWorldSyncedData(string key, object data)
        {
            return Main.SetWorldData(key, data);
        }

        public void resetWorldSyncedData(string key)
        {
            Main.ResetWorldData(key);
        }

        public bool hasWorldSyncedData(string key)
        {
            return Main.HasWorldData(key);
        }

        public object getWorldSyncedData(string key)
        {
            return Main.GetWorldData(key);
        }

        public string[] getAllWorldSyncedData()
        {
            return Main.GetAllWorldData();
        }

        public int getGamePlayer()
        {
            return Game.Player.Handle;
        }

        public LocalHandle getLocalPlayer()
        {
            return new LocalHandle(Game.Player.Character.Handle);
        }

        public Vector3 getEntityPosition(LocalHandle entity)
        {
            if (entity.Properties<IStreamedItem>() == null || entity.Properties<IStreamedItem>().StreamedIn)
                return new Prop(entity.Value).Position.ToLVector();
            else return entity.Properties<IStreamedItem>().Position;
        }

        public Vector3 getEntityRotation(LocalHandle entity)
        {
            if (entity.Properties<IStreamedItem>() == null || entity.Properties<IStreamedItem>().StreamedIn)
                return new Prop(entity.Value).Rotation.ToLVector();
            else return ((EntityProperties)entity.Properties<IStreamedItem>()).Rotation;
        }

        public Vector3 getEntityVelocity(LocalHandle entity)
        {
            return new Prop(entity.Value).Velocity.ToLVector();
        }

        public float getVehicleHealth(LocalHandle entity)
        {
            if (entity.Properties<IStreamedItem>() == null || entity.Properties<RemoteVehicle>().StreamedIn)
                return new Vehicle(entity.Value).EngineHealth;
            else return entity.Properties<RemoteVehicle>().Health;
        }

        public float getVehicleRPM(LocalHandle entity)
        {
            return new Vehicle(entity.Value).CurrentRPM;
        }

        public bool isPlayerInAnyVehicle(LocalHandle player)
        {
            return new Ped(player.Value).IsInVehicle();
        }

        public bool isPlayerOnFire(LocalHandle player)
        {
            return new Ped(player.Value).IsOnFire;
        }

        public bool isPlayerParachuting(LocalHandle player)
        {
            return new Ped(player.Value).ParachuteState == ParachuteState.Gliding;
        }

        public bool isPlayerInFreefall(LocalHandle player)
        {
            return Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, player.Value) == 0 &&
                new Ped(player.Value).IsInAir;
        }

        public bool isPlayerAiming(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? new Ped(player.Value).IsAiming : findPlayer(player).IsAiming;
        }

        public bool isPlayerShooting(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? new Ped(player.Value).IsShooting : findPlayer(player).IsShooting;
        }

        public bool isPlayerReloading(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? new Ped(player.Value).IsReloading : findPlayer(player).IsReloading;
        }

        public bool isPlayerInCover(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? new Ped(player.Value).IsInCover() : findPlayer(player).IsInCover;
        }

        public bool isPlayerOnLadder(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? Subtask.IsSubtaskActive(player.Value, ESubtask.USING_LADDER) : findPlayer(player).IsOnLadder;
        }

        public Vector3 getPlayerAimingPoint(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? Main.RaycastEverything(new Vector2(0, 0)).ToLVector() : findPlayer(player).AimCoords.ToLVector();
        }

        public bool isPlayerDead(LocalHandle player)
        {
            return player.Value == Game.Player.Character.Handle ? Game.Player.Character.IsDead : findPlayer(player).IsPlayerDead;
        }

        public bool doesEntityExist(LocalHandle entity)
        {
            return !entity.IsNull;
        }

        public void setEntityInvincible(LocalHandle entity, bool invincible)
        {
            entity.Properties<EntityProperties>().IsInvincible = invincible;

            new Prop(entity.Value).IsInvincible = invincible;
        }

        public bool getEntityInvincible(LocalHandle entity)
        {
            return entity.Properties<EntityProperties>().IsInvincible;
        }

        public bool getLocalPlayerInvincible()
        {
            return Main._playerGodMode;
        }

        public void createParticleEffectOnPosition(string ptfxLibrary, string ptfxName, Vector3 position, Vector3 rotation, double scale)
        {
            Util.Util.LoadPtfxAsset(ptfxLibrary);
            Function.Call(Hash._USE_PARTICLE_FX_ASSET_NEXT_CALL, ptfxLibrary);
            Function.Call((Hash) 0x25129531F77B9ED3, ptfxName, position.X, position.Y, position.Z, rotation.X,
                rotation.Y, rotation.Z,
                (float)scale, 0, 0, 0);
        }

        public void createParticleEffectOnEntity(string ptfxLibrary, string ptfxName, LocalHandle entity, Vector3 offset, Vector3 rotation, double scale, int boneIndex = -1)
        {
            Util.Util.LoadPtfxAsset(ptfxLibrary);
            Function.Call(Hash._USE_PARTICLE_FX_ASSET_NEXT_CALL, ptfxLibrary);

            if (boneIndex <= 0)
            {
                Function.Call((Hash) 0x0D53A3B8DA0809D2, ptfxName, entity.Value, offset.X, offset.Y, offset.Z,
                    rotation.X, rotation.Y, rotation.Z,
                    (float)scale, 0, 0, 0);
            }
            else
            {
                Function.Call((Hash)0x0E7E72961BA18619, ptfxName, entity.Value, offset.X, offset.Y, offset.Z,
                    rotation.X, rotation.Y, rotation.Z,
                    boneIndex, scale, 0, 0, 0);
            }
        }

        public void createExplosion(int explosionType, Vector3 position, double damageScale)
        {
            Function.Call((Hash) 0xE3AD2BDBAEE269AC, position.X, position.Y, position.Z, explosionType, (float)damageScale,
                true, false);
        }

        public void createOwnedExplosion(LocalHandle owner, int explosionType, Vector3 position, double damageScale)
        {
            Function.Call((Hash)0x172AA1B624FA1013, owner.Value, position.X, position.Y, position.Z, explosionType, (float)damageScale, true, false, 1f);
        }

        public void createProjectile(int weapon, Vector3 start, Vector3 target, int damage, double speed = -1, int dimension = 0)
        {
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, target.X, target.Y, target.Z, damage, 1, weapon, null, true, false, (float)speed);
        }

        public void createOwnedProjectile(LocalHandle owner, int weapon, Vector3 start, Vector3 target, int damage, double speed = -1, int dimension = 0)
        {
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, target.X, target.Y, target.Z, damage, 1, weapon, owner.Value, true, false, (float)speed);
        }

        public void setVehicleLivery(LocalHandle vehicle, int livery)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            veh.Livery = livery;
            if (veh.StreamedIn) new Vehicle(vehicle.Value).Mods.Livery = livery;
        }

        public int getVehicleLivery(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return 0;
            return veh.StreamedIn ? new Vehicle(vehicle.Value).Mods.Livery : veh.Livery;
        }

        public void setVehicleLocked(LocalHandle vehicle, bool locked)
        {
            var prop = vehicle.Properties<EntityProperties>();

            if (locked)
            {
                prop.Flag = (byte)PacketOptimization.SetBit(prop.Flag, EntityFlag.VehicleLocked);
            }
            else
            {
                prop.Flag = (byte)PacketOptimization.ResetBit(prop.Flag, EntityFlag.VehicleLocked);
            }

            Function.Call((Hash)0xB664292EAECF7FA6, vehicle.Value, locked ? 2 : 1);
        }

        public bool getVehicleLocked(LocalHandle vehicle)
        {
            return doesEntityExist(vehicle) && PacketOptimization.CheckBit(vehicle.Properties<EntityProperties>().Flag, EntityFlag.VehicleLocked);
        }

        public LocalHandle getVehicleTrailer(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null || veh.Trailer == 0) return new LocalHandle(0);

            return new LocalHandle(veh.Trailer, HandleType.NetHandle);
        }

        public LocalHandle getVehicleTraileredBy(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null || veh.TraileredBy == 0) return new LocalHandle(0);

            return new LocalHandle(veh.TraileredBy, HandleType.NetHandle);
        }

        public bool getVehicleSirenState(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return false;
            return veh.StreamedIn ? new Vehicle(vehicle.Value).IsSirenActive : veh.Siren;
        }

        public bool isVehicleTyrePopped(LocalHandle vehicle, int tyre)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return false;
            if (veh.StreamedIn) return Util.Util.BuildTyreArray(new Vehicle(vehicle.Value))[tyre];

            return (veh.Tires & (1 << tyre)) != 0;
        }

        public void popVehicleTyre(LocalHandle vehicle, int tyre, bool pop)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn)
            {
                if (pop)
                    new Vehicle(vehicle.Value).Wheels[tyre].Burst();
                else new Vehicle(vehicle.Value).Wheels[tyre].Fix();
            }

            if (pop)
            {
                veh.Tires |= (byte)(1 << tyre);
            }
            else
            {
                veh.Tires &= (byte)~(1 << tyre);
            }
        }

        public bool isVehicleDoorBroken(LocalHandle vehicle, int door)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return false;
            if (veh.StreamedIn) return new Vehicle(vehicle.Value).Doors[(VehicleDoorIndex)door].IsBroken;

            return (veh.Doors & (1 << door)) != 0;
        }

        public void setVehicleDoorState(LocalHandle vehicle, int door, bool open)
        {
            if (!doesEntityExist(vehicle)) return;
            var prop = vehicle.Properties<RemoteVehicle>();

            if (open) prop.Doors |= (byte)(1 << door);
            else prop.Doors &= (byte)~(1 << door);

            if (open)
            {
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, vehicle.Value, door, false, false);
            }
            else
            {
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, vehicle.Value, door, false);
            }
        }

        public bool getVehicleDoorState(LocalHandle vehicle, int door)
        {
            if (!doesEntityExist(vehicle)) return false;
            var prop = vehicle.Properties<RemoteVehicle>();

            return (prop.Doors & (1 << door)) != 0;
        }

        public void breakVehicleTyre(LocalHandle vehicle, int door, bool breakDoor)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn)
            {
                if (breakDoor)
                    new Vehicle(vehicle.Value).Doors[(VehicleDoorIndex)door].Break(true);
            }

            if (breakDoor)
            {
                veh.Doors |= (byte)(1 << door);
            }
            else
            {
                veh.Doors &= (byte)~(1 << door);
            }
        }

        public bool isVehicleWindowBroken(LocalHandle vehicle, int window)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return false;
            if (veh.StreamedIn) return !new Vehicle(vehicle.Value).Windows[(VehicleWindowIndex)window].IsIntact;

            if (veh.DamageModel == null) return false;
            return (veh.DamageModel.BrokenWindows & (1 << window)) != 0;
        }

        public void breakVehicleWindow(LocalHandle vehicle, int window, bool breakWindow)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn)
            {
                if (breakWindow)
                    new Vehicle(vehicle.Value).Windows[(VehicleWindowIndex)window].Smash();
                else new Vehicle(vehicle.Value).Windows[(VehicleWindowIndex)window].Repair();
            }

            if (breakWindow)
            {
                veh.Tires |= (byte)(1 << window);
            }
            else
            {
                veh.Tires &= (byte)~(1 << window);
            }
        }

        public void setVehicleExtra(LocalHandle vehicle, int slot, bool enabled)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn) new Vehicle(vehicle.Value).ToggleExtra(slot, enabled);

            if (enabled)
                veh.VehicleComponents |= (short)(1 << slot);
            else
                veh.VehicleComponents &= (short)~(1 << slot);
        }

        public bool getVehicleExtra(LocalHandle vehicle, int slot)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return false;
            if (veh.StreamedIn) return new Vehicle(vehicle.Value).IsExtraOn(slot);
            return (veh.VehicleComponents & (1 << slot)) != 0;
        }

        public void setVehicleNumberPlate(LocalHandle vehicle, string plate)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn) new Vehicle(veh.LocalHandle).Mods.LicensePlate = plate;
            veh.NumberPlate = plate;
        }

        public string getVehicleNumberPlate(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return null;
            return veh.StreamedIn ? new Vehicle(veh.LocalHandle).Mods.LicensePlate : veh.NumberPlate;
        }

        public void setVehicleEngineStatus(LocalHandle vehicle, bool turnedOn)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (!turnedOn)
            {
                veh.Flag = (byte)PacketOptimization.SetBit(veh.Flag, EntityFlag.EngineOff);
            }
            else
            {
                veh.Flag = (byte)PacketOptimization.ResetBit(veh.Flag, EntityFlag.EngineOff);
            }

            new Vehicle(vehicle.Value).IsEngineRunning = turnedOn;
            new Vehicle(vehicle.Value).IsDriveable = !turnedOn;
        }

        public bool getVehicleEngineStatus(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh != null)
            {
                return !PacketOptimization.CheckBit(veh.Flag, EntityFlag.EngineOff);
            }

            return false;
        }

        public void setVehicleSpecialLightStatus(LocalHandle vehicle, bool status)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            if (veh.StreamedIn)
            {
                new Vehicle(vehicle.Value).IsTaxiLightOn = status;
                new Vehicle(vehicle.Value).IsSearchLightOn = status;
            }

            if (status)
                veh.Flag = (byte) PacketOptimization.SetBit(veh.Flag, EntityFlag.SpecialLight);
            else
                veh.Flag = (byte) PacketOptimization.ResetBit(veh.Flag, EntityFlag.SpecialLight);
        }

        public bool getVehicleSpecialLightStatus(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            return veh != null && PacketOptimization.CheckBit(veh.Flag, EntityFlag.SpecialLight);
        }

        public void setEntityCollisionless(LocalHandle entity, bool status)
        {
            if (entity.Properties<IStreamedItem>() != null)
            {
                if (status)
                    entity.Properties<EntityProperties>().Flag =
                        (byte)
                            PacketOptimization.SetBit(entity.Properties<EntityProperties>().Flag,
                                EntityFlag.Collisionless);
                else
                    entity.Properties<EntityProperties>().Flag =
                        (byte)
                            PacketOptimization.ResetBit(entity.Properties<EntityProperties>().Flag,
                                EntityFlag.Collisionless);

                if (entity.Properties<IStreamedItem>().StreamedIn)
                {
                    new Prop(entity.Value).IsCollisionEnabled = !status;
                }
            }
            else
            {
                new Prop(entity.Value).IsCollisionEnabled = !status;
            }
        }

        public bool getEntityCollisionless(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<EntityProperties>();

            if (veh != null)
            {
                return PacketOptimization.CheckBit(veh.Flag, EntityFlag.Collisionless);
            }

            return false;
        }

        public void setVehicleMod(LocalHandle vehicle, int slot, int modType)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh.Mods == null) veh.Mods = new Dictionary<byte, int>();

            veh.Mods[(byte) slot] = modType;

            if (slot >= 60)
            {
                Util.Util.SetNonStandardVehicleMod(new Vehicle(vehicle.Value), slot, modType);
            }
            else
            {
                if (slot >= 17 && slot <= 22)
                    new Vehicle(vehicle.Value).Mods[(VehicleToggleModType)slot].IsInstalled = modType != 0;
                else
                    new Vehicle(vehicle.Value).SetMod(slot, modType, false);
            }
        }

        public int getVehicleMod(LocalHandle vehicle, int slot)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh != null)
            {
                return veh.Mods?[(byte) slot] ?? -1;
            }

            return 0;
        }

        public void removeVehicleMod(LocalHandle vehicle, int slot)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh != null)
            {
                veh.Mods?.Remove((byte) slot);

                if (veh.StreamedIn)
                    Function.Call((Hash)0x92D619E420858204, vehicle.Value, slot);
            }
        }

        public void setVehicleBulletproofTyres(LocalHandle vehicle, bool bulletproof)
        {
            setVehicleMod(vehicle, 61, bulletproof ? 0 : 1);
        }

        public bool getVehicleBulletproofTyres(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 61) == 0;
        }

        public void setVehicleNumberPlateStyle(LocalHandle vehicle, int style)
        {
            setVehicleMod(vehicle, 62, style);
        }

        public int getVehicleNumberPlateStyle(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 62);
        }

        public void setVehiclePearlescentColor(LocalHandle vehicle, int color)
        {
            setVehicleMod(vehicle, 63, color);
        }

        public int getVehiclePearlescentColor(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 63);
        }

        public void setVehicleWheelColor(LocalHandle vehicle, int color)
        {
            setVehicleMod(vehicle, 64, color);
        }

        public int getVehicleWheelColor(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 64);
        }

        public void setVehicleWheelType(LocalHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 65, type);
        }

        public int getVehicleWheelType(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 65);
        }

        public void setVehicleModColor1(LocalHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 66, Color.FromArgb(r,g,b).ToArgb());
        }
        /*
        public void getVehicleModColor1(LocalHandle vehicle, out byte red, out byte green, out byte blue)
        {
            var val = getVehicleMod(vehicle, 66);
            byte a;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
        }

        */

        public void setVehicleModColor2(LocalHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 67, Color.FromArgb(r,g,b).ToArgb());
        }
        /*
        public void getVehicleModColor2(NetHandle vehicle, out byte red, out byte green, out byte blue)
        {
            var val = getVehicleMod(vehicle, 67);
            byte a;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
        }
        */
        public void setVehicleTyreSmokeColor(LocalHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 68, Color.FromArgb(r,g,b).ToArgb());
        }
        /*
        public void getVehicleTyreSmokeColor(NetHandle vehicle, out byte red, out byte green, out byte blue)
        {
            var val = getVehicleMod(vehicle, 68);
            byte a;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
        }
        */
        
        public void setVehicleWindowTint(LocalHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 69, type);
        }

        public int getVehicleWindowTint(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 69);
        }

        public void setVehicleEnginePowerMultiplier(LocalHandle vehicle, double mult)
        {
            setVehicleMod(vehicle, 70, BitConverter.ToInt32(BitConverter.GetBytes((float)mult), 0));
        }

        public float getVehicleEnginePowerMultiplier(LocalHandle vehicle)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(getVehicleMod(vehicle, 70)), 0);
        }

        public void setVehicleEngineTorqueMultiplier(LocalHandle vehicle, double mult)
        {
            setVehicleMod(vehicle, 71, BitConverter.ToInt32(BitConverter.GetBytes((float)mult), 0));
        }

        public float getVehicleEngineTorqueMultiplier(LocalHandle vehicle)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(getVehicleMod(vehicle, 71)), 0);
        }

        public void setVehicleNeonState(LocalHandle vehicle, int slot, bool turnedOn)
        {
            var currentState = getVehicleMod(vehicle, 72);

            if (turnedOn)
                setVehicleMod(vehicle, 72, currentState | 1 << slot);
            else
                setVehicleMod(vehicle, 72, currentState & ~(1 << slot));
        }

        public bool getVehicleNeonState(LocalHandle vehicle, int slot)
        {
            return (getVehicleMod(vehicle, 72) & (1 << slot)) != 0;
        }

        public void setVehicleNeonColor(LocalHandle vehicle, int r, int g, int b)
        {
            setVehicleMod(vehicle, 73, Color.FromArgb(r,g,b).ToArgb());
        }
        /*
        public void getVehicleNeonColor(NetHandle vehicle, out byte red, out byte green, out byte blue)
        {
            var val = getVehicleMod(vehicle, 73);
            byte a;
            Extensions.ToArgb(val, out a, out red, out green, out blue);
        }
        */
        public void setVehicleDashboardColor(LocalHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 74, type);
        }

        public int getVehicleDashboardColor(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 74);
        }

        public void setVehicleTrimColor(LocalHandle vehicle, int type)
        {
            setVehicleMod(vehicle, 75, type);
        }

        public int getVehicleTrimColor(LocalHandle vehicle)
        {
            return getVehicleMod(vehicle, 75);
        }

        public string getVehicleDisplayName(int model)
        {
            return Function.Call<string>(Hash._GET_LABEL_TEXT,
                Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, model));
        }

        public float getVehicleMaxSpeed(int model)
        {
            return Function.Call<float>((Hash)0xF417C2502FFFED43, model);
        }

        public float getVehicleMaxBraking(int model)
        {
            return Function.Call<float>((Hash)0xDC53FD41B4ED944C, model);
        }

        public float getVehicleMaxTraction(int model)
        {
            return Function.Call<float>((Hash)0x539DE94D44FDFD0D, model);
        }

        public float getVehicleMaxAcceleration(int model)
        {
            return Function.Call<float>((Hash)0x8C044C5C84505B6A, model);
        }

        public float getVehicleMaxOccupants(int model)
        {
            return Function.Call<int>(Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS, model);
        }

        public int getVehicleClass(int model)
        {
            return Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, model);
        }

        public void detonatePlayerStickies()
        {
            Function.Call((Hash)0xFC4BD125DE7611E4, Game.Player.Character, (int)WeaponHash.StickyBomb, true);
        }

        public void setPlayerNametag(LocalHandle player, string text)
        {
            player.Properties<PlayerProperties>().NametagText = text;
        }

        public void resetPlayerNametag(LocalHandle player)
        {
            player.Properties<PlayerProperties>().NametagText = " ";
        }

        public void setPlayerNametagVisible(LocalHandle player, bool show)
        {
            var p = player.Properties<PlayerProperties>();

            p.NametagSettings = show ? PacketOptimization.ResetBit(p.NametagSettings, 1) : PacketOptimization.SetBit(p.NametagSettings, 1);
        }

        public bool getPlayerNametagVisible(LocalHandle player)
        {
            return PacketOptimization.CheckBit(player.Properties<PlayerProperties>().NametagSettings, 1);
        }

        public void setPlayerNametagColor(LocalHandle player, byte r, byte g, byte b)
        {
            var p = player.Properties<PlayerProperties>();

            p.NametagSettings = PacketOptimization.SetBit(p.NametagSettings, 2);

            var col = Util.Util.FromArgb(0, r, g, b) << 8;
            p.NametagSettings |= col;
        }

        public void resetPlayerNametagColor(LocalHandle player)
        {
            var p = player.Properties<PlayerProperties>();

            p.NametagSettings = PacketOptimization.ResetBit(p.NametagSettings, 2);

            p.NametagSettings &= 255;
        }

        public void setPlayerSkin(int model)
        {
            Util.Util.SetPlayerSkin((PedHash)model);
            setPlayerDefaultClothes();
        }

        public void setPlayerDefaultClothes()
        {
            Function.Call((Hash)0x45EEE61580806D63, Game.Player.Character);
        }

        public void setPlayerTeam(int team)
        {
            Main.LocalTeam = team;
        }

        public int getPlayerTeam()
        {
            return Main.LocalTeam;
        }

        public void playPlayerScenario(string name)
        {
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, Game.Player.Character, name, 0, 0);
        }

        public void playPlayerAnimation(string animDict, string animName, int flag, int duration = -1)
        {
            Util.Util.LoadDict(animDict);
            Game.Player.Character.Task.PlayAnimation(animDict, animName, -8f, 8f, duration, (AnimationFlags) flag, 8f);
        }

        public void stopPlayerAnimation()
        {
            Game.Player.Character.Task.ClearAll();
        }

        public void setVehiclePrimaryColor(LocalHandle vehicle, int color)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            veh.PrimaryColor = color;

            if (veh.StreamedIn) new Vehicle(vehicle.Value).Mods.PrimaryColor = (VehicleColor) color;
        }

        public void setVehicleSecondaryColor(LocalHandle vehicle, int color)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            veh.SecondaryColor = color;

            if (veh.StreamedIn) new Vehicle(vehicle.Value).Mods.SecondaryColor = (VehicleColor)color;
        }

        public void setVehicleCustomPrimaryColor(LocalHandle vehicle, int r, int g, int b)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            veh.PrimaryColor = Color.FromArgb(0x01, r, g, b).ToArgb();

            if (veh.StreamedIn)
                new Vehicle(vehicle.Value).Mods.CustomPrimaryColor = Color.FromArgb(0x01, r, g, b);
        }

        public void setVehicleCustomSecondaryColor(LocalHandle vehicle, int r, int g, int b)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return;
            veh.SecondaryColor = Color.FromArgb(0x01, r,g,b).ToArgb();

            if (veh.StreamedIn)
                new Vehicle(vehicle.Value).Mods.CustomSecondaryColor = Color.FromArgb(0x01, r, g, b);
        }

        public Color getVehicleCustomPrimaryColor(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return Color.White;
            Util.Util.ToArgb(veh.PrimaryColor, out byte a, out byte r, out byte g, out byte b);

            return Color.FromArgb(r, g, b);
        }

        public Color getVehicleCustomSecondaryColor(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return Color.White;
            Util.Util.ToArgb(veh.SecondaryColor, out byte a, out byte r, out byte g, out byte b);

            return Color.FromArgb(r, g, b);
        }

        public int getVehiclePrimaryColor(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return 0;
            if (veh.StreamedIn) return (int)new Vehicle(vehicle.Value).Mods.PrimaryColor;

            return veh.PrimaryColor;
        }

        public int getVehicleSecondaryColor(LocalHandle vehicle)
        {
            var veh = vehicle.Properties<RemoteVehicle>();

            if (veh == null) return 0;
            if (veh.StreamedIn)
                return (int)new Vehicle(vehicle.Value).Mods.SecondaryColor;

            return veh.SecondaryColor;
        }

        public void setPlayerClothes(LocalHandle player, int slot, int drawable, int texture)
        {
            var pl = player.Properties<RemotePlayer>();

            Function.Call((Hash)0x262B14F48D29DE80, player.Value, slot, drawable, texture, 2);

            if (pl.Textures == null) pl.Textures = new Dictionary<byte, byte>();
            if (pl.Props == null) pl.Props = new Dictionary<byte, byte>();

            pl.Textures[(byte) slot] = (byte)texture;
            pl.Textures[(byte) slot] = (byte) drawable;
        }

        public void setPlayerAccessory(LocalHandle player, int slot, int drawable, int texture)
        {
            var pl = player.Properties<RemotePlayer>();

            Function.Call((Hash)0x93376B65A266EB5F, player.Value, slot, drawable, texture, true);

            if (pl.Accessories == null) pl.Accessories = new Dictionary<byte, Tuple<byte, byte>>();

            pl.Accessories[(byte)slot] = new Tuple<byte, byte>((byte) drawable, (byte) texture);
        }

        public void clearPlayerAccessory(LocalHandle player, int slot)
        {
            var pl = player.Properties<RemotePlayer>();

            Function.Call((Hash)0x0943E5B8E078E76E, player.Value, slot);

            pl.Accessories?.Remove((byte) slot);
        }
        
        public int vehicleNameToModel(string modelName)
        {
            return (from object value in Enum.GetValues(typeof(VehicleHash)) where modelName.ToLower() == ((VehicleHash)value).ToString().ToLower() select ((int)(VehicleHash)value)).FirstOrDefault();
        }

        public int pedNameToModel(string modelName)
        {
            return (from object value in Enum.GetValues(typeof(PedHash)) where modelName.ToLower() == ((PedHash)value).ToString().ToLower() select ((int)(PedHash)value)).FirstOrDefault();
        }

        public int pickupNameToModel(string modelName)
        {
            return (from object value in Enum.GetValues(typeof(PickupHash)) where modelName.ToLower() == ((PickupHash)value).ToString().ToLower() select ((int)(PickupHash)value)).FirstOrDefault();
        }

        public int weaponNameToModel(string modelName)
        {
            return (from object value in Enum.GetValues(typeof(WeaponHash)) where modelName.ToLower() == ((WeaponHash)value).ToString().ToLower() select ((int)(WeaponHash)value)).FirstOrDefault();
        }

        public void loadInterior(Vector3 pos)
        {
            int interior;
            if ((interior = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, pos.X, pos.Y, pos.Z)) == 0) return;
            Function.Call((Hash)0x2CA429C029CCF247, interior); // LOAD_INTERIOR
            Function.Call(Hash.SET_INTERIOR_ACTIVE, interior, true);
            Function.Call(Hash.DISABLE_INTERIOR, interior, false);
            if (Function.Call<bool>(Hash.IS_INTERIOR_CAPPED, interior)) Function.Call(Hash.CAP_INTERIOR, interior, false);
        }

        public void clearPlayerTasks()
        {
            Game.Player.Character.Task.ClearAllImmediately();
        }

        public void setEntityPositionFrozen(LocalHandle entity, bool frozen)
        {
            new Prop(entity.Value).IsPositionFrozen = frozen;
        }

        public void setEntityVelocity(LocalHandle entity, Vector3 velocity)
        {
            new Prop(entity.Value).Velocity = velocity.ToVector();
        }

        public int getPlayerVehicleSeat(LocalHandle player)
        {
            return Util.Util.GetPedSeat(new Ped(player.Value));
        }

        public bool getPlayerSeatbelt(LocalHandle player)
        {
            Ped PlayerChar = Game.Player.Character;
            if (player.Value == PlayerChar.Handle) return !PlayerChar.GetConfigFlag(32);
            return !new Ped(player.Value).GetConfigFlag(32);

            //return !Function.Call<bool>((Hash)0x7EE53118C892B513, player.Value, 32, true);
        }

        public void setPlayerWeaponTint(int weapon, int tint)
        {
            Ped PlayerChar = Game.Player.Character;
            Function.Call((Hash)0x50969B9B89ED5738, PlayerChar, weapon, tint);
            ((RemotePlayer) Main.NetEntityHandler.NetToStreamedItem(PlayerChar.Handle, useGameHandle: true))
                .WeaponTints[weapon] = (byte) tint;
        }

        public int getPlayerWeaponTint(int weapon)
        {
            return ((RemotePlayer)Main.NetEntityHandler.NetToStreamedItem(Game.Player.Character.Handle, useGameHandle: true))
                .WeaponTints[weapon];
        }

        public void givePlayerWeaponComponent(int weapon, int component)
        {
            Game.Player.Character.Weapons[(GTA.WeaponHash)weapon].SetComponent((WeaponComponent) component, true);
        }

        public void removePlayerWeaponComponent(int weapon, int component)
        {
            Game.Player.Character.Weapons[(GTA.WeaponHash)weapon].SetComponent((WeaponComponent)component, false);
        }

        public bool hasPlayerWeaponComponent(int weapon, int component)
        {
            return Game.Player.Character.Weapons[(GTA.WeaponHash) weapon].IsComponentActive((WeaponComponent) component);
        }

        public WeaponComponent[] getAllWeaponComponents(WeaponHash weapon)
        {
            switch (weapon)
            {
                default:
                    return new WeaponComponent[0];
                case WeaponHash.Pistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.PistolClip01,
                        WeaponComponent.PistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp02,
                        WeaponComponent.PistolVarmodLuxe,
                    };
                case WeaponHash.CombatPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatPistolClip01,
                        WeaponComponent.CombatPistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.CombatPistolVarmodLowrider,
                    };
                case WeaponHash.APPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.APPistolClip01,
                        WeaponComponent.APPistolClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.APPistolVarmodLuxe,
                    };
                case WeaponHash.MicroSMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MicroSMGClip01,
                        WeaponComponent.MicroSMGClip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.MicroSMGVarmodLuxe,
                    };
                case WeaponHash.SMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SMGClip01,
                        WeaponComponent.SMGClip02,
                        WeaponComponent.SMGClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtPiSupp,
                        WeaponComponent.AtScopeMacro02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.SMGVarmodLuxe,
                    };
                case WeaponHash.AssaultRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultRifleClip01,
                        WeaponComponent.AssaultRifleClip02,
                        WeaponComponent.AssaultRifleClip03,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AssaultRifleVarmodLuxe,
                    };
                case WeaponHash.CarbineRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CarbineRifleClip01,
                        WeaponComponent.CarbineRifleClip02,
                        WeaponComponent.CarbineRifleClip03,
                        WeaponComponent.AtRailCover01,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.CarbineRifleVarmodLuxe,
                    };
                case WeaponHash.AdvancedRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AdvancedRifleClip01,
                        WeaponComponent.AdvancedRifleClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.AdvancedRifleVarmodLuxe,
                    };
                case WeaponHash.MG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MGClip01,
                        WeaponComponent.MGClip02,
                        WeaponComponent.AtScopeSmall02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.MGVarmodLowrider,
                    };
                case WeaponHash.CombatMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatMGClip01,
                        WeaponComponent.CombatMGClip02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.CombatMGVarmodLowrider,
                    };
                case WeaponHash.PumpShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtSrSupp,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.PumpShotgunVarmodLowrider,
                    };
                case WeaponHash.AssaultShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultShotgunClip01,
                        WeaponComponent.AssaultShotgunClip02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtArSupp,
                    };
                case WeaponHash.SniperRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SniperRifleClip01,
                        WeaponComponent.AtScopeLarge,
                        WeaponComponent.AtScopeMax,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.SniperRifleVarmodLuxe,
                    };
                case WeaponHash.HeavySniper:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.HeavySniperClip01,
                        WeaponComponent.AtScopeLarge,
                        WeaponComponent.AtScopeMax,
                    };
                case WeaponHash.GrenadeLauncher:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                    };
                case WeaponHash.Minigun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MinigunClip01,
                    };
                case WeaponHash.AssaultSMG:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AssaultSMGClip01,
                        WeaponComponent.AssaultSMGClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMacro,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AssaultSMGVarmodLowrider,
                    };
                case WeaponHash.BullpupShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtArSupp02,
                    };
                case WeaponHash.Pistol50:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.Pistol50Clip01,
                        WeaponComponent.Pistol50Clip02,
                        WeaponComponent.AtPiFlsh,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.Pistol50VarmodLuxe,
                    };
                case WeaponHash.CombatPDW:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.CombatPDWClip01,
                        WeaponComponent.CombatPDWClip02,
                        WeaponComponent.CombatPDWClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArAfGrip,
                    };
                case WeaponHash.SawnoffShotgun:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SawnoffShotgunVarmodLuxe,
                    };
                case WeaponHash.BullpupRifle:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.BullpupRifleClip01,
                        WeaponComponent.BullpupRifleClip02,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeSmall,
                        WeaponComponent.AtArSupp,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.BullpupRifleVarmodLow,
                    };
                case WeaponHash.SNSPistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SNSPistolClip01,
                        WeaponComponent.SNSPistolClip02,
                        WeaponComponent.SNSPistolVarmodLowrider,
                    };
                case WeaponHash.SpecialCarbine:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SpecialCarbineClip01,
                        WeaponComponent.SpecialCarbineClip02,
                        WeaponComponent.SpecialCarbineClip03,
                        WeaponComponent.AtArFlsh,
                        WeaponComponent.AtScopeMedium,
                        WeaponComponent.AtArSupp02,
                        WeaponComponent.AtArAfGrip,
                        WeaponComponent.SpecialCarbineVarmodLowrider,
                    };
                case WeaponHash.KnuckleDuster:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.KnuckleVarmodPimp,
                        WeaponComponent.KnuckleVarmodBallas,
                        WeaponComponent.KnuckleVarmodDollar,
                        WeaponComponent.KnuckleVarmodDiamond,
                        WeaponComponent.KnuckleVarmodHate,
                        WeaponComponent.KnuckleVarmodLove,
                        WeaponComponent.KnuckleVarmodPlayer,
                        WeaponComponent.KnuckleVarmodKing,
                        WeaponComponent.KnuckleVarmodVagos,
                    };
                case WeaponHash.MachinePistol:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.MachinePistolClip01,
                        WeaponComponent.MachinePistolClip02,
                        WeaponComponent.MachinePistolClip03,
                        WeaponComponent.AtPiSupp,
                    };
                case WeaponHash.SwitchBlade:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.SwitchbladeVarmodVar1,
                        WeaponComponent.SwitchbladeVarmodVar2,
                    };
                case WeaponHash.Revolver:
                    return new WeaponComponent[]
                    {
                        WeaponComponent.RevolverClip01,
                        WeaponComponent.RevolverVarmodBoss,
                        WeaponComponent.RevolverVarmodGoon,
                    };
            }
        }

        public int getPlayerCurrentWeapon()
        {
            return (int)Game.Player.Character.Weapons.Current.Hash;
        }

        public void disconnect(string reason)
        {
            Main.Client.Disconnect(reason);
        }

        public void setEntityPosition(LocalHandle ent, Vector3 pos, bool ground = false)
        {
            var handle = ent.Value;
            if (ground)
            {
                if (handle > 0) new Prop(handle).Position = new GTA.Math.Vector3(pos.X, pos.Y, getGroundHeight(pos));
                ent.Properties<EntityProperties>().Position = new Vector3(pos.X, pos.Y, getGroundHeight(pos));
            }
            else
            {
                if (handle > 0) new Prop(handle).Position = pos.ToVector();
                ent.Properties<EntityProperties>().Position = pos;
            }

        }

        public void setEntityRotation(LocalHandle ent, Vector3 rot)
        {
            var handle = ent.Value;
            if (handle > 0) new Prop(handle).Rotation = rot.ToVector();
            ent.Properties<EntityProperties>().Rotation = rot;
        }

        public void setPlayerIntoVehicle(LocalHandle vehicle, int seat)
        {
            Game.Player.Character.SetIntoVehicle(new Vehicle(vehicle.Value), (VehicleSeat)seat);
        }

        public void setPlayerHealth(int health)
        {
            Game.Player.Character.Health = health;
        }

        public int getPlayerHealth(LocalHandle player)
        {
            Ped PlayerChar = Game.Player.Character;
            return player.Value == PlayerChar.Handle ? PlayerChar.Health : findPlayer(player).PedHealth;
        }

        public void setTextLabelText(LocalHandle label, string text)
        {
            label.Properties<RemoteTextLabel>().Text = text;
        }

        public void setTextLabelColor(LocalHandle textLabel, int alpha, int r, int g, int b)
        {
            var p = textLabel.Properties<RemoteTextLabel>();

            p.Alpha = (byte)alpha;
            p.Red = (byte)r;
            p.Green = (byte)g;
            p.Blue = (byte)b;
        }

        public Color getTextLabelColor(LocalHandle textLabel)
        {
            var p = textLabel.Properties<RemoteTextLabel>();

            return Color.FromArgb(p.Alpha, p.Red, p.Green, p.Blue);
        }

        public void setTextLabelSeethrough(LocalHandle handle, bool seethrough)
        {
            handle.Properties<RemoteTextLabel>().EntitySeethrough = seethrough;
        }

        public bool getTextLabelSeethrough(LocalHandle handle)
        {
            return handle.Properties<RemoteTextLabel>().EntitySeethrough;
        }

        public Vector3 getOffsetInWorldCoords(LocalHandle entity, Vector3 offset)
        {
            return new Prop(entity.Value).GetOffsetInWorldCoords(offset.ToVector()).ToLVector();
        }

        public Vector3 getOffsetFromWorldCoords(LocalHandle entity, Vector3 pos)
        {
            return new Prop(entity.Value).GetOffsetFromWorldCoords(pos.ToVector()).ToLVector();
        }

        public void drawLine(Vector3 start, Vector3 end, int a, int r, int g, int b)
        {
            Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, r,g,b,a);
        }

        public void playSoundFrontEnd(string soundName, string soundSetName)
        {
            if (!SoundWhitelist.IsAllowed(soundName) || !SoundWhitelist.IsAllowed(soundSetName)) return;
            Function.Call((Hash)0x2F844A8B08D76685, soundSetName, true);
            Function.Call((Hash)0x67C540AA08E4A6F5, -1, soundName, soundSetName);
        }

        //public void playSoundFromEntity(LocalHandle entity, string soundName, string soundSetName)
        //{
        //    if (SoundWhitelist.IsAllowed(soundName) && SoundWhitelist.IsAllowed(soundSetName))
        //    {
        //        Function.Call((Hash)0x2F844A8B08D76685, soundSetName, true);
        //        Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, soundName, entity.Value, soundSetName, 0, 0);
        //    }
        //}

        //public void playSoundFromEntity2(LocalHandle entity, string soundName, string soundSetName)
        //{
        //    //if (entity.IsNull) return;
        //    Audio.PlaySoundFromEntity(new Prop(entity.Value), soundName, soundSetName);
        //}

        //public void playSoundFromCoord(Vector3 position, string soundName, string soundSetName)
        //{
        //    Function.Call((Hash)0x2F844A8B08D76685, soundSetName, true);
        //    Function.Call(Hash.PLAY_SOUND_FROM_COORD, soundName, position.X, position.Y, position.Z,  soundSetName, 0, 0, 0);
        //}

        public bool isEntityOnScreen(LocalHandle entity)
        {
            return !entity.IsNull && new Prop(entity.Value).IsOnScreen;
            //return Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, entity.Value);
        }

        public void displayHelpTextThisFrame(string text)
        {
            Function.Call(Hash.DISPLAY_HELP_TEXT_THIS_FRAME, text, false);
        }

        public void showShard(string text, int timeout = 5000)
        {
            NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage(text, timeout);
        }

        public void showColorShard(string text, string description, int color1, int color2, int time = 5000)
        {
            NativeUI.BigMessageThread.MessageInstance.ShowColoredShard(text, description, (HudColor) color1, (HudColor) color2, time);
        }

        public void showWeaponPurchasedShard(string text, string weaponName, int weapon, int time = 5000)
        {
            NativeUI.BigMessageThread.MessageInstance.ShowWeaponPurchasedMessage(text, weaponName, (GTA.WeaponHash) weapon, time);
        }

        public XmlGroup loadConfig(string config)
        {
            if (!config.EndsWith(".xml")) return null;
            var path = getResourceFilePath(config);

            var xml = new XmlGroup();
            xml.Load(path);
            return xml;
        }

        public dynamic fromJson(string json)
        {
            return JObject.Parse(json);
        }

        public string toJson(object data)
        {
            return JsonConvert.SerializeObject(data);
        }

        public void sendChatMessage(string sender, string text)
        {
            Main.Chat.AddMessage(sender, text);
        }

        public void sendChatMessage(string text)
        {
            Main.Chat.AddMessage(null, text);
        }

        public SizeF getScreenResolutionMantainRatio()
        {
            return UIMenu.GetScreenResolutionMantainRatio();
        }

        public SizeF getScreenResolutionMaintainRatio()
        {
            return UIMenu.GetScreenResolutionMantainRatio();
        }

        public Size getScreenResolution()
        {
            //return GTA.UI.Screen.Resolution;
            //return Screen.PrimaryScreen.WorkingArea.Size;
            return Main.screen;
        }

        public SharpDX.Size2 getScreenResolutionAccurate()
        {
            var dxgiFactory = new SharpDX.DXGI.Factory1();
            int width = ((SharpDX.Rectangle)dxgiFactory.Adapters[0].Outputs[0].Description.DesktopBounds).Width;
            int height = ((SharpDX.Rectangle)dxgiFactory.Adapters[0].Outputs[0].Description.DesktopBounds).Height;

            return new SharpDX.Size2(width, height);
        }

        public void sendNotification(string text)
        {
            Util.Util.SafeNotify(text);
        }

        public void displaySubtitle(string text)
        {
            GTA.UI.Screen.ShowSubtitle(text);
        }

        public void displaySubtitle(string text, double duration)
        {
            GTA.UI.Screen.ShowSubtitle(text, (int) duration);
        }

        public string formatTime(double ms, string format)
        {
            return TimeSpan.FromMilliseconds(ms).ToString(format);
        }

        public void setPlayerInvincible(bool invinc)
        {
            if (Main.NetEntityHandler.EntityToStreamedItem(Game.Player.Character.Handle) is RemotePlayer remotePlayer)
            {
                remotePlayer.IsInvincible = invinc;
            }

            Game.Player.IsInvincible = invinc;
        }

        public void setPlayerWantedLevel(int wantedLevel)
        {
            Function.Call(Hash.SET_FAKE_WANTED_LEVEL, wantedLevel);
        }

        public int getPlayerWantedLevel()
        {
            return Function.Call<int>((Hash)0x4C9296CBCD1B971E);
        }

        public bool getPlayerInvincible()
        {
            return Game.Player.IsInvincible;
        }
        
        public void setPlayerArmor(int armor)
        {
            Game.Player.Character.Armor = armor;
        }

        public int getPlayerArmor(LocalHandle player)
        {
            var PlayerChar = Game.Player.Character;
            return player.Value == PlayerChar.Handle ? PlayerChar.Armor : findPlayer(player).PedArmor;
        }

        public LocalHandle[] getStreamedPlayers()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item => item is SyncPed && item.StreamedIn)
                .Cast<SyncPed>().Select(op => new LocalHandle(op.Character?.Handle ?? 0)).ToArray();
        }

        public LocalHandle[] getStreamedVehicles()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemoteVehicle && item.StreamedIn).Cast<RemoteVehicle>().Select(op => new LocalHandle(op.LocalHandle)).ToArray();
        }

        public LocalHandle[] getStreamedObjects()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemoteProp && item.StreamedIn).Cast<RemoteProp>().Select(op => new LocalHandle(op.LocalHandle)).ToArray();
        }

        public LocalHandle[] getStreamedPickups()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemotePickup && item.StreamedIn)
                    .Cast<RemotePickup>().Select(op => new LocalHandle(op.LocalHandle)).ToArray();
        }

        public LocalHandle[] getStreamedPeds()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemotePed && item.StreamedIn)
                    .Cast<RemotePed>().Select(op => new LocalHandle(op.LocalHandle)).ToArray();
        }

        public LocalHandle[] getStreamedMarkers()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemoteMarker && item.StreamedIn)
                    .Cast<RemoteMarker>().Select(op => new LocalHandle(op.RemoteHandle, op.LocalOnly ? HandleType.LocalHandle : HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getStreamedTextLabels()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemoteTextLabel && item.StreamedIn)
                    .Cast<RemoteTextLabel>().Select(op => new LocalHandle(op.RemoteHandle, op.LocalOnly ? HandleType.LocalHandle : HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllPlayers()
        {
            return StreamerThread.SyncPeds.Select(op => new LocalHandle(op.RemoteHandle)).ToArray();
        }

        public LocalHandle[] getAllVehicles()
        {
            return Main.NetEntityHandler.ClientMap.Values.OfType<RemoteVehicle>().Select(op => new LocalHandle(op.RemoteHandle, HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllObjects()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item => item is RemoteProp)
                .Cast<RemoteProp>().Select(op => new LocalHandle(op.RemoteHandle, HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllPickups()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemotePickup)
                    .Cast<RemotePickup>().Select(op => new LocalHandle(op.RemoteHandle, HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllPeds()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemotePed)
                    .Cast<RemotePed>().Select(op => new LocalHandle(op.RemoteHandle, HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllMarkers()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemoteMarker)
                    .Cast<RemoteMarker>().Select(op => new LocalHandle(op.RemoteHandle, op.LocalOnly ? HandleType.LocalHandle : HandleType.NetHandle)).ToArray();
        }

        public LocalHandle[] getAllTextLabels()
        {
            return Main.NetEntityHandler.ClientMap.Values.Where(item =>
                item is RemoteTextLabel)
                    .Cast<RemoteTextLabel>().Select(op => new LocalHandle(op.RemoteHandle, op.LocalOnly ? HandleType.LocalHandle : HandleType.NetHandle)).ToArray();
        }

        public LocalHandle getPlayerVehicle(LocalHandle player)
        {
            return new LocalHandle(new Ped(player.Value).CurrentVehicle?.Handle ?? 0);
        }

        public void explodeVehicle(LocalHandle vehicle)
        {
            new Vehicle(vehicle.Value).Explode();
        }

        private SyncPed findPlayer(LocalHandle player)
        {
            return StreamerThread.SyncPeds.FirstOrDefault(p => p.RemoteHandle == player.Value || p.LocalHandle == player.Value);
        }

        public LocalHandle getPlayerByName(string name)
        {
            if (StreamerThread.SyncPeds.FirstOrDefault(op => op.Name == name) is SyncPed opp && opp.Character != null)
            {
                return new LocalHandle(opp.RemoteHandle, HandleType.NetHandle);
            }

            return new LocalHandle(0);
        }

        public string getPlayerName(LocalHandle player, bool cache = false)
        {
            if (cache && skipped < 200 && Names.ContainsKey(player))
            {
                skipped++;
                return Names[player];
            }
            skipped = 0;

            if (player == getLocalPlayer())
            {
                var name = Main.NetEntityHandler.ClientMap.Values.OfType<RemotePlayer>().First(op => op.LocalHandle == -2).Name;
                if (!cache) return name;
                if (Names.ContainsKey(player))
                {
                    Names.Add(player, name);
                }
                else
                {
                    Names[player] = name;
                }
                return name;
            }

            var opp = findPlayer(player);
            if (opp == null) return "Not Available";
            {
                var name = opp.Name;
                if (!cache) return name;
                if (Names.ContainsKey(player))
                {
                    Names.Add(player, name);
                }
                else
                {
                    Names[player] = name;
                }
                return name;
            }
        }

        public void forceSendAimData(bool force)
        {
            SyncCollector.ForceAimData = force;
        }

        public bool isAimDataForced()
        {
            return SyncCollector.ForceAimData;
        }

        public Vector3 getPlayerAimCoords(LocalHandle player)
        {
            if (player == getLocalPlayer()) return Main.RaycastEverything(new Vector2(0, 0)).ToLVector();

            var opp = findPlayer(player);
            return opp != null ? opp.AimCoords.ToLVector() : new Vector3();
        }

        private int skipped = 0;
        public int getPlayerPing(LocalHandle player, bool cache = false)
        {
            if (cache && skipped < 200 && Latencies.ContainsKey(player))
            {
                skipped++;
                return Latencies[player];
            }

            skipped = 0;
            if (player == getLocalPlayer())
            {
                var latency = (int)(Main.Latency * 1000f);
                if (!cache) return latency;

                if (Latencies.ContainsKey(player))
                {
                    Latencies.Add(player, latency);
                }
                else
                {
                    Latencies[player] = latency;
                }

                return latency;
            }

            var opp = findPlayer(player);
            if (opp == null) return 0;
            {
                var latency = (int)(opp.Latency * 1000f);
                if (!cache) return latency;

                if (Latencies.ContainsKey(player))
                {
                    Latencies.Add(player, latency);
                }
                else
                {
                    Latencies[player] = latency;
                }
                return latency;
            }
        }

        public LocalHandle createVehicle(int model, Vector3 pos, Vector3 rot)
        {
            var car = Main.NetEntityHandler.CreateLocalVehicle(model, pos, rot.Z);
            return new LocalHandle(car, HandleType.LocalHandle);
        }

        public LocalHandle createPed(int model, Vector3 pos, Vector3 rot)
        {
            var ped = Main.NetEntityHandler.CreateLocalPed(model, pos, rot.Z);
            return new LocalHandle(ped, HandleType.LocalHandle);
        }

        public LocalHandle createBlip(Vector3 pos)
        {
            var blip = Main.NetEntityHandler.CreateLocalBlip(pos);
            return new LocalHandle(blip, HandleType.LocalHandle);
        }

        public void setBlipPosition(LocalHandle blip, Vector3 pos)
        {
            var prop = blip.Properties<IStreamedItem>();

            if (prop != null)
            {
                if (blip.Properties<RemoteBlip>().StreamedIn)
                {
                    new Blip(blip.Value).Position = pos.ToVector();
                }
                blip.Properties<RemoteBlip>().Position = pos;
            }
            else
            {
                new Blip(blip.Value).Position = pos.ToVector();
            }
        }

        public Vector3 getBlipPosition(LocalHandle blip)
        {
            return new Blip(blip.Value).Exists() ? new Blip(blip.Value).Position.ToLVector() : blip.Properties<RemoteBlip>().Position;
        }

        public Vector3 getWaypointPosition()
        {
            var blip = World.GetWaypointBlip();
            return blip == null ? new Vector3() : blip.Position.ToLVector();
        }

        public bool isWaypointSet()
        {
            return Game.IsWaypointActive;
        }

        public void setWaypoint(double x, double y)
        {
            if (isWaypointSet()) removeWaypoint();

            Function.Call(Hash.SET_NEW_WAYPOINT, (float)x, (float)y);
        }

        public void removeWaypoint()
        {
            World.RemoveWaypoint();
        }

        public void setBlipColor(LocalHandle blip, int color)
        {
            if (blip.Properties<IStreamedItem>() == null)
            {
                new Blip(blip.Value).Color = (BlipColor)color;
                return;
            }

            if (blip.Properties<RemoteBlip>().StreamedIn)
                new Blip(blip.Value).Color = (BlipColor) color;
            blip.Properties<RemoteBlip>().Color = color;
        }

        public int getBlipColor(LocalHandle blip)
        {
            if (new Blip(blip.Value).Exists())
            {
                return (int)new Blip(blip.Value).Color;
            }

            return blip.Properties<RemoteBlip>().Color;
        }

        public void setBlipSprite(LocalHandle blip, int sprite)
        {
            if (blip.Properties<IStreamedItem>() == null)
            {
                new Blip(blip.Value).Sprite = (BlipSprite)sprite;
                return;
            }

            if (blip.Properties<RemoteBlip>().StreamedIn)
                new Blip(blip.Value).Sprite = (BlipSprite) sprite;
            blip.Properties<RemoteBlip>().Sprite = sprite;
        }

        public int getBlipSprite(LocalHandle blip)
        {
            if (new Blip(blip.Value).Exists())
            {
                return (int)new Blip(blip.Value).Sprite;
            }

            return blip.Properties<RemoteBlip>().Sprite;
        }

        public void setBlipName(LocalHandle blip, string name)
        {
            blip.Properties<RemoteBlip>().Name = name;
        }

        public string getBlipName(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().Name;
        }

        public void setBlipTransparency(LocalHandle blip, int alpha)
        {
            blip.Properties<RemoteBlip>().Alpha = (byte)alpha;
            Function.Call((Hash)0x45FF974EEE1C8734, blip.Value, alpha);
        }

        public int getBlipTransparency(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().Alpha;
        }

        public void setBlipShortRange(LocalHandle blip, bool shortRange)
        {
            if (blip.Properties<IStreamedItem>() == null)
            {
                new Blip(blip.Value).IsShortRange = shortRange;
                return;
            }

            if (blip.Properties<RemoteBlip>().StreamedIn)
                new Blip(blip.Value).IsShortRange = shortRange;
            blip.Properties<RemoteBlip>().IsShortRange = shortRange;
        }

        public bool getBlipShortRange(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().IsShortRange;
        }

        public void showBlipRoute(LocalHandle blip, bool show)
        {
            if (blip.Properties<IStreamedItem>() == null || blip.Properties<RemoteBlip>().StreamedIn)
            {
                new Blip(blip.Value).ShowRoute = show;
            }
        }

        public void setBlipScale(LocalHandle blip, double scale)
        {
            if (blip.Properties<IStreamedItem>() == null || blip.Properties<RemoteBlip>().StreamedIn)
                new Blip(blip.Value).Scale = (float)scale;
            blip.Properties<RemoteBlip>().Scale = (float)scale;
        }

        public float getBlipScale(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().Scale;
        }

        public void setBlipRouteVisible(LocalHandle blip, bool visible)
        {
            if (blip.Properties<IStreamedItem>() == null || blip.Properties<RemoteBlip>().StreamedIn)
                new Blip(blip.Value).ShowRoute = visible;
            blip.Properties<RemoteBlip>().RouteVisible = visible;
        }

        public bool getBlipRouteVisible(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().RouteVisible;
        }

        public void setBlipRouteColor(LocalHandle blip, int color)
        {
            if (blip.Properties<IStreamedItem>() == null || blip.Properties<RemoteBlip>().StreamedIn)
                Function.Call(Hash.SET_BLIP_ROUTE_COLOUR, blip.Value, color);
            blip.Properties<RemoteBlip>().RouteColor = color;
        }

        public int getBlipRouteColor(LocalHandle blip)
        {
            return blip.Properties<RemoteBlip>().RouteColor;
        }

        public void setChatVisible(bool display)
        {
            Main.ScriptChatVisible = display;
        }

        public bool getChatVisible()
        {
            return Main.ScriptChatVisible;
        }

        public float getAveragePacketSize()
        {
            float outp = 0;

            if (Main.AveragePacketSize.Count > 0)
                outp = (float)Main.AveragePacketSize.Average();

            return outp;
        }

        public float getBytesSentPerSecond()
        {
            return Main._bytesSentPerSecond;
        }

        public float getBytesReceivedPerSecond()
        {
            return Main._bytesReceivedPerSecond;
        }

        public void requestControlOfPlayer(LocalHandle player)
        {
            var opp = findPlayer(player);
            if (opp != null)
            {
                opp.IsBeingControlledByScript = true;
            }
        }

        public void stopControlOfPlayer(LocalHandle player)
        {
            var opp = findPlayer(player);
            if (opp != null)
            {
                opp.IsBeingControlledByScript = false;
            }
        }

        public void setHudVisible(bool visible)
        {
            Function.Call(Hash.DISPLAY_RADAR, visible);
            Function.Call(Hash.DISPLAY_HUD, visible);
        }

        public bool isSpectating()
        {
            return Main.IsSpectating;
        }

        public bool getHudVisible()
        {
            return !Function.Call<bool>(Hash.IS_HUD_HIDDEN);
        }

        public LocalHandle createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int r, int g, int b, int alpha, bool bobUpAndDown = false)
        {
            return new LocalHandle(Main.NetEntityHandler.CreateLocalMarker(markerType, pos.ToVector(), dir.ToVector(), rot.ToVector(), scale.ToVector(), alpha, r, g, b, 0, bobUpAndDown), HandleType.LocalHandle);
        }

        public void setMarkerType(LocalHandle marker, int type)
        {
            marker.Properties<RemoteMarker>().MarkerType = type;
        }

        public int getMarkerType(LocalHandle marker)
        {
            return marker.Properties<RemoteMarker>().MarkerType;
        }

        public void setMarkerBobUpAndDown(LocalHandle marker, bool state)
        {
            marker.Properties<RemoteMarker>().BobUpAndDown = state;
        }

        public bool getMarkerBobUpAndDown(LocalHandle marker)
        {
            return marker.Properties<RemoteMarker>().BobUpAndDown;
        }

        public void setMarkerColor(LocalHandle marker, int alpha, int r, int g, int b)
        {
            var p = marker.Properties<RemoteMarker>();

            p.Alpha = (byte) alpha;
            p.Red = (byte)r;
            p.Green = (byte)g;
            p.Blue = (byte)b;
        }

        public Color getMarkerColor(LocalHandle marker)
        {
            var p = marker.Properties<RemoteMarker>();

            return Color.FromArgb(p.Alpha, p.Red, p.Green, p.Blue);
        }

        public void setMarkerScale(LocalHandle marker, Vector3 scale)
        {
            var delta = new Delta_MarkerProperties {Scale = scale};

            Main.NetEntityHandler.UpdateMarker(marker.Value, delta, true);
        }

        public Vector3 getMarkerScale(LocalHandle marker)
        {
            return marker.Properties<RemoteMarker>().Scale;
        }

        public void setMarkerDirection(LocalHandle marker, Vector3 dir)
        {
            var delta = new Delta_MarkerProperties {Direction = dir};

            Main.NetEntityHandler.UpdateMarker(marker.Value, delta, true);
        }

        public Vector3 getMarkerDirection(LocalHandle marker)
        {
            return marker.Properties<RemoteMarker>().Direction;
        }

        public void deleteEntity(LocalHandle handle)
        {
            var item = handle.Properties<IStreamedItem>();
            if (item != null)
            {
                Main.NetEntityHandler.StreamOut(item);
                Main.NetEntityHandler.Remove(item);
            }
        }

        public void attachEntity(LocalHandle ent1, LocalHandle ent2, string bone, Vector3 positionOffset, Vector3 rotationOffset)
        {
            if (ent1.Properties<IStreamedItem>().AttachedTo != null)
            {
                Main.NetEntityHandler.DetachEntity(ent1.Properties<IStreamedItem>(), false);
            }

            Main.NetEntityHandler.AttachEntityToEntity(ent1.Properties<IStreamedItem>(), ent2.Properties<IStreamedItem>(), new Attachment()
            {
                Bone = bone,
                PositionOffset = positionOffset,
                RotationOffset = rotationOffset,
                NetHandle = ent2.Properties<IStreamedItem>().RemoteHandle,
            });
        }

        public void detachEntity(LocalHandle ent)
        {
            Main.NetEntityHandler.DetachEntity(ent.Properties<IStreamedItem>(), false);
        }

        public bool isEntityAttachedToAnything(LocalHandle ent)
        {
            return ent.Properties<IStreamedItem>() != null;
        }

        public bool isEntityAttachedToEntity(LocalHandle from, LocalHandle to)
        {
            return (from.Properties<IStreamedItem>().AttachedTo?.NetHandle ?? 0) == to.Properties<IStreamedItem>().RemoteHandle;
        }

        public LocalHandle createTextLabel(string text, Vector3 pos, double range, double size, bool entitySeethrough = false)
        {
            return new LocalHandle(Main.NetEntityHandler.CreateLocalLabel(text, pos.ToVector(), (float)range, (float)size, entitySeethrough, 0), HandleType.LocalHandle);
        }

        internal string getResourceFilePath(string fileName)
        {
            if (!isPathSafe(fileName)) throw new AccessViolationException("Illegal path to file!");
            return FileTransferId._DOWNLOADFOLDER_ + ParentResourceName + "\\" + fileName;
        }

        public Vector3 lerpVector(Vector3 start, Vector3 end, double currentTime, double duration)
        {
            return GTA.Math.Vector3.Lerp(start.ToVector(), end.ToVector(), (float) (currentTime/duration)).ToLVector();
        }

        public double lerpFloat(double start, double end, double currentTime, double duration)
        {
            return Util.Util.LinearFloatLerp((float) start, (float) end, (int) currentTime, (int) duration);
        }

        public bool isInRangeOf(Vector3 entity, Vector3 destination, double range)
        {
            return (entity - destination).LengthSquared() < (range*range);
        }
        
        public void dxDrawTexture(string path, Point pos, Size size, double rotation = 0, int r = 255, int g = 255, int b = 255, int a = 255)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            if (!isPathSafe(path)) throw new Exception("Illegal path for texture!");
            path = getResourceFilePath(path);
            Util.Util.DxDrawTexture(60, path, pos.X, pos.Y, size.Width, size.Height, (float) rotation, r, g, b, a);
        }

        public void drawGameTexture(string dict, string txtName, double x, double y, double width, double height, double heading, int r, int g, int b, int alpha)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            const float hh = 1080f;
            float ratio = (float)Main.screen.Width / Main.screen.Height;
            var ww = hh * ratio;


            float w = (float)(width / ww);
            float h = (float)(height / hh);
            float xx = (float)(x / ww) + w * 0.5f;
            float yy = (float)(y / hh) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, txtName, xx, yy, w, h, heading, r, g, b, alpha);
        }

        public void drawRectangle(double xPos, double yPos, double wSize, double hSize, int r, int g, int b, int alpha)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;

            float w = (float)wSize / width;
            float h = (float)hSize / height;
            float x = ((float)xPos / width) + w * 0.5f;
            float y = ((float)yPos / height) + h * 0.5f;

            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
        }

        public void drawText(string caption, double xPos, double yPos, double scale, int r, int g, int b, int alpha, int font, int justify, bool shadow, bool outline, int wordWrap)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;

            float x = (float) xPos / width;
            float y = (float) yPos / height;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, alpha);
            if (shadow) Function.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (outline) Function.Call(Hash.SET_TEXT_OUTLINE);
            switch (justify)
            {
                case 1:
                    Function.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case 2:
                    Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                    Function.Call(Hash.SET_TEXT_WRAP, 0, x);
                    break;
            }

            if (wordWrap != 0)
            {
                float xsize = (float)(xPos + wordWrap) / width;
                Function.Call(Hash.SET_TEXT_WRAP, x, xsize);
            }

            //Function.Call(Hash._SET_TEXT_ENTRY, "CELL_EMAIL_BCON");
            Function.Call(Hash._SET_NOTIFICATION_TEXT_ENTRY, new InputArgument(Main.StringCache.GetCached("CELL_EMAIL_BCON")));
            //NativeUI.UIResText.AddLongString(caption);

            const int maxStringLength = 99;
            int count = caption.Length;
            for (int i = 0; i < count; i += maxStringLength)
            {
                //Function.Call((Hash) 0x6C188BE134E074AA,
                //    new InputArgument(
                //        Main.StringCache.GetCached(caption.Substring(i,
                //            System.Math.Min(maxStringLength, caption.Length - i)))));
                Function.Call((Hash)0x6C188BE134E074AA, caption.Substring(i, System.Math.Min(maxStringLength, caption.Length - i)));
            }

            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        public UIResText addTextElement(string caption, double x, double y, double scale, int r, int g, int b, int a, int font, int alignment)
        {
            var txt = new UIResText(caption, new Point((int) x, (int) y), (float) scale, Color.FromArgb(a, r, g, b),
                (GTA.UI.Font) font, (UIResText.Alignment) alignment);
            JavascriptHook.TextElements.Add(txt);
            
            return txt;
        }

        public int getGameTime()
        {
            return Game.GameTime;
        }

        public int getGlobalTime()
        {
            return Environment.TickCount;
        }

        public double angleBetween(Vector3 from, Vector3 to)
        {
            return Math.Abs((Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X)) * (180.0 / Math.PI));
        }

        public bool isPed(LocalHandle ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent.Value);
        }

        public bool isVehicle(LocalHandle ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_VEHICLE, ent.Value);
        }

        public bool isProp(LocalHandle ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_AN_OBJECT, ent.Value);
        }

        //public float toFloat(double d)
        //{
        //    return (float) d;
        //}

        internal struct fArg
        {
            public float Value;

            public fArg(double f)
            {
                Value = (float)f;
            }
        }

        internal fArg f(double value)
        {
            return new fArg(value);
        }

        public void sleep(int ms)
        {
            var start = DateTime.Now;
            //do
            //{
            //    if (isDisposing) throw new Exception("resource is terminating");
            //    Script.Wait(0);
            //} while (DateTime.Now.Subtract(start).TotalMilliseconds < ms);

            do
            {
                if (isDisposing) throw new Exception("resource is terminating");
                Script.Yield();
            } while (DateTime.Now.Subtract(start).TotalMilliseconds < ms);
        }

        public void startAudio(string path, bool looped = false)
        {
            //if (path.StartsWith("http")) return;
            path = getResourceFilePath(path);
            AudioThread.StartAudio(path, looped);
        }

        public void pauseAudio()
        {
            JavascriptHook.AudioDevice?.Pause();
        }

        public void resumeAudio()
        {
            JavascriptHook.AudioDevice?.Play();
        }

        public void setAudioTime(double seconds)
        {
            if (JavascriptHook.AudioReader != null)
                JavascriptHook.AudioReader.CurrentTime = TimeSpan.FromSeconds(seconds);
        }

        public double getAudioTime()
        {
            if (JavascriptHook.AudioReader != null) return JavascriptHook.AudioReader.CurrentTime.TotalSeconds;
            return 0;
        }

        public bool isAudioPlaying()
        {
            if (JavascriptHook.AudioDevice != null) return JavascriptHook.AudioDevice.PlaybackState == PlaybackState.Playing;
            return false;
        }

        public void setGameVolume(double vol)
        {
            if (JavascriptHook.AudioDevice != null) JavascriptHook.AudioDevice.Volume = (float)vol;
        }

        public bool isAudioInitialized()
        {
            return JavascriptHook.AudioDevice != null;
        }

        public void stopAudio()
        {
            AudioThread.DisposeAudio();
        }

        public void triggerServerEvent(string eventName, params object[] arguments)
        {
            Main.TriggerServerEvent(eventName, ParentResourceName, arguments);
        }

        public string toString(object obj)
        {
            return obj.ToString();
        }

        public string getBoneName(int bone)
        {
            return ((Bone) bone).ToString();
        }

        public string getWeaponName(int weapon)
        {
            return ((WeaponHash) weapon).ToString();
        }

        public string getVehicleModelName(int model)
        {
            return ((VehicleHash) model).ToString();
        }

        public delegate void ServerEventTrigger(string eventName, object[] arguments);
        public delegate void ChatEvent(string msg);
        public delegate void StreamEvent(LocalHandle item, int entityType);
        public delegate void DataChangedEvent(LocalHandle entity, string key, object oldValue);
        public delegate void CustomDataReceived(string data);
        public delegate void EmptyEvent();
        public delegate void EntityEvent(LocalHandle entity);
        public delegate void PlayerKilledEvent(LocalHandle killer, int weapon);
        public delegate void IntChangeEvent(int oldValue);
        //public delegate void BoolChangeEvent(bool oldValue);
        public delegate void PlayerDamageEvent(LocalHandle attacker, int weaponUsed, int boneHit);
        public delegate void PlayerMeleeDamageEvent(LocalHandle attacker, int weaponUsed);
        public delegate void WeaponShootEvent(int weaponUsed, Vector3 aimCoords);

        public event EmptyEvent onResourceStart;
        public event EmptyEvent onResourceStop;
        public event EmptyEvent onUpdate;
        public event KeyEventHandler onKeyDown;
        public event KeyEventHandler onKeyUp;
        public event ServerEventTrigger onServerEventTrigger;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;
        public event StreamEvent onEntityStreamIn;
        public event StreamEvent onEntityStreamOut;
        public event DataChangedEvent onEntityDataChange;
        public event CustomDataReceived onCustomDataReceived;
        public event PlayerKilledEvent onPlayerDeath;
        public event EmptyEvent onPlayerRespawn;
        public event EntityEvent onPlayerPickup;
        public event EntityEvent onPlayerEnterVehicle;
        public event EntityEvent onPlayerExitVehicle;
        public event IntChangeEvent onVehicleHealthChange;
        public event IntChangeEvent onVehicleDoorBreak;
        public event IntChangeEvent onVehicleWindowSmash;
        public event IntChangeEvent onPlayerHealthChange;
        public event IntChangeEvent onPlayerArmorChange;
        public event IntChangeEvent onPlayerWeaponSwitch;
        public event IntChangeEvent onPlayerModelChange;
        public event EmptyEvent onVehicleSirenToggle;
        public event EmptyEvent onPlayerDetonateStickies;
        public event IntChangeEvent onVehicleTyreBurst;
        public event PlayerDamageEvent onLocalPlayerDamaged;
        public event PlayerMeleeDamageEvent onLocalPlayerMeleeHit;
        public event WeaponShootEvent onLocalPlayerShoot;

        internal void invokeonLocalPlayerShoot(int weaponUsed, Vector3 target)
        {
            onLocalPlayerShoot?.Invoke(weaponUsed, target);
        }

        internal void invokeonLocalPlayerMeleeHit(LocalHandle player, int weaponUsed)
        {
            onLocalPlayerMeleeHit?.Invoke(player, weaponUsed);
        }

        internal void invokeonLocalPlayerDamaged(LocalHandle player, int weaponUsed, int bone/*, byte[] health, byte[] armor*/)
        {
            onLocalPlayerDamaged?.Invoke(player, weaponUsed, bone/*, BitConverter.ToInt32(health, 0), BitConverter.ToInt32(armor, 0)*/);
        }

        internal void invokeonPlayerDetonateStickies()
        {
            onPlayerDetonateStickies?.Invoke();
        }

        internal void invokeonVehicleSirenToggle()
        {
            onVehicleSirenToggle?.Invoke();
        }

        internal void invokeonPlayerModelChange(int old)
        {
            onPlayerModelChange?.Invoke(old);
        }

        internal void invokeonVehicleTyreBurst(int old)
        {
            onVehicleTyreBurst?.Invoke(old);
        }

        internal void invokeonPlayerWeaponSwitch(int old)
        {
            onPlayerWeaponSwitch?.Invoke(old);
        }

        internal void invokeonPlayerArmorChange(int old)
        {
            onPlayerArmorChange?.Invoke(old);
        }

        internal void invokeonPlayerHealthChange(int old)
        {
            onPlayerHealthChange?.Invoke(old);
        }

        internal void invokeonVehicleWindowSmash(int old)
        {
            onVehicleWindowSmash?.Invoke(old);
        }

        internal void invokeonVehicleDoorBreak(int old)
        {
            onVehicleDoorBreak?.Invoke(old);
        }

        internal void invokeonVehicleHealthChange(int old)
        {
            onVehicleHealthChange?.Invoke(old);
        }

        internal void invokeonPlayerExitVehicle(LocalHandle veh)
        {
            onPlayerExitVehicle?.Invoke(veh);
        }

        internal void invokeonPlayerEnterVehicle(LocalHandle veh)
        {
            onPlayerEnterVehicle?.Invoke(veh);
        }

        internal void invokeonPlayerPickup(LocalHandle entity)
        {
            onPlayerPickup?.Invoke(entity);
        }

        internal void invokeonPlayerRespawn()
        {
            onPlayerRespawn?.Invoke();
        }

        internal void invokeonPlayerDeath(LocalHandle item, int weapon)
        {
            onPlayerDeath?.Invoke(item, weapon);
        }

        internal void invokeEntityStreamIn(LocalHandle item, int type)
        {
            onEntityStreamIn?.Invoke(item, type);
        }

        internal void invokeCustomDataReceived(string data)
        {
            onCustomDataReceived?.Invoke(data);
        }

        internal void invokeEntityDataChange(LocalHandle item, string key, object oldValue)
        {
            onEntityDataChange?.Invoke(item, key, oldValue);
        }

        internal void invokeEntityStreamOut(LocalHandle item, int type)
        {
            onEntityStreamOut?.Invoke(item, type);
        }

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
            onResourceStart?.Invoke();
        }

        internal void invokeUpdate()
        {
            onUpdate?.Invoke();
        }

        internal void invokeResourceStop()
        {
            onResourceStop?.Invoke();
        }

        internal void invokeKeyDown(object sender, KeyEventArgs e)
        {
            onKeyDown?.Invoke(sender, e);
        }

        internal void invokeKeyUp(object sender, KeyEventArgs e)
        {
            onKeyUp?.Invoke(sender, e);
        }

#region Menus

        public UIMenu createMenu(string title, double x, double y, int anchor)
        {
            return createMenu(null, title, x, y, anchor, false);
        }

        public UIMenu createMenu(string title, string subtitle, double x, double y, int anchor, bool enableBanner = true, bool disableControls = false, bool scaleWithSafezone = false)
        {
            var offset = convertAnchorPos((float)x, (float)y - 107, (Anchor)anchor, 431f, 107f + 38 + 38f * 10);
            if (string.IsNullOrEmpty(subtitle))
            {
                subtitle = "Available Options:";
            }

            if (string.IsNullOrEmpty(title))
            {
                title = "Menu";
            }

            var newM = new UIMenu(title, subtitle, new Point((int)offset.X, (int)offset.Y))
            {
                ScaleWithSafezone = scaleWithSafezone,
                ControlDisablingEnabled = disableControls,
                
            };

            if (enableBanner)
            {
                newM.SetBannerType(new UIResRectangle());
            }

            return newM;
        }

        public UIMenuItem createMenuItem(string label, string description)
        {
            return new UIMenuItem(label, description);
        }

        public UIMenuColoredItem createColoredItem(string label, string description, string hexColor, string hexHighlightColor)
        {
            return new UIMenuColoredItem(label, description, ColorTranslator.FromHtml(hexColor), ColorTranslator.FromHtml(hexHighlightColor));
        }

        public UIMenuCheckboxItem createCheckboxItem(string label, string description, bool isChecked)
        {
            return new UIMenuCheckboxItem(label, isChecked, description);
        }

        public UIMenuListItem createListItem(string label, string description, List<string> items, int index)
        {
            return new UIMenuListItem(label, items.Select(s => (dynamic)s).ToList(), index, description);
        }

        public MenuPool getMenuPool()
        {
            return new MenuPool();
        }

        public void drawMenu(UIMenu menu)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            
            if (!Main.Chat.IsFocused)
            {
                menu.ProcessControl();
                menu.ProcessMouse();
            }

            menu.Draw();

            if (!menu.Visible) return;
            Game.DisableControlThisFrame(Control.NextCamera);
            Game.DisableControlThisFrame(Control.NextWeapon);
            Game.DisableControlThisFrame(Control.VehicleNextRadio);
            Game.DisableControlThisFrame(Control.LookLeftRight);
            Game.DisableControlThisFrame(Control.LookUpDown);
        }

        public void setMenuBannerSprite(UIMenu menu, string spritedict, string spritename)
        {
            menu.SetBannerType(new Sprite(spritedict, spritename, new Point(), new Size()));
        }

        public void setMenuBannerTexture(UIMenu menu, string path)
        {
            var realpath = getResourceFilePath(path);
            menu.SetBannerType(realpath);
        }

        public void setMenuBannerRectangle(UIMenu menu, int alpha, int red, int green, int blue)
        {
            menu.SetBannerType(new UIResRectangle(new Point(), new Size(), Color.FromArgb(alpha, red, green, blue)));
        }

        public void setMenuTitle(UIMenu menu, string title)
        {
            menu.Title.Caption = title;
        }

        public void setMenuSubtitle(UIMenu menu, string text)
        {
            menu.Subtitle.Caption = text;
        }

        public string getUserInput(string defaultText, int maxlen = 0)
        {
            return Game.GetUserInput(defaultText);
        }

        internal PointF convertAnchorPos(float x, float y, Anchor anchor, float xOffset, float yOffset)
        {
            var res = UIMenu.GetScreenResolutionMantainRatio();
            //var res = getScreenResolution();
            switch (anchor)
            {
                //case Anchor.TopLeft:
                //    return new PointF(x + 10, y + 13);
                //case Anchor.MiddleLeft:
                //    return new PointF(x + 10, res.Height / 2 + y - 27);
                //case Anchor.BottomLeft:
                //    return new PointF(x + 10, res.Height - (y + 413));
                //case Anchor.TopCenter:
                //    return new PointF(res.Width / 2 + x - 221, y + 13);
                //case Anchor.MiddleCenter:
                //    return new PointF(res.Width / 2 + x - 221, res.Height / 2 + y - 27);
                //case Anchor.BottomCenter:
                //    return new PointF(res.Width / 2 + x - 221, res.Height - (y + 413));
                //case Anchor.TopRight:
                //    return new PointF(res.Width - x - 441, y + 13);
                //case Anchor.MiddleRight:
                //    return new PointF(res.Width - x - 441, res.Height / 2 + y - 27);
                //case Anchor.BottomRight:
                //    return new PointF(res.Width - x - 441, res.Height - (y + 413));
                case Anchor.TopLeft:
                    return new PointF(x, y);
                case Anchor.TopCenter:
                    return new PointF(res.Width / 2 + x, 0 + y);
                case Anchor.TopRight:
                    return new PointF(res.Width - x - xOffset, y);
                case Anchor.MiddleLeft:
                    return new PointF(x, res.Height / 2 + y - yOffset / 2);
                case Anchor.MiddleCenter:
                    return new PointF(res.Width / 2 + x, res.Height / 2 + y - yOffset / 2);
                case Anchor.MiddleRight:
                    return new PointF(res.Width - x - xOffset, res.Height / 2 + y - yOffset / 2);
                case Anchor.BottomLeft:
                    return new PointF(x, res.Height - y - yOffset);
                case Anchor.BottomCenter:
                    return new PointF(res.Width / 2 + x, res.Height - y - yOffset);
                case Anchor.BottomRight:
                    return new PointF(res.Width - x, res.Height - y - yOffset);
                default:
                    return PointF.Empty;
            }
        }

        internal enum Anchor
        {
            TopLeft = 0,
            TopCenter = 1,
            TopRight = 2,
            MiddleLeft = 3,
            MiddleCenter = 4,
            MiddleRight = 6,
            BottomLeft = 7,
            BottomCenter = 8,
            BottomRight = 9,
        }

        #endregion

        public bool isControlJustPressed(int control)
        {
            return Game.IsControlJustPressed((GTA.Control) control);
        }

        public bool isControlPressed(int control)
        {
            return Game.IsControlPressed((GTA.Control)control);
        }

        public bool isDisabledControlJustReleased(int control)
        {
            return Game.IsDisabledControlJustReleased((GTA.Control)control);
        }

        public bool isDisabledControlJustPressed(int control)
        {
            return Game.IsDisabledControlJustPressed((GTA.Control)control);
        }

        public bool isDisabledControlPressed(int control)
        {
            return Game.IsControlPressed((GTA.Control)control);
        }

        public bool isControlJustReleased(int control)
        {
            return Game.IsControlJustReleased((GTA.Control)control);
        }

        public void disableControlThisFrame(int control)
        {
            Game.DisableControlThisFrame((GTA.Control)control);
        }

        public void enableControlThisFrame(int control)
        {
            Game.EnableControlThisFrame((GTA.Control)control);
        }

        public void disableAllControlsThisFrame()
        {
            Game.DisableAllControlsThisFrame();
        }

        public float getControlNormal(int control)
        {
            return Game.GetControlValueNormalized((GTA.Control) control);
        }

        public float getDisabledControlNormal(int control)
        {
            return Game.GetDisabledControlValueNormalized((GTA.Control)control);
        }

        public void setControlNormal(int control, double value)
        {
            Game.SetControlValueNormalized((GTA.Control)control, (float)value);
        }

        public bool isChatOpen()
        {
            return Main.Chat.IsFocused;
        }

        public void loadAnimationDict(string dict)
        {
            Util.Util.LoadDict(dict);
        }

        public void loadModel(int model)
        {
            Util.Util.LoadModel(new Model(model));
        }

        public Scaleform requestScaleform(string scaleformName)
        {
            var sc = new Scaleform(scaleformName);
            return sc;
        }

        public void renderScaleform(Scaleform sc, double x, double y, double w, double h)
        {
            sc.Render2DScreenSpace(new PointF((float) x, (float) y), new PointF((float) w, (float) h));
        }

        public void setEntityTransparency(LocalHandle entity, int alpha)
        {
            if (alpha == 255)
            {
                Function.Call(Hash.RESET_ENTITY_ALPHA, entity.Value);
                return;
            }

            new Prop(entity.Value).Opacity = alpha;
        }

        public int getEntityType(LocalHandle entity)
        {
            return entity.Properties<IStreamedItem>().EntityType;
        }

        public int getEntityTransparency(LocalHandle entity)
        {
            return new Prop(entity.Value).Opacity;
        }

        public void setEntityDimension(LocalHandle entity, int dimension)
        {
            entity.Properties<IStreamedItem>().Dimension = dimension;
        }

        public int getEntityDimension(LocalHandle entity)
        {
            return entity.Properties<IStreamedItem>().Dimension;
        }

        public int getEntityModel(LocalHandle entity)
        {
            return new Prop(entity.Value).Model.Hash;
        }

        public int getEntityBoneIndexByName(LocalHandle entity, string boneName)
        {
            return Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, entity.Value, boneName);
        }

        //public void givePlayerWeapon(int weapon, int ammo, bool equipNow, bool ammoLoaded)
        //{
        //    CrossReference.EntryPoint.WeaponInventoryManager.Allow((WeaponHash) weapon);
        //    Main.PlayerChar.Weapons.Give((GTA.WeaponHash) weapon, ammo, equipNow, ammoLoaded);
        //}

        public void removeAllPlayerWeapons()
        {
            Game.Player.Character.Weapons.RemoveAll();
            CrossReference.EntryPoint.WeaponInventoryManager.Clear();
        }

        public bool doesPlayerHaveWeapon(int weapon)
        {
            return Game.Player.Character.Weapons.HasWeapon((GTA.WeaponHash) weapon);
        }

        public void removePlayerWeapon(int weapon)
        {
            CrossReference.EntryPoint.WeaponInventoryManager.Deny((WeaponHash) weapon);
        }

        public void setWeather(int weather)
        {
            if (weather >= 0 && weather < Enums._weather.Length)
                Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Enums._weather[weather]);
        }

        public int getWeather()
        {
            return Array.IndexOf(Enums._weather, Main.Weather.ToUpper());
        }

        public void resetWeather()
        {
            Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Main.Weather);
        }

        public void setTime(double hours, double minutes)
        {
            World.CurrentTimeOfDay = new TimeSpan((int) hours, (int) minutes, 00);
        }

        public TimeSpan getTime()
        {
            return World.CurrentTimeOfDay;
        }
        
        public void resetTime()
        {
            if (Main.Time != null)
            {
                World.CurrentTimeOfDay = Main.Time.Value;
            }
        }

        internal string[] ScreenEffects = {
            "SwitchHUDIn",
            "SwitchHUDOut",
            "FocusIn",
            "FocusOut",
            "MinigameEndNeutral",
            "MinigameEndTrevor",
            "MinigameEndFranklin",
            "MinigameEndMichael",
            "MinigameTransitionOut",
            "MinigameTransitionIn",
            "SwitchShortNeutralIn",
            "SwitchShortFranklinIn",
            "SwitchShortTrevorIn",
            "SwitchShortMichaelIn",
            "SwitchOpenMichaelIn",
            "SwitchOpenFranklinIn",
            "SwitchOpenTrevorIn",
            "SwitchHUDMichaelOut",
            "SwitchHUDFranklinOut",
            "SwitchHUDTrevorOut",
            "SwitchShortFranklinMid",
            "SwitchShortMichaelMid",
            "SwitchShortTrevorMid",
            "DeathFailOut",
            "CamPushInNeutral",
            "CamPushInFranklin",
            "CamPushInMichael",
            "CamPushInTrevor",
            "SwitchOpenMichaelIn",
            "SwitchSceneFranklin",
            "SwitchSceneTrevor",
            "SwitchSceneMichael",
            "SwitchSceneNeutral",
            "MP_Celeb_Win",
            "MP_Celeb_Win_Out",
            "MP_Celeb_Lose",
            "MP_Celeb_Lose_Out",
            "DeathFailNeutralIn",
            "DeathFailMPDark",
            "DeathFailMPIn",
            "MP_Celeb_Preload_Fade",
            "PeyoteEndOut",
            "PeyoteEndIn",
            "PeyoteIn",
            "PeyoteOut",
            "MP_race_crash",
            "SuccessFranklin",
            "SuccessTrevor",
            "SuccessMichael",
            "DrugsMichaelAliensFightIn",
            "DrugsMichaelAliensFight",
            "DrugsMichaelAliensFightOut",
            "DrugsTrevorClownsFightIn",
            "DrugsTrevorClownsFight",
            "DrugsTrevorClownsFightOut",
            "HeistCelebPass",
            "HeistCelebPassBW",
            "HeistCelebEnd",
            "HeistCelebToast",
            "MenuMGHeistIn",
            "MenuMGTournamentIn",
            "MenuMGSelectionIn",
            "ChopVision",
            "DMT_flight_intro",
            "DMT_flight",
            "DrugsDrivingIn",
            "DrugsDrivingOut",
            "SwitchOpenNeutralFIB5",
            "HeistLocate",
            "MP_job_load",
            "RaceTurbo",
            "MP_intro_logo",
            "HeistTripSkipFade",
            "MenuMGHeistOut",
            "MP_corona_switch",
            "MenuMGSelectionTint",
            "SuccessNeutral",
            "ExplosionJosh3",
            "SniperOverlay",
            "RampageOut",
            "Rampage",
            "Dont_tazeme_bro",
            "DeathFailOut",
        };

        public void playScreenEffect(string effectName, int duration, bool looped)
        {
            if (!ScreenEffects.Contains(effectName)) return;
            Function.Call(Hash._START_SCREEN_EFFECT, effectName, duration, looped);
        }

        internal string[] PoliceScanner = {
            "LAMAR_1_POLICE_LOST",
            "SCRIPTED_SCANNER_REPORT_AH_3B_01",
            "SCRIPTED_SCANNER_REPORT_AH_MUGGING_01",
            "SCRIPTED_SCANNER_REPORT_AH_PREP_01",
            "SCRIPTED_SCANNER_REPORT_AH_PREP_02",
            "SCRIPTED_SCANNER_REPORT_ARMENIAN_1_01",
            "SCRIPTED_SCANNER_REPORT_ARMENIAN_1_02",
            "SCRIPTED_SCANNER_REPORT_ASS_BUS_01",
            "SCRIPTED_SCANNER_REPORT_ASS_MULTI_01",
            "SCRIPTED_SCANNER_REPORT_BARRY_3A_01",
            "SCRIPTED_SCANNER_REPORT_BS_2A_01",
            "SCRIPTED_SCANNER_REPORT_BS_2B_01",
            "SCRIPTED_SCANNER_REPORT_BS_2B_02",
            "SCRIPTED_SCANNER_REPORT_BS_2B_03",
            "SCRIPTED_SCANNER_REPORT_BS_2B_04",
            "SCRIPTED_SCANNER_REPORT_BS_PREP_A_01",
            "SCRIPTED_SCANNER_REPORT_BS_PREP_B_01",
            "SCRIPTED_SCANNER_REPORT_CAR_STEAL_2_01",
            "SCRIPTED_SCANNER_REPORT_CAR_STEAL_4_01",
            "SCRIPTED_SCANNER_REPORT_DH_PREP_1_01",
            "SCRIPTED_SCANNER_REPORT_FIB_1_01",
            "SCRIPTED_SCANNER_REPORT_FIN_C2_01",
            "SCRIPTED_SCANNER_REPORT_Franklin_2_01",
            "SCRIPTED_SCANNER_REPORT_FRANLIN_0_KIDNAP",
            "SCRIPTED_SCANNER_REPORT_GETAWAY_01",
            "SCRIPTED_SCANNER_REPORT_JOSH_3_01",
            "SCRIPTED_SCANNER_REPORT_JOSH_4_01",
            "SCRIPTED_SCANNER_REPORT_JSH_2A_01",
            "SCRIPTED_SCANNER_REPORT_JSH_2A_02",
            "SCRIPTED_SCANNER_REPORT_JSH_2A_03",
            "SCRIPTED_SCANNER_REPORT_JSH_2A_04",
            "SCRIPTED_SCANNER_REPORT_JSH_2A_05",
            "SCRIPTED_SCANNER_REPORT_JSH_PREP_1A_01",
            "SCRIPTED_SCANNER_REPORT_JSH_PREP_1B_01",
            "SCRIPTED_SCANNER_REPORT_JSH_PREP_2A_01",
            "SCRIPTED_SCANNER_REPORT_JSH_PREP_2A_02",
            "SCRIPTED_SCANNER_REPORT_LAMAR_1_01",
            "SCRIPTED_SCANNER_REPORT_MIC_AMANDA_01",
            "SCRIPTED_SCANNER_REPORT_NIGEL_1A_01",
            "SCRIPTED_SCANNER_REPORT_NIGEL_1D_01",
            "SCRIPTED_SCANNER_REPORT_PS_2A_01",
            "SCRIPTED_SCANNER_REPORT_PS_2A_02",
            "SCRIPTED_SCANNER_REPORT_PS_2A_03",
            "SCRIPTED_SCANNER_REPORT_SEC_TRUCK_01",
            "SCRIPTED_SCANNER_REPORT_SEC_TRUCK_02",
            "SCRIPTED_SCANNER_REPORT_SEC_TRUCK_03",
            "SCRIPTED_SCANNER_REPORT_SIMEON_01",
            "SCRIPTED_SCANNER_REPORT_Sol_3_01",
            "SCRIPTED_SCANNER_REPORT_Sol_3_02"
        };

        public void playPoliceReport(string reportName)
        {
            if (!PoliceScanner.Contains(reportName)) return;
            Function.Call(Hash.PLAY_POLICE_REPORT, reportName, 0.0);
        }

        public string getStreetName(Vector3 position)
        {
            return World.GetStreetName(position.ToVector());
        }

        public string getZoneName(Vector3 position)
        {
            return World.GetZoneDisplayName(position.ToVector());
        }

        //To Check
        public string getZoneNameLabel(Vector3 position)
        {
            return World.GetZoneDisplayName(position.ToVector());
        }

        public float getGroundHeight(Vector3 position)
        {
            return World.GetGroundHeight(position.ToVector());
        }

        public string getGameText(string labelName)
        {
            return Function.Call<string>(Hash._GET_LABEL_TEXT, labelName);
        }

        public void toggleGameAIEntities(bool state)
        {
            Main.RemoveGameEntities = state;
        }

        private static bool SnowState = false;
        public bool toggleSnow()
        {
            var addr = Util.Util.FindPattern("\x74\x25\xB9\x40\x00\x00\x00\xE8\x00\x00\xC4\xFF", "xxxx???x??xx");


            //4c 6f 61 64 69 6e 67 20 47 54 41 4e 65 74 77 6f 72 6b


            var original = Util.Util.ReadMemory(addr, 20);

            if (!SnowState)
            {
                Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, "XMAS");

                Function.Call(Hash._SET_FORCE_VEHICLE_TRAILS, true);
                Function.Call(Hash._SET_FORCE_PED_FOOTSTEPS_TRACKS, true);

                if (addr != IntPtr.Zero)
                {
                    Util.Util.WriteMemory(addr, 0x90, 20);
                }
            }
            else
            {
                Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                Function.Call(Hash.SET_WEATHER_TYPE_NOW, "CLEAR");
                Function.Call(Hash._SET_FORCE_VEHICLE_TRAILS, false);
                Function.Call(Hash._SET_FORCE_PED_FOOTSTEPS_TRACKS, false);

                if (addr != IntPtr.Zero)
                {
                    Util.Util.WriteMemory(addr, original);
                }
            }
            SnowState = !SnowState;
            return SnowState;
        }
    }

}