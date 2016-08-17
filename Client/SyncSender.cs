using System;
using System.Threading;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetworkShared;
using Lidgren.Network;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public static class SyncSender
    {
        private const int LIGHT_SYNC_RATE = 1500;
        private const int PURE_SYNC_RATE = 100;

        public static void MainLoop()
        {
            bool lastPedData = false;
            int lastLightSyncSent = 0;

            while (true)
            {
                if (!Main.IsOnServer())
                {
                    Thread.Sleep(100);
                    continue;
                }

                object lastPacket;
                lock (SyncCollector.Lock)
                {
                    lastPacket = SyncCollector.LastSyncPacket;
                    SyncCollector.LastSyncPacket = null;
                }

                if (lastPacket == null) continue;
                try
                {
                    if (lastPacket is PedData)
                    {
                        var bin = PacketOptimization.WritePureSync((PedData) lastPacket);

                        var msg = Main.Client.CreateMessage();
                        msg.Write((byte) PacketType.PedPureSync);
                        msg.Write(bin.Length);
                        msg.Write(bin);

                        try
                        {
                            Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.PureSync);
                        }
                        catch (Exception ex)
                        {
                            Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                            LogManager.LogException(ex, "SENDPLAYERDATA");
                        }

                        if (!lastPedData || Environment.TickCount - lastLightSyncSent > LIGHT_SYNC_RATE)
                        {
                            lastLightSyncSent = Environment.TickCount;

                            LogManager.DebugLog("SENDING LIGHT VEHICLE SYNC");

                            var lightBin = PacketOptimization.WriteLightSync((PedData) lastPacket);

                            var lightMsg = Main.Client.CreateMessage();
                            lightMsg.Write((byte) PacketType.PedLightSync);
                            lightMsg.Write(lightBin.Length);
                            lightMsg.Write(lightBin);
                            try
                            {
                                Main.Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced,
                                    (int) ConnectionChannel.LightSync);
                            }
                            catch (Exception ex)
                            {
                                Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                                LogManager.LogException(ex, "SENDPLAYERDATA");
                            }

                            Main._bytesSent += lightBin.Length;
                            Main._messagesSent++;
                        }

                        lastPedData = true;

                        lock (Main._averagePacketSize)
                        {
                            Main._averagePacketSize.Add(bin.Length);
                            if (Main._averagePacketSize.Count > 10)
                                Main._averagePacketSize.RemoveAt(0);
                        }

                        Main._bytesSent += bin.Length;
                        Main._messagesSent++;
                    }
                    else
                    {
                        var bin = PacketOptimization.WritePureSync((VehicleData) lastPacket);

                        var msg = Main.Client.CreateMessage();
                        msg.Write((byte) PacketType.VehiclePureSync);
                        msg.Write(bin.Length);
                        msg.Write(bin);
                        try
                        {
                            Main.Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.PureSync);
                        }
                        catch (Exception ex)
                        {
                            Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                            LogManager.LogException(ex, "SENDPLAYERDATA");
                        }

                        if (lastPedData || Environment.TickCount - lastLightSyncSent > LIGHT_SYNC_RATE)
                        {
                            lastLightSyncSent = Environment.TickCount;

                            LogManager.DebugLog("SENDING LIGHT VEHICLE SYNC");

                            var lightBin = PacketOptimization.WriteLightSync((VehicleData) lastPacket);

                            var lightMsg = Main.Client.CreateMessage();
                            lightMsg.Write((byte) PacketType.VehicleLightSync);
                            lightMsg.Write(lightBin.Length);
                            lightMsg.Write(lightBin);
                            try
                            {
                                Main.Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced,
                                    (int) ConnectionChannel.LightSync);
                            }
                            catch (Exception ex)
                            {
                                Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                                LogManager.LogException(ex, "SENDPLAYERDATA");
                            }

                            Main._bytesSent += lightBin.Length;
                            Main._messagesSent++;
                        }

                        lastPedData = false;

                        lock (Main._averagePacketSize)
                        {
                            Main._averagePacketSize.Add(bin.Length);
                            if (Main._averagePacketSize.Count > 10)
                                Main._averagePacketSize.RemoveAt(0);
                        }

                        Main._bytesSent += bin.Length;
                        Main._messagesSent++;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "SYNCSENDER");
                }

                Thread.Sleep(PURE_SYNC_RATE);
            }
        }
    }

    public class SyncCollector : Script
    {
        public static bool ForceAimData;
        public static object LastSyncPacket;
        public static object Lock = new object();

        private static bool _lastShooting;
        private static bool _lastBullet;
        private static DateTime _lastShot;

        public SyncCollector()
        {
            var t = new Thread(SyncSender.MainLoop);
            t.IsBackground = true;
            t.Start();

            Tick += OnTick;
        }

        public void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;
            var player = Game.Player.Character;

            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var horn = Game.Player.IsPressingHorn;
                var siren = veh.SirenActive;
                var vehdead = veh.IsDead;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.VehicleHandle = Main.NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.PlayerHealth = (byte) Util.Clamp(0, player.Health, 255);
                obj.VehicleHealth = veh.EngineHealth;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.RPM = veh.CurrentRPM;
                obj.VehicleSeat = (short)Util.GetPedSeat(player);
                obj.Flag = 0;
                obj.Steering = veh.SteeringAngle;

                if (horn)
                    obj.Flag |= (byte)VehicleDataFlags.PressingHorn;
                if (siren)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;
                if (vehdead)
                    obj.Flag |= (byte)VehicleDataFlags.VehicleDead;

                if (Util.GetResponsiblePed(veh).Handle == player.Handle)
                    obj.Flag |= (byte)VehicleDataFlags.Driver;

                if (veh.IsInBurnout())
                    obj.Flag |= (byte)VehicleDataFlags.BurnOut;

                if (ForceAimData)
                    obj.Flag |= (byte) VehicleDataFlags.HasAimData;

                if (player.IsSubtaskActive(167) || player.IsSubtaskActive(168))
                {
                    obj.Flag |= (short) VehicleDataFlags.ExitingVehicle;
                }

                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    obj.AimCoords = new GTANetworkShared.Vector3(0, 0, 0);
                    obj.WeaponHash = Main.GetCurrentVehicleWeaponHash(Game.Player.Character);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    obj.WeaponHash = 0;
                    obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else
                {
                    if (player.IsSubtaskActive(200) &&
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                    if ((player.IsSubtaskActive(200) && // or 290
                         Game.Player.Character.Weapons.Current?.AmmoInClip != 0) ||
                        (Game.Player.Character.Weapons.Current?.Hash == WeaponHash.Unarmed && player.IsSubtaskActive(200)))
                        obj.Flag |= (byte)VehicleDataFlags.Aiming;
                    obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();
                }

                Vehicle trailer;

                if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    trailer = veh.TowedVehicle;
                else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                else trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);

                if (trailer != null && trailer.Exists())
                {
                    obj.Trailer = trailer.Position.ToLVector();
                }

                lock (Lock)
                {
                    LastSyncPacket = obj;
                }
            }
            else
            {
                bool aiming = player.IsSubtaskActive(ESubtask.AIMED_SHOOTING_ON_FOOT) || player.IsSubtaskActive(ESubtask.AIMING_THROWABLE); // Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                GTA.Math.Vector3 aimCoord = new Vector3();
                if (aiming || shooting)
                {
                    aimCoord = Main.RaycastEverything(new Vector2(0, 0));
                }

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Rotation.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = (byte)Util.Clamp(0, player.Health, 255);
                obj.Velocity = player.Velocity.ToLVector();

                obj.Flag = 0;

                if (player.IsRagdoll)
                    obj.Flag |= (int)PedDataFlags.Ragdoll;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                    Game.Player.Character.IsInAir)
                    obj.Flag |= (int)PedDataFlags.InFreefall;
                if (player.IsInMeleeCombat)
                    obj.Flag |= (int)PedDataFlags.InMeleeCombat;
                if (aiming || shooting)
                    obj.Flag |= (int)PedDataFlags.Aiming;
                if ((player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack)))
                    obj.Flag |= (int) PedDataFlags.Shooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (int)PedDataFlags.Jumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (int)PedDataFlags.ParachuteOpen;
                if (player.IsInCover())
                    obj.Flag |= (int)PedDataFlags.IsInCover;
                if (!Function.Call<bool>((Hash)0x6A03BF943D767C93, player))
                    obj.Flag |= (int)PedDataFlags.IsInLowerCover;
                if (player.IsInCoverFacingLeft)
                    obj.Flag |= (int)PedDataFlags.IsInCoverFacingLeft;
                if (player.IsReloading)
                    obj.Flag |= (int)PedDataFlags.IsReloading;
                if (ForceAimData)
                    obj.Flag |= (int)PedDataFlags.HasAimData;
                if (player.IsSubtaskActive(ESubtask.USING_LADDER))
                    obj.Flag |= (int) PedDataFlags.IsOnLadder;
                if (Function.Call<bool>(Hash.IS_PED_CLIMBING, player) && !player.IsSubtaskActive(ESubtask.USING_LADDER))
                    obj.Flag |= (int) PedDataFlags.IsVaulting;
                if (Function.Call<bool>(Hash.IS_ENTITY_ON_FIRE, player))
                    obj.Flag |= (int) PedDataFlags.OnFire;

                if (player.IsSubtaskActive(168))
                {
                    obj.Flag |= (int) PedDataFlags.ClosingVehicleDoor;
                }

                if (player.IsSubtaskActive(161) || player.IsSubtaskActive(162) || player.IsSubtaskActive(163) ||
                    player.IsSubtaskActive(164))
                {
                    obj.Flag |= (int)PedDataFlags.EnteringVehicle;

                    obj.VehicleTryingToEnter =
                        Main.NetEntityHandler.EntityToNet(Function.Call<int>(Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character));
                    
                    obj.SeatTryingToEnter = (sbyte)
                        Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character);
                }

                obj.Speed = Main.GetPedWalkingSpeed(player);

                lock (Lock)
                {
                    LastSyncPacket = obj;
                }

                bool sendShootingPacket;

                if (!WeaponDataProvider.IsWeaponAutomatic(unchecked ((WeaponHash) obj.WeaponHash.Value)))
                {
                    sendShootingPacket = (shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) &&
                                          !player.IsSubtaskActive(ESubtask.MELEE_COMBAT));
                }
                else
                {
                    if (!_lastShooting && !player.IsSubtaskActive(ESubtask.MELEE_COMBAT))
                    {
                        sendShootingPacket = (shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) &&
                                              !player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) ||
                                              ((player.IsInMeleeCombat || player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) &&
                                              Game.IsEnabledControlPressed(0, Control.Attack));
                    }
                    else
                    {
                        sendShootingPacket = (!player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) &&
                                              !player.IsSubtaskActive(ESubtask.MELEE_COMBAT) &&
                                              !player.IsReloading &&
                                              player.Weapons.Current.AmmoInClip > 0 &&
                                              Game.IsEnabledControlPressed(0, Control.Attack)) ||
                                             ((player.IsInMeleeCombat || player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) &&
                                              Game.IsEnabledControlPressed(0, Control.Attack));
                    }

                    if (!sendShootingPacket && _lastShooting && !_lastBullet)
                    {
                        _lastBullet = true;
                        return;
                    }
                }

                _lastBullet = false;

                if (Game.Player.Character.IsRagdoll)
                    sendShootingPacket = false;

                if (sendShootingPacket && !_lastShooting)
                {
                    _lastShooting = true;

                    _lastShot = DateTime.Now;

                    var bin = PacketOptimization.WriteBulletSync(0, true, aimCoord.ToLVector());

                    var msg = Main.Client.CreateMessage();

                    msg.Write((byte)PacketType.BulletSync);
                    msg.Write(bin.Length);
                    msg.Write(bin);

                    Main.Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.BulletSync);
                    
                    Main._bytesSent += bin.Length;
                    Main._messagesSent++;
                }

                else if (!sendShootingPacket && _lastShooting && DateTime.Now.Subtract(_lastShot).TotalMilliseconds > 50)
                {
                    _lastShooting = false;
                    var bin = PacketOptimization.WriteBulletSync(0, false, aimCoord.ToLVector());

                    var msg = Main.Client.CreateMessage();

                    msg.Write((byte)PacketType.BulletSync);
                    msg.Write(bin.Length);
                    msg.Write(bin);

                    Main.Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.BulletSync);

                    Main._bytesSent += bin.Length;
                    Main._messagesSent++;
                }
            }
        }
    }
}
