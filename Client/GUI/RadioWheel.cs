using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using GTA;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace GTANetwork.GUI
{
    public class RadioWheel : Script
    {
        private string RADIO_SPRITE_PATH = Main.GTANInstallDir + "\\images\\radio\\";

        internal string[] _radioNames
        {
            get
            {
                uint MapArea;
                MapArea = Function.Call<uint>(Hash.GET_HASH_OF_MAP_AREA_AT_COORDS, Game.Player.Character.Position.X,
                    Game.Player.Character.Position.Y, Game.Player.Character.Position.Z);

                if (MapArea == 4005646697)
                {
                    if (_hasUserMusic)
                    {
                        return new[]
                        {
                            "RADIO_01_CLASS_ROCK",
                            "RADIO_02_POP",
                            "RADIO_03_HIPHOP_NEW",
                            "RADIO_04_PUNK",
                            "RADIO_05_TALK_01",
                            "RADIO_06_COUNTRY",
                            "RADIO_07_DANCE_01",
                            "RADIO_08_MEXICAN",
                            "RADIO_09_HIPHOP_OLD",
                            "RADIO_OFF",
                            "RADIO_12_REGGAE",
                            "RADIO_13_JAZZ",
                            "RADIO_14_DANCE_02",
                            "RADIO_15_MOTOWN",
                            "RADIO_20_THELAB",
                            "RADIO_16_SILVERLAKE",
                            "RADIO_17_FUNK",
                            "RADIO_18_90S_ROCK",
                            "RADIO_19_USER",
                        };
                    }
                    else
                    {
                        return new[]
                        {
                            "RADIO_01_CLASS_ROCK",
                            "RADIO_02_POP",
                            "RADIO_03_HIPHOP_NEW",
                            "RADIO_04_PUNK",
                            "RADIO_05_TALK_01",
                            "RADIO_06_COUNTRY",
                            "RADIO_07_DANCE_01",
                            "RADIO_08_MEXICAN",
                            "RADIO_09_HIPHOP_OLD",
                            "RADIO_OFF",
                            "RADIO_12_REGGAE",
                            "RADIO_13_JAZZ",
                            "RADIO_14_DANCE_02",
                            "RADIO_15_MOTOWN",
                            "RADIO_20_THELAB",
                            "RADIO_16_SILVERLAKE",
                            "RADIO_17_FUNK",
                            "RADIO_18_90S_ROCK",
                        };
                    }
                }
                if (MapArea == Game.GenerateHash("countryside"))
                {
                    if (_hasUserMusic)
                    {
                        return new[]
                        {
                            "RADIO_01_CLASS_ROCK",
                            "RADIO_02_POP",
                            "RADIO_03_HIPHOP_NEW",
                            "RADIO_04_PUNK",
                            "RADIO_06_COUNTRY",
                            "RADIO_07_DANCE_01",
                            "RADIO_08_MEXICAN",
                            "RADIO_09_HIPHOP_OLD",
                            "RADIO_11_TALK_02",
                            "RADIO_OFF",
                            "RADIO_12_REGGAE",
                            "RADIO_13_JAZZ",
                            "RADIO_14_DANCE_02",
                            "RADIO_15_MOTOWN",
                            "RADIO_20_THELAB",
                            "RADIO_16_SILVERLAKE",
                            "RADIO_17_FUNK",
                            "RADIO_18_90S_ROCK",
                            "RADIO_19_USER",
                        };
                    }
                    else
                    {
                        return new[]
                        {
                            "RADIO_01_CLASS_ROCK",
                            "RADIO_02_POP",
                            "RADIO_03_HIPHOP_NEW",
                            "RADIO_04_PUNK",
                            "RADIO_06_COUNTRY",
                            "RADIO_07_DANCE_01",
                            "RADIO_08_MEXICAN",
                            "RADIO_09_HIPHOP_OLD",
                            "RADIO_11_TALK_02",
                            "RADIO_OFF",
                            "RADIO_12_REGGAE",
                            "RADIO_13_JAZZ",
                            "RADIO_14_DANCE_02",
                            "RADIO_15_MOTOWN",
                            "RADIO_20_THELAB",
                            "RADIO_16_SILVERLAKE",
                            "RADIO_17_FUNK",
                            "RADIO_18_90S_ROCK",
                        };
                    }
                }

                return new[]
                {
                    "RADIO_01_CLASS_ROCK",
                    "RADIO_02_POP",
                    "RADIO_03_HIPHOP_NEW",
                    "RADIO_04_PUNK",
                    "RADIO_05_TALK_01",
                    "RADIO_06_COUNTRY",
                    "RADIO_07_DANCE_01",
                    "RADIO_08_MEXICAN",
                    "RADIO_09_HIPHOP_OLD",
                    "RADIO_11_TALK_02",
                    "RADIO_OFF",
                    "RADIO_12_REGGAE",
                    "RADIO_13_JAZZ",
                    "RADIO_14_DANCE_02",
                    "RADIO_15_MOTOWN",
                    "RADIO_20_THELAB",
                    "RADIO_16_SILVERLAKE",
                    "RADIO_17_FUNK",
                    "RADIO_18_90S_ROCK",
                    "RADIO_19_USER",
                };
            }
        }

        public RadioWheel()
        {
            Tick += OnTick;

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "\\Rockstar Games\\GTA V\\User Music";

            _hasUserMusic = Directory.Exists(path) && Directory.GetFiles(path).Length > 0;

            Function.Call(Hash.REQUEST_ADDITIONAL_TEXT, "TRACKID");
        }

        private bool _hasUserMusic;
        private bool _disable;
        private DateTime _lastPress;
        private int _lastRadio = -1;

        private long? _ticksSinceRadioChange;
        
        public void OnTick(object sender, EventArgs e)
        {
            if (!Game.Player.Character.IsInVehicle()) return;

            if (_disable)
                Game.DisableControl(0, Control.VehicleRadioWheel);

            var radios = _radioNames;

            int currentRadio;

            int radioOff = Array.IndexOf(radios, "RADIO_OFF");
            string radioName = Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME);

            if (string.IsNullOrEmpty(radioName)) // Radio off
                currentRadio = radioOff;
            else
                currentRadio = Array.IndexOf(radios, radioName);

            if (Game.IsControlJustPressed(0, Control.VehicleRadioWheel))
            {
                _lastPress = DateTime.Now;
            }

            if (Game.IsControlPressed(0, Control.VehicleRadioWheel) && !_disable &&
                DateTime.Now.Subtract(_lastPress).TotalMilliseconds > 100)
            {
                _disable = true;
                Function.Call(Hash.SET_AUDIO_FLAG, "ActivateSwitchWheelAudio", 1);
                Function.Call(Hash._START_SCREEN_EFFECT, "SwitchHUDIn", 0, 0);

                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Short_Transition_In", "PLAYER_SWITCH_CUSTOM_SOUNDSET", 1);
            }

            if (Game.IsControlJustReleased(0, Control.VehicleRadioWheel))
            {
                if (_lastRadio != -1 && _lastRadio != currentRadio)
                {
                    Function.Call(Hash.SET_RADIO_TO_STATION_NAME,
                        _lastRadio == radioOff ? "OFF" : _radioNames[_lastRadio]);
                }

                _disable = false;
                _lastRadio = -1;
                Function.Call(Hash.SET_AUDIO_FLAG, "ActivateSwitchWheelAudio", 0);
                Function.Call(Hash._STOP_SCREEN_EFFECT, "SwitchHUDIn");
                Function.Call(Hash._START_SCREEN_EFFECT, "SwitchHUDOut", 0, 0);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Short_Transition_Out", "PLAYER_SWITCH_CUSTOM_SOUNDSET", 1);
            }

            if (DateTime.Now.Subtract(_lastPress).TotalMilliseconds <= 100 || !Game.IsDisabledControlPressed(0, Control.VehicleRadioWheel)) return;

            
            int stationLength = radios.Length;
            var res = UIMenu.GetScreenResolutionMantainRatio();

            Game.DisableControlThisFrame(0, Control.LookUpDown);
            Game.DisableControlThisFrame(0, Control.LookLeftRight);

            var mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int) Control.LookLeftRight);
            var mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int) Control.LookUpDown);
            
            if (_lastRadio == -1)
            {
                _lastRadio = currentRadio;
                return;
            }

            currentRadio = _lastRadio;
            double _loc7_ = 0.0174532925199433;

            var _loc6_ = (int)Math.Round(360d/stationLength);
            var _loc4_ = stationLength <= 18 ? 100 : 95;

            var center = new PointF(res.Width / 2, res.Height / 2 - 50);

            var _loc2_ = 0;
            int baseOffset = 0;

            while (_loc2_ < stationLength)
            {
                if (radios[_loc2_] == "RADIO_OFF")
                {
                    baseOffset = _loc2_*_loc6_ - 180;
                }

                _loc2_++;
            }


            var delta = new PointF(mouseX, mouseY);

            if (delta.X*delta.X + delta.Y*delta.Y > 0.005)
            {
                float angle = 0;

                angle = (float) (Math.Atan2(delta.Y, delta.X)/_loc7_);

                if (angle < 0)
                    angle += 360f;

                angle += 90;

                var radioIndex = ((int) Math.Round(angle/_loc6_))%stationLength;

                var lrd = currentRadio;
                currentRadio = radioIndex % stationLength;

                if (lrd != currentRadio)
                {
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Change_Station_Loud", "Radio_Soundset", 1);
                }
            }
            
            double _loc3_ = 0;
            double _loc5_ = 300;
            double _loc9_ = 0;
            double _loc8_ = 0;
            _loc2_ = 0;

            while (_loc2_ < stationLength)
            {
                _loc3_ = _loc6_ * _loc2_ - baseOffset;
                _loc9_ = Math.Sin(_loc3_ * _loc7_) * _loc5_;
                _loc8_ = (-Math.Cos(_loc3_ * _loc7_)) * _loc5_;

                string filepath = RADIO_SPRITE_PATH + "RADIO_OFF.png";

                if (File.Exists(RADIO_SPRITE_PATH + radios[_loc2_] + ".png"))
                    filepath = RADIO_SPRITE_PATH + radios[_loc2_] + ".png";

                int alpha = _loc2_ == currentRadio ? 255 : 100;

                Util.DxDrawTexture(_loc2_, filepath, ((float) _loc9_) + center.X - _loc4_/2, ((float) _loc8_) + center.Y - _loc4_ / 2, _loc4_, _loc4_, 0, 255, 255, 255, alpha);
                _loc2_ = _loc2_ + 1;
            }

            if (currentRadio != -1)
            {
                _loc3_ = _loc6_*currentRadio - baseOffset;
                _loc9_ = Math.Sin(_loc3_*_loc7_)*_loc5_;
                _loc8_ = (-Math.Cos(_loc3_*_loc7_))*_loc5_;
                Util.DxDrawTexture(currentRadio, RADIO_SPRITE_PATH + "selected.png",
                    ((float) _loc9_) + center.X - _loc4_/2 - 5, ((float) _loc8_) + center.Y - _loc4_/2 - 5, _loc4_ + 10, _loc4_ + 10, 0,
                    38, 154, 255, 255);

                var currentSong = Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);

                var l_33 = currentSong;
                var l_14/*"64"*/ = "";
                l_14/*64*/ += l_33;
                l_14/*64*/ += "S";
                var l_14_f10/*"32"*/ = "";
                l_14_f10/*32*/ += l_33;
                l_14_f10/*32*/ += "A";
                var l_14_f19/*"24"*/ = GTA.Native.Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME);
                if (!Function.Call<bool>(Hash.DOES_TEXT_LABEL_EXIST, l_14))
                {
                    l_14/*"64"*/ = "CELL_195";
                }
                if (!Function.Call<bool>(Hash.DOES_TEXT_LABEL_EXIST, l_14_f10))
                {
                    l_14_f10/*"32"*/ = "CELL_195";
                    l_14_f19/*"24"*/ = "CELL_195";
                }

                var text1 = Function.Call<string>(Hash._GET_LABEL_TEXT, l_14);
                var text2 = Function.Call<string>(Hash._GET_LABEL_TEXT, l_14_f10);
                //var text3 = Function.Call<string>(Hash._GET_LABEL_TEXT, l_14_f19);
                var text3 = Function.Call<string>(Hash._GET_LABEL_TEXT, radios[currentRadio]);

                if (currentSong == 1)
                {
                    text1 = "";
                    text2 = "";
                }

                new UIResText(text3 + "~n~" + text2 + "~n~" + text1, new Point((int)center.X, (int)center.Y - 100), 0.5f)
                {
                    Outline = true,
                    TextAlignment = UIResText.Alignment.Centered,
                    Font = Font.ChaletComprimeCologne,
                }.Draw();
            }

            int realRadio;

            if (string.IsNullOrEmpty(radioName)) // Radio off
                realRadio = radioOff;
            else
                realRadio = Array.IndexOf(radios, radioName);

            if (currentRadio != realRadio)
            {
                if (_ticksSinceRadioChange == null)
                {
                    _ticksSinceRadioChange = Util.TickCount;
                }
                else if (Util.TickCount - _ticksSinceRadioChange.Value > 1000)
                {
                    _ticksSinceRadioChange = null;
                    if (currentRadio != radioOff)
                    {
                        Function.Call(Hash.SET_RADIO_TO_STATION_NAME, _radioNames[currentRadio]);
                    }
                    else
                    {
                        Function.Call(Hash.SET_RADIO_TO_STATION_NAME, "OFF");
                    }
                }
            }

            _lastRadio = currentRadio;
        }
    }
}