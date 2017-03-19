
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
using GTANetwork.Sync;

namespace GTANetwork
{
    internal partial class Main
    {
        private void Spectate(SizeF res)
        {
            Ped PlayerChar = Game.Player.Character;
            if (!IsSpectating && _lastSpectating)
            {

                PlayerChar.Opacity = 255;
                PlayerChar.IsPositionFrozen = false;
                Game.Player.IsInvincible = false;
                PlayerChar.IsCollisionEnabled = true;
                SpectatingEntity = 0;
                CurrentSpectatingPlayer = null;
                _currentSpectatingPlayerIndex = 100000;
                PlayerChar.PositionNoOffset = _preSpectatorPos;
            }

            if (IsSpectating)
            {
                if (SpectatingEntity != 0)
                {
                    PlayerChar.Opacity = 0;
                    PlayerChar.IsPositionFrozen = true;
                    Game.Player.IsInvincible = true;
                    PlayerChar.IsCollisionEnabled = false;

                    Control[] exceptions = new[]
                    {
                            Control.LookLeftRight,
                            Control.LookUpDown,
                            Control.LookLeft,
                            Control.LookRight,
                            Control.LookUp,
                            Control.LookDown,
                        };

                    Game.DisableAllControlsThisFrame(0);
                    foreach (var c in exceptions)
                        Game.EnableControlThisFrame(0, c);

                    var ent = NetEntityHandler.NetToEntity(SpectatingEntity);

                    if (ent != null)
                    {
                        if (Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent) && new Ped(ent.Handle).IsInVehicle())
                            PlayerChar.PositionNoOffset = ent.Position + new Vector3(0, 0, 1.3f);
                        else
                            PlayerChar.PositionNoOffset = ent.Position;
                    }
                }
                else if (SpectatingEntity == 0 && CurrentSpectatingPlayer == null &&
                         NetEntityHandler.ClientMap.Values.Count(op => op is SyncPed && !((SyncPed)op).IsSpectating &&
                                (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                (((SyncPed)op).Dimension == 0 || ((SyncPed)op).Dimension == Main.LocalDimension)) > 0)
                {
                    CurrentSpectatingPlayer =
                        NetEntityHandler.ClientMap.Values.Where(
                            op =>
                                op is SyncPed && !((SyncPed)op).IsSpectating &&
                                (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                (((SyncPed)op).Dimension == 0 || ((SyncPed)op).Dimension == Main.LocalDimension))
                            .ElementAt(_currentSpectatingPlayerIndex %
                                       NetEntityHandler.ClientMap.Values.Count(
                                           op =>
                                               op is SyncPed && !((SyncPed)op).IsSpectating &&
                                               (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                               (((SyncPed)op).Dimension == 0 ||
                                                ((SyncPed)op).Dimension == Main.LocalDimension))) as SyncPed;
                }
                else if (SpectatingEntity == 0 && CurrentSpectatingPlayer != null)
                {
                    PlayerChar.Opacity = 0;
                    PlayerChar.IsPositionFrozen = true;
                    Game.Player.IsInvincible = true;
                    PlayerChar.IsCollisionEnabled = false;
                    Game.DisableAllControlsThisFrame(0);

                    if (CurrentSpectatingPlayer.Character == null)
                        PlayerChar.PositionNoOffset = CurrentSpectatingPlayer.Position;
                    else if (CurrentSpectatingPlayer.IsInVehicle)
                        PlayerChar.PositionNoOffset = CurrentSpectatingPlayer.Character.Position + new Vector3(0, 0, 1.3f);
                    else
                        PlayerChar.PositionNoOffset = CurrentSpectatingPlayer.Character.Position;

                    if (Game.IsControlJustPressed(0, Control.PhoneLeft))
                    {
                        _currentSpectatingPlayerIndex--;
                        CurrentSpectatingPlayer = null;
                    }
                    else if (Game.IsControlJustPressed(0, Control.PhoneRight))
                    {
                        _currentSpectatingPlayerIndex++;
                        CurrentSpectatingPlayer = null;
                    }

                    if (CurrentSpectatingPlayer != null)
                    {
                        var center = new Point((int)(res.Width / 2), (int)(res.Height / 2));

                        new UIResText("Now spectating:~n~" + CurrentSpectatingPlayer.Name,
                            new Point(center.X, (int)(res.Height - 200)), 0.4f, Color.White, GTA.UI.Font.ChaletLondon,
                            UIResText.Alignment.Centered)
                        {
                            Outline = true,
                        }.Draw();

                        new Sprite("mparrow", "mp_arrowxlarge", new Point(center.X - 264, (int)(res.Height - 232)), new Size(64, 128), 180f, Color.White).Draw();
                        new Sprite("mparrow", "mp_arrowxlarge", new Point(center.X + 200, (int)(res.Height - 232)), new Size(64, 128)).Draw();
                    }
                }
            }

            _lastSpectating = IsSpectating;

        }
    }
}


