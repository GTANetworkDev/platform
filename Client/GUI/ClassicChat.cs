using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTANetwork.Javascript;
using NativeUI;
using Control = GTA.Control;

namespace GTANetwork.GUI
{
	public class ChatThread : Script
	{
        
		public ChatThread()
		{
			base.Tick += (sender, args) =>
			{
			    if (Main.Chat != null && Main.ChatVisible && Main.ScriptChatVisible && Main.MainMenu != null && (!Main.MainMenu.Visible || Main.MainMenu.TemporarilyHidden))
				{
					Main.Chat.Tick();

                    foreach (var text in JavascriptHook.TextElements)
                    {
                        text.Draw();
                    }
                }
			};
		}
	}

    public class ClassicChat : IChat
    {
        public event EventHandler OnComplete;

        public ClassicChat()
        {
            CurrentInput = "";
            _mainScaleform = new Scaleform("multiplayer_chat");
            _messages = new List<Tuple<string, Color>>();
        }

        public bool HasInitialized;

        public void Init()
        {
            _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");
            _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
            HasInitialized = true;
        }

        public bool IsFocused
        {
            get { return _isFocused; }
            set
            {
                if (value && !_isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");
                }
                else if (!value && _isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
                }

                _isFocused = value;
            }
        }

        private Scaleform _mainScaleform;

        public string CurrentInput { get; private set; }
        private List<string> _inputHistory = new List<string>();
        private int _historyIndex = -1;

        private int _switch = 1;
        private Keys _lastKey;
        private bool _isFocused;

        private int _pagingIndex;
        private const int _messagesPerPage = 9;

        private DateTime _lastMsg = DateTime.MinValue;
        private DateTime _focusStart = DateTime.MinValue;
        private bool _lastFadedOut;
        private List<Tuple<string, Color>> _messages;

        public void Clear()
        {
            _messages.Clear();
        }
        
        private PointF GetInputboxPos(bool scaleWithSafezone)
        {
            var aspectRatio = (GTA.UI.Screen.Resolution.Width/(float) GTA.UI.Screen.Resolution.Height);
            var res = UIMenu.GetScreenResolutionMantainRatio();
            var safezone = UIMenu.GetSafezoneBounds();
            PointF offset = new PointF(0, 0);

            if (!scaleWithSafezone)
                offset = new PointF(-safezone.X, -safezone.Y);

            if (Math.Abs(aspectRatio - 1.777778f) < 0.001f)
            {
                return new PointF(((safezone.X - 1220 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 774 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.6f) < 0.001f)
            {
                return new PointF(((safezone.X - 1122 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 781 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.481481f) < 0.001f)
            {
                return new PointF(((safezone.X - 1054 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 781 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.5625f) < 0.001f)
            {
                return new PointF(((safezone.X - 1100 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 781 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.770833f) < 0.001f)
            {
                return new PointF(((safezone.X - 1216 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 778 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.25f) < 0.001f)
            {
                return new PointF(((safezone.X - 857 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 778 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.33333f) < 0.001f)
            {
                return new PointF(((safezone.X - 915 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 778 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.66666f) < 0.001f)
            {
                return new PointF(((safezone.X - 1158 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 778 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }
            else if (Math.Abs(aspectRatio - 1.55555f) < 0.001f)
            {
                return new PointF(((safezone.X - 1200 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 774 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
            }

            return new PointF(((safezone.X - 1220 + offset.X) / res.Width) * GTA.UI.Screen.Width, ((safezone.Y - 774 + offset.Y) / res.Height) * GTA.UI.Screen.Height);
        }
        
        public void Tick()
        {
            if (!Main.IsOnServer()) return;
            
            var timePassed = Math.Min(DateTime.Now.Subtract(_focusStart).TotalMilliseconds, DateTime.Now.Subtract(_lastMsg).TotalMilliseconds);

            int alpha = 100;
            if (timePassed > 60000 && !_isFocused)
                alpha = (int)MiscExtensions.QuadraticEasingLerp(100f, 0f, (int)Math.Min(timePassed - 60000, 2000), 2000);
            if (timePassed < 300 && _lastFadedOut)
                alpha = (int)MiscExtensions.QuadraticEasingLerp(0f, 100f, (int)Math.Min(timePassed, 300), 300);
            
            
            var pos = GetInputboxPos(Main.PlayerSettings.ScaleChatWithSafezone);
            _mainScaleform.Render2DScreenSpace(new PointF(pos.X, pos.Y), new PointF(GTA.UI.Screen.Width, GTA.UI.Screen.Height));

            var textAlpha = (alpha/100f)*126 + 126;
            var c = 0;

            if (_messages.Any())
            for (int indx = Math.Min(_messagesPerPage + _pagingIndex, _messages.Count-1); indx >= (_messages.Count <= _messagesPerPage ? 0 : _pagingIndex); indx--)
            {
                var msg = _messages[indx];

                string output = msg.Item1;

                if (Main.PlayerSettings.ScaleChatWithSafezone)
                {
                    new UIResText(output, UIMenu.GetSafezoneBounds() + new Size(0, 25*c), 0.35f,
                        Color.FromArgb((int) textAlpha, msg.Item2))
                    {
                        Outline = true,
                    }.Draw();
                }
                else
                {
                    new UIResText(output, new Point(0, 25 * c), 0.35f,
                        Color.FromArgb((int)textAlpha, msg.Item2))
                    {
                        Outline = true,
                    }.Draw();
                }
                c++;
            }

            if (_pagingIndex != 0 && _messages.Count > _messagesPerPage)
            {
                Point start = UIMenu.GetSafezoneBounds();
                if (!Main.PlayerSettings.ScaleChatWithSafezone)
                    start = new Point();
                start = new Point(start.X - 15, start.Y);
                var chatHeight = 25*(_messagesPerPage + 1);
                var availableChoices = _messages.Count - _messagesPerPage;
                var barHeight = (1f/ availableChoices) *chatHeight;

                new UIResRectangle(start, new Size(10, chatHeight), Color.FromArgb(50, 0, 0, 0)).Draw();
                new UIResRectangle(start + new Size(0, (int)(chatHeight - chatHeight*((_pagingIndex + 1)/(float)availableChoices))), new Size(10, (int)barHeight), Color.FromArgb(150, 0, 0, 0)).Draw();
            }
            
            if (!IsFocused) return;

            if (!Main.CanOpenChatbox) IsFocused = false;

            Game.DisableControlThisFrame(0, Control.NextCamera);
            Game.DisableAllControlsThisFrame(0);
        }

        public void AddMessage(string sender, string msg)
        {
            Color textColor = Color.White;
            if (sender != null && Regex.IsMatch(sender, "^~#[a-fA-F0-9]{6}~"))
            {
                textColor = ColorTranslator.FromHtml(sender.Substring(1, 7));
                if (sender.Length == 9) sender = null;
            }

            if (string.IsNullOrEmpty(sender))
                _messages.Insert(0, new Tuple<string, Color>(msg, textColor));
            else
                _messages.Insert(0, new Tuple<string, Color>(sender + ": " + msg, textColor));

            if (_messages.Count > 50)
                _messages.RemoveAt(50);

            _lastFadedOut = DateTime.Now.Subtract(_lastMsg).TotalMilliseconds > 15000;
            _lastMsg = DateTime.Now;
        }

        public static string SanitizeString(string input)
        {
            input = Regex.Replace(input, "~.~", "", RegexOptions.IgnoreCase);
            return input;
        }
        
        public void OnKeyDown(Keys key)
        {
            if (key == Keys.PageUp && Main.IsOnServer() && _pagingIndex + _messagesPerPage + 1 < _messages.Count)
                _pagingIndex++;

            else if (key == Keys.PageDown && Main.IsOnServer() && _pagingIndex != 0)
                _pagingIndex--;

            if (!IsFocused || !Main.ChatVisible || !Main.ScriptChatVisible) return;

            if (key == Keys.Up && _inputHistory.Count > _historyIndex + 1)
            {
                _historyIndex++;
                _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
                _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");

                CurrentInput = _inputHistory[_historyIndex];
                _mainScaleform.CallFunction("ADD_TEXT", CurrentInput);
            }
            else if (key == Keys.Down && _inputHistory.Count > _historyIndex - 1 && _historyIndex != -1)
            {
                _historyIndex--;
                if (_historyIndex == -1)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
                    _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");
                    CurrentInput = "";
                    _mainScaleform.CallFunction("ADD_TEXT", CurrentInput);
                }
                else
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
                    _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");

                    CurrentInput = _inputHistory[_historyIndex];
                    _mainScaleform.CallFunction("ADD_TEXT", CurrentInput);
                }
            }


            if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) || (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                ActivateKeyboardLayout(1, 0);

            _lastKey = key;

            if (key == Keys.Escape)
            {
                _historyIndex = -1;
                IsFocused = false;
                CurrentInput = "";
            }

            
            var keyChar = GetCharFromKey(key,
                Game.IsKeyPressed(Keys.ShiftKey),
                Game.IsKeyPressed(Keys.Menu) && Game.IsKeyPressed(Keys.ControlKey));

            if (keyChar.Length == 0) return;

            if (keyChar[0] == (char)8)
            {
                _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "");
                _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "");

                if (CurrentInput.Length > 0)
                {
                    CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                    _mainScaleform.CallFunction("ADD_TEXT", CurrentInput);
                }
                return;
            }
            if (keyChar[0] == (char)13)
            {
                _mainScaleform.CallFunction("ADD_TEXT", "ENTER");
                if (OnComplete != null) OnComplete.Invoke(this, EventArgs.Empty);
                _historyIndex = -1;
                if (!string.IsNullOrWhiteSpace(CurrentInput))
                {
                    _inputHistory.Insert(0, CurrentInput);
                    if (_inputHistory.Count > 5) _inputHistory.RemoveAt(5);
                }

                _pagingIndex = 0;
                CurrentInput = "";
                return;
            }
            else if (keyChar[0] == 27 || keyChar[0] == '~')
            {
                return;
            }
            var str = keyChar;

            CurrentInput += str;
            _mainScaleform.CallFunction("ADD_TEXT", str);
        }


        [DllImport("user32.dll")]
        public static extern int ToUnicodeEx(uint virtualKeyCode, uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
        StringBuilder receivingBuffer,
        int bufferSize, uint flags, IntPtr kblayout);

        [DllImport("user32.dll")]
        public static extern int ActivateKeyboardLayout(int hkl, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        public static string GetCharFromKey(Keys key, bool shift, bool altGr)
        {
            var buf = new StringBuilder(256);
            var keyboardState = new byte[256];
            if (shift)
                keyboardState[(int)Keys.ShiftKey] = 0xff;
            if (altGr)
            {
                keyboardState[(int)Keys.ControlKey] = 0xff;
                keyboardState[(int)Keys.Menu] = 0xff;
            }

            ToUnicodeEx((uint)key, 0, keyboardState, buf, 256, 0, InputLanguage.CurrentInputLanguage.Handle);
            return ((((ushort)GetKeyState(0x14)) & 0xffff) != 0) ? buf.ToString().ToUpper() : buf.ToString();
        }
    }
}