using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;

namespace GTANetwork.GUI
{
    public class TabMapItem : TabItem
    {
        public static string MAP_PATH = Main.GTANInstallDir + "\\bin\\scripts\\map\\map.png";
        public static string BLIP_PATH = Main.GTANInstallDir + "\\bin\\scripts\\map\\blips\\";
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
        private bool _showPlayerBlip;
        private DateTime _lastShowPlayerBlip;

        private Dictionary<string, Texture> TextureDict = new Dictionary<string, Texture>();


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
                    var newPos = World3DToMap2D(Game.LocalPlayer.Character.Position);
                    Position = new PointF(newPos.Width/Zoom, newPos.Height/Zoom);
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
            if (Game.IsControlPressed(0, GameControl.MoveDownOnly))
            {
                Position = new PointF(Position.X, Position.Y + (20 / Zoom));
            }

            if (Game.IsControlPressed(0, GameControl.MoveUpOnly))
            {
                Position = new PointF(Position.X, Position.Y - (20 / Zoom));
            }

            if (Game.IsControlPressed(0, GameControl.MoveLeftOnly))
            {
                Position = new PointF(Position.X - (20 / Zoom), Position.Y);
            }

            if (Game.IsControlPressed(0, GameControl.MoveRightOnly))
            {
                Position = new PointF(Position.X + (20 / Zoom), Position.Y);
            }

            if (Game.IsControlPressed(0, GameControl.CursorScrollDown))
            {
                Zoom /= 1.1f;
            }

            if (Game.IsControlPressed(0, GameControl.CursorScrollUp))
            {
                Zoom *= 1.1f;
            }
        }

        public Texture GetTexture(string filepath)
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

        
        public void Draw(GraphicsEventArgs e)
        {
            base.Draw();

            if (!Focused)
            {
                DrawSprite("minimap_sea_2_0", "minimap_sea_2_0", new Point(BottomRight.X - 1024, TopLeft.Y), new Size(1024, 1024));
            }
            else
            {
                Util.EnableControl(0, GameControl.CursorX);
                Util.EnableControl(0, GameControl.CursorY);
                var res = UIMenu.GetScreenResolutionMantainRatio();
                var mouseX = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)GameControl.CursorX) * res.Width;
                var mouseY = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)GameControl.CursorY) * res.Height;
                var center = new Point((int)(res.Width/2), (int)(res.Height/2));

                if (Game.IsControlJustPressed(0, GameControl.CursorAccept))
                {
                    _isHeldDown = true;
                    _heldDownPoint = new PointF(mouseX, mouseY);
                    _mapPosAtHelddown = Position;
                    _holdDownTime = DateTime.Now;
                }
                else if (Game.IsControlJustReleased(0, GameControl.CursorAccept))
                {
                    Position = _mapPosAtHelddown + new SizeF((_heldDownPoint.X - mouseX) / Zoom, (_heldDownPoint.Y - mouseY) / Zoom);
                    _isHeldDown = false;

                    if (DateTime.Now.Subtract(_holdDownTime).TotalMilliseconds < 100)
                    {
                        if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                        {
                            Function.Call(Hash.SET_WAYPOINT_OFF);
                        }
                        else
                        {
                            var wpyPos = new PointF(center.X - Position.X*Zoom, center.Y - Position.Y*Zoom);
                            var ourShit = new SizeF(mouseX - wpyPos.X, mouseY - wpyPos.Y);
                            var realPos = Map2DToWorld3d(wpyPos, wpyPos + ourShit);
                            Function.Call(Hash.SET_NEW_WAYPOINT, realPos.X, realPos.Y*-1f);
                        }
                    }
                }

                var mapPos = Position;

                if (_isHeldDown)
                {
                    mapPos = _mapPosAtHelddown + new SizeF((_heldDownPoint.X - mouseX) / Zoom, (_heldDownPoint.Y - mouseY) / Zoom);
                }


                var newPos = new PointF(center.X - mapPos.X*Zoom, center.Y - mapPos.Y*Zoom);
                var newSize = new SizeF(1024*Zoom, 1024*Zoom);

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

                if (_showPlayerBlip)
                {
                    Sprite.DrawTexture(GetTexture(BLIP_PATH + "163.png"),
                        new Point((int) (newPos.X + World3DToMap2D(Game.LocalPlayer.Character.Position).Width - 16),
                            (int) (newPos.Y + World3DToMap2D(Game.LocalPlayer.Character.Position).Height - 16)),
                        new Size(32, 32), e);
                }

                if (DateTime.Now.Subtract(_lastShowPlayerBlip).TotalMilliseconds >= 1000)
                {
                    _lastShowPlayerBlip = DateTime.Now;
                    _showPlayerBlip = !_showPlayerBlip;
                }


                var blipList = new List<string>();

                foreach (var blip in World.GetAllBlips())
                {
                    if (((int)blip.Sprite) == 8 && File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int) blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position) - new Size(16, 16);
                        var siz = new Size(32, 32);
                        var col = Color.Purple;

                        Util.DxDrawTexture(GetTexture(fname), pos.X, pos.Y, siz.Width, siz.Height, 0f, col.R, col.G, col.B, col.A);
                        blipList.Add(((int)blip.Sprite) + ".png");
                    }
                }
                
                foreach (var blipHandle in Main.NetEntityHandler.Blips)
                {
                    var blip = World.GetBlipByHandle(blipHandle);
                    if (!blip.Exists()) continue;

                    if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position) - new Size(16, 16);
                        var siz = new Size(32, 32);
                        var ident = fname +
                                    (blipList.Count(k => k == fname) > 0
                                        ? blipList.Count(k => k == fname).ToString()
                                        : "");
                        Util.DxDrawTexture(GetTexture(fname), pos.X, pos.Y, siz.Width, siz.Height, 0f, blip.Color.R, blip.Color.G, blip.Color.B, blip.Color.A);
                        blipList.Add(((int)blip.Sprite) + ".png");
                    }
                }

                foreach (var opp in Main.Opponents)
                {
                    if (opp.Value.Character?.GetAttachedBlip() == null) continue;

                    var blip = opp.Value.Character.GetAttachedBlip();

                    if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position) - new Size(8, 8);
                        var siz = new Size(16, 16);
                        Util.DxDrawTexture(GetTexture(fname), pos.X, pos.Y, siz.Width, siz.Height, 0f, blip.Color.R, blip.Color.G, blip.Color.B, blip.Color.A);
                        blipList.Add(((int)blip.Sprite) + ".png");
                    }
                }

                foreach (var blipHandle in Main.BlipCleanup)
                {
                    var blip = World.GetBlipByHandle(blipHandle);
                    if (!blip.Exists()) continue;

                    if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position) - new Size(16, 16);
                        var siz = new Size(32, 32);
                        var ident = fname +
                                    (blipList.Count(k => k == fname) > 0
                                        ? blipList.Count(k => k == fname).ToString()
                                        : "");
                        Util.DxDrawTexture(GetTexture(fname), pos.X, pos.Y, siz.Width, siz.Height, 0f, blip.Color.R, blip.Color.G, blip.Color.B, blip.Color.A);
                        blipList.Add(((int)blip.Sprite) + ".png");
                    }
                }
            }
        }

        public static Color GetBlipcolor(int col, int a)
        {
            switch (col)
            {
                case 0:
                default: return Color.FromArgb(a, 255, 255, 255);
                case 3:
                    return Color.FromArgb(a, 93, 182, 229);
                case 2:
                    return Color.FromArgb(a, 114, 204, 114);
                case 1:
                    return Color.FromArgb(a, 224, 50, 50);
                case 66:
                    return Color.FromArgb(a, 250, 200, 80);

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
                Y = (mapPoint.Y - absoluteZero.Y - mapPos.Y) * pixelRatio,
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

            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;
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

            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;


            float w = (size.Width / width);
            float h = (size.Height / height);
            float x = (pos.X / width) + w * 0.5f;
            float y = (pos.Y / height) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, name, x, y, w, h, 0f, (int)col.R, (int)col.G, (int)col.B, (int)col.A);
        }
    }
}