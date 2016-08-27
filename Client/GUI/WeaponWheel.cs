using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using NativeUI;
using Control = GTA.Control;
using Font = GTA.Font;

namespace GTANetwork.GUI
{
    public class WeaponWheel : Script
    {
        public static readonly string WEAPON_SPRITE_PATH = Main.GTANInstallDir + "\\images\\weapons\\";
        
        public WeaponWheel()
        {
            for (int i = 0; i < 8; i++)
            {
                _slots[i] = new WeaponSlot(i);
            }

            Tick += OnTick;
        }
        
        private bool _disable;
        private DateTime _lastPress;
        /*
        private float posX;
        private float posY;
        private float scaleX = 100;
        private float scaleY = 100;
        private float rot = 0;*/

        private int _currentIndex = 2;

        private WeaponSlot[] _slots = new WeaponSlot[8];

        public void OnTick(object sender, EventArgs e)
        {
            if (Game.IsControlJustReleased(0, Control.SelectWeapon) && _disable)
            {
                _disable = false;
                
                Function.Call(Hash.SET_AUDIO_FLAG, "ActivateSwitchWheelAudio", 0);
                Function.Call(Hash._STOP_SCREEN_EFFECT, "SwitchHUDIn");
                Function.Call(Hash._START_SCREEN_EFFECT, "SwitchHUDOut", 0, 0);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Short_Transition_Out", "PLAYER_SWITCH_CUSTOM_SOUNDSET", 1);

                // switch current weapon;

                var currentSlot = _slots[_currentIndex];
                var currentGun = currentSlot.CurrentWeapon;

                if (Game.Player.Character.Weapons.Current.Hash != currentGun)
                {
                    Game.Player.Character.Weapons.Select(currentGun);
                }
            }

            if (Game.Player.Character.IsInVehicle() || Main.Chat.IsFocused)
                return;

            if (_disable)
            {
                Game.DisableControl(0, Control.SelectWeapon);
                Game.DisableControl(0, Control.Aim);
                Game.DisableControl(0, Control.AccurateAim);
                Game.DisableControl(0, Control.Attack);
                Game.DisableControl(0, Control.Attack2);
            }

            Game.DisableControl(0, Control.SelectNextWeapon);
            Game.DisableControl(0, Control.SelectPrevWeapon);

            if (Game.IsControlJustPressed(0, Control.SelectWeapon))
            {
                _lastPress = DateTime.Now;
            }

            if (Game.IsControlPressed(0, Control.SelectWeapon) && !_disable &&
                DateTime.Now.Subtract(_lastPress).TotalMilliseconds > 50)
            {
                _disable = true;
                Function.Call(Hash.SET_AUDIO_FLAG, "ActivateSwitchWheelAudio", 1);
                Function.Call(Hash._START_SCREEN_EFFECT, "SwitchHUDIn", 0, 0);

                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Short_Transition_In", "PLAYER_SWITCH_CUSTOM_SOUNDSET", 1);
            }

            if (DateTime.Now.Subtract(_lastPress).TotalMilliseconds <= 50 ||
                !Game.IsDisabledControlPressed(0, Control.SelectWeapon))
                return;


            Game.DisableControlThisFrame(0, Control.LookUpDown);
            Game.DisableControlThisFrame(0, Control.LookLeftRight);

            var mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.WeaponWheelLeftRight);
            var mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.WeaponWheelUpDown);

            var delta = new PointF(mouseX, mouseY);

            if (delta.X * delta.X + delta.Y * delta.Y > 0.005)
            {
                float angle = 0;
                var loc7 = 0.0174532925199433;
                angle = (float)(Math.Atan2(delta.Y * 100f, delta.X * 100f) / loc7);

                if (angle < 0)
                    angle += 360f;

                //angle += 90;

                var loc8 = 360f/8f;
                var radioIndex = ((int)Math.Round(angle / loc8)) % 8;

                var lrd = _currentIndex;
                _currentIndex = radioIndex;

                if (lrd != _currentIndex)
                {
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
                }
            }

            var cGun = Game.Player.Character.Weapons.Current;
            var cSlot = WeaponSlot.GetWeaponSlot(cGun.Hash);

            if (Game.IsControlJustPressed(0, Control.WeaponWheelNext))
            {
                _slots[_currentIndex].GoLeftRight(true);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
            }
            else if (Game.IsControlJustPressed(0, Control.WeaponWheelPrev))
            {
                _slots[_currentIndex].GoLeftRight(false);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
            }

            var res = UIMenu.GetScreenResolutionMantainRatio();


            if (_slots[_currentIndex].WeaponCount > 0)
            {
                var wName = WeaponSlot.GetDisplayNameFromHash(_slots[_currentIndex].CurrentWeapon);

                new UIResText(wName,
                    new Point((int) (res.Width/2), (int) (res.Height/2) - 200),
                    0.5f, Color.White, Font.ChaletComprimeCologne, UIResText.Alignment.Centered)
                {
                    Outline = true,
                }.Draw();

                if (_slots[_currentIndex].WeaponCount > 1)
                {
                    new UIResText(
                        (_slots[_currentIndex].CurrentWeaponIndex + 1) + " / " + _slots[_currentIndex].WeaponCount,
                        new Point((int) (res.Width/2), (int) (res.Height/2) - 170),
                        0.5f, Color.White, Font.ChaletComprimeCologne, UIResText.Alignment.Centered)
                    {
                        Outline = true,
                    }.Draw();
                    
                    new Sprite("commonmenu", "arrowleft",
                        new Point((int) (res.Width/2) - 78, (int) (res.Height/2) - 175), new Size(48, 48), 0f, Main.UIColor).Draw();
                    new Sprite("commonmenu", "arrowright",
                        new Point((int) (res.Width/2) + 30, (int) (res.Height/2) - 175), new Size(48, 48), 0f, Main.UIColor).Draw();
                }
            }
            
            for (int i = 0; i < 8; i++)
            {
                _slots[i].Draw(_currentIndex == i, cSlot == i);
            }

            var chuteHash = Game.GenerateHash("GADGET_PARACHUTE");
            if (Game.Player.Character.Weapons.HasWeapon((WeaponHash) chuteHash))
            {
                UI.ShowSubtitle("chuute");

                Util.DxDrawTexture(69, WeaponWheel.WEAPON_SPRITE_PATH + "parachute_bg.png",
                    res.Width/2 + 250, res.Height/2 + 170,
                    100, 100, 0, 255, 255, 255, 255, true);
                Util.DxDrawTexture(70, WeaponWheel.WEAPON_SPRITE_PATH + "parachute.png",
                    res.Width / 2 + 250, res.Height / 2 + 170,
                    57, 57, 0, 255, 255, 255, 255, true);
            }
        }
    }

    public class WeaponSlot
    {
        private int _slot;
        private List<WeaponHash> _weapons = new List<WeaponHash>();
        private WeaponHash _currentWeapon;
        
        public WeaponSlot(int slot)
        {
            _slot = slot;

            foreach (var hash in GetAllWeapons().Where(h => GetWeaponSlot(h) == _slot))
            {
                if (Game.Player.Character.Weapons.HasWeapon(hash))
                {
                    if (_currentWeapon == default(WeaponHash)) _currentWeapon = hash;

                    _weapons.Add(hash);
                }
            }
        }

        public static string GetLabelFromHash(WeaponHash hash)
        {
            switch (hash)
            {
                case WeaponHash.Pistol:
                    return "WTT_PIST";
                case WeaponHash.CombatPistol:
                    return "WTT_PIST_CBT";
                case WeaponHash.APPistol:
                    return "WTT_PIST_AP";
                case WeaponHash.SMG:
                    return "WTT_SMG";
                case WeaponHash.MicroSMG:
                    return "WTT_SMG_MCR";
                case WeaponHash.AssaultRifle:
                    return "WTT_RIFLE_ASL";
                case WeaponHash.CarbineRifle:
                    return "WTT_RIFLE_CBN";
                case WeaponHash.AdvancedRifle:
                    return "WTT_RIFLE_ADV";
                case WeaponHash.MG:
                    return "WTT_MG";
                case WeaponHash.CombatMG:
                    return "WTT_MG_CBT";
                case WeaponHash.PumpShotgun:
                    return "WTT_SG_PMP";
                case WeaponHash.SawnOffShotgun:
                    return "WTT_SG_SOF";
                case WeaponHash.AssaultShotgun:
                    return "WTT_SG_ASL";
                case WeaponHash.HeavySniper:
                    return "WTT_SNIP_HVY";
                case WeaponHash.SniperRifle:
                    return "WTT_SNIP_RIF";
                case WeaponHash.GrenadeLauncher:
                    return "WTT_GL";
                case WeaponHash.RPG:
                    return "WTT_RPG";
                case WeaponHash.Minigun:
                    return "WTT_MINIGUN";
                case WeaponHash.AssaultSMG:
                    return "WTT_SMG_ASL";
                case WeaponHash.BullpupShotgun:
                    return "WTT_SG_BLP";
                case WeaponHash.Pistol50:
                    return "WTT_PIST_50";
                case WeaponHash.Bottle:
                    return "WTT_BOTTLE";
                case WeaponHash.Gusenberg:
                    return "WTT_GUSENBERG";
                case WeaponHash.SNSPistol:
                    return "WTT_SNSPISTOL";
                case WeaponHash.VintagePistol:
                    return "WTT_VPISTOL";
                case WeaponHash.Dagger:
                    return "WTT_DAGGER";
                case WeaponHash.FlareGun:
                    return "WTT_FLAREGUN";
                case WeaponHash.Musket:
                    return "WTT_MUSKET";
                case WeaponHash.Firework:
                    return "WTT_FWRKLNCHR";
                case WeaponHash.MarksmanRifle:
                    return "WTT_HMKRIFLE";
                case WeaponHash.HeavyShotgun:
                    return "WTT_HVYSHOT";
                case WeaponHash.ProximityMine:
                    return "WTT_PRXMINE";
                case WeaponHash.HomingLauncher:
                    return "WTT_HOMLNCH";
                case WeaponHash.CombatPDW:
                    return "WTT_COMBATPDW";
                case WeaponHash.KnuckleDuster:
                    return "WTT_KNUCKLE";
                case WeaponHash.MarksmanPistol:
                    return "WTT_MKPISTOL";
                case WeaponHash.Machete:
                    return "WTT_MACHETE";
                case WeaponHash.MachinePistol:
                    return "WTT_MCHPIST";
                case WeaponHash.Flashlight:
                    return "WTT_FLASHLIGHT";
                case WeaponHash.DoubleBarrelShotgun:
                    return "WTT_DBSHGN";
                case WeaponHash.CompactRifle:
                    return "WTT_CMPRIFLE";
                case WeaponHash.SwitchBlade:
                    return "WTT_SWBLADE";
                case WeaponHash.Revolver:
                    return "WTT_REVOLVER";
            }

            return "UNDEFINED";

            /*
            IntPtr data = Marshal.AllocCoTaskMem(39 * 8);
            string result = string.Empty;

            for (int i = 0, count = Function.Call<int>(Hash.GET_NUM_DLC_WEAPONS); i < count; i++)
            {
                if (Function.Call<bool>(Hash.GET_DLC_WEAPON_DATA, i, data))
                {
                    if (MemoryAccess.ReadInt(data + 8) == (int)hash)
                    {
                        result = MemoryAccess.ReadString(data + 23 * 8);
                        break;
                    }
                }
            }

            Marshal.FreeCoTaskMem(data);

            return result;*/
        }

        private static string GetManualTextFromHash(WeaponHash hash)
        {
            return Regex.Replace(hash.ToString(), "[a-z][A-Z]", m => $"{m.Value[0]} {char.ToLower(m.Value[1])}");
        }

        public static string GetDisplayNameFromHash(WeaponHash hash)
        {
            var label = GetLabelFromHash(hash);

            var txt = Game.GetGXTEntry(label);

            if (txt == "NULL")
            {
                return GetManualTextFromHash(hash);
            }

            return txt.Substring(4);
        }

        public static WeaponHash[] GetAllWeapons()
        {
            var arr = Enum.GetValues(typeof(WeaponHash)).Cast<WeaponHash>().ToArray();

            var indx = Array.IndexOf(arr, WeaponHash.Unarmed);

            var tmp = arr[0];
            arr[0] = arr[indx];
            arr[indx] = tmp;

            return arr;
        }

        public static int GetWeaponSlot(WeaponHash weapon)
        {
            uint group = Function.Call<uint>(Hash.GET_WEAPONTYPE_GROUP, (int) weapon);

            switch (group)
            {
                default:
                    return 0;
                case 2685387236u: // Unarmed
                case 3566412244u: // Melee
                    return 2;
                case 416676503u: // Pistol
                case 690389602u: // Stungun
                    return 6;
                case 3337201093u: // SMG
                case 1159398588u: // MG
                    return 7;
                case 970310034u: // Assault Rifle
                    return 0;
                case 4257178988u: // Fire extinguisher
                case 1548507267u: // Thrown
                case 1595662460u: // Petrol can
                    return 5;
                case 860033945u: // Shotgun
                    return 3;
                case 3082541095u: // Sniper
                    return 1;
                case 2725924767u: // Heavy
                    return 4;
            }
        }

        public void GoLeftRight(bool right)
        {
            UpdateWeapons();

            if (_weapons.Count == 0) return;

            var currentIndex = _weapons.IndexOf(_currentWeapon);


            int newIndex;
            if (right) newIndex = (currentIndex + 1)%_weapons.Count;
            else
            {
                if (currentIndex == 0) newIndex = _weapons.Count - 1;
                else newIndex = (currentIndex - 1) % _weapons.Count;
            }
            
            _currentWeapon = _weapons[newIndex];
        }

        public void UpdateWeapons()
        {
            _weapons.Clear();

            foreach (var hash in GetAllWeapons().Where(h => GetWeaponSlot(h) == _slot))
            {
                if (Game.Player.Character.Weapons.HasWeapon(hash))
                {
                    _weapons.Add(hash);
                }
            }

            if (_weapons.Count > 0 && _weapons.IndexOf(_currentWeapon) == -1)
            {
                _currentWeapon = _weapons[0];
            }
            else if (_weapons.Count == 0)
            {
                _currentWeapon = default(WeaponHash);
            }
        }

        public int WeaponCount => _weapons.Count;
        public int CurrentWeaponIndex => _weapons.IndexOf(CurrentWeapon);
        public WeaponHash CurrentWeapon => _currentWeapon;

        public void Draw(bool highlighted, bool selected)
        {
            UpdateWeapons();

            var res = UIMenu.GetScreenResolutionMantainRatio();

            var centerX = res.Width/2;
            var centerY = res.Height/2 - 95;

            var angleRad = _slot/8f*2*Math.PI;

            float range = 236f;

            var posX = Math.Cos(angleRad)*range;
            var posY = Math.Sin(angleRad)*range;
            
            var scaleX = 223;
            var scaleY = 119;
            
            posX -= scaleX/2;
            posY -= scaleY/2;

            posX += centerX;
            posY += centerY;

            var rangeArc = 287f;

            var posXArc = Math.Cos(angleRad) * rangeArc;
            var posYArc = Math.Sin(angleRad) * rangeArc;

            var scaleXArc = 225f;
            var scaleYArc = 31f;

            posXArc -= scaleXArc / 2;
            posYArc -= scaleYArc / 2;

            posXArc += centerX;
            posYArc += centerY;

            var angleNorm = (float) (angleRad/(2*Math.PI));

            angleNorm += 0.25f;
            
            string im = "black";
            int alpha = 150;
            
            if (selected)
            {
                im = "white";
                alpha = 255;
            }

            Util.DxDrawTexture(_slot, WeaponWheel.WEAPON_SPRITE_PATH + "bg_" + (highlighted ? "white" : "black") + ".png",
                (float) posX, (float) posY, (float)scaleX, (float)scaleY,
                angleNorm,
                Main.UIColor.R, Main.UIColor.G, Main.UIColor.B, 255);

            Util.DxDrawTexture(_slot, WeaponWheel.WEAPON_SPRITE_PATH + "arc_" + im + ".png",
                (float)posXArc, (float)posYArc, scaleXArc, scaleYArc,
                angleNorm,
                Main.UIColor.R, Main.UIColor.G, Main.UIColor.B, alpha);

            string filename = WeaponWheel.WEAPON_SPRITE_PATH + (unchecked((uint) _currentWeapon)) + ".png";
            if (File.Exists(filename))
            {
                int height, width;
                /*
                using (var bm = new Bitmap(filename))
                {
                    height = bm.Height;
                    width = bm.Width;
                }
                */

                width = 200;
                height = 100;

                var xGun = (float) Math.Cos(angleRad)*240f;
                var yGun = (float) Math.Sin(angleRad)*240f;

                xGun += centerX;
                yGun += centerY;

                Util.DxDrawTexture(_slot, filename,
                    xGun, yGun, width, height,
                    0,
                    255, 255, 255, 255, true);
            }
        }
    }
}