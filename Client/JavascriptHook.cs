using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using GTANetworkShared;
using Microsoft.ClearScript;
using Microsoft.ClearScript.Windows;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
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

    public class JavascriptHook
    {
        public JavascriptHook()
        {
            _hooker = new RageKeyboardHooker();

            _hooker.OnKeyDown += OnKeyDown;
            _hooker.OnKeyUp += OnKeyUp;

            GameFiber.StartNew(delegate
            {
                while (true)
                {
                    _hooker.Update();
                    OnTick(null, EventArgs.Empty);
                    GameFiber.Yield();
                }
            });
        }

        public static List<ClientsideScriptWrapper> ScriptEngines = new List<ClientsideScriptWrapper>();
        public static List<Action> ThreadJumper = new List<Action>();
        private RageKeyboardHooker _hooker;

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
            LogManager.DebugLog("INVOKING MESSAGE EVENT");
            ThreadJumper.Add(() =>
            {
                LogManager.DebugLog("THREDJUMP START");
                if (msg.StartsWith("/"))
                {
                    lock (ScriptEngines)
                    {
                        ScriptEngines.ForEach(en => en.Engine.Script.API.invokeChatCommand(msg));
                    }
                }
                else
                {
                    LogManager.DebugLog("SCRIPTENGINE LOCK");
                    lock (ScriptEngines)
                    {
                        LogManager.DebugLog("FOREACH");
                        ScriptEngines.ForEach(en => en.Engine.Script.API.invokeChatMessage(msg));
                        LogManager.DebugLog("FOREACH OVER");
                    }
                }
            });
        }
        
        public void OnTick(object sender, EventArgs e)
        {
            if (ThreadJumper.Count > 0)
            {
                ThreadJumper.ForEach(a =>
                {
                    try
                    {
                        a.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                    }
                });
                ThreadJumper.Clear();
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
            ThreadJumper.Add(() =>
            {
                List<ClientsideScriptWrapper> scripts = sc.ClientsideScripts.Select(StartScript).ToList();

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
            scriptEngine.AddHostType("menuControl", typeof(RAGENativeUI.Common.MenuControls));
                

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

        private Dictionary<string, Texture> TextureDict = new Dictionary<string, Texture>();
        private Texture GetTexture(string filepath)
        {
            if (!TextureDict.ContainsKey(filepath))
            {
                var newTxt = Game.CreateTextureFromFile(filepath);
                TextureDict.Add(filepath, newTxt);
                return newTxt;
            }
            else
            {
                return TextureDict[filepath];
            }
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
            Function.Call(ourHash, Main.ParseRageArguments(args).ToArray());
        }

        public object returnNative(string hash, int returnType, params object[] args)
        {
            Hash ourHash;
            if (!Hash.TryParse(hash, out ourHash))
                return null;
            var fArgs = Main.ParseRageArguments(args).ToArray();
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

        public int getGamePlayer()
        {
            return Game.LocalPlayer.Id;
        }

        public LocalHandle getLocalPlayer()
        {
            return new LocalHandle(Game.LocalPlayer.Character.Handle.Value);
        }

        public void playSoundFrontEnd(string audioLib, string audioName)
        {
            Function.Call((Hash)0x2F844A8B08D76685, audioLib, true);
            Function.Call((Hash)0x67C540AA08E4A6F5, -1, audioName, audioLib);
        }

        public void showShard(string text)
        {
            Main.BigMessageThread.MessageInstance.ShowMissionPassedMessage(text);
        }

        public void showShard(string text, int timeout)
        {
            Main.BigMessageThread.MessageInstance.ShowMissionPassedMessage(text, timeout);
        }

        public SizeF getScreenResolutionMantainRatio()
        {
            return UIMenu.GetScreenResolutionMantainRatio();
        }

        public void sendNotification(string text)
        {
            Util.SafeNotify(text);
        }

        public void setPlayerInvincible(bool invinc)
        {
            Game.LocalPlayer.IsInvincible = invinc;
        }

        public bool getPlayerInvincible()
        {
            return Game.LocalPlayer.IsInvincible;
        }

        public void setPlayerHealth(LocalHandle ped, int health)
        {
            World.GetEntityByHandle<Ped>(new PoolHandle(ped.Value)).Health = health;
        }

        public int getPlayerHealth(LocalHandle ped)
        {
            return World.GetEntityByHandle<Ped>(new PoolHandle(ped.Value)).Health;
        }

        public void setPlayerArmor(LocalHandle ped, int armor)
        {
            World.GetEntityByHandle<Ped>(ped.Value).Armor = armor;
        }

        public int getPlayerArmor(LocalHandle ped)
        {
            return World.GetEntityByHandle<Ped>(new PoolHandle(ped.Value)).Armor;
        }

        public LocalHandle[] getAllPlayers()
        {
            return Main.Opponents.Select(op => new LocalHandle(op.Value.Character?.Handle ?? 0)).ToArray();
        }

        public LocalHandle getPlayerByName(string name)
        {
            var opp = Main.Opponents.FirstOrDefault(op => op.Value.Name == name);
            if (opp.Value != null && opp.Value.Character != null)
                return new LocalHandle(opp.Value.Character.Handle);
            return new LocalHandle(0);
        }

        public string getPlayerName(LocalHandle player)
        {
            var opp = Main.Opponents.FirstOrDefault(op => op.Value.Character != null && op.Value.Character.Handle == player.Value);
            if (opp.Value != null)
                return opp.Value.Name;
            return null;
        }

        public LocalHandle createBlip(Vector3 pos)
        {
            var blip = new Blip(pos.ToVector());
            if (!Main.BlipCleanup.Contains(blip.Handle.Value))
                Main.BlipCleanup.Add(blip.Handle.Value);
            return new LocalHandle(blip.Handle);
        }

        public void setBlipPosition(LocalHandle blip, Vector3 pos)
        {
            if (World.GetBlipByHandle(blip.Value).Exists())
            {
                World.GetBlipByHandle(blip.Value).Position = pos.ToVector();
            }
        }

        public void removeBlip(LocalHandle blip)
        {
            if (World.GetBlipByHandle(blip.Value).Exists())
            {
                World.GetBlipByHandle(blip.Value).Delete();
            }
        }
        
        public void setBlipScale(LocalHandle blip, double scale)
        {
            setBlipScale(blip, (float) scale);
        }

        public void setBlipScale(LocalHandle blip, float scale)
        {
            if (World.GetBlipByHandle(blip.Value).Exists())
            {
                World.GetBlipByHandle(blip.Value).Scale = scale;
            }
        }

        public LocalHandle createMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int r, int g, int b, int alpha)
        {
            return new LocalHandle(Main.NetEntityHandler.CreateLocalMarker(markerType, pos.ToVector(), dir.ToVector(), rot.ToVector(), scale.ToVector(), alpha, r, g, b));
        }

        public void deleteMarker(LocalHandle handle)
        {
            Main.NetEntityHandler.DeleteLocalMarker(unchecked((int)handle.Value));
        }
        
        public string getResourceFilePath(string fileName)
        {
            return FileTransferId._DOWNLOADFOLDER_ + ParentResourceName + "\\" + fileName;
        }
        

        
        public void dxDrawTexture(string path, Point pos, Size size)
        {
            path = getResourceFilePath(path);
            Util.DxDrawTexture(GetTexture(path), pos.X, pos.Y, size.Width, size.Height, 0f, 255, 255, 255, 255);
        }

        public void drawGameTexture(string dict, string txtName, double x, double y, double width, double height, double heading,
            int r, int g, int b, int alpha)
        {
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;
            const float hh = 1080f;
            float ratio = (float)screenw / screenh;
            var ww = hh * ratio;


            float w = (float)(width / ww);
            float h = (float)(height / hh);
            float xx = (float)(x / ww) + w * 0.5f;
            float yy = (float)(y / hh) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, txtName, xx, yy, w, h, (float)heading, r, g, b, alpha);
        }

        public void drawRectangle(double xPos, double yPos, double wSize, double hSize, int r, int g, int b, int alpha)
        {
            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float w = (float)wSize / width;
            float h = (float)hSize / height;
            float x = (((float)xPos) / width) + w * 0.5f;
            float y = (((float)yPos) / height) + h * 0.5f;

            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
        }

        public void drawText(string caption, double xPos, double yPos, double scale, int r, int g, int b, int alpha, int font,
            int justify, bool shadow, bool outline, int wordWrap)
        {
            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float x = (float)(xPos) / width;
            float y = (float)(yPos) / height;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, (float)scale);
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
            ResText.AddLongString(caption);
            Function.Call(Hash._DRAW_TEXT, x, y);
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

        public void wait(int ms)
        {
            GameFiber.Sleep(ms);
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
            newM.SetBannerType(new ResRectangle());
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