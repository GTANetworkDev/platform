using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetworkShared;
using Microsoft.ClearScript;
using Microsoft.ClearScript.Windows;
using NativeUI;
using NAudio.Wave;
using Vector3 = GTANetworkShared.Vector3;

namespace GTANetwork
{
    public class ClientsideScriptWrapper
    {
        public ClientsideScriptWrapper(JScriptEngine en, string rs, string filename)
        {
            Engine = en;
            ResourceParent = rs;
            Filename = filename;
        }

        public JScriptEngine Engine { get; set; }
        public string ResourceParent { get; set; }
        public string Filename { get; set; }
    }

    public static class AudioThread
    {
        public static void StartAudio(string path)
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

        public static void DisposeAudio()
        {
            var t = new Thread((ThreadStart)delegate
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
            });
            t.IsBackground = true;
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
        }

        public static List<UIResText> TextElements { get; set; }

        public static List<ClientsideScriptWrapper> ScriptEngines;

        public static List<Action> ThreadJumper;

        public static WaveOutEvent AudioDevice { get; set; }
        public static Mp3FileReader AudioReader { get; set; }

        public static void InvokeServerEvent(string eventName, object[] arguments)
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines) ScriptEngines.ForEach(en => en.Engine.Script.API.invokeServerEvent(eventName, arguments));
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
                        ScriptEngines.ForEach(en => en.Engine.Script.API.invokeChatCommand(msg));
                    }
                }
                else
                {
                    lock (ScriptEngines)
                    {
                        ScriptEngines.ForEach(en => en.Engine.Script.API.invokeChatMessage(msg));
                    }
                }
            });
        }
        
        public void OnTick(object sender, EventArgs e)
        {
            var tmpList = new List<Action>(ThreadJumper);
            ThreadJumper.Clear();

            foreach (var a in tmpList)
            { 
                try
                {
                    a.Invoke();
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }

            lock (ScriptEngines)
            {
                foreach (var engine in ScriptEngines)
                {
                    try
                    {
                        engine.Engine.Script.API.invokeUpdate();
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
                        engine.Engine.Script.API.invokeKeyDown(sender, e);
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
                        engine.Engine.Script.API.invokeKeyUp(sender, e);
                    }
                    catch (ScriptEngineException ex)
                    {
                        LogException(ex);
                    }
                }
            }
        }

        public static void StartScripts(ScriptCollection sc)
        {
            var localSc = new List<ClientsideScript>(sc.ClientsideScripts);

            ThreadJumper.Add(() =>
            {
                List<ClientsideScriptWrapper> scripts = localSc.Select(StartScript).ToList();

                foreach (var compiledResources in scripts)
                {
                    dynamic obj = new ExpandoObject();
                    var asDict = obj as IDictionary<string, object>;

                    foreach (var resourceEngine in
                            scripts.Where(
                                s => s.ResourceParent == compiledResources.ResourceParent &&
                                s != compiledResources))
                    {
                        asDict.Add(resourceEngine.Filename, resourceEngine.Engine.Script);
                    }
                    
                    compiledResources.Engine.AddHostObject("resource", obj);
                    compiledResources.Engine.Script.API.invokeResourceStart();
                }
            });
        }

        public static ClientsideScriptWrapper StartScript(ClientsideScript script)
        {
            ClientsideScriptWrapper csWrapper = null;
            
            var scriptEngine = new JScriptEngine();
            scriptEngine.AddHostObject("host", new HostFunctions());
            scriptEngine.AddHostObject("API", new ScriptContext());
            scriptEngine.AddHostType("Enumerable", typeof(Enumerable));
            scriptEngine.AddHostType("List", typeof(IList));
            scriptEngine.AddHostType("KeyEventArgs", typeof(KeyEventArgs));
            scriptEngine.AddHostType("Keys", typeof(Keys));
            scriptEngine.AddHostType("Point", typeof(Point));
            scriptEngine.AddHostType("Size", typeof(Size));
            scriptEngine.AddHostType("Vector3", typeof(Vector3));
            scriptEngine.AddHostType("menuControl", typeof(UIMenu.MenuControls));
                

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

        public static void StopAllScripts()
        {
            ThreadJumper.Add(() =>
            {
                lock (ScriptEngines)
                {
                    foreach (var engine in ScriptEngines)
                    {
                        engine.Engine.Script.API.invokeResourceStop();
                        engine.Engine.Dispose();
                    }

                    ScriptEngines.Clear();
                    AudioDevice?.Stop();
                    AudioDevice?.Dispose();
                    AudioReader?.Dispose();
                    AudioDevice = null;
                    AudioReader = null;
                }
            });
        }

        public static void StopScript(string resourceName)
        {
            ThreadJumper.Add(delegate
            {
                lock (ScriptEngines)
                    for (int i = ScriptEngines.Count - 1; i >= 0; i--)
                    {
                        if (ScriptEngines[i].ResourceParent != resourceName) continue;
                        ScriptEngines[i].Engine.Script.API.invokeResourceStop();
                        ScriptEngines[i].Engine.Dispose();
                        ScriptEngines.RemoveAt(i);
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

            Util.SafeNotify("~r~~h~Clientside Javascript Error~h~~w~");
            
            foreach (var s in splitter(ex.Message, 99))
            {
                Util.SafeNotify(s);
            }

            LogManager.LogException(ex, "CLIENTSIDE SCRIPT ERROR");
        }
    }

    public class ScriptContext
    {
        internal string ParentResourceName;

        internal bool isPathSafe(string path)
        {
            if (ParentResourceName == null) throw new NullReferenceException("Illegal call to isPathSafe inside constructor!");

            var absPath = System.IO.Path.GetFullPath(FileTransferId._DOWNLOADFOLDER_ + ParentResourceName + Path.DirectorySeparatorChar + path);
            var resourcePath = System.IO.Path.GetFullPath(FileTransferId._DOWNLOADFOLDER_ + ParentResourceName);

            return absPath.StartsWith(resourcePath);
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
        }

        public LocalHandle NetToLocal(NetHandle handle)
        {
            return new LocalHandle(Main.NetEntityHandler.NetToEntity(handle.Value)?.Handle ?? 0);
        }

        public void callNative(string hash, params object[] args)
        {
            Hash ourHash;
            if (!Hash.TryParse(hash, out ourHash))
                return;
            Function.Call(ourHash, args.Select(o =>
            {
                if (o is LocalHandle)
                    return new InputArgument(((LocalHandle) o).Value);
                return new InputArgument(o);
            }).ToArray());
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
                case ReturnType.Bool:
                    return Function.Call<bool>(ourHash, fArgs);
                default:
                    return null;
            }
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
            return new Prop(entity.Value).Position.ToLVector();
        }

        public bool isPlayerInAnyVehicle(LocalHandle player)
        {
            return new Ped(player.Value).IsInVehicle();
        }

        public void playSoundFrontEnd(string audioLib, string audioName)
        {
            Function.Call((Hash)0x2F844A8B08D76685, audioLib, true);
            Function.Call((Hash)0x67C540AA08E4A6F5, -1, audioName, audioLib);
        }

        public void showShard(string text)
        {
            NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage(text);
        }

        public void showShard(string text, int timeout)
        {
            NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage(text, timeout);
        }

        public SizeF getScreenResolutionMantainRatio()
        {
            return UIMenu.GetScreenResolutionMantainRatio();
        }

        public void sendNotification(string text)
        {
            Util.SafeNotify(text);
        }

        public void displaySubtitle(string text)
        {
            UI.ShowSubtitle(text);
        }

        public void displaySubtitle(string text, double duration)
        {
            UI.ShowSubtitle(text, (int) duration);
        }

        public string formatTime(double ms, string format)
        {
            return TimeSpan.FromMilliseconds(ms).ToString(format);
        }

        public void setPlayerInvincible(bool invinc)
        {
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

        public void setPlayerHealth(LocalHandle ped, int health)
        {
            new Ped(ped.Value).Health = health;
        }

        public int getPlayerHealth(LocalHandle ped)
        {
            return new Ped(ped.Value).Health;
        }

        public void setPlayerArmor(LocalHandle ped, int armor)
        {
            new Ped(ped.Value).Armor = armor;
        }

        public int getPlayerArmor(LocalHandle ped)
        {
            return new Ped(ped.Value).Armor;
        }

        public LocalHandle[] getAllPlayers()
        {
            return Main.NetEntityHandler.ClientMap.Where(item => item is SyncPed).Cast<SyncPed>().Select(op => new LocalHandle(op.Character?.Handle ?? 0)).ToArray();
        }

        public LocalHandle getPlayerVehicle(LocalHandle player)
        {
            return new LocalHandle(new Ped(player.Value).CurrentVehicle?.Handle ?? 0);
        }

        public void explodeVehicle(LocalHandle vehicle)
        {
            new Vehicle(vehicle.Value).Explode();
        }

        public LocalHandle getPlayerByName(string name)
        {
            var opp = Main.NetEntityHandler.ClientMap.FirstOrDefault(op => op is SyncPed && ((SyncPed) op).Name == name) as SyncPed;
            if (opp != null && opp.Character != null)
                return new LocalHandle(opp.Character.Handle);
            return new LocalHandle(0);
        }

        public string getPlayerName(LocalHandle player)
        {
            var opp = Main.NetEntityHandler.ClientMap.FirstOrDefault(op => op is SyncPed && ((SyncPed)op).Character.Handle == player.Value) as SyncPed;
            if (opp != null)
                return opp.Name;
            return null;
        }

        public LocalHandle createBlip(Vector3 pos)
        {
            var blip = World.CreateBlip(pos.ToVector());
            if (!Main.BlipCleanup.Contains(blip.Handle))
                Main.BlipCleanup.Add(blip.Handle);
            return new LocalHandle(blip.Handle);
        }

        public void setBlipPosition(LocalHandle blip, Vector3 pos)
        {
            if (new Blip(blip.Value).Exists())
            {
                new Blip(blip.Value).Position = pos.ToVector();
            }
        }

        public void removeBlip(LocalHandle blip)
        {
            if (new Blip(blip.Value).Exists())
            {
                new Blip(blip.Value).Remove();
            }
        }
        
        public void setBlipScale(LocalHandle blip, double scale)
        {
            setBlipScale(blip, (float) scale);
        }

        public void setBlipScale(LocalHandle blip, float scale)
        {
            if (new Blip(blip.Value).Exists())
            {
                new Blip(blip.Value).Scale = scale;
            }
        }

        public LocalHandle createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int r, int g, int b, int alpha)
        {
            return new LocalHandle(Main.NetEntityHandler.CreateLocalMarker(markerType, pos.ToVector(), dir.ToVector(), rot.ToVector(), scale.ToVector(), alpha, r, g, b));
        }

        public void setMarkerPosition(LocalHandle marker, Vector3 pos)
        {
            var delta = new Delta_MarkerProperties();
            delta.Position = pos;
            
            Main.NetEntityHandler.UpdateMarker(marker.Value, delta, true);
        }

        public void setMarkerScale(LocalHandle marker, Vector3 scale)
        {
            var delta = new Delta_MarkerProperties();
            delta.Scale = scale;

            Main.NetEntityHandler.UpdateMarker(marker.Value, delta, true);
        }

        public void deleteEntity(LocalHandle handle)
        {
            Main.NetEntityHandler.DeleteLocalEntity(handle.Value);
        }

        public LocalHandle createTextLabel(string text, Vector3 pos, float range, float size)
        {
            return new LocalHandle(Main.NetEntityHandler.CreateLocalLabel(text, pos.ToVector(), range, size, 0));
        }

        public string getResourceFilePath(string fileName)
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
            return Util.LinearFloatLerp((float) start, (float) end, (int) currentTime, (int) duration);
        }

        public bool isInRangeOf(Vector3 entity, Vector3 destination, double range)
        {
            return (entity - destination).LengthSquared() < (range*range);
        }
        
        public void dxDrawTexture(string path, Point pos, Size size, int id = 60)
        {
            if (!isPathSafe(path)) throw new Exception("Illegal path for texture!");
            path = getResourceFilePath(path);
            Util.DxDrawTexture(id, path, pos.X, pos.Y, size.Width, size.Height, 0f, 255, 255, 255, 255);
        }

        public void drawGameTexture(string dict, string txtName, double x, double y, double width, double height, double heading,
            int r, int g, int b, int alpha)
        {
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float hh = 1080f;
            float ratio = (float)screenw / screenh;
            var ww = hh * ratio;


            float w = (float)(width / ww);
            float h = (float)(height / hh);
            float xx = (float)(x / ww) + w * 0.5f;
            float yy = (float)(y / hh) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, txtName, xx, yy, w, h, heading, r, g, b, alpha);
        }

        public void drawRectangle(double xPos, double yPos, double wSize, double hSize, int r, int g, int b, int alpha)
        {
            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height*ratio;

            float w = (float)wSize / width;
            float h = (float)hSize / height;
            float x = (((float)xPos) / width) + w * 0.5f;
            float y = (((float)yPos) / height) + h * 0.5f;

            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
        }

        public void drawText(string caption, double xPos, double yPos, double scale, int r, int g, int b, int alpha, int font,
            int justify, bool shadow, bool outline, int wordWrap)
        {
            
            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float x = (float)(xPos) / width;
            float y = (float)(yPos) / height;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, alpha);
            if (shadow)
                Function.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (outline)
                Function.Call(Hash.SET_TEXT_OUTLINE);
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

            Function.Call(Hash._SET_TEXT_ENTRY, "jamyfafi");
            NativeUI.UIResText.AddLongString(caption);
            Function.Call(Hash._DRAW_TEXT, x, y);
        }

        public UIResText addTextElement(string caption, double x, double y, double scale, int r, int g, int b, int a, int font, int alignment)
        {
            var txt = new UIResText(caption, new Point((int) x, (int) y), (float) scale, Color.FromArgb(a, r, g, b),
                (GTA.Font) font, (UIResText.Alignment) alignment);
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

        public float toFloat(double d)
        {
            return (float) d;
        }

        public void sleep(int ms)
        {
            Script.Wait(ms);
        }

        public void startAudio(string path)
        {
            if (path.StartsWith("http")) return;
            path = getResourceFilePath(path);
            AudioThread.StartAudio(path);
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
            Main.TriggerServerEvent(eventName, arguments);
        }

        public delegate void ServerEventTrigger(string eventName, object[] arguments);
        public delegate void ChatEvent(string msg);
        public delegate void StreamEvent(IStreamedItem item);

        public event EventHandler onResourceStart;
        public event EventHandler onResourceStop;
        public event EventHandler onUpdate;
        public event KeyEventHandler onKeyDown;
        public event KeyEventHandler onKeyUp;
        public event ServerEventTrigger onServerEventTrigger;
        public event ChatEvent onChatMessage;
        public event ChatEvent onChatCommand;
        public event StreamEvent onEntityStreamIn;
        public event StreamEvent onEntityStreamOut;

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

        #region Menus

        public UIMenu createMenu(string banner, string subtitle, double x, double y, int anchor)
        {
            var offset = convertAnchorPos((float)x, (float)y, (Anchor)anchor, 431f, 107f + 38 + 38f * 10);
            return new UIMenu(banner, subtitle, new Point((int)(offset.X), (int)(offset.Y))) { ScaleWithSafezone = false };
        }

        public UIMenu createMenu(string subtitle, double x, double y, int anchor)
        {
            var offset = convertAnchorPos((float)x, (float)y - 107, (Anchor)anchor, 431f, 107f + 38 + 38f * 10);
            var newM = new UIMenu("", subtitle, new Point((int)(offset.X), (int)(offset.Y)));
            newM.ScaleWithSafezone = false;
            newM.SetBannerType(new UIResRectangle());
            return newM;
        }

        public UIMenuItem createMenuItem(string label, string description)
        {
            return new UIMenuItem(label, description);
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
            menu.ProcessControl();
            menu.ProcessMouse();
            menu.Draw();
        }

        public Scaleform requestScaleform(string scaleformName)
        {
            var sc = new Scaleform(0);
            sc.Load(scaleformName);
            return sc;
        }

        public void renderScaleform(Scaleform sc, double x, double y, double w, double h)
        {
            sc.Render2DScreenSpace(new PointF((float) x, (float) y), new PointF((float) w, (float) h));
        }

        internal PointF convertAnchorPos(float x, float y, Anchor anchor, float xOffset, float yOffset)
        {
            var res = UIMenu.GetScreenResolutionMantainRatio();

            switch (anchor)
            {
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
                    return new PointF(res.Width - x - xOffset, res.Height / 2 + y - yOffset/2);
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
    }

}