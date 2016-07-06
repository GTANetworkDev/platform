using System;
using GTANetwork.GUI.DirectXHook.Interface;

namespace GTANetwork.GUI.DirectXHook.Hook
{
    public interface IDXHook: IDisposable
    {
        CaptureConfig Config
        {
            get;
            set;
        }

        ScreenshotRequest Request
        {
            get;
            set;
        }

        void Hook();

        void Cleanup();
    }
}
