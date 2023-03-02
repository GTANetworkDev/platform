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

    public class CleanupGame : Script
    {
        public CleanupGame()
        {
            Tick += OnTick;
        }

        private static DateTime LastDateTime = DateTime.Now;

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                CallCollection thisCol = new CallCollection();

                //thisCol.Call((Hash) 0xB96B00E976BE977F, 0.0); //_SET_WAVES_INTENSITY

                thisCol.Call(Hash.SET_RANDOM_TRAINS, 0);
                thisCol.Call(Hash.CAN_CREATE_RANDOM_COPS, false);

                thisCol.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, -1);
                thisCol.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);

                //if (Main.RemoveGameEntities)
                //{
                thisCol.Call(Hash.SET_PED_POPULATION_BUDGET, 0);
                thisCol.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 0);

                thisCol.Call(Hash.SUPPRESS_SHOCKING_EVENTS_NEXT_FRAME);
                thisCol.Call(Hash.SUPPRESS_AGITATION_EVENTS_NEXT_FRAME);

                thisCol.Call(Hash.SET_FAR_DRAW_VEHICLES, false);
                thisCol.Call((Hash)0xF796359A959DF65D, false); // _DISPLAY_DISTANT_VEHICLES
                thisCol.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);

                thisCol.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                thisCol.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                thisCol.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                thisCol.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                //}


                //Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, PlayerChar, true, true);
                //Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, PlayerChar, true);

                thisCol.Call((Hash)0xD2B315B6689D537D, Game.Player, false); //Some secret ingredient

                //Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, playerChar, true);

                //Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);
                //Function.Call(Hash.SET_MISSION_FLAG, Game.Player.Character, 0);
                ////Function.Call(Hash._RESET_LOCALPLAYER_STATE);
                //Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);

                thisCol.Call(Hash.DESTROY_MOBILE_PHONE);
                thisCol.Call((Hash)0x015C49A93E3E086E, true); //_DISABLE_PHONE_THIS_FRAME
                thisCol.Call(Hash.DISPLAY_CASH, false);

                thisCol.Call(Hash.SET_AUTO_GIVE_PARACHUTE_WHEN_ENTER_PLANE, Game.Player, false);

                thisCol.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);
                thisCol.Call((Hash)0x5DB660B38DD98A31, Game.Player, 0f); //SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER

                thisCol.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 0, false);
                thisCol.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                thisCol.Call(Hash.SET_MAX_WANTED_LEVEL, 0);

                if (Function.Call<bool>(Hash.IS_STUNT_JUMP_IN_PROGRESS)) thisCol.Call(Hash.CANCEL_STUNT_JUMP);

                thisCol.Execute();

                //if (!Main.RemoveGameEntities) return;

                if (DateTime.Now.Subtract(LastDateTime).TotalMilliseconds >= 500)
                {
                    var playerChar = Game.Player.Character;

                    LastDateTime = DateTime.Now;
                    foreach (var entity in World.GetAllPeds())
                    {
                        if (!Main.NetEntityHandler.ContainsLocalHandle(entity.Handle) && entity != playerChar)
                        {
                            entity.MarkAsNoLongerNeeded();
                            entity.Kill(); //"Some special peds like Epsilon guys or seashark minigame will refuse to despawn if you don't kill them first." - Guad
                            entity.Delete();
                        }
                    }

                    foreach (var entity in World.GetAllVehicles())
                    {
                        var veh = Main.NetEntityHandler.NetToStreamedItem(entity.Handle, useGameHandle: true) as RemoteVehicle;
                        if (veh == null)
                        {
                            entity.MarkAsNoLongerNeeded();
                            entity.Delete();
                        }
                    }
                }
            }
        }
    }

    //public class LeeroyJenkins : Script
    //{
    //    public LeeroyJenkins() { Tick += OnTick; }

    //    private static DateTime LastDateTime = DateTime.Now;

    //    private static void OnTick(object sender, EventArgs e)
    //    {
    //        var proc = Process.GetProcessesByName("GameOverlayUI");
    //        if (DateTime.Now.Subtract(LastDateTime).TotalSeconds >= 1 && proc.Any())
    //        {
    //            LastDateTime = DateTime.Now;
    //            ThreadPool.QueueUserWorkItem(delegate { foreach (var process in proc) process.Kill(); });
    //        }

    //    }
    //}

    public class Controls : Script
    {
        public Controls()
        {
            Tick += OnTick;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            CallCollection thisCol = new CallCollection();
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.FrontendSocialClub, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.FrontendSocialClubSecondary, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.EnterCheatCode, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.SpecialAbility, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.SpecialAbilityPC, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.SpecialAbilitySecondary, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.CharacterWheel, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Phone, true);
            thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Duck, true);

            //Game.DisableControlThisFrame(Control.FrontendSocialClub);
            //Game.DisableControlThisFrame(Control.FrontendSocialClubSecondary);
            //Game.DisableControlThisFrame(Control.EnterCheatCode);

            //Game.DisableControlThisFrame(Control.SpecialAbility);
            //Game.DisableControlThisFrame(Control.SpecialAbilityPC);
            //Game.DisableControlThisFrame(Control.SpecialAbilitySecondary);
            //Game.DisableControlThisFrame(Control.CharacterWheel);
            //Game.DisableControlThisFrame(Control.Phone);
            //Game.DisableControlThisFrame(Control.Duck);

            if (Main.IsConnected())
            {
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.FrontendPause, true);
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.FrontendPauseAlternate, true);
                //Game.DisableControlThisFrame(Control.FrontendPause);
                //Game.DisableControlThisFrame(Control.FrontendPauseAlternate);
            }

            var playerChar = Game.Player.Character;
            if (playerChar.IsJumping)
            {
                //Game.DisableControlThisFrame(Control.MeleeAttack1);
                //Game.DisableControlThisFrame(Control.MeleeAttackLight);
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.MeleeAttackLight, true);

            }

            if (playerChar.IsRagdoll)
            {
                //Game.DisableControlThisFrame(Control.Attack);
                //Game.DisableControlThisFrame(Control.Attack2);
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Attack, true);
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Attack2, true);

            }

            if (Game.IsControlPressed(Control.Aim) && !playerChar.IsInVehicle() && playerChar.Weapons.Current.Hash != WeaponHash.Unarmed)
            {
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Jump, true);
                //Game.DisableControlThisFrame(Control.Jump);
            }

            //CRASH WORKAROUND: DISABLE PARACHUTE RUINER2
            if (playerChar.IsInVehicle())
            {
                if (playerChar.CurrentVehicle.IsInAir && playerChar.CurrentVehicle.Model.Hash == 941494461)
                {
                    thisCol.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
                    //Game.DisableAllControlsThisFrame();
                }
            }

            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, playerChar) == 2)
            {
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Aim, true);
                thisCol.Call(Hash.DISABLE_CONTROL_ACTION, 0, Control.Attack, true);

                //Game.DisableControlThisFrame(Control.Aim);
                //Game.DisableControlThisFrame(Control.Attack);
            }
            thisCol.Execute();
        }
    }

    internal partial class Main
    {
        private static void DisableSlowMo()
        {
            var address = Util.Util.FindPattern("\x32\xc0\xf3\x0f\x11\x09", "xxxxxx"); // Weapon/radio slowdown\
            if (address != IntPtr.Zero)
            {
                Util.Util.WriteMemory(address, 0x90, 6);
            }
        }


        private static void UnlockObjects()
        {
            var address = Util.Util.FindPattern("\x48\x85\xC0\x0F\x84\x00\x00\x00\x00\x8B\x48\x50", "xxxxx????xxx"); // unlock objects; credit goes to the GTA-MP team
            if (address != IntPtr.Zero)
            {
                Util.Util.WriteMemory(address, 0x90, 24);
            }
        }

        private static void ClearLocalEntities()
        {
            lock (EntityCleanup)
            {
                for (var index = EntityCleanup.Count - 1; index >= 0; index--)
                {
                    var prop = new Prop(EntityCleanup[index]);
                    if (prop.Exists()) prop.Delete();
                }
                EntityCleanup.Clear();
            }
        }

        private static void ClearLocalBlips()
        {
            lock (BlipCleanup)
            {
                for (var index = BlipCleanup.Count - 1; index >= 0; index--)
                {
                    var b = new Blip(BlipCleanup[index]);
                    if (b.Exists()) b.Delete();
                }
                BlipCleanup.Clear();
            }
        }

        private void ResetPlayer()
        {
            var playerChar = Game.Player.Character;

            playerChar.Position = _vinewoodSign;
            playerChar.IsPositionFrozen = false;

            CustomAnimation = null;
            AnimationFlag = 0;

            Util.Util.SetPlayerSkin(PedHash.Clown01SMY);

            playerChar = Game.Player.Character;
            var player = Game.Player;

            playerChar.Health = 200;
            playerChar.Style.SetDefaultClothes();

            playerChar.IsPositionFrozen = false;
            player.IsInvincible = false;
            playerChar.IsCollisionEnabled = true;
            playerChar.Opacity = 255;
            playerChar.IsInvincible = false;
            playerChar.Weapons.RemoveAll();
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, player, 1f);
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, player, 1f);

            Function.Call(Hash.SET_FAKE_WANTED_LEVEL, 0);
            Function.Call(Hash.DETACH_ENTITY, playerChar.Handle, true, true);
        }

        private static void ResetWorld()
        {
            World.RenderingCamera = MainMenuCamera;
            MainMenu.Visible = true;
            MainMenu.TemporarilyHidden = false;
            IsSpectating = false;
            Weather = null;
            Time = null;
            LocalTeam = -1;
            LocalDimension = 0;

            //Script.Wait(500);
            //PlayerChar.SetDefaultClothes();
        }

        private static void ClearStats()
        {
            BytesReceived = 0;
            BytesSent = 0;
            MessagesReceived = 0;
            MessagesSent = 0;
        }
    }
}