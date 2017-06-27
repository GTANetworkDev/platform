
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.GUI;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Streamer;
using GTANetwork.Sync;
using GTANetwork.Util;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.Win32;
using NativeUI;
using NativeUI.PauseMenu;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork
{
    public class Events : Script
    {
        public Events()
        {
            Tick += InvokeEvents;
        }

        private static bool _lastDead;
        private static bool _lastKilled;
        private static bool _lastVehicleSiren;

        private static int _lastPlayerHealth = 100;
        private static int _lastPlayerArmor = 0;
        
        public static DateTime LastCarEnter;
        private static WeaponHash _lastPlayerWeapon = WeaponHash.Unarmed;
        private static PedHash _lastPlayerModel = PedHash.Clown01SMY;

        private static Vehicle _lastPlayerCar;

        public static void InvokeEvents(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            #region invokeonLocalPlayerShoot
            if (player != null && player.IsShooting)
            {
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonLocalPlayerShoot((int)(player.Weapons.Current?.Hash ?? 0), Main.RaycastEverything(new Vector2(0, 0)).ToLVector()));
            }
            #endregion

            //#region invokeonPlayerRespawn
            var hasRespawned = (Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) < 8000 &&
                                                Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) != -1 &&
                                                Game.Player.CanControlCharacter);

            if (hasRespawned && !_lastDead)
            {
                _lastDead = true;
                var msg = Main.Client.CreateMessage();
                msg.Write((byte)PacketType.PlayerRespawned);
                Main.Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerRespawn());

                if (Main.Weather != null) Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Main.Weather);
                if (Main.Time.HasValue)
                {
                    World.CurrentDayTime = new TimeSpan(Main.Time.Value.Hours, Main.Time.Value.Minutes, 00);
                }

                Function.Call(Hash.PAUSE_CLOCK, true);

                var us = Main.NetEntityHandler.EntityToStreamedItem(Game.Player.Character.Handle);

                Main.NetEntityHandler.ReattachAllEntities(us, true);
                foreach (
                    var source in
                        Main.NetEntityHandler.ClientMap.Values.Where(
                            item => item is RemoteParticle && ((RemoteParticle)item).EntityAttached == us.RemoteHandle)
                            .Cast<RemoteParticle>())
                {
                    Main.NetEntityHandler.StreamOut(source);
                    Main.NetEntityHandler.StreamIn(source);
                }
            }

            var pHealth = player.Health;
            var pArmor = player.Armor;
            var pGun = player.Weapons.Current?.Hash ?? WeaponHash.Unarmed;
            var pModel = player.Model.Hash;

            if (pHealth != _lastPlayerHealth)
            {
                int test = _lastPlayerHealth;
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerHealthChange(test));
            }

            if (pArmor != _lastPlayerArmor)
            {
                int test = _lastPlayerArmor;
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerArmorChange(test));
            }

            if (pGun != _lastPlayerWeapon)
            {
                WeaponHash test = _lastPlayerWeapon;
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerWeaponSwitch((int)test));
            }

            if (pModel != (int)_lastPlayerModel)
            {
                PedHash test = _lastPlayerModel;
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerModelChange((int)test));
            }

            _lastPlayerHealth = pHealth;
            _lastPlayerArmor = pArmor;
            _lastPlayerWeapon = pGun;
            _lastPlayerModel = (PedHash)pModel;

            _lastDead = hasRespawned;
           
            var killed = Game.Player.Character.IsDead;
            if (killed && !_lastKilled)
            {
                var msg = Main.Client.CreateMessage();
                msg.Write((byte)PacketType.PlayerKilled);
                var killer = Function.Call<int>(Hash.GET_PED_SOURCE_OF_DEATH, Game.Player.Character);
                var weapon = Function.Call<int>(Hash.GET_PED_CAUSE_OF_DEATH, Game.Player.Character);

                var killerEnt = Main.NetEntityHandler.EntityToNet(killer);
                msg.Write(killerEnt);
                msg.Write(weapon);

                Main.Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerDeath(new LocalHandle(killer), weapon));

                //TODO: ADD OPTION TO ENABLE IT
                NativeUI.BigMessageThread.MessageInstance.ShowColoredShard("WASTED", "", HudColor.HUD_COLOUR_BLACK, HudColor.HUD_COLOUR_RED, 7000);
                Function.Call(Hash.REQUEST_SCRIPT_AUDIO_BANK, "HUD_MINI_GAME_SOUNDSET", true);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
            }

            _lastKilled = killed;

            if (player.CurrentVehicle != null)
            {
                var playerCar = player.CurrentVehicle;
                var netPlayerCar = 0;


                RemoteVehicle cc = null;

                //invokeonVehicleHealthChange
                //invokeonVehicleWindowSmash
                #region invokeonVehicleDoorBreak
                if ((netPlayerCar = Main.NetEntityHandler.EntityToNet(playerCar.Handle)) != 0)
                {
                    var item = Main.NetEntityHandler.NetToStreamedItem(netPlayerCar) as RemoteVehicle;

                    if (item != null)
                    {
                        var lastHealth = item.Health;
                        var lastDamageModel = item.DamageModel;

                        item.Position = playerCar.Position.ToLVector();
                        item.Rotation = playerCar.Rotation.ToLVector();
                        item.Health = playerCar.EngineHealth;
                        item.IsDead = playerCar.IsDead;
                        item.DamageModel = playerCar.GetVehicleDamageModel();

                        if (lastHealth != playerCar.EngineHealth)
                        {
                            JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleHealthChange((int)lastHealth));
                        }

                        if (playerCar.IsEngineRunning ^ !PacketOptimization.CheckBit(item.Flag, EntityFlag.EngineOff))
                        {
                            playerCar.IsEngineRunning = !PacketOptimization.CheckBit(item.Flag, EntityFlag.EngineOff);
                        }

                        if (lastDamageModel != null)
                        {
                            if (lastDamageModel.BrokenWindows != item.DamageModel.BrokenWindows)
                            {
                                for (var i = 0; i < 8; i++)
                                {
                                    if (((lastDamageModel.BrokenWindows ^ item.DamageModel.BrokenWindows) & 1 << i) == 0)
                                        continue;
                                    var i1 = i;
                                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleWindowSmash(i1));
                                }
                            }

                            if (lastDamageModel.BrokenDoors != item.DamageModel.BrokenDoors)
                            {
                                for (var i = 0; i < 8; i++)
                                {
                                    if (((lastDamageModel.BrokenDoors ^ item.DamageModel.BrokenDoors) & 1 << i) == 0)
                                        continue;
                                    var i1 = i;
                                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleDoorBreak(i1));
                                }
                            }
                        }
                    }

                    cc = item;
                }
                #endregion

                #region invokeonPlayerExitVehicle
                if (playerCar != _lastPlayerCar)
                {
                    if (_lastPlayerCar != null)
                    {
                        var c = Main.NetEntityHandler.EntityToStreamedItem(_lastPlayerCar.Handle) as RemoteVehicle;

                        if (Main.VehicleSyncManager.IsSyncing(c))
                        {
                            _lastPlayerCar.IsInvincible = c?.IsInvincible ?? false;
                        }
                        else
                        {
                            _lastPlayerCar.IsInvincible = true;
                        }

                        if (c != null)
                        {
                            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _lastPlayerCar, !PacketOptimization.CheckBit(c.Flag, EntityFlag.EngineOff), true, true);
                        }

                        var h = _lastPlayerCar.Handle;
                        var lh = new LocalHandle(h);
                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerExitVehicle(lh));
                        _lastVehicleSiren = false;
                        _lastPlayerCar = null;
                    }

                    //invokeonPlayerEnterVehicle
                    if (!Main.NetEntityHandler.ContainsLocalHandle(playerCar.Handle))
                    {
                        playerCar.Delete();
                        playerCar = null;
                    }
                    else
                    {
                        playerCar.IsInvincible = cc?.IsInvincible ?? false;

                        var handle = new LocalHandle(playerCar.Handle);
                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerEnterVehicle(handle));
                        LastCarEnter = DateTime.Now;
                        _lastPlayerCar = playerCar;
                    }
                }
                #endregion

                #region invokeonVehicleSirenToggle
                if (Util.Util.GetResponsiblePed(playerCar).Handle == player.Handle)
                {
                    playerCar.IsInvincible = cc?.IsInvincible ?? false;
                }
                else
                {
                    playerCar.IsInvincible = true;
                }

                var siren = playerCar.IsSirenActive;

                if (siren != _lastVehicleSiren)
                {
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleSirenToggle());
                }

                _lastVehicleSiren = siren;
                #endregion
            }
            else
            {
                if (_lastPlayerCar != null)
                {
                    var c = Main.NetEntityHandler.EntityToStreamedItem(_lastPlayerCar.Handle) as RemoteVehicle;

                    if (Main.VehicleSyncManager.IsSyncing(c))
                    {
                        _lastPlayerCar.IsInvincible = c?.IsInvincible ?? false;
                    }
                    else
                    {
                        _lastPlayerCar.IsInvincible = true;
                    }

                    if (c != null)
                    {
                        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _lastPlayerCar, !PacketOptimization.CheckBit(c.Flag, EntityFlag.EngineOff), true, true);
                    }

                    var h = _lastPlayerCar.Handle;
                    var lh = new LocalHandle(h);
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerExitVehicle(lh));
                    _lastVehicleSiren = false;
                    _lastPlayerCar = null;
                }
            }
            
        }
    }
}
