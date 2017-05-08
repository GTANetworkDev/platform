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
        struct interpolation
        {
            internal Vector3 vecStart;
            internal Vector3 vecTarget;
            internal Vector3 vecError;
            internal long StartTime;
            internal long FinishTime;
            internal float LastAlpha;
        }

        private interpolation currentInterop = new interpolation();

        internal void StartInterpolation()
        {
            currentInterop = new interpolation();

            if (_isInVehicle)
            {
                if (_lastPosition == null) return;
                //if (Main.VehicleLagCompensation)
                //{

                    var dir = Position - _lastPosition.Value;
                    currentInterop.vecTarget = Position + dir;
                    currentInterop.vecError = dir;
                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                //}
                //else
                //{
                //    var dir = Position - _lastPosition.Value;
                //    currentInterop.vecTarget = Position;
                //    currentInterop.vecError = dir;
                //    currentInterop.vecError *= Util.Util.Lerp(0.25f, Util.Util.Unlerp(100, 100, 400), 1f);
                //}

                if (MainVehicle != null)
                    currentInterop.vecStart = MainVehicle.Position;
            }
            else
            {
                //if (Main.OnFootLagCompensation)
                //{
                    var dir = Position - _lastPosition;
                    currentInterop.vecTarget = Position; // + dir;
                    currentInterop.vecError = dir ?? new Vector3();
                    currentInterop.vecStart = Position;

                    //MainVehicle == null ? dir : MainVehicle.Position - currentInterop.vecTarget;
                    //currentInterop.vecError *= Util.Lerp(0.25f, Util.Unlerp(100, 100, 400), 1f);
                //}
                //else
                //{
                //    var dir = Position - _lastPosition;

                //    currentInterop.vecTarget = Position;
                //    currentInterop.vecError = dir ?? new Vector3();
                //    currentInterop.vecError *= Util.Util.Lerp(0.25f, Util.Util.Unlerp(100, 100, 400), 1f);
                //}

                if (Character != null)
                    currentInterop.vecStart = Character.Position;
            }

            currentInterop.StartTime = Util.Util.TickCount - DataLatency;
            currentInterop.FinishTime = currentInterop.StartTime + 100;
            currentInterop.LastAlpha = 0f;
        }

    }
}
