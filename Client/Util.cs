using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using GTANetworkShared;
using Rage;
using Rage.Native;
using RAGENativeUI;
using Quaternion = Rage.Quaternion;
using Vector3 = Rage.Vector3;

namespace GTANetwork
{
    public static class Util
    {
        public static int GetStationId()
        {
            if (!Game.LocalPlayer.Character.IsInAnyVehicle(false)) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

        public static string GetUserInput(string defaultText, int maxLen)
        {
            NativeFunction.CallByName<uint>("DISPLAY_ONSCREEN_KEYBOARD", true, "FMMC_KEY_TIP8", "", defaultText, "", "", "", maxLen + 1);
            int result = 0;
            while (result == 0)
            {
                NativeFunction.CallByName<uint>("DISABLE_ALL_CONTROL_ACTIONS", 0);
                result = NativeFunction.CallByHash<int>(0x0CF2B696BBF945AE);
                GameFiber.Yield();
            }
            if (result == 2)
                return null;
            return (string)NativeFunction.CallByName("GET_ONSCREEN_KEYBOARD_RESULT", typeof(string));
        }

        public static Vector3 GetGameplayCameraPos()
        {
            return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_COORD);
        }

        public static Vector3 GetGameplayCameraRot()
        {
            return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT, 2);
        }

        public static unsafe PointF WorldToScreen(Vector3 pos)
        {
            float pointX, pointY;

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, pos.X, pos.Y, pos.Z, &pointX, &pointY))
            {
                return new PointF();
            }

            return new PointF(pointX * ClassicChat.UIWIDTH, pointY * ClassicChat.UIHEIGHT);
        }

        public static void DrawMarker(int type, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, Color color)
        {
            Function.Call(Hash.DRAW_MARKER, type, pos.X, pos.Y, pos.Z, dir.X, dir.Y, dir.Z, rot.X, rot.Y, rot.Z, scale.X, scale.Y, scale.Z, (int)color.R, (int)color.G, (int)color.B, (int)color.A, false, false, 2, false, 0, 0, false);
        }

        public static void SetPlayerSkin(uint skin)
        {
            var model = new Model(skin);
            if (!model.IsValid) return;
            model.LoadAndWait();

            //Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, model.Hash);
            Game.LocalPlayer.Model = model;

            model.Dismiss();
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
                    Game.DisplayNotification(msg);
                }
                catch (Exception) { }
            }
        }

        public static string GetStationName(int id)
        {
            return Function.Call<string>(Hash.GET_RADIO_STATION_NAME, id);
        }
        
        public static void DxDrawTexture(int idx, string filename, float xPos, float yPos, float txdWidth, float txdHeight, float rot, int r, int g, int b, int a)
        {
            int screenw = Game.Resolution.Width;
            int screenh = Game.Resolution.Height;

            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            float width = height * ratio;

            float reduceX = ClassicChat.UIWIDTH / width;
            float reduceY = ClassicChat.UIWIDTH / height;


            Point extra = new Point(0, 0);
            if (screenw == 1914 && screenh == 1052)
                extra = new Point(15, 0);

            /*UI.DrawTexture(filename, idx, 1, 60,
                new PointF(xPos * reduceX + extra.X, yPos * reduceY + extra.Y),
                new PointF(0f, 0f),
                new SizeF(txdWidth * reduceX, txdHeight * reduceY),
                rot, Color.FromArgb(a, r, g, b), 1f);*/
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
            Quaternion result = new Quaternion();
            result.X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
        }

        public static int GetTrackId()
        {
            if (!Game.LocalPlayer.Character.IsInAnyVehicle(false)) return -1;
            return Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
        }

        public static bool IsVehicleEmpty(Vehicle veh)
        {
            if (veh == null) return true;
            return veh.IsEmpty;
        }

        public static string LoadDict(string dict)
        {
            var counter = 200;
            while (counter < 200 && !Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                GameFiber.Yield();
                counter++;
            }
            return dict;
        }

        private static string[] _radioNames = { "RADIO_01_CLASS_ROCK", "RADIO_02_POP", "RADIO_03_HIPHOP_NEW", "RADIO_04_PUNK", "RADIO_05_TALK_01", "RADIO_06_COUNTRY", "RADIO_07_DANCE_01", "RADIO_08_MEXICAN", "RADIO_09_HIPHOP_OLD", "RADIO_11_TALK_02", "RADIO_12_REGGAE", "RADIO_13_JAZZ", "RADIO_14_DANCE_02", "RADIO_15_MOTOWN", "RADIO_16_SILVERLAKE", "RADIO_17_FUNK", "RADIO_18_90S_ROCK", "RADIO_19_USER", "RADIO_20_THELAB", "RADIO_OFF" };
        public static int GetRadioStation()
        {
            string radioName = Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME);
            if (radioName == "")
            {
                return (int)RadioStation.OFF;
            }
            else
            {
                return Array.IndexOf(_radioNames, radioName);
            }
        }

        public static Vector3 LinearVectorLerp(Vector3 start, Vector3 end, int currentTime, int duration)
        {
            return new Vector3()
            {
                X = LinearFloatLerp(start.X, end.X, currentTime, duration),
                Y = LinearFloatLerp(start.Y, end.Y, currentTime, duration),
                Z = LinearFloatLerp(start.Z, end.Z, currentTime, duration),
            };
        }

        public static float LinearFloatLerp(float start, float end, int currentTime, int duration)
        {
            float change = end - start;
            return change * currentTime / duration + start;
        }

        public static Dictionary<int, int> GetVehicleMods(Vehicle veh)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                dict.Add(i, Function.Call<int>(Hash.GET_VEHICLE_MOD, veh.Handle.Value, i));
            }
            return dict;
        }

        public static Dictionary<int, int> GetPlayerProps(Ped ped)
        {
            var props = new Dictionary<int, int>();
            for (int i = 0; i < 15; i++)
            {
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle.Value, i);
                if (mod == -1) continue;
                props.Add(i, mod);
            }
            return props;
        }

        public static int GetPedSeat(Ped ped)
        {
            if (ped == null || !ped.IsInAnyVehicle(false)) return -3;
            return ped.SeatIndex;
        }

        public static int GetFreePassengerSeat(Vehicle veh)
        {
            if (veh == null) return -3;
            return veh.GetFreePassengerSeatIndex() ?? -3;
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
                    settings.DisplayName = string.IsNullOrWhiteSpace(Function.Call<string>(Hash.GET_PLAYER_NAME, Game.LocalPlayer.Id)) ? "Player" : Function.Call<string>(Hash.GET_PLAYER_NAME, Game.LocalPlayer.Id);
                }

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path))
                {
                    ser.Serialize(stream, settings = new PlayerSettings());
                    Util.SafeNotify("No settings! " + path);
                }
            }

            return settings;
        }

        public static void SaveSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, Main.PlayerSettings);
        }

        public static unsafe Vector3 GetLastWeaponImpact(Ped ped)
        {
            Vector3 coord;
            if (!Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, ped.Handle.Value, &coord))
            {
                return new Vector3();
            }
            return coord;
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

    public static class EntityExtensions
    {
        public static unsafe bool HighBeamsOn(this Vehicle veh)
        {
            int lightState1, lightState2;
            Function.Call(Hash.GET_VEHICLE_LIGHTS_STATE, veh.Handle.Value, &lightState1, &lightState2);
            return lightState2 == 1;
        }

        public static unsafe bool LightsOn(this Vehicle veh)
        {
            int lightState1, lightState2;
            Function.Call(Hash.GET_VEHICLE_LIGHTS_STATE, veh.Handle.Value, &lightState1, &lightState2);
            return lightState1 == 1;
        }

        public static unsafe void LightsOn(this Vehicle veh, bool on)
        {
            Function.Call(Hash.SET_VEHICLE_LIGHTS, veh.Handle.Value, on ? 3 : 4);
        }

        public static bool IsTireBurst(this Vehicle veh, int wheel)
        {
            return Function.Call<bool>(Hash.IS_VEHICLE_TYRE_BURST, veh.Handle.Value, wheel, false);
        }

        public static void SetCurrentRPM(this Vehicle veh, float newRPM)
        {
            if (veh == null) return;
            int offset = 0x7D4;
            var address = veh.MemoryAddress + offset;
            byte[] newBytes = BitConverter.GetBytes(newRPM);
            Marshal.Copy(newBytes, 0, address, newBytes.Length);
        }

        public static float GetCurrentRPM(this Vehicle veh)
        {
            if (veh == null) return 0f;
            int offset = 0x7D4;
            var address = veh.MemoryAddress + offset;
            var rawInt32 = Marshal.ReadInt32(address);
            int[] rawIntArray = new[] {rawInt32};
            byte[] bytes = new byte[4];
            Buffer.BlockCopy(rawIntArray, 0, bytes, 0, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static void SetShortRange(this Blip blip, bool shortRange)
        {
            Function.Call(Hash.SET_BLIP_AS_SHORT_RANGE, blip, shortRange);
        }

        public static void SetMod(this Vehicle veh, int category, int index, bool modded)
        {
            Function.Call(Hash.SET_VEHICLE_MOD, veh.Handle.Value, category, index, modded);
        }

        public static int GetMod(this Vehicle veh, int category)
        {
            return Function.Call<int>(Hash.GET_VEHICLE_MOD, veh.Handle.Value, category);
        }

        public static void SetColors(this Vehicle veh, VehicleColor color1, VehicleColor color2)
        {
            Function.Call(Hash.SET_VEHICLE_COLOURS, veh.Handle.Value, (int)color1, (int)color2);
        }

        public static unsafe int GetPrimaryColor(this Vehicle veh)
        {
            int color1, color2;
            Function.Call(Hash.GET_VEHICLE_COLOURS, veh.Handle.Value, &color1, &color2);
            return color1;
        }

        public static unsafe int GetSecondaryColor(this Vehicle veh)
        {
            int color1, color2;
            Function.Call(Hash.GET_VEHICLE_COLOURS, veh.Handle.Value, &color1, &color2);
            return color2;
        }
    }

    public static class VectorExtensions
    {
        public static bool IsInRangeOf(this ISpatial spatial, Vector3 pos, float range)
        {
            return (spatial.Position - pos).LengthSquared() < range*range;
        }

        public static Rage.Quaternion ToQuaternion(this GTANetworkShared.Quaternion q)
        {
            return new Rage.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        public static Rage.Vector3 ToVector(this GTANetworkShared.Vector3 v)
        {
            return new Rage.Vector3(v.X, v.Y, v.Z);
        }

        public static GTANetworkShared.Vector3 ToLVector(this Rage.Vector3 vec)
        {
            return new GTANetworkShared.Vector3()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
            };
        }

        public static GTANetworkShared.Quaternion ToLQuaternion(this Rage.Quaternion vec)
        {
            return new GTANetworkShared.Quaternion()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
                W = vec.W,
            };
        }
    }
}