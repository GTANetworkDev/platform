using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using NativeUI.PauseMenu;

namespace GTANetwork.GUI
{
    public class TabMapItem : TabItem
    {
        public static string MAP_PATH = "scripts\\map\\map.png";
        private PointF Position = new PointF(1000, 2000);
        private Size Size  = new Size(6420, 7898);
        private float Zoom = 1f;
        private bool _isHeldDown;
        private PointF _heldDownPoint;
        private PointF _mapPosAtHelddown;
        private Sprite _crosshair = new Sprite("minimap", "minimap_g0", new Point(), new Size(256, 1));
        private float Ratio = 6420f/ 7898f;
        private bool _focused;
        private DateTime _holdDownTime;


        public TabMapItem() : base("Map")
        {
            CanBeFocused = true;
            DrawBg = false;
        }

        public override bool Focused
        {
            get { return _focused; }
            set
            {
                if (!_focused && value)
                {
                    Main.MainMenu.HideTabs = true;
                }
                else if (_focused && !value)
                {
                    Main.MainMenu.HideTabs = false;
                }
                _focused = value; 
            }
        }

        public override void ProcessControls()
        {
            if (Game.IsControlPressed(0, Control.MoveDownOnly))
            {
                Position = new PointF(Position.X, Position.Y + (20 / Zoom));
            }

            if (Game.IsControlPressed(0, Control.MoveUpOnly))
            {
                Position = new PointF(Position.X, Position.Y - (20 / Zoom));
            }

            if (Game.IsControlPressed(0, Control.MoveLeftOnly))
            {
                Position = new PointF(Position.X - (20 / Zoom), Position.Y);
            }

            if (Game.IsControlPressed(0, Control.MoveRightOnly))
            {
                Position = new PointF(Position.X + (20 / Zoom), Position.Y);
            }

            if (Game.IsControlPressed(0, Control.CursorScrollDown))
            {
                Zoom /= 1.1f;
            }

            if (Game.IsControlPressed(0, Control.CursorScrollUp))
            {
                Zoom *= 1.1f;
            }
        }

        public override void Draw()
        {
            base.Draw();

            if (!Focused)
            {
                DrawSprite("minimap_sea_2_0", "minimap_sea_2_0", new Point(BottomRight.X - 1024, TopLeft.Y), new Size(1024, 1024));
            }
            else
            {
                Game.EnableControl(0, Control.CursorX);
                Game.EnableControl(0, Control.CursorY);
                var res = UIMenu.GetScreenResolutionMantainRatio();
                var mouseX = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)Control.CursorX) * res.Width;
                var mouseY = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)Control.CursorY) * res.Height;
                var center = new Point((int)(res.Width/2), (int)(res.Height/2));

                if (Game.IsControlJustPressed(0, Control.CursorAccept))
                {
                    _isHeldDown = true;
                    _heldDownPoint = new PointF(mouseX, mouseY);
                    _mapPosAtHelddown = Position;
                    _holdDownTime = DateTime.Now;
                }
                else if (Game.IsControlJustReleased(0, Control.CursorAccept))
                {
                    Position = _mapPosAtHelddown + new SizeF((_heldDownPoint.X - mouseX) / Zoom, (_heldDownPoint.Y - mouseY) / Zoom);
                    _isHeldDown = false;

                    if (DateTime.Now.Subtract(_holdDownTime).TotalMilliseconds < 300)
                    {
                        var wpyPos = new PointF(center.X - Position.X * Zoom, center.Y - Position.Y * Zoom);
                        var realPos = Map2DToWorld3d(wpyPos, new PointF(945, 1910));
                        Function.Call(Hash.SET_NEW_WAYPOINT, realPos.X, realPos.Y);
                        UI.Notify(realPos.X + " " + realPos.Y);
                    }
                }

                var mapPos = Position;

                if (_isHeldDown)
                {
                    mapPos = _mapPosAtHelddown + new SizeF((_heldDownPoint.X - mouseX) / Zoom, (_heldDownPoint.Y - mouseY) / Zoom);
                }


                var newPos = new PointF(center.X - mapPos.X*Zoom, center.Y - mapPos.Y*Zoom);
                var newSize = new SizeF(1024*Zoom, 1024*Zoom);

                UI.ShowSubtitle($"{newPos.X} {newPos.Y}");

                DrawSprite("minimap_sea_0_0", "minimap_sea_0_0", newPos, newSize);
                DrawSprite("minimap_sea_0_1", "minimap_sea_0_1", newPos + new SizeF(1024 * Zoom, 0), newSize);

                DrawSprite("minimap_sea_1_0", "minimap_sea_1_0", newPos + new SizeF(0, 1024 * Zoom), newSize);
                DrawSprite("minimap_sea_1_1", "minimap_sea_1_1", newPos + new SizeF(1024 * Zoom, 1024 * Zoom), newSize);

                DrawSprite("minimap_sea_2_0", "minimap_sea_2_0", newPos + new SizeF(0, 2048 * Zoom), newSize);
                DrawSprite("minimap_sea_2_1", "minimap_sea_2_1", newPos + new SizeF(1024 * Zoom, 2048 * Zoom), newSize);

                _crosshair.Size = new Size(256, 3);
                _crosshair.Position = center + new Size(10, 0);
                _crosshair.Heading = 0;
                _crosshair.Draw();

                _crosshair.Position = center - new Size(266, 0);
                _crosshair.Heading = 180;
                _crosshair.Draw();

                _crosshair.Size = new Size(3, 256);
                _crosshair.Heading = 90;
                _crosshair.Position = center + new Size(0, 10);
                _crosshair.Draw();

                _crosshair.Position = center - new Size(0, 266);
                _crosshair.Heading = 270;
                _crosshair.Draw();


                DrawSprite("cross", "circle_checkpoints_cross", newPos + World3DToMap2D(Game.Player.Character.Position), new Size(16, 16), Color.Red);
            }
        }

        public PointF AbsoluteToRelative(Vector2 pos, PointF mapPos)
        {
            var res = UIMenu.GetScreenResolutionMantainRatio();
            var center = new Point((int)(res.Width / 2), (int)(res.Height / 2));
            return new PointF(center.X - pos.X*Zoom, center.Y - pos.Y*Zoom) + new SizeF(mapPos.X, mapPos.Y);
        }

        public Vector2 Map2DToWorld3d(PointF mapPos, PointF mapPoint)
        {
            var absoluteZero = new PointF(945 * Zoom, 1910 * Zoom);
            float pixelRatio = 4.39106f / Zoom;


            return new Vector2()
            {
                X = (mapPoint.X - absoluteZero.X - mapPos.X) * pixelRatio,
                Y = (mapPoint.Y - absoluteZero.Y - mapPos.X) * pixelRatio * -1f,
            };
        }

        public SizeF World3DToMap2D(Vector3 worldPos)
        {
            var absoluteZero = new PointF(945 * Zoom, 1910 * Zoom);
            float pixelRatio = 4.39106f / Zoom;
            
            return new SizeF()
            {
                Width = absoluteZero.X + (worldPos.X / pixelRatio),
                Height = absoluteZero.Y + (worldPos.Y * -1f / pixelRatio),
            };
        }

        

        public void DrawSprite(string dict, string name, PointF pos, SizeF size)
        {
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;


            float w = (size.Width / width);
            float h = (size.Height / height);
            float x = (pos.X / width) + w * 0.5f;
            float y = (pos.Y / height) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, name, x, y, w, h, 0f, 255, 255, 255, 255);
        }

        public void DrawSprite(string dict, string name, PointF pos, SizeF size, Color col)
        {
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;


            float w = (size.Width / width);
            float h = (size.Height / height);
            float x = (pos.X / width) + w * 0.5f;
            float y = (pos.Y / height) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, name, x, y, w, h, 0f, col.R, col.G, col.B, col.A);
        }
    }
}