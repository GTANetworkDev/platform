using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Streamer;
using GTANetwork.Sync;
using GTANetwork.Util;
using GTANetworkShared;
using NativeUI;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork
{
    public class PrintVersionThread : Script
    {
        static UIResText _versionLabel = new UIResText("GTAN " + Main.CurrentVersion + "-" + Main.PlayerSettings.UpdateChannel, new Point(), 0.35f, Color.FromArgb(100, 200, 200, 200));

        public PrintVersionThread()
        {
            Tick += OnTick;
            _versionLabel.Position = new Point((int)(Main.res.Width / 2), 0);
            _versionLabel.TextAlignment = UIResText.Alignment.Centered;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected()) _versionLabel.Draw();
        }
    }

    internal partial class Main
    {
        private void TickSpinner()
        {
            OnTick(null, EventArgs.Empty);
        }

        public static void AddMap(ServerMap map)
        {
            //File.WriteAllText(GTANInstallDir + "\\logs\\map.json", JsonConvert.SerializeObject(map));
            Ped PlayerChar = Game.Player.Character;
            try
            {
                NetEntityHandler.ServerWorld = map.World;

                if (map.World.LoadedIpl != null)
                    foreach (var ipl in map.World.LoadedIpl)
                    {
                        Function.Call(Hash.REQUEST_IPL, ipl);
                    }

                if (map.World.RemovedIpl != null)
                    foreach (var ipl in map.World.RemovedIpl)
                    {
                        Function.Call(Hash.REMOVE_IPL, ipl);
                    }

                if (map.Objects != null)
                    foreach (var pair in map.Objects)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pair.Key)) continue;
                        NetEntityHandler.CreateObject(pair.Key, pair.Value);
                        //GTA.UI.Screen.ShowSubtitle("Creating object...", 500000);
                    }

                if (map.Vehicles != null)
                    foreach (var pair in map.Vehicles)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pair.Key)) continue;
                        NetEntityHandler.CreateVehicle(pair.Key, pair.Value);
                        //GTA.UI.Screen.ShowSubtitle("Creating vehicle...", 500000);
                    }

                if (map.Blips != null)
                {
                    foreach (var blip in map.Blips)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(blip.Key)) continue;
                        NetEntityHandler.CreateBlip(blip.Key, blip.Value);
                    }
                }

                if (map.Markers != null)
                {
                    foreach (var marker in map.Markers)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(marker.Key)) continue;
                        NetEntityHandler.CreateMarker(marker.Key, marker.Value);
                    }
                }

                if (map.Pickups != null)
                {
                    foreach (var pickup in map.Pickups)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pickup.Key)) continue;
                        NetEntityHandler.CreatePickup(pickup.Key, pickup.Value);
                    }
                }

                if (map.TextLabels != null)
                {
                    //map.TextLabels.GroupBy(x => x.Key).Select(y => y.First()); //Remove duplicates before procceeding

                    foreach (var label in map.TextLabels)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(label.Key)) continue;
                        NetEntityHandler.CreateTextLabel(label.Key, label.Value);
                    }
                }

                if (map.Peds != null)
                {
                    foreach (var ped in map.Peds)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(ped.Key)) continue;
                        NetEntityHandler.CreatePed(ped.Key, ped.Value);
                    }
                }

                if (map.Particles != null)
                {
                    foreach (var ped in map.Particles)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(ped.Key)) continue;
                        NetEntityHandler.CreateParticle(ped.Key, ped.Value);
                    }
                }

                if (map.Players != null)
                {
                    LogManager.DebugLog("STARTING PLAYER MAP");

                    foreach (var pair in map.Players)
                    {
                        if (NetEntityHandler.NetToEntity(pair.Key)?.Handle == PlayerChar.Handle)
                        {
                            // It's us!
                            var remPl = NetEntityHandler.NetToStreamedItem(pair.Key) as RemotePlayer;
                            remPl.Name = pair.Value.Name;
                        }
                        else
                        {
                            var ourSyncPed = NetEntityHandler.GetPlayer(pair.Key);
                            NetEntityHandler.UpdatePlayer(pair.Key, pair.Value);
                            if (ourSyncPed.Character != null)
                            {
                                ourSyncPed.Character.RelationshipGroup = (pair.Value.Team == LocalTeam &&
                                                                            pair.Value.Team != -1)
                                    ? Main.FriendRelGroup
                                    : Main.RelGroup;

                                for (int i = 0; i < 15; i++) //NEEDS A CHECK
                                {
                                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ourSyncPed.Character, i,
                                        pair.Value.Props.Get((byte)i),
                                        pair.Value.Textures.Get((byte)i), 2);
                                }

                                lock (NetEntityHandler.HandleMap)
                                    NetEntityHandler.HandleMap.Set(pair.Key, ourSyncPed.Character.Handle);

                                ourSyncPed.Character.Opacity = pair.Value.Alpha;
                                /*
                                if (ourSyncPed.Character.AttachedBlip != null)
                                {
                                    ourSyncPed.Character.AttachedBlip.Sprite = (BlipSprite)pair.Value.BlipSprite;
                                    ourSyncPed.Character.AttachedBlip.Color = (BlipColor)pair.Value.BlipColor;
                                    ourSyncPed.Character.AttachedBlip.Alpha = pair.Value.BlipAlpha;
                                }
                                */
                                NetEntityHandler.ReattachAllEntities(ourSyncPed, false);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowNotification("FATAL ERROR WHEN PARSING MAP");
                GTA.UI.Screen.ShowNotification(ex.Message);
                Client.Disconnect("Map Parse Error");

                LogManager.LogException(ex, "MAP PARSE");

                return;
            }

            World.CurrentDayTime = new TimeSpan(map.World.Hours, map.World.Minutes, 00);

            Time = new TimeSpan(map.World.Hours, map.World.Minutes, 00);
            if (map.World.Weather >= 0 && map.World.Weather < Enums._weather.Length)
            {
                Weather = Enums._weather[map.World.Weather];
                Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Enums._weather[map.World.Weather]);
            }

            Function.Call(Hash.PAUSE_CLOCK, true);
        }

        public static void StartClientsideScripts(ScriptCollection scripts)
        {
            if (scripts.ClientsideScripts == null) return;
            JavascriptHook.StartScripts(scripts);
        }

        public static Dictionary<int, int> CheckPlayerVehicleMods()
        {
            Ped PlayerChar = Game.Player.Character;
            if (!PlayerChar.IsInVehicle()) return null;

            if (_modSwitch % 30 == 0)
            {
                var id = _modSwitch / 30;
                var mod = PlayerChar.CurrentVehicle.Mods[(VehicleModType)id].Index;
                if (mod != -1)
                {
                    lock (_vehMods)
                    {
                        if (!_vehMods.ContainsKey(id)) _vehMods.Add(id, mod);

                        _vehMods[id] = mod;
                    }
                }
            }

            _modSwitch++;

            if (_modSwitch >= 1500) _modSwitch = 0;

            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch % 30 == 0)
            {
                Ped PlayerChar = Game.Player.Character;
                var id = _pedSwitch / 30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, PlayerChar.Handle, id);
                if (mod != -1)
                {
                    lock (_pedClothes)
                    {
                        if (!_pedClothes.ContainsKey(id)) _pedClothes.Add(id, mod);

                        _pedClothes[id] = mod;
                    }
                }
            }

            _pedSwitch++;

            if (_pedSwitch >= 450) _pedSwitch = 0;

            return _pedClothes;
        }

        public static SyncPed GetPedWeHaveDamaged()
        {
            var us = Game.Player.Character;

            SyncPed[] myArray;

            lock (StreamerThread.StreamedInPlayers) myArray = StreamerThread.StreamedInPlayers.ToArray();

            foreach (var index in myArray)
            {
                if (index == null) continue;

                var them = new Ped(index.LocalHandle);
                if (!them.HasBeenDamagedBy(us)) continue;

                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, them);
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, us);
                //Util.Util.SafeNotify("Shot at" + index.Name + " " + DateTime.Now.Millisecond);
                return index;
            }
            return null;
        }

        public static byte GetPedWalkingSpeed(Ped ped)
        {
            byte output = 0;

            if (SyncPed.GetAnimalAnimationDictionary(ped.Model.Hash) != null)
            {
                // Player has an animal skin
                var hash = (PedHash)ped.Model.Hash;

                switch (hash)
                {
                    case PedHash.ChickenHawk:
                    case PedHash.Cormorant:
                    case PedHash.Crow:
                    case PedHash.Seagull:
                    case PedHash.Pigeon:
                        if (ped.Velocity.Length() > 0.1) output = 1;
                        if (ped.IsInAir || ped.Velocity.Length() > 0.5) output = 3;
                        break;
                    case PedHash.Dolphin:
                    case PedHash.Fish:
                    case PedHash.Humpback:
                    case PedHash.KillerWhale:
                    case PedHash.Stingray:
                    case PedHash.HammerShark:
                    case PedHash.TigerShark:
                        if (ped.Velocity.Length() > 0.1) output = 1;
                        if (ped.Velocity.Length() > 0.5) output = 2;
                        break;
                }
            }
            if (Function.Call<bool>(Hash.IS_PED_WALKING, ped)) output = 1;
            if (Function.Call<bool>(Hash.IS_PED_RUNNING, ped)) output = 2;
            if (Function.Call<bool>(Hash.IS_PED_SPRINTING, ped) || ped.IsPlayer && Game.IsControlPressed(0, Control.Sprint)) output = 3;

            //if (Function.Call<bool>(Hash.IS_PED_STRAFING, ped)) ;

            /*if (ped.IsSubtaskActive(ESubtask.AIMING_GUN))
            {
                if (ped.Velocity.LengthSquared() > 0.1f*0.1f)
                    output = 1;
            }
            */

            return output;
        }

        public static int GetCurrentVehicleWeaponHash(Ped ped)
        {
            if (!ped.IsInVehicle()) return 0;
            var outputArg = new OutputArgument();
            var success = Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, ped, outputArg);
            return success ? outputArg.GetResult<int>() : 0;
        }

        public static IEnumerable<ProcessModule> GetModules()
        {
            var modules = Process.GetCurrentProcess().Modules;

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                yield return modules[i];
            }
        }

        private static void SaveSettings()
        {
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private bool VerifyDLC()
        {
            bool legit = true;

            legit = legit && (Game.Version >= (GameVersion)27);

            GTANetworkShared.VehicleHash[] dlcCars = new GTANetworkShared.VehicleHash[]
            {
                GTANetworkShared.VehicleHash.Trophytruck,GTANetworkShared.VehicleHash.Cliffhanger,
                GTANetworkShared.VehicleHash.Lynx,GTANetworkShared.VehicleHash.Contender,
                GTANetworkShared.VehicleHash.Gargoyle,GTANetworkShared.VehicleHash.Sheava,
                GTANetworkShared.VehicleHash.Brioso,GTANetworkShared.VehicleHash.Tropos,
                GTANetworkShared.VehicleHash.Tyrus,GTANetworkShared.VehicleHash.Rallytruck,
                GTANetworkShared.VehicleHash.le7b,GTANetworkShared.VehicleHash.Tampa2,
                GTANetworkShared.VehicleHash.Omnis,GTANetworkShared.VehicleHash.Trophytruck2,
                GTANetworkShared.VehicleHash.Avarus,GTANetworkShared.VehicleHash.Blazer4,
                GTANetworkShared.VehicleHash.Chimera,GTANetworkShared.VehicleHash.Daemon2,
                GTANetworkShared.VehicleHash.Defiler,GTANetworkShared.VehicleHash.Esskey,
                GTANetworkShared.VehicleHash.Faggio,GTANetworkShared.VehicleHash.Faggio3,
                GTANetworkShared.VehicleHash.Hakuchou2,GTANetworkShared.VehicleHash.Manchez,
                GTANetworkShared.VehicleHash.Nightblade,GTANetworkShared.VehicleHash.Raptor,
                GTANetworkShared.VehicleHash.Ratbike,GTANetworkShared.VehicleHash.Sanctus,
                GTANetworkShared.VehicleHash.Shotaro,GTANetworkShared.VehicleHash.Tornado6,
                GTANetworkShared.VehicleHash.Vortex,GTANetworkShared.VehicleHash.Wolfsbane,
                GTANetworkShared.VehicleHash.Youga2,GTANetworkShared.VehicleHash.Zombiea,
                GTANetworkShared.VehicleHash.Zombieb, GTANetworkShared.VehicleHash.Voltic2,
                GTANetworkShared.VehicleHash.Ruiner2, GTANetworkShared.VehicleHash.Dune4,
                GTANetworkShared.VehicleHash.Dune5, GTANetworkShared.VehicleHash.Phantom2,
                GTANetworkShared.VehicleHash.Technical2, GTANetworkShared.VehicleHash.Boxville5,
                GTANetworkShared.VehicleHash.Blazer5,
                GTANetworkShared.VehicleHash.Comet3, GTANetworkShared.VehicleHash.Diablous,
                GTANetworkShared.VehicleHash.Diablous2, GTANetworkShared.VehicleHash.Elegy,
                GTANetworkShared.VehicleHash.Fcr, GTANetworkShared.VehicleHash.Fcr2,
                GTANetworkShared.VehicleHash.Italigtb, GTANetworkShared.VehicleHash.Italigtb2,
                GTANetworkShared.VehicleHash.Nero, GTANetworkShared.VehicleHash.Nero2,
                GTANetworkShared.VehicleHash.Penetrator, GTANetworkShared.VehicleHash.Specter,
                GTANetworkShared.VehicleHash.Specter2, GTANetworkShared.VehicleHash.Tempesta,
                GTANetworkShared.VehicleHash.GP1, GTANetworkShared.VehicleHash.Infernus2,
                GTANetworkShared.VehicleHash.Ruston, GTANetworkShared.VehicleHash.Turismo2,
            };


            return dlcCars.Aggregate(legit, (current, car) => current && new Model((int)car).IsValid);
        }


        private void IntegrityCheck()
        {
#if INTEGRITYCHECK
            if (!VerifyDLC())
            {
                _mainWarning = new Warning("alert", "Could not verify game integrity.\nPlease restart your game, or update Grand Theft Auto V.");
                _mainWarning.OnAccept = () =>
                {
                    if (Client != null && IsOnServer()) Client.Disconnect("Quit");
                    CEFManager.Dispose();
                    CEFManager.DisposeCef();
                    Script.Wait(1000);
                    Environment.Exit(0);
                    //Process.GetProcessesByName("GTA5")[0].Kill();
                    //Process.GetCurrentProcess().Kill();
                };
            }
#endif
        }

        public void DeleteObject(GTANetworkShared.Vector3 pos, float radius, int modelHash)
        {
            Prop returnedProp = Function.Call<Prop>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, pos.X, pos.Y, pos.Z, radius, modelHash, 0);
            if (returnedProp != null && returnedProp.Handle != 0)
            {
                returnedProp.Delete();
            }
        }

        public static void LoadingPromptText(string text)
        {
            Function.Call((Hash)0xABA17D7CE615ADBF, "STRING"); //_SET_LOADING_PROMPT_TEXT_ENTRY
            Function.Call((Hash)0x6C188BE134E074AA, text); //ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            Function.Call((Hash)0x10D373323E5B9C0D); //_REMOVE_LOADING_PROMPT
            Function.Call((Hash)0xBD12F8228410D9B4, 4); //_SHOW_LOADING_PROMPT
        }

        public static void ShowLoadingPrompt(string text)
        {
            Function.Call((Hash)0xABA17D7CE615ADBF, "STRING"); //_SET_LOADING_PROMPT_TEXT_ENTRY
            Function.Call((Hash)0x6C188BE134E074AA, text); //ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            Function.Call((Hash)0xBD12F8228410D9B4, 4); //_SHOW_LOADING_PROMPT
        }

        public static void StopLoadingPrompt()
        {
            Function.Call((Hash)0x10D373323E5B9C0D); //_REMOVE_LOADING_PROMPT
        }

        #region Serialization
        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException)
                {
                    return null;
                }
            }
            return output;
        }
        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }
        #endregion

        #region Raycasting
        public static Vector3 RaycastEverything(Vector2 screenCoord)
        {
            Vector3 camPos, camRot;

            if (World.RenderingCamera.Handle == -1)
            {
                camPos = GameplayCamera.Position;
                camRot = GameplayCamera.Rotation;
            }
            else
            {
                camPos = World.RenderingCamera.Position;
                camRot = World.RenderingCamera.Rotation;
            }

            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Ped PlayerChar = Game.Player.Character;
            Entity ignoreEntity = PlayerChar;

            if (PlayerChar.IsInVehicle())
            {
                ignoreEntity = PlayerChar.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHit)
            {
                return raycastResults.HitPosition;
            }

            return camPos + dir * raycastToDist;
        }

        public static Vector3 RaycastEverything(Vector2 screenCoord, Vector3 camPos, Vector3 camRot)
        {
            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Ped PlayerChar = Game.Player.Character;
            Entity ignoreEntity = PlayerChar;

            if (PlayerChar.IsInVehicle())
            {
                ignoreEntity = PlayerChar.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHit)
            {
                return raycastResults.HitPosition;
            }

            return camPos + dir * raycastToDist;
        }

        public static bool WorldToScreenRel(Vector3 worldCoords, out Vector2 screenCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                screenCoords = new Vector2();
                return false;
            }
            screenCoords = new Vector2((num1.GetResult<float>() - 0.5f) * 2, (num2.GetResult<float>() - 0.5f) * 2);
            return true;
        }

        public static PointF WorldToScreen(Vector3 worldCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                return new PointF();
            }
            return new PointF(num1.GetResult<float>(), num2.GetResult<float>());
        }

        public static Vector3 ScreenRelToWorld(Vector3 camPos, Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new Vector3(10, 0, 0);
            var rotDown = camRot + new Vector3(-10, 0, 0);
            var rotLeft = camRot + new Vector3(0, 0, -10);
            var rotRight = camRot + new Vector3(0, 0, 10);

            var camRight = RotationToDirection(rotRight) - RotationToDirection(rotLeft);
            var camUp = RotationToDirection(rotUp) - RotationToDirection(rotDown);

            var rollRad = -DegToRad(camRot.Y);

            var camRightRoll = camRight * (float)Math.Cos(rollRad) - camUp * (float)Math.Sin(rollRad);
            var camUpRoll = camRight * (float)Math.Sin(rollRad) + camUp * (float)Math.Cos(rollRad);

            var point3D = camPos + camForward * 10.0f + camRightRoll + camUpRoll;
            Vector2 point2D;
            if (!WorldToScreenRel(point3D, out point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            Vector2 point2DZero;
            if (!WorldToScreenRel(point3DZero, out point2DZero)) return camPos + camForward * 10.0f;

            const double eps = 0.001;
            if (Math.Abs(point2D.X - point2DZero.X) < eps || Math.Abs(point2D.Y - point2DZero.Y) < eps) return camPos + camForward * 10.0f;
            var scaleX = (coord.X - point2DZero.X) / (point2D.X - point2DZero.X);
            var scaleY = (coord.Y - point2DZero.Y) / (point2D.Y - point2DZero.Y);
            var point3Dret = camPos + camForward * 10.0f + camRightRoll * scaleX + camUpRoll * scaleY;
            return point3Dret;
        }
        #endregion

        #region Math & Conversion
        public static int GetPedSpeed(Vector3 firstVector, Vector3 secondVector)
        {
            float speed = (firstVector - secondVector).Length();
            if (speed < 0.02f)
            {
                return 0;
            }
            else if (speed >= 0.02f && speed < 0.05f)
            {
                return 1;
            }
            else if (speed >= 0.05f && speed < 0.12f)
            {
                return 2;
            }
            else if (speed >= 0.12f)
                return 3;
            return 0;
        }

        public static Vector3 RotationToDirection(Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static Vector3 DirectionToRotation(Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double DegToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public static double RadToDeg(double deg)
        {
            return deg * 180.0 / Math.PI;
        }

        public static double BoundRotationDeg(double angleDeg)
        {
            var twoPi = (int)(angleDeg / 360);
            var res = angleDeg - twoPi * 360;
            if (res < 0) res += 360;
            return res;
        }
        #endregion

        public static int DEBUG_STEP
        {
            get { return _debugStep; }
            set
            {
                _debugStep = value;
                LogManager.DebugLog("LAST STEP: " + value.ToString());

                if (SlowDownClientForDebug)
                    GTA.UI.Screen.ShowSubtitle(value.ToString());
            }
        }

    }
}
