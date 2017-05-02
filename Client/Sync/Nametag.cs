using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetwork.Streamer;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;


namespace GTANetwork.Sync
{
    internal partial class SyncPed
    {

        //bool enteringSeat = _seatEnterStart != 0 && Util.Util.TickCount - _seatEnterStart < 500;
        //if ((enteringSeat || Character.IsSubtaskActive(67) || IsBeingControlledByScript || Character.IsExitingLeavingCar()))
        //{
        //    if (!Main.ToggleNametagDraw) DrawNametag();
        //    return;
        //}
        //if (!Main.ToggleNametagDraw) DrawNametag();

        internal void DrawNametag()
        {
            if (!Main.UIVisible) return;
            if ((NametagSettings & 1) != 0) return;

           // CallCollection thisCollection = new CallCollection();

            if (!StreamedIn && IsSpectating || (Flag & (int)EntityFlag.PlayerSpectating) != 0 || ModelHash == 0 || string.IsNullOrEmpty(Name)) return;
            if(Character != null && Character.Exists())
            {
                Ped PlayerChar = Game.Player.Character;
                if (((Character.IsInRangeOfEx(PlayerChar.Position, 25f))) || Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, Character)) //Natives can slow down
                {
                    if (Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, PlayerChar, Character, 17)) //Natives can slow down
                    {
                        var targetPos = Character.GetBoneCoord(Bone.IK_Head) + new Vector3(0, 0, 0.5f);

                        targetPos += Character.Velocity / Game.FPS;

                        Function.Call(Hash.SET_DRAW_ORIGIN, targetPos.X, targetPos.Y, targetPos.Z, 0);

                        var nameText = Name ?? "<nameless>";

                        if (!string.IsNullOrEmpty(NametagText))
                            nameText = NametagText;

                        if (TicksSinceLastUpdate > 10000)
                            nameText = "~r~AFK~w~~n~" + nameText;

                        var dist = (GameplayCamera.Position - Character.Position).Length();
                        var sizeOffset = Math.Max(1f - (dist / 30f), 0.3f);

                        Color defaultColor = Color.FromArgb(245, 245, 245);

                        if ((NametagSettings & 2) != 0)
                        {
                            Util.Util.ToArgb(NametagSettings >> 8, out byte a, out byte r, out byte g, out byte b);

                            defaultColor = Color.FromArgb(r, g, b);
                        }

                        Util.Util.DrawText(nameText, 0, 0, 0.4f * sizeOffset, defaultColor.R, defaultColor.G, defaultColor.B, 255, 0, 1, false, true, 0);

                        if (Character != null)
                        {
                            var armorColor = Color.FromArgb(200, 220, 220, 220);
                            var bgColor = Color.FromArgb(100, 0, 0, 0);
                            var armorPercent = Math.Min(Math.Max(PedArmor / 100f, 0f), 1f);
                            var armorBar = Math.Round(150 * armorPercent);
                            armorBar = (armorBar * sizeOffset);

                            //Less latency with rectangles disabled
                            Util.Util.DrawRectangle(-75 * sizeOffset, 36 * sizeOffset, armorBar, 20 * sizeOffset, armorColor.R, armorColor.G,
                                armorColor.B, armorColor.A);
                            Util.Util.DrawRectangle(-75 * sizeOffset + armorBar, 36 * sizeOffset, (sizeOffset * 150) - armorBar, sizeOffset * 20,
                                bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                            Util.Util.DrawRectangle(-71 * sizeOffset, 40 * sizeOffset, (142 * Math.Min(Math.Max((PedHealth / 100f), 0f), 1f)) * sizeOffset, 12 * sizeOffset,
                                50, 250, 50, 150);
                        }

                        Function.Call(Hash.CLEAR_DRAW_ORIGIN);
                    }
                }
            }

        }
    }
}
