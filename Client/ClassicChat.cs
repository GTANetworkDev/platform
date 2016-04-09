using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using NativeUI;
using Control = GTA.Control;

namespace GTANetwork
{
    public class ClassicChat
    {
        public event EventHandler OnComplete;

        public ClassicChat()
        {
            CurrentInput = "";
            _mainScaleform = new Scaleform(0);
            _mainScaleform.Load("multiplayer_chat");
            _messages = new List<string>();
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

        public string CurrentInput;

        private int _switch = 1;
        private Keys _lastKey;
        private bool _isFocused;

        private DateTime _lastMsg = DateTime.MinValue;
        private DateTime _focusStart = DateTime.MinValue;
        private bool _lastFadedOut;
        private List<string> _messages;

        public void Clear()
        {
            _messages.Clear();
        }


        private PointF GetInputboxPos(bool scaleWithSafezone)
        {
            var aspectRatio = (Game.ScreenResolution.Width/(float) Game.ScreenResolution.Height);
            var res = UIMenu.GetScreenResolutionMantainRatio();
            var safezone = UIMenu.GetSafezoneBounds();
            PointF offset = new PointF(0, 0);

            if (!scaleWithSafezone)
                offset = new PointF(-safezone.X, -safezone.Y);

            if (Math.Abs(aspectRatio - 1.777778f) < 0.001f)
            {
                return new PointF(((safezone.X - 1220 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 774 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.6f) < 0.001f)
            {
                return new PointF(((safezone.X - 1122 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 781 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.481481f) < 0.001f)
            {
                return new PointF(((safezone.X - 1054 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 781 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.5625f) < 0.001f)
            {
                return new PointF(((safezone.X - 1100 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 781 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.770833f) < 0.001f)
            {
                return new PointF(((safezone.X - 1216 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 778 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.25f) < 0.001f)
            {
                return new PointF(((safezone.X - 857 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 778 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.33333f) < 0.001f)
            {
                return new PointF(((safezone.X - 915 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 778 + offset.Y) / res.Height) * UI.HEIGHT);
            }
            else if (Math.Abs(aspectRatio - 1.66666f) < 0.001f)
            {
                return new PointF(((safezone.X - 1158 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 778 + offset.Y) / res.Height) * UI.HEIGHT);
            }

            return new PointF(((safezone.X - 1220 + offset.X) / res.Width) * UI.WIDTH, ((safezone.Y - 774 + offset.Y) / res.Height) * UI.HEIGHT);
        }
        
        public void Tick()
        {
            if (!Main.IsOnServer()) return;
            
            var timePassed = Math.Min(DateTime.Now.Subtract(_focusStart).TotalMilliseconds, DateTime.Now.Subtract(_lastMsg).TotalMilliseconds);

            int alpha = 100;
            if (timePassed > 15000 && !_isFocused)
                alpha = (int)MiscExtensions.QuadraticEasingLerp(100f, 0f, (int)Math.Min(timePassed - 15000, 2000), 2000);
            if (timePassed < 300 && _lastFadedOut)
                alpha = (int)MiscExtensions.QuadraticEasingLerp(0f, 100f, (int)Math.Min(timePassed, 300), 300);

            int maxWidth = 0;
            if (_messages.Count > 0)
            {
                maxWidth = _messages.Max(f => StringMeasurer.MeasureString(f));
            }
            
            var pos = GetInputboxPos(Main.PlayerSettings.ScaleChatWithSafezone);
            _mainScaleform.Render2DScreenSpace(new PointF(pos.X, pos.Y), new PointF(UI.WIDTH, UI.HEIGHT));

            var textAlpha = (alpha/100f)*126 + 126;
            var c = 0;
            foreach (var msg in _messages)
            {
                string output = msg;
                var limit = UIMenu.GetScreenResolutionMantainRatio().Width - UIMenu.GetSafezoneBounds().X;
                while (StringMeasurer.MeasureString(output) > limit)
                    output = output.Substring(0, output.Length - 5);

                if (Main.PlayerSettings.ScaleChatWithSafezone)
                {
                    new UIResText(output, UIMenu.GetSafezoneBounds() + new Size(0, 25*c), 0.35f,
                        Color.FromArgb((int) textAlpha, 255, 255, 255))
                    {
                        Outline = true,
                    }.Draw();
                }
                else
                {
                    new UIResText(output, new Point(0, 25 * c), 0.35f,
                        Color.FromArgb((int)textAlpha, 255, 255, 255))
                    {
                        Outline = true,
                    }.Draw();
                }
                c++;
            }
            
            if (!IsFocused) return;
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
        }

        public void AddMessage(string sender, string msg)
        {
            if (string.IsNullOrEmpty(sender))
                _messages.Add(msg);
            else
                _messages.Add(sender + ": " + msg);

            if (_messages.Count > 10)
                _messages.RemoveAt(0);
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
            /*if (key == Keys.PageUp && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_UP");

            else if (key == Keys.PageDown && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_DOWN");*/

            if (!IsFocused) return;

            if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) || (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                ActivateKeyboardLayout(1, 0);

            _lastKey = key;

            if (key == Keys.Escape)
            {
                IsFocused = false;
                CurrentInput = "";
            }

            
            var keyChar = GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), false);

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
                CurrentInput = "";
                return;
            }
            else if (keyChar[0] == 27)
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