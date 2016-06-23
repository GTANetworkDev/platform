using System;
using GTA;

namespace GTANetwork.GUI
{
    public class RadioWheel : Script
    {
        public RadioWheel()
        {
            Tick += OnTick;
        }

        private bool _disable;
        private DateTime _lastPress;

        public void OnTick(object sender, EventArgs e)
        {
            if (_disable)
                Game.DisableControl(0, Control.VehicleRadioWheel);

            if (Game.IsControlJustPressed(0, Control.VehicleRadioWheel))
            {
                _lastPress = DateTime.Now;
            }

            if (Game.IsControlPressed(0, Control.VehicleRadioWheel) && !_disable && DateTime.Now.Subtract(_lastPress).TotalMilliseconds > 100)
            {
                _disable = true;
            }

            if (Game.IsControlJustReleased(0, Control.VehicleRadioWheel))
            {
                _disable = false;
            }


        }
    }
}