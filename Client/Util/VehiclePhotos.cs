#if false

using System;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;

namespace GTANetwork.Util
{
    public class VehiclePhotos : Script
    {
        public VehiclePhotos()
        {
            Tick += OnTick;
        }

        private readonly VehicleHash[] Hashes = Enum.GetValues(typeof (VehicleHash)).Cast<VehicleHash>().ToArray();

        private int Index = 0;
        private Camera MainCam;
        private bool Started;
        private Vehicle currentVehicle;

        private Vector3 StartPos;
        private float StartRot;

        public void OnTick(object sender, EventArgs e)
        {
            if (!Started && Game.IsControlJustPressed(0, Control.Context))
            {
                Start();
            }

            if (!Started) return;

            if (Index >= Hashes.Length) return;

            if(currentVehicle != null && currentVehicle.Exists()) currentVehicle.Delete();
            var hash = Hashes[Index++];
            var mod = new Model(hash);
            mod.Request(1000);
            currentVehicle = World.CreateVehicle(mod, StartPos, StartRot);
            Game.Player.Character.SetIntoVehicle(currentVehicle, VehicleSeat.Driver);
            mod.MarkAsNoLongerNeeded();
            Script.Wait(100);

            PlaceCamera(currentVehicle);

            Script.Wait(200);

            Screenshot.TakeScreenshot(hash.ToString() + ".png");

            Script.Wait(200);
        }

        
        private void PlaceCamera(Vehicle car)
        {
            float multiplier = 1f;
            MainCam.PointAt(car);
            MainCam.Position = car.GetOffsetInWorldCoords(new Vector3(-3f * multiplier, 5f * multiplier, 2f * multiplier));

            while (!IsVehicleOnScreen(car))
            {
                multiplier += 0.1f;
                MainCam.Position = car.GetOffsetInWorldCoords(new Vector3(-3f * multiplier, 5f * multiplier, 2f * multiplier));
                Script.Wait(100);
            }
        }

        private bool IsVehicleOnScreen(Vehicle car)
        {
            Vector3 min, max;
            car.Model.GetDimensions(out min, out max);

            Vector3 p1, p2;

            p1 = car.GetOffsetInWorldCoords(min);
            p2 = car.GetOffsetInWorldCoords(max);

            var w2s = Main.WorldToScreen(p1);
            var w2s2 = Main.WorldToScreen(p2);

            if (w2s.X == 0 && w2s.Y == 0) return false;
            if (w2s2.X == 0 && w2s2.Y == 0) return false;

            return true;
        }

        public void Start()
        {
            StartPos = Game.Player.Character.Position;
            StartRot = Game.Player.Character.Rotation.Z;

            Started = true;

            MainCam = World.CreateCamera(StartPos, new Vector3(), GameplayCamera.FieldOfView);
            World.RenderingCamera = MainCam;
        }
    }
}

#endif