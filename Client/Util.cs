using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using GTA;
using GTA.Native;
using GTANetworkShared;
using Quaternion = GTA.Math.Quaternion;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public static class Util
    {
        public static T Clamp<T>(T min, T value, T max) where T : IComparable
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;

            return value;
        }

        public static bool ModelRequest;
        public static void LoadModel(Model model)
        {
            LogManager.DebugLog("REQUESTING MODEL " + model.Hash);
            ModelRequest = true;
            while (!model.IsLoaded)
            {
                model.Request();
                //Function.Call(Hash.REQUEST_COLLISION_FOR_MODEL, model.Hash);
                Script.Yield();
            }
            ModelRequest = false;
            LogManager.DebugLog("MODEL REQUESTED!");
        }

        public static long TickCount
        {
            get { return DateTime.Now.Ticks / 10000; }
        }

        public static float Unlerp(double left, double center, double right)
        {
            return (float)((center - left) / (right - left));
        }

        // Dirty & dangerous
        public static dynamic Lerp(dynamic from, float fAlpha, dynamic to)
        {
            return ((to - from)*fAlpha + from);
        }

        public static int GetStationId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

	    public static IEnumerable<Blip> GetAllBlips()
	    {
		    foreach (BlipSprite sprite in Enum.GetValues(typeof (BlipSprite)))
		    {
			    int Handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, (int)sprite);
			    while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, Handle))
			    {
				    yield return new Blip(Handle);
					Handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, (int)sprite);
			    }
		    }
		} 

        public static void SetPlayerSkin(PedHash skin)
        {
            var model = new Model(skin);

            model.Request(5000);

            Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, model.Hash);

            model.MarkAsNoLongerNeeded();
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
                    UI.Notify(msg);
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
            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;

            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            float width = height * ratio;

            float reduceX = UI.WIDTH / width;
            float reduceY = UI.HEIGHT / height;


            Point extra = new Point(0, 0);
            if (screenw == 1914 && screenh == 1052)
                extra = new Point(15, 0);

            UI.DrawTexture(filename, idx, 1, 60,
                new PointF(xPos * reduceX + extra.X, yPos * reduceY + extra.Y),
                new PointF(0f, 0f),
                new SizeF(txdWidth * reduceX, txdHeight * reduceY),
                rot, Color.FromArgb(a, r, g, b), 1f);
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

        public static Ped GetResponsiblePed(Vehicle veh)
        {
            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
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
            Quaternion result = new Quaternion();
            result.X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
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
            for (int i = 0; i < veh.PassengerSeats; i++)
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
                dict.Add(i, veh.GetMod((VehicleMod)i));
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
            var address = new IntPtr(veh.MemoryAddress + 0x8AC);
            var bytes = BitConverter.GetBytes(angle);
            Marshal.Copy(bytes, 0, address, bytes.Length);
        }

        public static int GetPedSeat(Ped ped)
        {
            if (ped == null || !ped.IsInVehicle()) return -3;
            if (ped.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < ped.CurrentVehicle.PassengerSeats; i++)
            {
                if (ped.CurrentVehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static int GetFreePassengerSeat(Vehicle veh)
        {
            if (veh == null) return -3;
            for (int i = 0; i < veh.PassengerSeats; i++)
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
                    settings.DisplayName = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
                }

                if (settings.DisplayName.Length > 32)
                {
                    settings.DisplayName = settings.DisplayName.Substring(0, 32);
                }

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