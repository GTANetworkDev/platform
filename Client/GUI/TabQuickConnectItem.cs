using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;
using NativeUI;
using NativeUI.PauseMenu;
using Font = GTA.Font;

namespace GTANetwork.GUI
{
    public class TabButtonArrayItem : TabItem
    {
        public TabButtonArrayItem(string name) : base(name)
        {
            CanBeFocused = true;
            Buttons = new List<TabButton>();
            DrawBg = false;
        }

        public List<TabButton> Buttons { get; set; }

        private bool _visible;
        public override bool Visible {
            get { return _visible; }
            set
            {
                _visible = value;
            }
        }

        private int _index;
        public int Index
        {
            get
            {
                return _index;
            }
            set
            {
                _index = (1000 - (1000 % Buttons.Count) + value) % Buttons.Count;
            }
        }

        public override void ProcessControls()
        {
            if (!Focused) return;

            if (JustOpened)
            {
                JustOpened = false;
                return;
            }

            if (Game.IsControlJustPressed(0, Control.FrontendUp) || Game.IsControlJustPressed(0, Control.MoveUpOnly))
            {
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
                Index--;
            }

            else if (Game.IsControlJustPressed(0, Control.FrontendDown) || Game.IsControlJustPressed(0, Control.MoveDownOnly))
            {
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
                Index++;
            }

            if (Game.IsControlJustPressed(0, Control.FrontendAccept))
            {
                Buttons[Index].OnActivated();
            }
        }

        public override void Draw()
        {
            base.Draw();

            for (int i = 0; i < Buttons.Count; i++)
            {
                Buttons[i].Position = SafeSize.AddPoints(new Point(15, 15 + 55 * i));
                Buttons[i].Size = new Size(BottomRight.X - TopLeft.X, 40);
                Buttons[i].Focused = Focused;
                Buttons[i].Active = Index == i;
                Buttons[i].Visible = true;
                var hovered = UIMenu.IsMouseInBounds(Buttons[i].Position, Buttons[i].Size);
                Buttons[i].Hovered = hovered && Index != i;

                if (hovered && Game.IsControlJustPressed(0, Control.CursorAccept))
                {
                    if (Index != i)
                        Index = i;
                    else
                        Buttons[i].OnActivated();
                }

                Buttons[i].Draw();
            }
        }
    }

    public class TabButton
    {
        public bool Visible { get; set; }
        public bool Focused { get; set; }
        public bool Active { get; set; }
        public string Text { get; set; }
        public Point Position { get; set; }
        public Size Size { get; set; }
        public bool JustOpened { get; set; }
        public bool Hovered { get; set; }

        public event EventHandler Activated;

        public void OnActivated()
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }

        public void Draw()
        {
            if (!Visible) return;
            var col = Focused ? Hovered ? Color.FromArgb(100, 50, 50, 50) : Color.FromArgb(200, 0, 0, 0) : Color.FromArgb(100, 0, 0, 0);
            new UIResRectangle(Position, Size, col).Draw();
            new UIResText(Text, Position + new Size(Size.Width/2, Size.Height/5), 0.35f, Color.White, Font.ChaletLondon, UIResText.Alignment.Centered).Draw();
            if (Active)
            {
                new UIResRectangle(Position.SubtractPoints(new Point(0, 5)), new Size(Size.Width, 5), Focused ? Color.DodgerBlue : Color.FromArgb(50, Color.DodgerBlue)).Draw();
            }
        }
    }
}