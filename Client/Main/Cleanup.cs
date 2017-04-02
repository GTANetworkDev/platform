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
        public CleanupGame() { Tick += OnTick; }

        private static DateTime LastDateTime = DateTime.Now;

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                Function.Call(Hash.SET_RANDOM_TRAINS, 0);
                Function.Call(Hash.CAN_CREATE_RANDOM_COPS, false);

                Function.Call(Hash.SET_PED_POPULATION_BUDGET, 0);
                Function.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 0);

                Function.Call(Hash.SUPPRESS_SHOCKING_EVENTS_NEXT_FRAME);
                Function.Call(Hash.SUPPRESS_AGITATION_EVENTS_NEXT_FRAME);

                Function.Call(Hash.SET_FAR_DRAW_VEHICLES, false);
                Function.Call((Hash) 0xF796359A959DF65D, false); // Display distant vehicles
                Function.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);
                Function.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, -1);

                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);

                //Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, PlayerChar, true, true);
                //Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, PlayerChar, true);

                Function.Call((Hash)0xD2B315B6689D537D, Game.Player, false); //Some secret ingredient

                //Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, playerChar, true);

                //Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);
                //Function.Call(Hash.SET_MISSION_FLAG, Game.Player.Character, 0);
                ////Function.Call(Hash._RESET_LOCALPLAYER_STATE);
                //Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);

                Function.Call(Hash.DESTROY_MOBILE_PHONE);
                Function.Call((Hash) 0x015C49A93E3E086E, true); //_DISABLE_PHONE_THIS_FRAME
                Function.Call(Hash.DISPLAY_CASH, false);

                Function.Call(Hash.SET_AUTO_GIVE_PARACHUTE_WHEN_ENTER_PLANE, Game.Player, false);

                Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);
                Function.Call((Hash) 0x5DB660B38DD98A31, Game.Player, 0f); //SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER

                Game.Player.WantedLevel = 0;
                Game.MaxWantedLevel = 0;

                if (Function.Call<bool>(Hash.IS_STUNT_JUMP_IN_PROGRESS)) Function.Call(Hash.CANCEL_STUNT_JUMP);

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

            //Entities
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
        public Controls() { Tick += OnTick; }

        private static void OnTick(object sender, EventArgs e)
        {
            Game.DisableControlThisFrame(0, Control.FrontendSocialClub);
            Game.DisableControlThisFrame(0, Control.FrontendSocialClubSecondary);
            Game.DisableControlThisFrame(0, Control.EnterCheatCode);

            Game.DisableControlThisFrame(0, Control.SpecialAbility);
            Game.DisableControlThisFrame(0, Control.SpecialAbilityPC);
            Game.DisableControlThisFrame(0, Control.SpecialAbilitySecondary);
            Game.DisableControlThisFrame(0, Control.CharacterWheel);
            Game.DisableControlThisFrame(0, Control.Phone);
            Game.DisableControlThisFrame(0, Control.Duck);

            if (Main.IsConnected())
            {
                Game.DisableControlThisFrame(0, Control.FrontendPause);
                Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);
            }

            var playerChar = Game.Player.Character;
            if (playerChar.IsJumping)
            {
                //Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                Game.DisableControlThisFrame(0, Control.MeleeAttackLight);
            }

            if (playerChar.IsRagdoll)
            {
                Game.DisableControlThisFrame(0, Control.Attack);
                Game.DisableControlThisFrame(0, Control.Attack2);
            }

            if (Game.IsControlPressed(0, Control.Aim) && !playerChar.IsInVehicle() &&
                playerChar.Weapons.Current.Hash != WeaponHash.Unarmed)
            {
                Game.DisableControlThisFrame(0, Control.Jump);
            }

            //CRASH WORKAROUND: DISABLE PARACHUTE RUINER2
            if (playerChar.IsInVehicle())
            {
                if (playerChar.CurrentVehicle.IsInAir && playerChar.CurrentVehicle.Model.Hash == 941494461)
                {
                    Game.DisableAllControlsThisFrame(0);
                }
            }

            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, playerChar) == 2)
            {
                Game.DisableControlThisFrame(0, Control.Aim);
                Game.DisableControlThisFrame(0, Control.Attack);
            }
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
                    if (b.Exists()) b.Remove();
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
            playerChar.SetDefaultClothes();

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
