using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.Networking;
using GTANetwork.Util;
using NativeUI;
using NativeUI.PauseMenu;
using Control = GTA.Control;
using Screen = GTA.UI.Screen;

namespace GTANetwork.GUI
{
    public class TabMapItem : TabItem
    {
        public static string MAP_PATH = Main.GTANInstallDir + "images\\map\\map.png";
        public static string BLIP_PATH = Main.GTANInstallDir + "images\\map\\blips\\";

        public PointF Position
        {
            get { return _position1; }
            set
            {
                if (DateTime.Now.Subtract(_lastScroll).TotalMilliseconds > 100)
                {
                    _lastScroll = DateTime.Now;
                    //Audio.PlaySoundFrontend("continue", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                }

                _position1 = value;
            }
        }

        private Size Size  = new Size(6420, 7898);
        private float Zoom = 1f;
        private bool _isHeldDown;
        private bool _justOpened_mouse;
        private bool _justOpened_kb;
        private PointF _heldDownPoint;
        private PointF _mapPosAtHelddown;
        private Sprite _crosshair = new Sprite("minimap", "minimap_g0", new Point(), new Size(256, 1));
        private float Ratio = 6420f/ 7898f;
        private bool _focused;
        private DateTime _holdDownTime;
        private DateTime _lastScroll;


        public TabMapItem() : base("Map")
        {
            CanBeFocused = true;
            DrawBg = false;
            Position = new PointF(1000, 2000);
        }

        public override bool Focused
        {
            get { return _focused; }
            set
            {
                if (!_focused && value)
                {
                    Main.MainMenu.HideTabs = true;
                    var newPos = World3DToMap2D(Game.Player.Character.Position);
                    Position = new PointF(newPos.Width/Zoom, newPos.Height/Zoom);
                    _justOpened_mouse = true;
                    _justOpened_kb = true;
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
                _wasMouseInput = false;
            }

            if (Game.IsControlPressed(0, Control.MoveUpOnly))
            {
                Position = new PointF(Position.X, Position.Y - (20 / Zoom));
                _wasMouseInput = false;
            }

            if (Game.IsControlPressed(0, Control.MoveLeftOnly))
            {
                Position = new PointF(Position.X - (20 / Zoom), Position.Y);
                _wasMouseInput = false;
            }

            if (Game.IsControlPressed(0, Control.MoveRightOnly))
            {
                Position = new PointF(Position.X + (20 / Zoom), Position.Y);
                _wasMouseInput = false;
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

        private Size offsetP = new Size();
        private Size offsetS = new Size();

        private bool _wasMouseInput;
        private PointF _lastMouseInput;
        private PointF _position1;

        public override void Draw()
        {
            base.Draw();

            if (!Focused)
            {
                DrawSprite("minimap_sea_2_0", "minimap_sea_2_0", new Point(BottomRight.X - 1024, TopLeft.Y), new Size(1024, 1024));

                if (Game.IsControlJustPressed(0, Control.Attack))
                {
                    Focused = true;
                    _justOpened_mouse = true;
                    _justOpened_kb = true;
                    Audio.PlaySoundFrontend("accept", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                }
            }
            else
            {
                Game.EnableControlThisFrame(0, Control.CursorX);
                Game.EnableControlThisFrame(0, Control.CursorY);
                var res = UIMenu.GetScreenResolutionMantainRatio();
                var mouseX = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)Control.CursorX) * res.Width;
                var mouseY = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, (int)Control.CursorY) * res.Height;
                var center = new Point((int)(res.Width/2), (int)(res.Height/2));


                if (Math.Abs(mouseX - _lastMouseInput.X) > 1f || Math.Abs(mouseY - _lastMouseInput.Y) > 1f)
                {
                    _wasMouseInput = true;
                }

                if (Game.IsControlJustPressed(0, Control.CursorAccept))
                {
                    _isHeldDown = true;
                    _heldDownPoint = new PointF(mouseX, mouseY);
                    _mapPosAtHelddown = Position;
                    _holdDownTime = DateTime.Now;
                }
                else if (Game.IsControlJustReleased(0, Control.CursorAccept))
                {
                    if (_justOpened_mouse)
                    {
                        _justOpened_mouse = false;
                        _justOpened_kb = false;
                    }
                    else
                    {
                        Position = _mapPosAtHelddown +
                                   new SizeF((_heldDownPoint.X - mouseX)/Zoom, (_heldDownPoint.Y - mouseY)/Zoom);
                        _isHeldDown = false;

                        if (DateTime.Now.Subtract(_holdDownTime).TotalMilliseconds < 100)
                        {
                            if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                            {
                                Function.Call(Hash.SET_WAYPOINT_OFF);
                                Audio.PlaySoundFrontend("CANCEL", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            }
                            else
                            {
                                var wpyPos = new PointF(center.X - Position.X*Zoom, center.Y - Position.Y*Zoom);
                                var ourShit = new SizeF(mouseX - wpyPos.X, mouseY - wpyPos.Y);
                                var realPos = Map2DToWorld3d(wpyPos, wpyPos + ourShit);
                                Function.Call(Hash.SET_NEW_WAYPOINT, realPos.X, realPos.Y*-1f);
                                Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            }
                        }
                    }
                }

                if (Game.IsControlJustReleased(0, Control.FrontendAccept))
                {
                    if (_justOpened_kb)
                    {
                        _justOpened_kb = false;
                        _justOpened_mouse = false;
                    }
                    else
                    {
                        if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                        {
                            Function.Call(Hash.SET_WAYPOINT_OFF);
                            Audio.PlaySoundFrontend("CANCEL", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        }
                        else
                        {
                            var wpyPos = new PointF(center.X - Position.X*Zoom, center.Y - Position.Y*Zoom);
                            var ourShit = new SizeF(center.X - wpyPos.X, center.Y - wpyPos.Y);
                            var realPos = Map2DToWorld3d(wpyPos, wpyPos + ourShit);
                            Function.Call(Hash.SET_NEW_WAYPOINT, realPos.X, realPos.Y*-1f);
                            Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        }
                    }
                }

                if (_isHeldDown)
                {
                    Position = _mapPosAtHelddown + new SizeF((_heldDownPoint.X - mouseX) / Zoom, (_heldDownPoint.Y - mouseY) / Zoom);
                }

                var newPos = new PointF(center.X - Position.X*Zoom, center.Y - Position.Y*Zoom);
                var newSize = new SizeF(1024.5f*Zoom, 1024.5f*Zoom);

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

                const float playerScale = 0.8f;

                Util.Util.DxDrawTexture(0, BLIP_PATH + "player.png",
                    newPos.X + World3DToMap2D(Game.Player.Character.Position).Width,
                    newPos.Y + World3DToMap2D(Game.Player.Character.Position).Height, 32* playerScale, 32* playerScale,
                    -Game.Player.Character.Rotation.Z, 255, 255, 255, 255, true);

                

                var blipList = new List<string>();
                var localCopy = new List<IStreamedItem>(Main.NetEntityHandler.ClientMap.Values);

                foreach (var blip in Util.Util.GetAllBlips())
                {
					if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
					{
					    var blipInfo = Main.NetEntityHandler.EntityToStreamedItem(blip.Handle) as RemoteBlip;

					    float scale = 1f;

					    if (blipInfo != null)
					    {
					        scale = blipInfo.Scale;
					    }
                        
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
						var pos = newPos + World3DToMap2D(blip.Position);
                        var siz = new Size((int)(scale * 32), (int)(scale * 32));
                        var col = GetBlipcolor(blip.Color, blip.Alpha);

                        if (pos.X > 0 && pos.Y > 0 && pos.X < res.Width && pos.Y < res.Height)
						    Util.Util.DxDrawTexture(blipList.Count, fname, pos.X, pos.Y, siz.Width, siz.Height, 0f, col.R, col.G, col.B, col.A, true);

					    var len = scale*32;
					    var halfLen = len/2;
                        var hoverPos = new PointF(mouseX, mouseY);

					    if (!_wasMouseInput) hoverPos = center;

					    if (blipInfo != null &&
                            !string.IsNullOrEmpty(blipInfo.Name) &&
                            hoverPos.X > pos.X - halfLen && hoverPos.Y > pos.Y - halfLen &&
                            hoverPos.X < pos.X + halfLen && hoverPos.Y < pos.Y + halfLen) // hovering over blip
					    {
                            var labelPos = pos - new Size(-32, 14);

                            new Sprite("mplobby", "mp_arrowsmall", new Point((int)labelPos.X - 19, (int)labelPos.Y - 15),
                                new Size(20, 60), 180f, Color.Black).Draw();
                            new UIResRectangle(new Point((int)labelPos.X, (int)labelPos.Y), new Size(15 + StringMeasurer.MeasureString(blipInfo.Name), 30), Color.Black).Draw();
                            new UIResText(blipInfo.Name, new Point((int)labelPos.X + 5, (int)labelPos.Y), 0.35f).Draw();
                        }
					}
                }
                
                foreach (var blip in localCopy.Where(item => item is RemoteBlip && !item.StreamedIn).Cast<RemoteBlip>()) // draw the unstreamed blips
                {
                    if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position.ToVector());
                        var scale = blip.Scale;
                        var siz = new Size((int)(scale * 32), (int) (scale * 32));
                        var col = GetBlipcolor((BlipColor)blip.Color, blip.Alpha);

                        if (pos.X > 0 && pos.Y > 0 && pos.X < res.Width && pos.Y < res.Height)
                            Util.Util.DxDrawTexture(blipList.Count, fname, pos.X, pos.Y, siz.Width, siz.Height, 0f, col.R, col.G, col.B, col.A, true);
                    }
                }

                foreach (var opp in Main.NetEntityHandler.ClientMap.Where(item => item is SyncPed).Cast<SyncPed>())
                {
                    if (opp.Character?.AttachedBlip == null || string.IsNullOrWhiteSpace(opp.Name) || opp.Character.AttachedBlip.Alpha == 0) continue;

                    var blip = opp.Character.AttachedBlip;
                    
                    var pos = newPos + World3DToMap2D(blip.Position) - new Size(-32, 14);

                    new Sprite("mplobby", "mp_arrowsmall", new Point((int) pos.X - 19, (int) pos.Y - 15) + offsetP,
                        new Size(20, 60) + offsetS, 180f, Color.Black).Draw();
                    new UIResRectangle(new Point((int)pos.X, (int)pos.Y), new Size(15 + StringMeasurer.MeasureString(opp.Name), 30), Color.Black).Draw();
                    new UIResText(opp.Name, new Point((int)pos.X + 5, (int)pos.Y), 0.35f).Draw();
                }



                var p1 = new PointF(center.X - Position.X * Zoom, center.Y - Position.Y * Zoom);
                var p2 = new SizeF(center.X - p1.X, center.Y - p1.Y);
                var centerPos = Map2DToWorld3d(p1, p1 + p2);

                centerPos = new Vector2(centerPos.X, centerPos.Y * -1f);

                var zone = World.GetZoneName(centerPos);

                if (!string.IsNullOrEmpty(zone))
                {
                    new UIResRectangle(new Point(30, (int)res.Height - 50),
                        new Size(15 + StringMeasurer.MeasureString(zone), 30), Color.Black).Draw();
                    new UIResText(zone, new Point(35, (int)res.Height - 50), 0.35f).Draw();
                }

                /*

                foreach (var blipHandle in Main.NetEntityHandler.Blips)
                    {
                        var blip = new Blip(blipHandle);
                        if (!blip.Exists()) continue;

                        if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                        {
                            var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                            var pos = newPos + World3DToMap2D(blip.Position) - new Size(16, 16);
                            var siz = new Size(32, 32);
                            var col = GetBlipcolor(blip.Color, blip.Alpha);
                            var ident = fname +
                                        (blipList.Count(k => k == fname) > 0
                                            ? blipList.Count(k => k == fname).ToString()
                                            : "");
                            Util.DxDrawTexture(blipList.Count, fname, pos.X, pos.Y, siz.Width, siz.Height, 0f, col.R, col.G, col.B, col.A);
                            blipList.Add(((int)blip.Sprite) + ".png");
                        }
                    }


                foreach (var blipHandle in Main.BlipCleanup)
                {
                    var blip = new Blip(blipHandle);
                    if (!blip.Exists()) continue;

                    if (File.Exists(BLIP_PATH + ((int)blip.Sprite) + ".png"))
                    {
                        var fname = BLIP_PATH + ((int)blip.Sprite) + ".png";
                        var pos = newPos + World3DToMap2D(blip.Position) - new Size(16, 16);
                        var siz = new Size(32, 32);
                        var col = GetBlipcolor(blip.Color, blip.Alpha);
                        var ident = fname +
                                    (blipList.Count(k => k == fname) > 0
                                        ? blipList.Count(k => k == fname).ToString()
                                        : "");
                        Util.DxDrawTexture(blipList.Count, fname, pos.X, pos.Y, siz.Width, siz.Height, 0f, col.R, col.G, col.B, col.A);
                        blipList.Add(((int)blip.Sprite) + ".png");
                    }
                }*/
            }
        }

        public static Color GetBlipcolor(BlipColor col, int a)
        {
            switch ((int)col - 1)
            {
                default: return Color.FromArgb(a, 255, 255, 255); // (21, 27)
                case 1: return Color.FromArgb(a, 224, 50, 50); // (54, 27)
                case 2: return Color.FromArgb(a, 114, 204, 114); // (87, 27)
                case 3: return Color.FromArgb(a, 93, 182, 229); // (120, 27)
                case 4: return Color.FromArgb(a, 240, 240, 240); // (153, 27)
                case 5: return Color.FromArgb(a, 240, 200, 80); // (186, 27)
                case 6: return Color.FromArgb(a, 194, 80, 80); // (219, 27)
                case 7: return Color.FromArgb(a, 156, 110, 175); // (252, 27)
                case 8: return Color.FromArgb(a, 255, 123, 196); // (285, 27)
                case 9: return Color.FromArgb(a, 229, 176, 147); // (318, 27)
                case 10: return Color.FromArgb(a, 199, 131, 209); // (21, 60)
                case 11: return Color.FromArgb(a, 215, 189, 121); // (54, 60)
                case 12: return Color.FromArgb(a, 139, 179, 167); // (87, 60)
                case 13: return Color.FromArgb(a, 123, 156, 84); // (120, 60)
                case 14: return Color.FromArgb(a, 144, 127, 153); // (153, 60)
                case 15: return Color.FromArgb(a, 106, 196, 191); // (186, 60)
                case 16: return Color.FromArgb(a, 214, 196, 153); // (219, 60)
                case 17: return Color.FromArgb(a, 234, 142, 80); // (252, 60)
                case 18: return Color.FromArgb(a, 152, 203, 234); // (285, 60)
                case 19: return Color.FromArgb(a, 178, 98, 135); // (318, 60)
                case 20: return Color.FromArgb(a, 144, 142, 122); // (21, 92)
                case 21: return Color.FromArgb(a, 166, 117, 94); // (54, 92)
                case 22: return Color.FromArgb(a, 175, 168, 168); // (87, 92)
                case 23: return Color.FromArgb(a, 232, 142, 155); // (120, 92)
                case 24: return Color.FromArgb(a, 187, 214, 91); // (153, 92)
                case 25: return Color.FromArgb(a, 12, 123, 86); // (186, 92)
                case 26: return Color.FromArgb(a, 123, 196, 255); // (219, 92)
                case 27: return Color.FromArgb(a, 171, 60, 230); // (252, 92)
                case 28: return Color.FromArgb(a, 206, 169, 13); // (285, 92)
                case 29: return Color.FromArgb(a, 71, 99, 173); // (318, 92)
                case 30: return Color.FromArgb(a, 42, 166, 185); // (21, 125)
                case 31: return Color.FromArgb(a, 186, 157, 125); // (54, 125)
                case 32: return Color.FromArgb(a, 201, 225, 255); // (87, 125)
                case 33: return Color.FromArgb(a, 240, 240, 150); // (120, 125)
                case 34: return Color.FromArgb(a, 237, 140, 161); // (153, 125)
                case 35: return Color.FromArgb(a, 249, 138, 138); // (186, 125)
                case 36: return Color.FromArgb(a, 252, 239, 166); // (219, 125)
                case 37: return Color.FromArgb(a, 240, 240, 240); // (252, 125)
                case 38: return Color.FromArgb(a, 45, 110, 185); // (285, 125)
                case 39: return Color.FromArgb(a, 154, 154, 154); // (318, 125)
                case 40: return Color.FromArgb(a, 77, 77, 77); // (21, 158)
                case 41: return Color.FromArgb(a, 240, 153, 153); // (54, 158)
                case 42: return Color.FromArgb(a, 101, 180, 212); // (87, 158)
                case 43: return Color.FromArgb(a, 171, 237, 171); // (120, 158)
                case 44: return Color.FromArgb(a, 255, 163, 87); // (153, 158)
                case 45: return Color.FromArgb(a, 240, 240, 240); // (186, 158)
                case 46: return Color.FromArgb(a, 235, 239, 30); // (219, 158)
                case 47: return Color.FromArgb(a, 255, 149, 14); // (252, 158)
                case 48: return Color.FromArgb(a, 246, 60, 161); // (285, 158)
                case 49: return Color.FromArgb(a, 221, 49, 49); // (318, 158)
                case 50: return Color.FromArgb(a, 100, 79, 142); // (21, 190)
                case 51: return Color.FromArgb(a, 255, 133, 85); // (54, 190)
                case 52: return Color.FromArgb(a, 57, 102, 57); // (87, 190)
                case 53: return Color.FromArgb(a, 174, 219, 242); // (120, 190)
                case 54: return Color.FromArgb(a, 47, 92, 115); // (153, 190)
                case 55: return Color.FromArgb(a, 155, 155, 155); // (186, 190)
                case 56: return Color.FromArgb(a, 126, 107, 41); // (219, 190)
                case 57: return Color.FromArgb(a, 93, 182, 229); // (252, 190)
                case 58: return Color.FromArgb(a, 50, 39, 71); // (285, 190)
                case 59: return Color.FromArgb(a, 224, 50, 50); // (318, 190)
                case 60: return Color.FromArgb(a, 240, 200, 80); // (21, 223)
                case 61: return Color.FromArgb(a, 203, 54, 148); // (54, 223)
                case 62: return Color.FromArgb(a, 205, 205, 205); // (87, 223)
                case 63: return Color.FromArgb(a, 29, 100, 153); // (120, 223)
                case 64: return Color.FromArgb(a, 214, 116, 15); // (153, 223)
                case 65: return Color.FromArgb(a, 135, 125, 142); // (186, 223)
                case 66: return Color.FromArgb(a, 240, 200, 80); // (219, 223)
                case 67: return Color.FromArgb(a, 93, 182, 229); // (252, 223)
                case 68: return Color.FromArgb(a, 93, 182, 229); // (285, 223)
                case 69: return Color.FromArgb(a, 114, 204, 114); // (318, 223)
                case 70: return Color.FromArgb(a, 240, 200, 80); // (21, 255)
                case 71: return Color.FromArgb(a, 240, 200, 80); // (54, 255)
                case 72: return Color.FromArgb(a, 133, 126, 109); // (87, 255)
                case 73: return Color.FromArgb(a, 240, 200, 80); // (120, 255)
                case 74: return Color.FromArgb(a, 93, 182, 229); // (153, 255)
                case 75: return Color.FromArgb(a, 224, 50, 50); // (186, 255)
                case 76: return Color.FromArgb(a, 112, 25, 25); // (219, 255)
                case 77: return Color.FromArgb(a, 93, 182, 229); // (252, 255)
                case 78: return Color.FromArgb(a, 47, 92, 115); // (285, 255)
                case 79: return Color.FromArgb(a, 158, 118, 103); // (318, 255)
                case 80: return Color.FromArgb(a, 131, 143, 137); // (21, 288)
                case 81: return Color.FromArgb(a, 240, 160, 0); // (54, 288)
                case 82: return Color.FromArgb(a, 159, 201, 166); // (87, 288)
                case 83: return Color.FromArgb(a, 164, 76, 242); // (120, 288)
                case 84: return Color.FromArgb(a, 93, 182, 229); // (153, 288)
                case 85: return Color.FromArgb(a, 127, 120, 103); // (186, 288)
                case 86: return Color.FromArgb(a, 127, 119, 103); // (219, 288)
                case 87: return Color.FromArgb(a, 126, 119, 102); // (252, 288)
                case 88: return Color.FromArgb(a, 126, 119, 102); // (285, 288)
                case 89: return Color.FromArgb(a, 126, 118, 102); // (318, 288)
            }
        }

        public PointF AbsoluteToRelative(Vector2 pos, PointF mapPos)
        {
            var res = UIMenu.GetScreenResolutionMantainRatio();
            var center = new Point((int)(res.Width / 2), (int)(res.Height / 2));
            return new PointF(center.X - pos.X*Zoom, center.Y - pos.Y*Zoom) + new SizeF(mapPos.X, mapPos.Y);
        }

        private const int absX = 943;
        private const int absY = 1912;

        public Vector2 Map2DToWorld3d(PointF mapPos, PointF mapPoint)
        {
            var absoluteZero = new PointF(absX * Zoom, absY * Zoom);
            float pixelRatio = 4.39106f / Zoom;


            return new Vector2()
            {
                X = (mapPoint.X - absoluteZero.X - mapPos.X) * pixelRatio,
                Y = (mapPoint.Y - absoluteZero.Y - mapPos.Y) * pixelRatio,
            };
        }

        public SizeF World3DToMap2D(Vector3 worldPos)
        {
            var absoluteZero = new PointF(absX * Zoom, absY * Zoom);
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

            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;
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

            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;
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