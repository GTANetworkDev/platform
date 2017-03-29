using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;
using GTANetwork.GUI;

namespace GTANetwork.Javascript
{
    public delegate void BooleanEvent(bool value);
    public delegate void IntegerEvent(int value);
    public delegate void MessageEvent(string msg, bool hasColor, int r, int g, int b);

    public enum StringSanitation
    {
        None = 0,
        Javascript = 1,
        Html = 2,
        All = 3,
    }

    public class JavascriptChat : IChat
    {
        public event EventHandler OnComplete;

        public event ScriptContext.EmptyEvent onTick;
        public event KeyEventHandler onKeyDown;
        public event BooleanEvent onFocusChange;
        public event ScriptContext.EmptyEvent onInit;
        public event ScriptContext.EmptyEvent onClearRequest;
        public event MessageEvent onAddMessageRequest;
        public event IntegerEvent onCharInput;
        public event BooleanEvent onChatHideRequest;

        public int SanitationLevel { get; set; }        

        private bool isHidden;
        private Keys _lastKey;

        public void Tick()
        {
            if (_pushString)
            {
                Main.ChatOnComplete(null, EventArgs.Empty);
                _pushString = false;
            }

            if ((!Main.ChatVisible || !Main.ScriptChatVisible) && !isHidden)
            {
                onChatHideRequest?.Invoke(true);
                isHidden = true;
            }
            else if ((Main.ChatVisible && Main.ScriptChatVisible) && isHidden)
            {
                onChatHideRequest?.Invoke(false);
                isHidden = false;
            }
        }

        public void sendMessage(string msg)
        {
            CurrentInput = msg;
            _pushString = true;
        }

        private bool _pushString;

        public void OnKeyDown(Keys key)
        {
            if (!IsFocused || !Main.ChatVisible || !Main.ScriptChatVisible) return;

            
            if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) || (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                ClassicChat.ActivateKeyboardLayout(1, 0);

            _lastKey = key;

            if (key == Keys.Escape)
            {
                IsFocused = false;
                CurrentInput = string.Empty;
            }


            var keyChar = ClassicChat.GetCharFromKey(key, GTA.Game.IsKeyPressed(Keys.ShiftKey), GTA.Game.IsKeyPressed(Keys.Menu) && GTA.Game.IsKeyPressed(Keys.ControlKey));

            if (keyChar.Length == 0) return;
            
            onCharInput?.Invoke((int)keyChar[0]);
        }

        private bool _isFocused;

        public bool IsFocused
        {
            get
            {
                return _isFocused;
            }
            set
            {
                if (_isFocused != value)
                    onFocusChange?.Invoke(value);

                _isFocused = value;
            }
        }

        public void Init()
        {
            onInit?.Invoke();
        }

        public string CurrentInput { get; set; }

        public void Clear()
        {
            onClearRequest?.Invoke();
        }

        public void AddMessage(string sender, string message)
        {
            Color? textColor = null;
            if (sender != null && Regex.IsMatch(sender, "^~#[a-fA-F0-9]{6}~"))
            {
                textColor = ColorTranslator.FromHtml(sender.Substring(1, 7));
                if (sender.Length == 9) sender = null;
            }

            var finalMsg = string.Empty;

            if (string.IsNullOrEmpty(sender))
            {
                finalMsg = message;
            }
            else
            {
                finalMsg = sender + ": " + message;
            }

            switch ((StringSanitation) SanitationLevel)
            {
                case StringSanitation.Javascript:
                    finalMsg = HttpUtility.JavaScriptStringEncode(finalMsg, false);
                    break;
                case StringSanitation.Html:
                    finalMsg = HttpUtility.HtmlEncode(finalMsg);
                    break;
            }

            var tempCol = textColor ?? Color.White;
            
            onAddMessageRequest?.Invoke(finalMsg, textColor.HasValue, tempCol.R, tempCol.G, tempCol.B);
        }
    }
}