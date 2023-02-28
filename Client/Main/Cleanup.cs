using System;
using GTA;
using GTA.Native;
using GTANetwork.Streamer;
using Control = GTA.Control;
using WeaponHash = GTA.WeaponHash;

namespace GTANetwork
{

    public class CleanupGame : Script
    {
        public CleanupGame()
        {
            Tick += OnTick;
        }

        private static long _lastEntityRemoval;

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                Ped PlayerChar = Game.Player.Character;
                Function.Call(Hash.SET_RANDOM_TRAINS, 0);
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, 1f);
                Function.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);
                Function.Call(Hash.SET_FAR_DRAW_VEHICLES, false);

                Function.Call(Hash.SET_PED_POPULATION_BUDGET, 0);
                Function.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 0);

                //Function.Call(Hash.DESTROY_MOBILE_PHONE);
                Function.Call((Hash)0x015C49A93E3E086E, true); //_DISABLE_PHONE_THIS_FRAME
                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, PlayerChar, true, true);
                Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, PlayerChar, true);
                Function.Call((Hash)0xF796359A959DF65D, false); // Display distant vehicles
                Function.Call(Hash.SET_AUTO_GIVE_PARACHUTE_WHEN_ENTER_PLANE, Game.Player, false);
                Function.Call((Hash)0xD2B315B6689D537D, Game.Player, false); //Some secret ingredient
                Function.Call(Hash.DISPLAY_CASH, false);

                Function.Call(Hash.SUPPRESS_SHOCKING_EVENTS_NEXT_FRAME);
                Function.Call(Hash.SUPPRESS_AGITATION_EVENTS_NEXT_FRAME);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, PlayerChar, true);
                Function.Call(Hash.CAN_CREATE_RANDOM_COPS, false);

                Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);
                Function.Call(Hash.SET_MISSION_FLAG, Game.Player.Character, 0);
                Function.Call(Hash._RESET_LOCALPLAYER_STATE);
                Function.Call(Hash.SET_RANDOM_EVENT_FLAG, 0);


                Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);
                Function.Call((Hash)0x5DB660B38DD98A31, Game.Player, 0f); //SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER
                Game.MaxWantedLevel = 0;
                Game.Player.WantedLevel = 0;

                if (Function.Call<bool>(Hash.IS_STUNT_JUMP_IN_PROGRESS))
                {
                    Function.Call(Hash.CANCEL_STUNT_JUMP);
                }

                if (Main.RemoveGameEntities && Util.Util.TickCount - _lastEntityRemoval > 500) // Save ressource
                {
                    _lastEntityRemoval = Util.Util.TickCount;
                    Ped[] Peds = World.GetAllPeds();
                    int Length = Peds.Length;
                    for (int i = 0; i < Length; i++)
                    {
                        Ped entity = Peds[i];
                        if (!Main.NetEntityHandler.ContainsLocalHandle(entity.Handle) && entity != PlayerChar)
                        {
                            entity.Kill(); //"Some special peds like Epsilon guys or seashark minigame will refuse to despawn if you don't kill them first." - Guad
                            entity.Delete();
                        }
                    }

                    if (Main.RemoveGameEntities && Util.Util.TickCount - _lastEntityRemoval > 1000) // Save ressource
                    {
                        _lastEntityRemoval = Util.Util.TickCount;
                        var vehicles = World.GetAllVehicles();
                        for (var i = 0; i < vehicles.Length; i++)
                        {
                            var entity = vehicles[i];
                            if (entity == null) continue;
                            if (Main.NetEntityHandler.NetToStreamedItem(entity.Handle, useGameHandle: true) is RemoteVehicle) continue;
                            entity.Delete();

                            ////TO CHECK
                            //if (!Util.Util.IsVehicleEmpty(entity) || VehicleSyncManager.IsInterpolating(entity.Handle) || veh.TraileredBy != 0 || VehicleSyncManager.IsSyncing(veh) || (entity.Handle != Game.Player.LastVehicle?.Handle || !(DateTime.Now.Subtract(Events.LastCarEnter).TotalMilliseconds > 3000)) && entity.Handle == Game.Player.LastVehicle?.Handle) continue;
                            //if (!(entity.Position.DistanceToSquared(veh.Position.ToVector()) > 2f)) continue;

                            //entity.PositionNoOffset = veh.Position.ToVector();
                            //entity.Quaternion = veh.Rotation.ToVector().ToQuaternion();

                            //veh.Position = entity.Position.ToLVector();
                            //veh.Rotation = entity.Rotation.ToLVector();
                        }
                    }
                }
            }
        }

    }

    public class Controls : Script
    {
        public Controls()
        {
            Tick += OnTick;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                Ped PlayerChar = Game.Player.Character;
                Game.DisableControlThisFrame(Control.EnterCheatCode);
                Game.DisableControlThisFrame(Control.FrontendPause);
                Game.DisableControlThisFrame(Control.FrontendPauseAlternate);
                Game.DisableControlThisFrame(Control.FrontendSocialClub);
                Game.DisableControlThisFrame(Control.FrontendSocialClubSecondary);

                Game.DisableControlThisFrame(Control.SpecialAbility);
                Game.DisableControlThisFrame(Control.SpecialAbilityPC);
                Game.DisableControlThisFrame(Control.SpecialAbilitySecondary);
                Game.DisableControlThisFrame(Control.CharacterWheel);
                Game.DisableControlThisFrame(Control.Phone);
                Game.DisableControlThisFrame(Control.Duck);

                if (PlayerChar.IsJumping)
                {
                    //Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                    Game.DisableControlThisFrame(Control.MeleeAttackLight);
                }

                if (PlayerChar.IsRagdoll)
                {
                    Game.DisableControlThisFrame(Control.Attack);
                    Game.DisableControlThisFrame(Control.Attack2);
                }

                if (Main._wasTyping)
                {
                    Game.DisableControlThisFrame(Control.FrontendPauseAlternate);
                }

                if (Game.IsControlPressed(Control.Aim) && !PlayerChar.IsInVehicle() && PlayerChar.Weapons.Current.Hash != WeaponHash.Unarmed)
                {
                    Game.DisableControlThisFrame(Control.Jump);
                }

                //CRASH WORKAROUND: DISABLE PARACHUTE RUINER2
                if (PlayerChar.IsInVehicle())
                {
                    if (PlayerChar.CurrentVehicle.IsInAir && PlayerChar.CurrentVehicle.Model.Hash == 941494461)
                    {
                        Game.DisableAllControlsThisFrame();
                    }
                }

                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, PlayerChar) == 2)
                {
                    Game.DisableControlThisFrame(Control.Aim);
                    Game.DisableControlThisFrame(Control.Attack);
                }

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

            playerChar.MaxHealth = 200;
            playerChar.Health = 200;
            playerChar.Style.SetDefaultClothes();

            playerChar.IsPositionFrozen = false;
            Game.Player.IsInvincible = false;
            playerChar.IsCollisionEnabled = true;
            playerChar.Opacity = 255;
            playerChar.IsInvincible = false;
            playerChar.Weapons.RemoveAll();
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, 1f);
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1f);

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