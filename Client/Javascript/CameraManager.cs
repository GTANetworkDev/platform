using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTANetwork.Util;

namespace GTANetwork.Javascript
{
    public class CameraManager
    {
        private List<GlobalCamera> _cameras = new List<GlobalCamera>();
        private GlobalCamera _activeCamera;
        private GlobalCamera _oldInterp;

        public void Reset()
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                _cameras[i].Dispose();
            }

            SetActive(null);

            _oldInterp = null;
            _activeCamera = null;

            _cameras.Clear();
        }

        public GlobalCamera GetActive()
        {
            return _activeCamera;
        }

        public GlobalCamera Create(GTANetworkShared.Vector3 position, GTANetworkShared.Vector3 rotation)
        {
            var gCam = new GlobalCamera();
            gCam.Position = position;
            gCam.Rotation = rotation;
            gCam.Fov = GameplayCamera.FieldOfView;

            _cameras.Add(gCam);

            return gCam;
        }

        public void Delete(GlobalCamera cam)
        {
            if (cam.Active) SetActive(null);

            _cameras.Remove(cam);
        }

        public void SetActive(GlobalCamera cam)
        {
            if (_activeCamera != null)
            {
                _activeCamera.Active = false;
                _activeCamera.Dispose();
                _activeCamera = null;
            }

            if (_oldInterp != null)
            {
                _oldInterp.Active = false;
                _oldInterp.Dispose();
                _oldInterp = null;
            }

            if (cam == null)
            {
                World.RenderingCamera = null;
                _activeCamera = null;
                return;
            }

            if (cam.CamObj == null)
            {
                cam.CamObj = World.CreateCamera(cam.Position.ToVector(), cam.Rotation.ToVector(), cam.Fov);

                if (cam.EntityAttached != 0)
                {
                    if (cam.BoneAttached != 0)
                    {
                        cam.CamObj.AttachTo(new Ped(cam.EntityAttached), cam.BoneAttached, cam.AttachOffset.ToVector());
                    }
                    else
                    {
                        cam.CamObj.AttachTo(new Prop(cam.EntityAttached), cam.AttachOffset.ToVector());
                    }
                }

                if (cam.EntityPointing != 0)
                {
                    if (cam.BonePointing != 0)
                    {
                        cam.CamObj.PointAt(new Ped(cam.EntityPointing), cam.BonePointing, cam.PointOffset.ToVector());
                    }
                    else
                    {
                        cam.CamObj.PointAt(new Prop(cam.EntityPointing), cam.PointOffset.ToVector());
                    }
                }
                else if (cam.VectorPointing != null)
                {
                    cam.CamObj.PointAt(cam.VectorPointing.ToVector());
                }

                if (!string.IsNullOrEmpty(cam.Shake))
                {
                    Function.Call(Hash.SHAKE_CAM, cam.CamObj.Handle, cam.Shake, cam.ShakeAmp);
                }

                World.RenderingCamera = cam.CamObj;
            }
            else
            {
                World.RenderingCamera = cam.CamObj;
            }

            cam.Active = true;
            _activeCamera = cam;
        }

        public void SetActiveWithInterp(GlobalCamera cam, int duration, bool easePos, bool easeRot)
        {
            if (_activeCamera == null || cam == null) return;

            if (_oldInterp != null)
            {
                _oldInterp.Active = false;
                _oldInterp.Dispose();
                _oldInterp = null;
            }

            if (cam.CamObj == null)
            {
                cam.CamObj = World.CreateCamera(cam.Position.ToVector(), cam.Rotation.ToVector(), cam.Fov);

                if (cam.EntityAttached != 0)
                {
                    if (cam.BoneAttached != 0)
                    {
                        cam.CamObj.AttachTo(new Ped(cam.EntityAttached), cam.BoneAttached, cam.AttachOffset.ToVector());
                    }
                    else
                    {
                        cam.CamObj.AttachTo(new Prop(cam.EntityAttached), cam.AttachOffset.ToVector());
                    }
                }

                if (cam.EntityPointing != 0)
                {
                    if (cam.BonePointing != 0)
                    {
                        cam.CamObj.PointAt(new Ped(cam.EntityPointing), cam.BonePointing, cam.PointOffset.ToVector());
                    }
                    else
                    {
                        cam.CamObj.PointAt(new Prop(cam.EntityPointing), cam.PointOffset.ToVector());
                    }
                }
                else if (cam.VectorPointing != null)
                {
                    cam.CamObj.PointAt(cam.VectorPointing.ToVector());
                }

                if (!string.IsNullOrEmpty(cam.Shake))
                {
                    Function.Call(Hash.SHAKE_CAM, cam.CamObj.Handle, cam.Shake, cam.ShakeAmp);
                }
            }

            _activeCamera.CamObj.InterpTo(cam.CamObj, duration, easePos, easeRot);

            cam.Active = true;
            _activeCamera.Active = false;
            _oldInterp = _activeCamera;
            _activeCamera = cam;
        }
    }

    public class GlobalCamera
    {
        internal GTANetworkShared.Vector3 Position { get; set; }
        internal GTANetworkShared.Vector3 Rotation { get; set; }

        internal int EntityPointing { get; set; }
        internal int BonePointing { get; set; }
        internal GTANetworkShared.Vector3 PointOffset { get; set; }
        internal GTANetworkShared.Vector3 VectorPointing { get; set; }

        internal int EntityAttached { get; set; }
        internal int BoneAttached { get; set; }
        internal GTANetworkShared.Vector3 AttachOffset { get; set; }

        internal string Shake { get; set; }
        internal float ShakeAmp { get; set; }
        internal float Fov { get; set; }
        internal bool Active { get; set; }

        internal Camera CamObj { get; set; }
        
        internal void Dispose()
        {
            if (CamObj != null)
            {
                CamObj.Destroy();
                CamObj = null;
            }
        }
    }
}