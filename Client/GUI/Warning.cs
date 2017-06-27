using System;
using System.Drawing;
using GTA;
using GTA.Native;
using NativeUI;
using Font = GTA.UI.Font;

namespace GTANetwork.GUI
{
    public class Warning
    {
        public string Header { get; set; }
        public string Message { get; set; }
        public bool Visible { get; set; }
        public bool Error { get; set; }

        public Action OnAccept { get; set; }

        public Warning(string message) : this("alert", message)
        { }

        public Warning(string header, string message)
        {
            Header = header;
            Message = message;
            Visible = true;
        }

        public void Draw()
        {
            if (!Visible) return;

            Game.DisableAllControlsThisFrame();

            var res = UIMenu.GetScreenResolutionMantainRatio();
            
            var center = new Point((int) (res.Width/2), (int) (res.Height/2));

            new UIResRectangle(new Point(0, 0), new Size((int)res.Width + 1, (int)res.Height + 1), Color.Black).Draw();

            new UIResText(Header,
                new Point((int)(res.Width / 2), (int)(res.Height / 2) - 200),
                2f, Error ? Color.FromArgb(224, 50, 50) : Color.FromArgb(240, 200, 80),
                Font.Pricedown, UIResText.Alignment.Centered).Draw();

            new UIResText(Message,
                new Point((int)(res.Width / 2), (int)(res.Height / 2) - 80),
                0.4f, Color.White, Font.ChaletLondon, UIResText.Alignment.Centered).Draw();

            new UIResRectangle(center - new Size(400, 90), new Size(800, 3), Color.White).Draw();
            new UIResRectangle(center - new Size(400, 55 - (25 * (Message.Split('\n').Length) - 1)),
                new Size(800, 3), Color.White).Draw();

            var scaleform = new Scaleform("instructional_buttons");
            scaleform.CallFunction("CLEAR_ALL");
            scaleform.CallFunction("TOGGLE_MOUSE_BUTTONS", 0);
            scaleform.CallFunction("CREATE_CONTAINER");

            scaleform.CallFunction("SET_DATA_SLOT", 0, Function.Call<string>((Hash)0x0499D7B09FC9B407, 2, (int)Control.FrontendAccept, 0), "Accept");
            scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
            scaleform.Render2D();

            if (Game.IsDisabledControlJustPressed(Control.FrontendAccept))
            {
                OnAccept?.Invoke();
            }
        }
    }
}