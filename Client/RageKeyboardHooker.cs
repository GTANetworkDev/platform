using System.Linq;
using System.Windows.Forms;
using Rage;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace GTANetwork
{
    public class RageKeyboardHooker
    {
        private KeyboardState _lastState;

        public event KeyEventHandler OnKeyDown;
        public event KeyEventHandler OnKeyUp;

        public RageKeyboardHooker()
        {
            
        }

        public void Update()
        {
            var newState = Game.GetKeyboardState();

            if (_lastState != null)
            {
                var newPressedKeys = newState.PressedKeys.Except(_lastState.PressedKeys);
                var newReleasedKeys = newState.ReleasedKeys.Except(_lastState.ReleasedKeys);

                foreach (var key in newPressedKeys)
                {
                    OnKeyDown?.Invoke(this, new KeyEventArgs(key));
                }

                foreach (var key in newReleasedKeys)
                {
                    OnKeyUp?.Invoke(this, new KeyEventArgs(key));
                }
            }

            _lastState = newState;
        }
    }
}