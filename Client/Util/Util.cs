﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Quaternion = GTA.Math.Quaternion;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork.Util
{
    public enum HandleType
    {
        GameHandle,
        LocalHandle,
        NetHandle,
    }

    public struct LocalHandle
    {
        public LocalHandle(int handle)
        {
            _internalId = handle;
            HandleType = HandleType.GameHandle;
        }

        public LocalHandle(int handle, HandleType localId)
        {
            _internalId = handle;
            HandleType = localId;
        }

        private int _internalId;

        public int Raw => _internalId;

        public int Value
        {
            get
            {
                switch (HandleType)
                {
                    case HandleType.LocalHandle:
                        return Main.NetEntityHandler.NetToEntity(Main.NetEntityHandler.NetToStreamedItem(_internalId, true))?.Handle ?? 0;
                    case HandleType.NetHandle:
                        return Main.NetEntityHandler.NetToEntity(_internalId)?.Handle ?? 0;
                    case HandleType.GameHandle:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return _internalId;
            }
        }

        public T Properties<T>()
        {
            if (HandleType == HandleType.LocalHandle)
                return (T) Main.NetEntityHandler.NetToStreamedItem(_internalId, true);
            else if (HandleType == HandleType.NetHandle)
                return (T) Main.NetEntityHandler.NetToStreamedItem(_internalId);
            else
                return (T) Main.NetEntityHandler.EntityToStreamedItem(_internalId);
        }

        public HandleType HandleType;

        public override bool Equals(object obj)
        {
            return (obj as LocalHandle?)?.Value == Value;
        }

        public static bool operator ==(LocalHandle left, LocalHandle right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(LocalHandle left, LocalHandle right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool IsNull => Value == 0;
    }

    public static class Util
    {


        //public static Vector3 

        public static T Clamp<T>(T min, T value, T max) where T : IComparable
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;

            return value;
        }

        public static Point Floor(this PointF point)
        {
            return new Point((int)point.X, (int) point.Y);
        }

        public static bool IsPed(this Entity ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent);
        }

        public static string ToF2(this Vector3 vector)
        {
            return $"X:{vector.X:F2} Y:{vector.Y:F2} Z:{vector.Y:F2}";
        }

        public static bool IsExitingLeavingCar(this Ped player)
        {
            return player.IsSubtaskActive(161) || player.IsSubtaskActive(162) || player.IsSubtaskActive(163) ||
                   player.IsSubtaskActive(164) || player.IsSubtaskActive(167) || player.IsSubtaskActive(168);
        }

        public static void SetNonStandardVehicleMod(Vehicle veh, int slot, int value)
        {
            var eSlot = (NonStandardVehicleMod) slot;

            switch (eSlot)
            {
                case NonStandardVehicleMod.BulletproofTyres:
                    Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, veh, value != 0);
                    break;
                case NonStandardVehicleMod.NumberPlateStyle:
                    Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, veh, value);
                    break;
                case NonStandardVehicleMod.PearlescentColor:
                    veh.Mods.PearlescentColor = (VehicleColor)value;
                    break;
                case NonStandardVehicleMod.WheelColor:
                    veh.Mods.RimColor = (VehicleColor) value;
                    break;
                case NonStandardVehicleMod.WheelType:
                    veh.Mods.WheelType = (VehicleWheelType) value;
                    break;
                case NonStandardVehicleMod.ModColor1:
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_1, veh, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.ModColor2:
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_2, veh, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.TyreSmokeColor:
                    Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, veh, (value & 0xFF0000) >> 16, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.WindowTint:
                    Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, veh, value);
                    break;
                case NonStandardVehicleMod.EnginePowerMultiplier:
                    Function.Call(Hash._SET_VEHICLE_ENGINE_POWER_MULTIPLIER, veh, BitConverter.ToSingle(BitConverter.GetBytes(value), 0));
                    break;
                case NonStandardVehicleMod.EngineTorqueMultiplier:
                    Function.Call(Hash._SET_VEHICLE_ENGINE_TORQUE_MULTIPLIER, veh, BitConverter.ToSingle(BitConverter.GetBytes(value), 0));
                    break;
                case NonStandardVehicleMod.NeonLightPos:
                    for (int i = 0; i < 8; i++)
                    {
                        Function.Call(Hash._SET_VEHICLE_NEON_LIGHT_ENABLED, veh, i, (value & 1 << i) != 0);
                    }
                    break;
                case NonStandardVehicleMod.NeonLightColor:
                    Function.Call(Hash._SET_VEHICLE_NEON_LIGHTS_COLOUR, veh, (value & 0xFF0000) >> 16, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.DashboardColor:
                    Function.Call((Hash)6956317558672667244uL, veh, value);
                    break;
                case NonStandardVehicleMod.TrimColor:
                    Function.Call((Hash)17585947422526242585uL, veh, value);
                    break;
            }
        }

        public static bool ModelRequest;
        public static void LoadModel(Model model)
        {
            if (!model.IsValid) return;
            LogManager.DebugLog("REQUESTING MODEL " + model.Hash);
            ModelRequest = true;
            DateTime start = DateTime.Now;
            while (!model.IsLoaded)
            {
                model.Request();
                //Function.Call(Hash.REQUEST_COLLISION_FOR_MODEL, model.Hash);
                Script.Yield();
                if (DateTime.Now.Subtract(start).TotalMilliseconds > 1000) break;
            }
            ModelRequest = false;
            LogManager.DebugLog("MODEL REQUESTED: " + model.IsLoaded);
        }

        public static void LoadWeapon(int model)
        {
            if (model == (int) GTANetworkShared.WeaponHash.Unarmed ||
                model == 0) return;

            var start = Util.TickCount;
            while (!Function.Call<bool>(Hash.HAS_WEAPON_ASSET_LOADED, model))
            {
                Script.Yield();
                Function.Call(Hash.REQUEST_WEAPON_ASSET, model, 31, 0);

                if (Util.TickCount - start > 500) break;
            }
        }

        public static long TickCount => DateTime.Now.Ticks / 10000;

        public static int BuildTyreFlag(Vehicle veh)
        {
            byte tyreFlag = 0;

            for (int i = 0; i < 8; i++)
            {
                if (veh.IsTireBurst(i))
                    tyreFlag |= (byte)(1 << i);
            }

            return tyreFlag;
        }

        public static bool[] BuildTyreArray(Vehicle veh)
        {
            var flag = BuildTyreFlag(veh);
            bool[] arr = new bool[8];

            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = (flag & (1 << i)) != 0;
            }

            return arr;
        }

        public static float Unlerp(double left, double center, double right)
        {
            return (float)((center - left) / (right - left));
        }

        // Dirty & dangerous
        public static dynamic Lerp(dynamic from, dynamic to, float fAlpha)
        {
            return ((to - from) * fAlpha + from);
        }

        public static int GetStationId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

	    public static IEnumerable<Blip> GetAllBlips()
	    {
		    for(int i = 0; i < 600; i++)
		    {
			    int Handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, i);
			    while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, Handle))
			    {
				    yield return new Blip(Handle);
					Handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, i);
			    }
		    }
		}

        public static void SetPlayerSkin(PedHash skin)
        {
            var PlayerChar = Game.Player.Character;
            var health = PlayerChar.Health;
            var model = new Model(skin);

            ModelRequest = true;
            model.Request(1000);

            if (model.IsInCdImage && model.IsValid)
            {
                while (!model.IsLoaded) Script.Yield();

                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, model.Hash);
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, PlayerChar);
            }
            PlayerChar = Game.Player.Character;
            ModelRequest = false;
            //model.MarkAsNoLongerNeeded();

            PlayerChar.MaxHealth = 200;
            PlayerChar.Health = health;
        }

        public static float Denormalize(this float h)
        {
            return h < 0f ? h + 360f : h;
        }

        public static Vector3 Denormalize(this Vector3 v)
        {
            return new Vector3(v.X.Denormalize(), v.Y.Denormalize(), v.Z.Denormalize());
        }

        public static float ToRadians(this float val)
        {
            return (float)(Math.PI/180)*val;
        }

        public static Vector3 ToRadians(this Vector3 i)
        {
            return new Vector3()
            {
                X = ToRadians(i.X),
                Y = ToRadians(i.Y),
                Z = ToRadians(i.Z),
            };
        }

        public static void SafeNotify(string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                try
                {
                    GTA.UI.Screen.ShowNotification(msg);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public static string GetStationName(int id)
        {
            return Function.Call<string>(Hash.GET_RADIO_STATION_NAME, id);
        }

        public static int GetMod(this Vehicle veh, int id)
        {
            return veh.Mods[(VehicleModType) id].Index;
        }

        public static int SetMod(this Vehicle veh, int id, int var, bool useless)
        {
            return veh.Mods[(VehicleModType)id].Index = var;
        }

        public static bool IsOnScreen(this Entity entity)
        {
            return entity != null && entity.IsOnScreen;
        }

        public static bool IsInRangeOfEx(this Entity ent, Vector3 pos, float range)
        {
            return ent.Position.DistanceToSquared(pos) < (range*range);
        }

        public static VehicleDamageModel GetVehicleDamageModel(this Vehicle veh)
        {
            if (veh == null || !veh.Exists()) return new VehicleDamageModel();
            var mod = new VehicleDamageModel()
            {
                BrokenDoors = 0,
                BrokenWindows = 0
            };
            for (int i = 0; i < 8; i++)
            {
                if (veh.Doors[(VehicleDoorIndex)i].IsBroken) mod.BrokenDoors |= (byte)(1 << i);
                if (!veh.Windows[(VehicleWindowIndex) i].IsIntact) mod.BrokenWindows |= (byte) (1 << i);
            }
            /*
            var memAdd = veh.MemoryAddress;
            if (memAdd != IntPtr.Zero)
            {
                mod.BrokenLights = MemoryAccess.ReadInt(memAdd + 0x79C); // Old: 0x77C
            }
            */
            return mod;
        }

        public static void SetVehicleDamageModel(this Vehicle veh, VehicleDamageModel model, bool leavedoors = true)
        {
            if (veh == null || model == null || !veh.Exists()) return;

            bool isinvincible = veh.IsInvincible;

            veh.IsInvincible = false;

            // set doors
            for (int i = 0; i < 8; i++)
            {
                if ((model.BrokenDoors & (byte) (1 << i)) != 0)
                {
                    veh.Doors[(VehicleDoorIndex)i].Break(leavedoors);
                }

                if ((model.BrokenWindows & (byte)(1 << i)) != 0)
                {
                    veh.Windows[(VehicleWindowIndex)i].Smash();
                }
                else if (!veh.Windows[(VehicleWindowIndex)i].IsIntact)
                {
                    veh.Windows[(VehicleWindowIndex)i].Repair();
                }
            }
            /*
            var addr = veh.MemoryAddress;
            if (addr != IntPtr.Zero)
            {
                MemoryAccess.WriteInt(addr + 0x79C, model.BrokenLights); // 0x784 ?
            }
            */

            veh.IsInvincible = isinvincible;
        }

        public static void WriteMemory(IntPtr pointer, byte value, int length)
        {
            for (int i = 0; i < length; i++)
            {
                GTA.Native.MemoryAccess.WriteByte(pointer + i, value);
            }
        }

        public static void WriteMemory(IntPtr pointer, byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                MemoryAccess.WriteByte(pointer + i, value[i]);
            }
        }

        public static byte[] ReadMemory(IntPtr pointer, int length)
        {
            byte[] memory = new byte[length];
            for (int i = 0; i < length; i++)
            {
                memory[i] = MemoryAccess.ReadByte(pointer + i);
            }
            return memory;
        }



        public static unsafe IntPtr FindPattern(string bytes, string mask)
        {
            var patternPtr = Marshal.StringToHGlobalAnsi(bytes);
            var maskPtr = Marshal.StringToHGlobalAnsi(bytes);

            IntPtr output;

            try
            {
                output =
                    new IntPtr(
                        unchecked(
                            (long)
                                MemoryAccess.FindPattern(
                                    (sbyte*)patternPtr.ToPointer(),
                                    (sbyte*)patternPtr.ToPointer()
                                    )));
            }
            finally
            {
                Marshal.FreeHGlobal(patternPtr);
                Marshal.FreeHGlobal(maskPtr);
            }

            return output;
        }

        private static int _idX;
        private static int _lastframe;
        
        public static void DxDrawTexture(int idx, string filename, float xPos, float yPos, float txdWidth, float txdHeight, float rot, int r, int g, int b, int a, bool centered = false)
        {
            const float height = 1080f;
            float ratio = (float)Main.screen.Width / Main.screen.Height;
            float width = height * ratio;

            float reduceX = xPos / width;
            float reduceY = yPos / height;

            float scaleX = txdWidth/width;
            float scaleY = txdHeight/height;

            if (!centered)
            {
                reduceX += scaleX*0.5f;
                reduceY += scaleY*0.5f;
            }

            var cF = Function.Call<int>(Hash.GET_FRAME_COUNT);

            if (cF != _lastframe)
            {
                _idX = 0;
                _lastframe = cF;
            }
            
            GTA.UI.CustomSprite.RawDraw(filename, 70,
                new PointF(reduceX, reduceY),
                new SizeF(scaleX, scaleY / ratio),
                new PointF(0.5f, 0.5f),
                rot, Color.FromArgb(a, r, g, b));
        }

        public static void DrawSprite(string dict, string txtName, double x, double y, double width, double height, double heading,
            int r, int g, int b, int alpha)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            const float hh = 1080f;
            float ratio = (float)Main.screen.Width / Main.screen.Height;
            var ww = hh * ratio;


            float w = (float)(width / ww);
            float h = (float)(height / hh);
            float xx = (float)(x / ww) + w * 0.5f;
            float yy = (float)(y / hh) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, txtName, xx, yy, w, h, heading, r, g, b, alpha);
        }

        public static void DrawRectangle(double xPos, double yPos, double wSize, double hSize, int r, int g, int b, int alpha, CallCollection thisCol = null)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            const float height = 1080f;
            float ratio = (float)Main.screen.Width / Main.screen.Height;
            var width = height * ratio;

            float w = (float)wSize / width;
            float h = (float)hSize / height;
            float x = (((float)xPos) / width) + w * 0.5f;
            float y = (((float)yPos) / height) + h * 0.5f;

            if (thisCol == null)
            {
                Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
            }
            else
            {
                thisCol.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
            }
        }

        public static void DrawText(string caption, double xPos, double yPos, double scale, int r, int g, int b, int alpha, int font,
            int justify, bool shadow, bool outline, int wordWrap, CallCollection thisCol = null)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            const float height = 1080f;
            float ratio = (float)Main.screen.Width / Main.screen.Height;
            var width = height * ratio;

            float x = (float)(xPos) / width;
            float y = (float)(yPos) / height;
            bool localCollection = false;
            if (thisCol == null)
            {
                localCollection = true;
                thisCol = new CallCollection();
            }

            thisCol.Call(Hash.SET_TEXT_FONT, font);
            thisCol.Call(Hash.SET_TEXT_SCALE, 1.0f, scale);
            thisCol.Call(Hash.SET_TEXT_COLOUR, r, g, b, alpha);
            if (shadow)
                thisCol.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (outline)
                thisCol.Call(Hash.SET_TEXT_OUTLINE);
            switch (justify)
            {
                case 1:
                    thisCol.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case 2:
                    thisCol.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                    thisCol.Call(Hash.SET_TEXT_WRAP, 0, x);
                    break;
            }

            if (wordWrap != 0)
            {
                float xsize = (float)(xPos + wordWrap) / width;
                thisCol.Call(Hash.SET_TEXT_WRAP, x, xsize);
            }

            thisCol.Call(Hash._SET_TEXT_ENTRY, "CELL_EMAIL_BCON");

            const int maxStringLength = 99;

            for (int i = 0; i < caption.Length; i += maxStringLength)
            {
                thisCol.Call((Hash)0x6C188BE134E074AA,
                    caption.Substring(i,
                            System.Math.Min(maxStringLength, caption.Length - i)));
                //Function.Call((Hash)0x6C188BE134E074AA, caption.Substring(i, System.Math.Min(maxStringLength, caption.Length - i)));
            }

            thisCol.Call(Hash._DRAW_TEXT, x, y);
            if (localCollection)
            {
                thisCol.Execute();
            }
        }

        public static float GetOffsetDegrees(float a, float b)
        {
            float c = (b > a) ? b - a : 0 - (a - b);
            if (c > 180f)
                c = 0 - (360 - c);
            else if (c <= -180)
                c = 360 + c;
            return c;
        }

        public static Vector3 ToEuler(this Quaternion q)
        {
            var pitchYawRoll = new Vector3();

            double sqw = q.W * q.W;
            double sqx = q.X * q.X;
            double sqy = q.Y * q.Y;
            double sqz = q.Z * q.Z;

            pitchYawRoll.Y = (float)Math.Atan2(2f * q.X * q.W + 2f * q.Y * q.Z, 1 - 2f * (sqz + sqw));     // Yaw 
            pitchYawRoll.X = (float)Math.Asin(2f * (q.X * q.Z - q.W * q.Y));                             // Pitch 
            pitchYawRoll.Z = (float)Math.Atan2(2f * q.X * q.Y + 2f * q.Z * q.W, 1 - 2f * (sqy + sqz));

            return pitchYawRoll;
        }

        public static Ped GetResponsiblePed(Vehicle veh, Ped ped)
        {
            Ped PedOnSeat;
            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.IsSeatFree((VehicleSeat)i)) continue;
                if (!Ped.Exists(PedOnSeat = veh.GetPedOnSeat((VehicleSeat)i))) continue;
                if (PedOnSeat.Handle == 0) continue;
                if (PedOnSeat.Handle == ped.Handle) return PedOnSeat;
            }
            return new Ped(0);
        }

        public static Quaternion ToQuaternion(this Vector3 vect)
        {
            vect = new Vector3()
            {
                X = vect.X.Denormalize() * -1,
                Y = vect.Y.Denormalize() - 180f,
                Z = vect.Z.Denormalize() - 180f,
            };

            vect = vect.ToRadians();

            float rollOver2 = vect.Z * 0.5f;
            float sinRollOver2 = (float)Math.Sin((double)rollOver2);
            float cosRollOver2 = (float)Math.Cos((double)rollOver2);
            float pitchOver2 = vect.Y * 0.5f;
            float sinPitchOver2 = (float)Math.Sin((double)pitchOver2);
            float cosPitchOver2 = (float)Math.Cos((double)pitchOver2);
            float yawOver2 = vect.X * 0.5f; // pitch
            float sinYawOver2 = (float)Math.Sin((double)yawOver2);
            float cosYawOver2 = (float)Math.Cos((double)yawOver2);
            Quaternion result = new Quaternion()
            {
                X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2,
                Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2,
                Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2,
                W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2
            };
            return result;
        }

        public static int FromArgb(byte a, byte r, byte g, byte b)
        {
            return b | g << 8 | r << 16 | a << 24;
        }

        public static void ToArgb(int argb, out byte a, out byte r, out byte g, out byte b)
        {
            b = (byte)(argb & 0xFF);
            g = (byte)((argb & 0xFF00) >> 8);
            r = (byte)((argb & 0xFF0000) >> 16);
            a = (byte)((argb & 0xFF000000) >> 24);
        }

        public static int GetTrackId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
        }

        public static bool IsVehicleEmpty(Vehicle veh)
        {
            if (veh == null) return true;
            if (!veh.IsSeatFree(VehicleSeat.Driver)) return false;
            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (!veh.IsSeatFree((VehicleSeat)i))
                    return false;
            }
            return true;
        }

        public static string LoadDict(string dict)
        {
            LogManager.DebugLog("REQUESTING DICTIONARY " + dict);
            Function.Call(Hash.REQUEST_ANIM_DICT, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 1000);

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static string LoadPtfxAsset(string dict)
        {
            LogManager.DebugLog("REQUESTING PTFX " + dict);
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 5000);

            while (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, dict))
            {
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            //if (wasLoading) Script.Wait(100);

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static string LoadAnimDictStreamer(string dict)
        {
            LogManager.DebugLog("REQUESTING DICTIONARY " + dict);
            Function.Call(Hash.REQUEST_ANIM_DICT, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 1000);

            ModelRequest = true;

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            ModelRequest = false;

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static Vector3 LinearVectorLerp(Vector3 start, Vector3 end, long currentTime, long duration)
        {
            return new Vector3()
            {
                X = LinearFloatLerp(start.X, end.X, currentTime, duration),
                Y = LinearFloatLerp(start.Y, end.Y, currentTime, duration),
                Z = LinearFloatLerp(start.Z, end.Z, currentTime, duration),
            };
        }

        public static float LinearFloatLerp(float start, float end, long currentTime, long duration)
        {
            float change = end - start;
            return change * currentTime / duration + start;
        }

        public static Dictionary<int, int> GetVehicleMods(Vehicle veh)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                dict.Add(i, veh.GetMod(i));
            }
            return dict;
        }

        public static Dictionary<int, int> GetPlayerProps(Ped ped)
        {
            var props = new Dictionary<int, int>();
            for (int i = 0; i < 15; i++)
            {
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, i);
                if (mod == -1) continue;
                props.Add(i, mod);
            }
            return props;
        }

        public static unsafe void SetVehicleSteeringAngle(this Vehicle veh, float angle)
        {
            var address = veh.MemoryAddress + 0x8AC;
            var bytes = BitConverter.GetBytes(angle);
            Marshal.Copy(bytes, 0, address, bytes.Length);
        }

        public static int GetPedSeat(Ped ped)
        {
            var vehicle = ped != null ? ped.CurrentVehicle : null;
            if (vehicle == null) return -3;
            if (vehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < vehicle.PassengerCapacity; i++)
            {
                if (vehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static int GetPedSeatAtVehicle(Ped ped, Vehicle vehicle)
        {
            if (ped == null || vehicle == null) return -3;
            if (vehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < vehicle.PassengerCapacity; i++)
            {
                if (vehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static Vector3 GetOffsetInWorldCoords(this Entity ent, Vector3 offset)
        {
            return ent.GetOffsetPosition(offset);
        }

        public static Vector3 GetOffsetFromWorldCoords(this Entity ent, Vector3 pos)
        {
            return ent.GetPositionOffset(pos);
        }

        public static bool IsTireBurst(this Vehicle veh, int wheel)
        {
            return Function.Call<bool>(Hash.IS_VEHICLE_TYRE_BURST, veh, wheel, false);
        }

        public static int GetFreePassengerSeat(Vehicle veh)
        {
            if (veh == null) return -3;
            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.IsSeatFree((VehicleSeat)i))
                    return i;
            }
            return -3;
        }

        
        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);

                if (string.IsNullOrWhiteSpace(settings.DisplayName))
                {
                    settings.DisplayName = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Player" : Game.Player.Name;
                }

                if (settings.DisplayName.Length > 32)
                {
                    settings.DisplayName = settings.DisplayName.Substring(0, 32);
                }

                settings.DisplayName = settings.DisplayName.Replace(' ', '_');

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path))
                {
                    ser.Serialize(stream, settings = new PlayerSettings());
                }
            }

            return settings;
        }

        public static void SaveSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, Main.PlayerSettings);
        }

        public static Vector3 GetLastWeaponImpact(Ped ped)
        {
            var coord = new OutputArgument();
            if (!Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, ped.Handle, coord))
            {
                return new Vector3();
            }
            return coord.GetResult<Vector3>();
        }

        public static Quaternion LerpQuaternion(Quaternion start, Quaternion end, float speed)
        {
            return new Quaternion()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
                W = start.W + (end.W - start.W) * speed,
            };
        }

        public static Vector3 LerpVector(Vector3 start, Vector3 end, float speed)
        {
            return new Vector3()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
            };
        }

        public static Vector3 QuaternionToEuler(Quaternion quat)
        {
            //heading = atan2(2*qy*qw-2*qx*qz , 1 - 2*qy2 - 2*qz2) (yaw)
            //attitude = asin(2 * qx * qy + 2 * qz * qw) (pitch)
            //bank = atan2(2 * qx * qw - 2 * qy * qz, 1 - 2 * qx2 - 2 * qz2) (roll)

            return new Vector3()
            {
                X = (float)Math.Asin(2 * quat.X * quat.Y + 2 *quat.Z * quat.W),
                Y = (float)Math.Atan2(2 * quat.X * quat.W - 2 * quat.Y * quat.Z, 1 -  2 * quat.X*quat.X - 2 * quat.Z * quat.Z),
                Z = (float)Math.Atan2(2*quat.Y*quat.W - 2*quat.X*quat.Z, 1 - 2*quat.Y*quat.Y - 2*quat.Z * quat.Z),
            };

            /*except when qx*qy + qz*qw = 0.5 (north pole)
            which gives:
            heading = 2 * atan2(x,w)
            bank = 0

            and when qx*qy + qz*qw = -0.5 (south pole)
            which gives:
            heading = -2 * atan2(x,w)
            bank = 0 */
        }
    }

    public static class VectorExtensions
    {
        public static GTA.Math.Quaternion ToQuaternion(this GTANetworkShared.Quaternion q)
        {
            return new GTA.Math.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        public static GTA.Math.Vector3 ToVector(this GTANetworkShared.Vector3 v)
        {
            if ((object)v == null) return new Vector3();
            return new GTA.Math.Vector3(v.X, v.Y, v.Z);
        }

        public static GTANetworkShared.Vector3 ToLVector(this GTA.Math.Vector3 vec)
        {
            return new GTANetworkShared.Vector3()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
            };
        }

        public static GTANetworkShared.Quaternion ToLQuaternion(this GTA.Math.Quaternion vec)
        {
            return new GTANetworkShared.Quaternion()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
                W = vec.W,
            };
        }

        public static float LengthSquared(this GTANetworkShared.Vector3 left)
        {
            return left.X * left.X + left.Y * left.Y + left.Z + left.Z;
        }

        public static float Length(this GTANetworkShared.Vector3 left)
        {
            return (float)Math.Sqrt(left.LengthSquared());
        }

        public static GTANetworkShared.Vector3 Sub(this GTANetworkShared.Vector3 left, GTANetworkShared.Vector3 right)
        {
            if ((object) left == null && (object) right == null) return new GTANetworkShared.Vector3();
            if ((object) left == null) return right;
            if ((object) right == null) return left;
            return new GTANetworkShared.Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static GTANetworkShared.Vector3 Add(this GTANetworkShared.Vector3 left, GTANetworkShared.Vector3 right)
        {
            if ((object)left == null && (object)right == null) return new GTANetworkShared.Vector3();
            if ((object)left == null) return right;
            if ((object)right == null) return left;
            return new GTANetworkShared.Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }
    }
}